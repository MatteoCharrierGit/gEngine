using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Editor.Undo;
using gEngine.MathUtils;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor;

public enum GizmoMode
{
    Translate,
    Rotate,
    Scale
}

/// <summary>
/// Le maniglie move/rotate/scale sull'entità selezionata, dentro la vista Scena.
///
/// ⚠️ <b>Scritto a mano invece che con ImGuizmo.NET</b>, che il piano dava per scontato.
/// Non c'è un ImGuizmo agganciabile a questo stack: i binding esistenti su NuGet
/// (<c>Twizzle.ImGuizmo.NET</c>) vogliono un <i>altro</i> binding di ImGui (1.89), mentre
/// qui c'è <c>ImGui.NET</c> 1.91.6.1 con <c>rlImgui-cs</c> compilato contro di esso.
/// Affiancarli significa due assembly che definiscono entrambi <c>ImGuiNET.ImGui</c> e due
/// <c>cimgui.dll</c> native che si sovrascrivono a vicenda — e ImGui 1.89 e 1.91 hanno un
/// <c>ImGuiContext</c> con ABI diverso, che è proprio la struttura che
/// <c>SetImGuiContext</c> dovrebbe condividere. Il bundle autoconsistente
/// (<c>Twizzle.ImGui-Bundle.NET</c>) risolverebbe il conflitto fra nativi, ma rlImgui-cs
/// resterebbe legato all'assembly <c>ImGui.NET</c>: due contesti ImGui in volo.
/// Restava o forkare rlImgui-cs per sempre, o scrivere ~300 righe di matematica che il
/// progetto ha già in casa. Di conseguenza <b>cade la motivazione scritta in roadmap</b>
/// ("l'unico punto che giustifica di non aver avvolto ImGui dietro un port: vuole il
/// contesto grezzo"): la conclusione regge — un pannello è codice UI — ma non per questo.
///
/// Il disegno passa dal draw list di ImGui: le maniglie sono linee 2D proiettate dal mondo
/// con <see cref="Camera3D.WorldToViewport"/>, non geometria 3D. L'interazione invece è in
/// 3D, con le semirette di <see cref="Camera3D.GetRay"/> — le stesse del picking.
///
/// Gli assi sono <b>locali dell'oggetto</b> (l'equivalente del pivot "Local" di Unity).
/// Non è solo una scelta d'uso: rende rotazione e scala esatte per costruzione, perché
/// <c>Rotation</c> e <c>Scale</c> del <c>TransformComponent</c> sono già in quello spazio
/// e il genitore non entra nel conto. L'unico che deve attraversare la gerarchia è lo
/// spostamento, perché <c>Position</c> vive nello spazio del <b>genitore</b>.
/// </summary>
public class TransformGizmo
{
    private const float LengthInPixels = 90f;
    private const float GrabRadiusInPixels = 8f;
    private const int CircleSegments = 48;

    private const int AxisX = 0;
    private const int AxisY = 1;
    private const int AxisZ = 2;

    public GizmoMode Mode { get; set; } = GizmoMode.Translate;

    /// <summary>
    /// Lo stato dell'entità all'inizio del trascinamento, per l'annulla. Vive quanto il gesto.
    /// </summary>
    private EntitySnapshot? _dragBefore;

    /// <summary>
    /// Chiude il gesto: un comando solo per tutto il trascinamento.
    ///
    /// ⚠️ Un trascinamento che <b>non ha spostato niente</b> (la maniglia afferrata e lasciata
    /// dov'era) non deve lasciare un annulla: <c>ChangedSomething</c> lo scarta. Senza,
    /// afferrare un asse per sbaglio riempirebbe la storia di comandi che non fanno nulla, e
    /// premere Ctrl+Z sembrerebbe non funzionare.
    /// </summary>
    private void EndDragUndo(World world, EditorContext context, Entity entity)
    {
        if (_dragBefore is not { } before)
            return;

        _dragBefore = null;

        var verb = Mode switch
        {
            GizmoMode.Rotate => "ruota",
            GizmoMode.Scale => "scala",
            _ => "sposta"
        };

        var command = EntityStateCommand.Between(world, entity, $"{verb} {EntityName(world, entity)}", before);

        if (command.ChangedSomething)
            context.Undo.Push(command);
    }

