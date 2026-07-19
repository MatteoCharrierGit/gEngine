using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Editor.Undo;
using gEngine.Rendering;
using gEngine.Scenes;

namespace gEngine.Editor;

/// <summary>
/// Il catalogo degli oggetti di scena che l'editor sa creare: cubo, sfera, luce, camera,
/// vuoto.
///
/// <b>Non è codice nuovo, è un assemblaggio.</b> "Un cubo" è <c>EntityOperations.Create</c> più
/// il <b>default dichiarato</b> del MeshRenderer, preso dal <see cref="SceneComponentRegistry"/>.
/// Quei default esistono dalla Fase 4.7 e sono scelti perché aggiungere il componente <b>si
/// veda</b> (scala 1, intensità 1, visibile): ricostruirli qui vorrebbe dire avere due verità su
/// cosa sia un cubo, e quella sbagliata sarebbe questa.
///
/// Sta in un file suo e non dentro un pannello perché lo usano <b>due</b> pannelli — la
/// Hierarchy e il File system — e la seconda copia sarebbe il posto dove un domani il cubo nasce
/// diverso.
///
/// ⚠️ Serve il registry, e può <b>mancare</b>: lo dichiara il gioco fra le sue Resource. Senza,
/// resta creabile solo il <b>vuoto</b>, che non chiede niente a nessuno. Chi disegna il menu
/// mostra il resto <b>spento col motivo</b>, non lo nasconde — sparire farebbe cercare la
/// registrazione mancante nel posto sbagliato.
/// </summary>
public static class SceneObjects
{
    /// <summary>
    /// Cosa si sta creando. Un enum e non l'etichetta del menu: switchare sulla stringa
    /// mostrata all'utente lega il comportamento al testo, e il giorno che "Cubo" diventa
    /// "Cubo (primitiva)" il cubo smette di avere una mesh — in silenzio.
    /// </summary>
    public enum Kind { Empty, Cube, Sphere, Light, Camera }

    /// <param name="NeedsRegistry">
    /// Se per costruirlo servono i default dichiarati dal gioco. Falso solo per il vuoto.
    /// </param>
    public readonly record struct Entry(Kind Kind, string Label, string Tooltip, bool NeedsRegistry);

    /// <summary>
    /// L'ordine è quello del menu, e non è alfabetico: prima le due forme che si vedono, poi
    /// quelle che cambiano come si vede il resto, e il vuoto in fondo perché è il caso di chi sa
    /// già cosa vuole costruirci sopra.
    /// </summary>
    public static IReadOnlyList<Entry> All { get; } =
    [
        new(Kind.Cube, "Cubo", "Transform + MeshRenderer: un cubo unitario bianco e visibile.", true),
        new(Kind.Sphere, "Sfera", "Come il cubo, ma sfera: inscritta nel cubo unitario.", true),
        new(Kind.Light, "Luce", "Luce direzionale bianca a intensita' 1: si aggiunge una luce per illuminare.", true),
        new(Kind.Camera, "Camera", "Camera prospettica NON primaria: non ruba l'inquadratura a quella che c'e' gia'.", true),
        new(Kind.Empty, "Vuoto", "Solo Transform e nome. L'unica creabile anche senza il registry del gioco.", false)
    ];

    /// <summary>
    /// Crea l'oggetto <b>passando dall'undo</b>, e restituisce l'entità — o <c>null</c> se
    /// mancava il registry, che è il solo motivo per cui può non farsi.
    ///
    /// L'undo non è un di più: creare è un'azione che si fa per sbaglio con un clic, e senza
    /// Ctrl+Z l'unico rimedio sarebbe cercare l'entità nuova nella Hierarchy ed eliminarla a
    /// mano.
    /// </summary>
    public static Entity? Create(World world, EditorContext context, Entry entry, Entity? parent = null)
    {
        if (entry.NeedsRegistry && context.Components is null)
            return null;

        var command = EntityLifetimeCommand.ForCreation(
            world, $"crea {entry.Label.ToLowerInvariant()}",
            () => Build(world, context.Components, entry, parent));

        context.Undo.Push(command);
        return command.Entity;
    }

    private static Entity Build(World world, SceneComponentRegistry? registry, Entry entry, Entity? parent)
    {
        var entity = EntityOperations.Create(world, parent, entry.Label);

        if (registry is null)
            return entity; // solo il vuoto arriva qui: vedi la guardia in Create

        switch (entry.Kind)
        {
            case Kind.Cube:
                AddDefault(world, registry, entity, typeof(MeshRendererComponent));
                break;

            case Kind.Sphere:
                // ⚠️ Si parte dal default del MeshRenderer e si cambia UN campo, invece di
                // costruirne uno a mano: tutti gli altri (Tint, Visible, Layer, Size) sono le
                // scelte della Fase 4.7 sul perche' un componente aggiunto si veda, e
                // riscriverle qui significherebbe che il giorno che cambiano la sfera resta
                // indietro.
                //
                // Il campo si cambia PRIMA di aggiungere, cosi' non c'e' nessuna domanda su
                // cosa significhi mutare un componente gia' nello storage - che su
                // MeshRendererComponent, unica class fra i componenti, sarebbe una domanda vera.
                if (registry.TryCreateDefault(typeof(MeshRendererComponent), out var component) &&
                    component is MeshRendererComponent mesh)
                {
                    mesh.Kind = MeshKind.Sphere;
                    world.AddComponent(entity, mesh);
                }

                break;

            case Kind.Light:
                AddDefault(world, registry, entity, typeof(LightComponent));
                break;

            case Kind.Camera:
                AddDefault(world, registry, entity, typeof(CameraComponent));
                break;

            case Kind.Empty:
                break; // il Transform gliel'ha gia' dato EntityOperations.Create
        }

        return entity;
    }

    /// <summary>
    /// Aggiunge il default dichiarato per quel tipo, se c'è.
    ///
    /// ⚠️ "Non c'è" non è un errore da far esplodere: un gioco può aver registrato il tipo senza
    /// factory del default — è legittimo e dichiarato (vedi <c>ParentComponent</c>). L'entità
    /// nasce comunque col suo Transform: l'utente vede una cosa incompleta invece di
    /// un'eccezione dentro un frame di disegno.
    ///
    /// Non generico: il tipo arriva come <c>Type</c> perché è così che il registry lo indicizza,
    /// ed è la stessa faccia non generica che usa l'Inspector.
    /// </summary>
    private static void AddDefault(World world, SceneComponentRegistry registry, Entity entity, Type type)
    {
        if (registry.TryCreateDefault(type, out var component))
            world.AddComponent(entity, component);
    }
}
