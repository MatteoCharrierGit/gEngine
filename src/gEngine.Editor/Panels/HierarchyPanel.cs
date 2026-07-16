using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Elenca le entità del World come albero, rispettando la gerarchia dei
/// <see cref="ParentComponent"/>, e permette di selezionarne una.
///
/// L'albero è ricostruito da zero a ogni frame. Sembra sprecato, ma il
/// <c>ParentComponent</c> tiene solo il riferimento verso l'alto (scelta di design:
/// un solo punto di verità, niente lista figli da mantenere in sincronia), quindi
/// la direzione padre→figli va comunque derivata; e l'editor può cambiare la
/// gerarchia in qualsiasi momento, perciò una cache andrebbe invalidata. Con scene
/// costruite a mano il costo è irrilevante: se un giorno darà fastidio, il posto
/// giusto per la cache è lo stesso dirty flag già rimandato per le world matrix.
/// </summary>
public class HierarchyPanel : IEditorPanel
{
    private readonly Dictionary<int, List<Entity>> _childrenByParent = [];
    private readonly List<Entity> _roots = [];

    /// <summary>Comando richiesto in questo frame, applicato a fine disegno. Vedi ApplyPendingCommand.</summary>
    private enum Command { None, Create, Duplicate, Destroy }

    private Command _command = Command.None;

    public void Draw(World world, EditorContext context)
    {
        // FirstUseEver e non Always: è solo la posizione del PRIMO avvio, poi comanda il
        // layout che l'utente si è salvato in imgui.ini. Senza, ImGui darebbe a ogni
        // finestra la stessa posizione di default e i pannelli nascerebbero impilati.
        ImGui.SetNextWindowPos(new Vector2(20, 40), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(280, 460), ImGuiCond.FirstUseEver);

        // Begin restituisce false quando la finestra è collassata, ma End va chiamato
        // comunque: la coppia deve restare bilanciata o ImGui va in assert.
        if (!ImGui.Begin("Hierarchy"))
        {
            ImGui.End();
            return;
        }

        DrawToolbar(world, context);
        ImGui.Separator();

        BuildTree(world);

        foreach (var root in _roots)
            DrawEntity(world, context, root);

        // Clic nel vuoto del pannello = deseleziona. IsWindowHovered senza flag ignora
        // le zone coperte dagli item, quindi non ruba il clic alle righe dell'albero.
        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            context.ClearSelection();

        // Le operazioni sono applicate DOPO aver disegnato l'albero: creare o eliminare
        // un'entità mentre lo si percorre significherebbe modificare gli storage che
        // stiamo iterando.
        ApplyPendingCommand(world, context);

        ImGui.End();
    }

    private void DrawToolbar(World world, EditorContext context)
    {
        if (ImGui.SmallButton("Nuova"))
            _command = Command.Create;

        var hasSelection = context.Selected is { } selected && world.Exists(selected);

        ImGui.SameLine();
        ImGui.BeginDisabled(!hasSelection);

        if (ImGui.SmallButton("Duplica"))
            _command = Command.Duplicate;

        ImGui.SameLine();
        if (ImGui.SmallButton("Elimina"))
            _command = Command.Destroy;

        ImGui.EndDisabled();
    }

    private void ApplyPendingCommand(World world, EditorContext context)
    {
        var command = _command;
        _command = Command.None;

        if (command == Command.None)
            return;

        if (command == Command.Create)
        {
            var created = world.CreateEntity();
            world.AddComponent(created, new NameComponent { Value = "Nuova entità" });
            world.AddComponent(created, new TransformComponent
            {
                // Scale a zero renderebbe l'entità invisibile e sembrerebbe un bug della
                // creazione: un transform neutro è l'unico default onesto.
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            });

            context.Select(created);
            return;
        }

        if (context.Selected is not { } selected || !world.Exists(selected))
            return;

        if (command == Command.Duplicate)
        {
            context.Select(EntityOperations.Duplicate(world, selected));
            return;
        }

        if (command == Command.Destroy)
        {
            EntityOperations.DestroyRecursive(world, selected);

            // La selezione punterebbe a un'entità morta: l'Inspector la scarterebbe
            // comunque (controlla Exists), ma lasciarla lì è uno stato zombie che prima o
            // poi qualcuno leggerà senza verificare.
            context.ClearSelection();
        }
    }

    private void BuildTree(World world)
    {
        _childrenByParent.Clear();
        _roots.Clear();

        foreach (var (entity, parent) in world.Query<ParentComponent>())
        {
            if (!world.Exists(parent.Parent))
                continue; // genitore morto/mai esistito: gestito sotto come radice

            if (!_childrenByParent.TryGetValue(parent.Parent.Id, out var children))
            {
                children = [];
                _childrenByParent[parent.Parent.Id] = children;
            }

            children.Add(entity);
        }

        // Radice = nessun genitore, oppure un genitore che non esiste. Il secondo caso
        // evita che un riferimento rotto faccia sparire l'entità dall'albero senza che
        // nulla lo segnali: meglio vederla a livello zero che non vederla affatto.
        foreach (var entity in world.AllEntities)
        {
            if (world.TryGetComponent<ParentComponent>(entity, out var parent) &&
                world.Exists(parent.Parent))
                continue;

            _roots.Add(entity);
        }
    }

    private void DrawEntity(World world, EditorContext context, Entity entity)
    {
        var hasChildren = _childrenByParent.TryGetValue(entity.Id, out var children);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;

        if (context.IsSelected(entity))
            flags |= ImGuiTreeNodeFlags.Selected;

        if (!hasChildren)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

        // L'identità ImGui della riga è l'id dell'entità, non l'etichetta: entità omonime
        // (o entrambe senza nome) devono restare righe distinte e selezionabili a parte.
        ImGui.PushID(entity.Id);

        var open = ImGui.TreeNodeEx(Label(world, entity), flags);

        // IsItemToggledOpen esclude il clic sulla freccia: aprire un nodo non è selezionarlo.
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            context.Select(entity);

        if (open && hasChildren)
        {
            foreach (var child in children!)
                DrawEntity(world, context, child);

            // Solo i nodi con figli fanno push sullo stack (le foglie hanno NoTreePushOnOpen).
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private static string Label(World world, Entity entity)
    {
        if (world.TryGetComponent<NameComponent>(entity, out var name) &&
            !string.IsNullOrWhiteSpace(name.Value))
            return name.Value;

        return $"Entity {entity.Id}";
    }
}