    private static string EntityName(World world, Entity entity)
    {
        return world.TryGetComponent<NameComponent>(entity, out var name) &&
               !string.IsNullOrWhiteSpace(name.Value)
            ? name.Value
            : $"Entity {entity.Id}";
    }

    private Drag? _drag;

    /// <summary>
    /// Lo stato di un trascinamento in corso. Tutto ciò che serve è catturato al momento
    /// del grab e <b>non si ricalcola</b>: se l'origine o l'asse inseguissero l'oggetto
    /// mentre lo si muove, ogni frame misurerebbe da un punto diverso e la maniglia
    /// scapperebbe via da sola.
    /// </summary>
    private sealed class Drag
    {
        public required int Axis { get; init; }
        public required GizmoMode Mode { get; init; }
        public required Vector3 Origin { get; init; }
        public required Vector3 Direction { get; init; }
        public required float Length { get; init; }

        public required Vector3 StartPosition { get; init; }
        public required Quaternion StartRotation { get; init; }
        public required Vector3 StartScale { get; init; }

        /// <summary>Parametro lungo l'asse al grab (sposta/scala).</summary>
        public float StartParameter { get; init; }

        /// <summary>Assi di riferimento sul piano di rotazione, fissati al grab.</summary>
        public Vector3 PlaneU { get; init; }
        public Vector3 PlaneV { get; init; }

        /// <summary>
        /// L'angolo va accumulato, non sottratto: girando oltre ±180° la differenza
        /// "angolo corrente − angolo iniziale" salta di 2π e l'oggetto scatta.
        /// </summary>
        public float LastAngle { get; set; }
        public float TotalAngle { get; set; }
    }

    /// <param name="imageHovered">
    /// Se il puntatore è davvero sopra l'immagine della vista secondo ImGui. Non basta che
    /// le coordinate cadano sulla maniglia: un altro pannello può stare sopra il viewport,
    /// e senza questo si afferrerebbe un asse attraverso la finestra che lo copre.
    /// </param>
    /// <returns>
    /// True se il gizmo si sta prendendo il mouse (maniglia sotto il puntatore, o
    /// trascinamento in corso). Chi chiama deve saltare il picking: un clic per afferrare
    /// una maniglia non è un clic per selezionare qualcos'altro.
    /// </returns>
    public bool Draw(World world, EditorContext context, Camera3D camera,
        Vector2 imageOrigin, Vector2 viewportSize, bool imageHovered)
    {
        var toolbarWantsMouse = DrawToolbar(imageOrigin);

        if (context.Selected is not { } entity || !world.Exists(entity) ||
            !world.TryGetComponent<TransformComponent>(entity, out var transform))
        {
            _drag = null;
            return toolbarWantsMouse;
        }

        var worldMatrix = world.GetWorldMatrix(entity);
        var origin = worldMatrix.Translation;

        // Profondità lungo la direzione di vista, non distanza euclidea: è quella che
        // governa la dimensione a schermo. Dietro la camera non c'è niente da disegnare.
        var forward = camera.GetCameraAxes().Forward;
        var depth = Vector3.Dot(origin - camera.Position, forward);
        if (depth <= 0.001f)
        {
            _drag = null;
            return toolbarWantsMouse;
        }

        var length = WorldLengthForPixels(camera, depth, LengthInPixels, viewportSize);
        var axes = WorldAxes(worldMatrix);

        var viewProjection = camera.GetViewProjection(viewportSize.X / viewportSize.Y);
        var mouse = ImGui.GetIO().MousePos - imageOrigin;

        // Hit test solo se non si sta già trascinando: durante il trascinamento comanda
        // l'asse afferrato, non quello che passa sotto il puntatore (che il trascinamento
        // stesso sta spostando).
        var hovered = _drag?.Axis
                      ?? (imageHovered ? HitTest(viewProjection, viewportSize, origin, axes, length, mouse) : -1);

        // !toolbarWantsMouse: le maniglie passano SOTTO la toolbar, quindi un clic su
        // "Muovi" afferrerebbe anche l'asse che ci corre sotto.
        if (_drag is null && !toolbarWantsMouse && hovered >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _drag = BeginDrag(hovered, origin, axes[hovered], length, transform,
                camera.GetRay(mouse, viewportSize));

            // Il "prima" dell'annulla si prende qui, all'inizio del gesto: Apply riscrive il
            // transform a ogni frame del trascinamento, quindi un attimo dopo il "prima" è già
            // perso. È lo stesso confine di gesto dell'Inspector, riconosciuto meglio: qui il
            // trascinamento ha un inizio e una fine espliciti invece che dedotti.
            _dragBefore = EntitySnapshot.Capture(world, entity);
        }

        // IsMouseDown e non IsMouseReleased: se il rilascio avviene fuori dalla finestra
        // (o in un frame saltato) il released non lo vediamo mai e la maniglia resterebbe
        // incollata al puntatore.
        if (_drag is not null && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _drag = null;
            EndDragUndo(world, context, entity);
        }

        if (_drag is not null)
        {
            Apply(world, entity, _drag, camera.GetRay(mouse, viewportSize));

            // Il transform è stato riscritto dentro Apply: rileggilo, o si disegnerebbe
            // il gizmo di dov'era l'oggetto un attimo fa.
            worldMatrix = world.GetWorldMatrix(entity);
            origin = worldMatrix.Translation;
            axes = WorldAxes(worldMatrix);
        }

        DrawHandles(camera, viewProjection, viewportSize, imageOrigin, origin, axes, length, hovered);

        return toolbarWantsMouse || hovered >= 0 || _drag is not null;
    }

    // ---- interazione -------------------------------------------------------------

    private Drag BeginDrag(int axis, Vector3 origin, Vector3 direction, float length,
        TransformComponent transform, Ray ray)
    {
        var u = Perpendicular(direction);

        var drag = new Drag
        {
            Axis = axis,
            Mode = Mode,
            Origin = origin,
            Direction = direction,
            Length = length,
            StartPosition = transform.Position,
            StartRotation = transform.Rotation,
            StartScale = transform.Scale,
            StartParameter = ClosestParameterOnAxis(origin, direction, ray) ?? 0f,
            PlaneU = u,
            PlaneV = Vector3.Cross(direction, u)
        };

        drag.LastAngle = AngleOnPlane(origin, direction, drag.PlaneU, drag.PlaneV, ray) ?? 0f;
        drag.TotalAngle = 0f;

        return drag;
    }

    private static void Apply(World world, Entity entity, Drag drag, Ray ray)
    {
        if (!world.TryGetComponent<TransformComponent>(entity, out var transform))
            return;

        switch (drag.Mode)
        {
            case GizmoMode.Translate:
            {
                if (ClosestParameterOnAxis(drag.Origin, drag.Direction, ray) is not { } parameter)
                    return;

                var worldDelta = drag.Direction * (parameter - drag.StartParameter);

                // Position è nello spazio del GENITORE, il gizmo ragiona in mondo: senza
                // questo passaggio, trascinare una figlia la manderebbe altrove di tanto
                // quanto il genitore è ruotato o scalato.
                transform.Position = drag.StartPosition + ToParentSpace(world, entity, worldDelta);
                break;
            }

            case GizmoMode.Scale:
            {
                if (ClosestParameterOnAxis(drag.Origin, drag.Direction, ray) is not { } parameter)
                    return;

                // Trascinare di una lunghezza del gizmo raddoppia. L'alternativa
                // (parameter / startParameter) esplode se si afferra vicino all'origine.
                var factor = MathF.Max(0.01f, 1f + (parameter - drag.StartParameter) / drag.Length);

                var scale = drag.StartScale;
                var scaled = Axis(scale, drag.Axis) * factor;
                transform.Scale = WithAxis(scale, drag.Axis, scaled);
                break;
            }

            case GizmoMode.Rotate:
            {
                if (AngleOnPlane(drag.Origin, drag.Direction, drag.PlaneU, drag.PlaneV, ray) is not { } angle)
                    return;

                drag.TotalAngle += WrapToPi(angle - drag.LastAngle);
                drag.LastAngle = angle;

                // L'asse del gizmo È l'asse locale dell'oggetto, quindi in spazio locale è
                // semplicemente UnitX/Y/Z e il genitore non c'entra. Concatenate(a, b) =
                // "prima a, poi b": la rotazione extra va applicata PRIMA di quella di
                // partenza, altrimenti l'asse verrebbe interpretato nello spazio sbagliato.
                var localAxis = drag.Axis switch
                {
                    AxisX => Vector3.UnitX,
                    AxisY => Vector3.UnitY,
                    _ => Vector3.UnitZ
                };

                transform.Rotation = Quaternion.Concatenate(
                    Quaternion.CreateFromAxisAngle(localAxis, drag.TotalAngle),
                    drag.StartRotation);
                break;
            }
        }

        // ⚠️ TransformComponent è una struct: TryGetComponent ne ha dato una COPIA. Senza
        // questa riscrittura il gizmo sembrerebbe funzionare e non muoverebbe niente — lo
        // stesso write-back che è già morso nell'Inspector.
        world.AddComponent(entity, transform);
    }

    private static Vector3 ToParentSpace(World world, Entity entity, Vector3 worldDelta)
    {
        if (!world.TryGetComponent<ParentComponent>(entity, out var parent))
            return worldDelta;

        if (!Matrix4x4.Invert(world.GetWorldMatrix(parent.Parent), out var inverse))
            return worldDelta;

        return Vector3.TransformNormal(worldDelta, inverse);
    }

    // ---- hit test ----------------------------------------------------------------

    private int HitTest(Matrix4x4 viewProjection, Vector2 viewportSize,
        Vector3 origin, Vector3[] axes, float length, Vector2 mouse)
    {
        var best = -1;
        var bestDistance = GrabRadiusInPixels;

        for (var axis = 0; axis < 3; axis++)
        {
            var distance = Mode == GizmoMode.Rotate
                ? DistanceToCircle(viewProjection, viewportSize, origin, axes[axis], length, mouse)
                : DistanceToAxis(viewProjection, viewportSize, origin, axes[axis], length, mouse);

            if (distance is not { } d || d >= bestDistance)
                continue;

            bestDistance = d;
            best = axis;
        }

        return best;
    }

    private static float? DistanceToAxis(Matrix4x4 viewProjection, Vector2 viewportSize,
        Vector3 origin, Vector3 direction, float length, Vector2 mouse)
    {
        if (!Camera3D.WorldToViewport(origin, viewProjection, viewportSize, out var a) ||
            !Camera3D.WorldToViewport(origin + direction * length, viewProjection, viewportSize, out var b))
            return null;

        return DistanceToSegment(mouse, a, b);
    }

    private static float? DistanceToCircle(Matrix4x4 viewProjection, Vector2 viewportSize,
        Vector3 origin, Vector3 normal, float radius, Vector2 mouse)
    {
        var u = Perpendicular(normal);
        var v = Vector3.Cross(normal, u);

        float? best = null;
        Vector2? previous = null;

        for (var i = 0; i <= CircleSegments; i++)
        {
            var angle = i / (float)CircleSegments * MathF.Tau;
            var point = origin + (u * MathF.Cos(angle) + v * MathF.Sin(angle)) * radius;

            if (!Camera3D.WorldToViewport(point, viewProjection, viewportSize, out var projected))
            {
                previous = null; // punto dietro la camera: spezza la catena, non saltarla
                continue;
            }

            if (previous is { } start)
            {
                var distance = DistanceToSegment(mouse, start, projected);
                if (best is null || distance < best) best = distance;
            }

            previous = projected;
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();

        if (lengthSquared < 1e-6f)
            return Vector2.Distance(point, a);

        var t = Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, a + ab * t);
    }

    // ---- disegno -----------------------------------------------------------------

    /// <returns>True se il puntatore è sopra la toolbar.</returns>
    private bool DrawToolbar(Vector2 imageOrigin)
    {
        ImGui.SetCursorScreenPos(imageOrigin + new Vector2(8, 8));

        ImGui.BeginGroup();
        ModeButton("Muovi", GizmoMode.Translate);
        ImGui.SameLine();
        ModeButton("Ruota", GizmoMode.Rotate);
        ImGui.SameLine();
        ModeButton("Scala", GizmoMode.Scale);
        ImGui.EndGroup();

        // Il rettangolo del gruppo, non IsItemHovered(): dopo EndGroup l'"item" è il
        // gruppo, ma l'hover se lo prendono i bottoni che ci stanno dentro e il gruppo
        // risponderebbe di no. Il test sul rettangolo è quello che si intende davvero.
        return ImGui.IsMouseHoveringRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
    }

    private void ModeButton(string label, GizmoMode mode)
    {
        var selected = Mode == mode;

        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));

        if (ImGui.SmallButton(label))
            Mode = mode;

        if (selected)
            ImGui.PopStyleColor();
    }

    private void DrawHandles(Camera3D camera, Matrix4x4 viewProjection, Vector2 viewportSize,
        Vector2 imageOrigin, Vector3 origin, Vector3[] axes, float length, int hovered)
    {
        var drawList = ImGui.GetWindowDrawList();

        if (!Camera3D.WorldToViewport(origin, viewProjection, viewportSize, out var center))
            return;

        for (var axis = 0; axis < 3; axis++)
        {
            var color = ColorFor(axis, active: axis == hovered);

            if (Mode == GizmoMode.Rotate)
            {
                DrawCircle(drawList, viewProjection, viewportSize, imageOrigin, origin, axes[axis], length, color);
                continue;
            }

            if (!Camera3D.WorldToViewport(origin + axes[axis] * length, viewProjection, viewportSize, out var tip))
                continue;

            drawList.AddLine(imageOrigin + center, imageOrigin + tip, color, 2.5f);

            // La punta dice quale modo è attivo: una freccia sposta, un quadrato scala.
            if (Mode == GizmoMode.Translate)
                drawList.AddCircleFilled(imageOrigin + tip, 4.5f, color);
            else
                drawList.AddRectFilled(imageOrigin + tip - new Vector2(4, 4), imageOrigin + tip + new Vector2(4, 4), color);
        }

        drawList.AddCircleFilled(imageOrigin + center, 3f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)));
    }

    private static void DrawCircle(ImDrawListPtr drawList, Matrix4x4 viewProjection, Vector2 viewportSize,
        Vector2 imageOrigin, Vector3 origin, Vector3 normal, float radius, uint color)
    {
        var u = Perpendicular(normal);
        var v = Vector3.Cross(normal, u);

        Vector2? previous = null;

        for (var i = 0; i <= CircleSegments; i++)
        {
            var angle = i / (float)CircleSegments * MathF.Tau;
            var point = origin + (u * MathF.Cos(angle) + v * MathF.Sin(angle)) * radius;

            if (!Camera3D.WorldToViewport(point, viewProjection, viewportSize, out var projected))
            {
                previous = null;
                continue;
            }

            if (previous is { } start)
                drawList.AddLine(imageOrigin + start, imageOrigin + projected, color, 2f);

            previous = projected;
        }
    }

    private static uint ColorFor(int axis, bool active)
    {
        if (active)
            return ImGui.GetColorU32(new Vector4(1f, 0.9f, 0.2f, 1f));

        return axis switch
        {
            AxisX => ImGui.GetColorU32(new Vector4(0.9f, 0.25f, 0.3f, 1f)),
            AxisY => ImGui.GetColorU32(new Vector4(0.4f, 0.85f, 0.3f, 1f)),
            _ => ImGui.GetColorU32(new Vector4(0.3f, 0.5f, 0.95f, 1f))
        };
    }

    // ---- matematica --------------------------------------------------------------

    /// <summary>
    /// Lunghezza in unità mondo che, alla profondità data, copre un numero fisso di pixel:
    /// è ciò che tiene il gizmo della stessa dimensione a schermo, vicino o lontano.
    /// </summary>
    private static float WorldLengthForPixels(Camera3D camera, float depth, float pixels, Vector2 viewportSize)
    {
        var worldHeight = 2f * depth * MathF.Tan(float.DegreesToRadians(camera.FovY) * 0.5f);
        return worldHeight * (pixels / viewportSize.Y);
    }

    /// <summary>Gli assi locali dell'oggetto, visti nel mondo.</summary>
    private static Vector3[] WorldAxes(Matrix4x4 worldMatrix)
    {
        return
        [
            SafeNormalize(Vector3.TransformNormal(Vector3.UnitX, worldMatrix), Vector3.UnitX),
            SafeNormalize(Vector3.TransformNormal(Vector3.UnitY, worldMatrix), Vector3.UnitY),
            SafeNormalize(Vector3.TransformNormal(Vector3.UnitZ, worldMatrix), Vector3.UnitZ)
        ];
    }

    // Scala zero su un asse azzera la colonna della matrice: normalizzare darebbe NaN, e
    // un NaN nel transform si propaga a tutta la scena senza dire da dove è venuto.
    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        var length = value.Length();
        return length < 1e-6f ? fallback : value / length;
    }

    /// <summary>
    /// Il punto dell'asse più vicino alla semiretta del mouse, come parametro lungo l'asse.
    /// È il modo standard di trasformare "ho mosso il mouse" in "di quanto lungo l'asse":
    /// due rette sghembe nello spazio, si cerca il loro avvicinamento massimo.
    /// </summary>
    /// <returns>Null quando si guarda quasi lungo l'asse: lì è degenere e il punto più
    /// vicino schizza all'infinito.</returns>
    private static float? ClosestParameterOnAxis(Vector3 origin, Vector3 direction, Ray ray)
    {
        var rayDirection = SafeNormalize(ray.Direction, Vector3.UnitZ);

        var b = Vector3.Dot(direction, rayDirection);
        var denominator = 1f - b * b;

        if (MathF.Abs(denominator) < 1e-5f)
            return null;

        var r = origin - ray.Origin;
        var c = Vector3.Dot(direction, r);
        var f = Vector3.Dot(rayDirection, r);

        return (b * f - c) / denominator;
    }

    /// <summary>
    /// L'angolo a cui la semiretta buca il piano di rotazione, nel riferimento (u, v).
    /// </summary>
    /// <returns>Null se la semiretta è parallela al piano (ci scivola sopra) o lo
    /// incontra dietro l'occhio.</returns>
    private static float? AngleOnPlane(Vector3 origin, Vector3 normal, Vector3 u, Vector3 v, Ray ray)
    {
        var rayDirection = SafeNormalize(ray.Direction, Vector3.UnitZ);
        var denominator = Vector3.Dot(rayDirection, normal);

        if (MathF.Abs(denominator) < 1e-4f)
            return null;

        var distance = Vector3.Dot(origin - ray.Origin, normal) / denominator;

        if (distance <= 0f)
            return null;

        var point = ray.Origin + rayDirection * distance - origin;

        return MathF.Atan2(Vector3.Dot(point, v), Vector3.Dot(point, u));
    }

    /// <summary>Un vettore qualunque perpendicolare a <paramref name="value"/>.</summary>
    private static Vector3 Perpendicular(Vector3 value)
    {
        // Il prodotto vettoriale con un asse quasi parallelo dà un vettore quasi nullo:
        // si sceglie l'asse su cui il vettore pesa di meno, che è garantito non parallelo.
        var axis = MathF.Abs(value.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
        return Vector3.Normalize(Vector3.Cross(value, axis));
    }

    /// <summary>Riporta un angolo in (-π, π]. Vedi <see cref="Drag.TotalAngle"/>.</summary>
    private static float WrapToPi(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }

    private static float Axis(Vector3 value, int axis) => axis switch
    {
        AxisX => value.X,
        AxisY => value.Y,
        _ => value.Z
    };

    private static Vector3 WithAxis(Vector3 value, int axis, float component) => axis switch
    {
        AxisX => value with { X = component },
        AxisY => value with { Y = component },
        _ => value with { Z = component }
    };
}
