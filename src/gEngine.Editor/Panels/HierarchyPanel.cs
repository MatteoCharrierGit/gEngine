using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Rendering;
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
public class HierarchyPanel() : PanelBase("Hierarchy", new Vector2(20, 40), new Vector2(280, 460))
{
    private readonly Dictionary<int, List<Entity>> _childrenByParent = [];
    private readonly List<Entity> _roots = [];

    /// <summary>Comando richiesto in questo frame, applicato a fine disegno. Vedi ApplyPendingCommand.</summary>
    private enum Command { None, Create, CreateChild, Duplicate, Destroy }

    private Command _command = Command.None;

    /// <summary>
    /// L'entità su cui agisce <see cref="_command"/>.
    ///
    /// Esplicito e <b>non</b> "la selezione": il menu contestuale parla della riga su cui si
    /// è cliccato: leggere la selezione al momento di applicare il comando vorrebbe dire che
    /// due righe diverse — quella cliccata e quella selezionata — possono essere quella
    /// sbagliata. Che poi il destro selezioni anche è un servizio all'Inspector, non il
    /// canale con cui il comando sa su chi agire.
    /// </summary>
    private Entity _target;

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        BuildTree(world);

        foreach (var root in _roots)
            DrawEntity(world, context, root);

        // Clic nel vuoto del pannello = deseleziona. IsWindowHovered senza flag ignora
        // le zone coperte dagli item, quindi non ruba il clic alle righe dell'albero.
        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            context.ClearSelection();

        DrawWindowContextMenu();

        // Le operazioni sono applicate DOPO aver disegnato l'albero: creare o eliminare
        // un'entità mentre lo si percorre significherebbe modificare gli storage che
        // stiamo iterando.
        ApplyPendingCommand(world, context);
    }

    /// <summary>
    /// Il menu del destro <b>nel vuoto</b>: qui non c'è un'entità di cui parlare, quindi
    /// l'unica voce sensata è crearne una.
    ///
    /// ⚠️ <c>NoOpenOverItems</c> non è cosmetico: senza, il destro su una riga aprirebbe
    /// <b>anche</b> questo popup, oltre a quello della riga. Sono due id diversi, quindi
    /// ImGui non ha niente da segnalare — si vedrebbe solo il menu sbagliato, quello senza
    /// Duplica/Elimina, esattamente dove ci si aspetta l'altro.
    /// </summary>
    private void DrawWindowContextMenu()
    {
        if (!ImGui.BeginPopupContextWindow("hierarchy-vuoto",
                ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            return;

        if (ImGui.MenuItem("Crea entità"))
            _command = Command.Create;

        ImGui.EndPopup();
    }

    /// <summary>
    /// Il menu del destro <b>su una riga</b>. I bottoni che c'erano al posto suo (Nuova /
    /// Duplica / Elimina in una toolbar) chiedevano di selezionare prima e cliccare poi, e
    /// tenevano occupata una riga del pannello per dire cose che valgono per l'entità che si
    /// sta guardando.
    /// </summary>
    private void DrawEntityContextMenu(Entity entity)
    {
        if (!ImGui.BeginPopupContextItem())
            return;

        if (ImGui.MenuItem("Crea entità figlia"))
        {
            _command = Command.CreateChild;
            _target = entity;
        }

        if (ImGui.MenuItem("Duplica"))
        {
            _command = Command.Duplicate;
            _target = entity;
        }

        // Separato: le due voci sopra aggiungono, questa toglie — e si porta via i figli.
        ImGui.Separator();

        if (ImGui.MenuItem("Elimina"))
        {
            _command = Command.Destroy;
            _target = entity;
        }

        ImGui.EndPopup();
    }

    private void ApplyPendingCommand(World world, EditorContext context)
    {
        var command = _command;
        _command = Command.None;

        if (command == Command.None)
            return;

        if (command == Command.Create)
        {
            context.Select(EntityOperations.Create(world));
            return;
        }

        // Il bersaglio è stato scelto un frame fa, e nel frattempo il mondo ha girato: un
        // system (o il caricamento di un'altra scena) può averlo distrutto.
        if (!world.Exists(_target))
            return;

        switch (command)
        {
            case Command.CreateChild:
                context.Select(EntityOperations.Create(world, _target));
                break;

            case Command.Duplicate:
                context.Select(EntityOperations.Duplicate(world, _target));
                break;

            case Command.Destroy:
                EntityOperations.DestroyRecursive(world, _target);

                // Solo se la selezione è finita nel sottoalbero eliminato: il destro su
                // un'entità seleziona, ma "elimina questa" non è "dimentica quell'altra".
                if (context.Selected is { } selected && !world.Exists(selected))
                    context.ClearSelection();

                break;
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
        // Anche il destro seleziona: il menu che sta per aprirsi parla di questa entità, e
        // l'Inspector deve mostrare quella di cui si sta per decidere la sorte — non quella
        // cliccata l'ultima volta col sinistro.
        if ((ImGui.IsItemClicked() || ImGui.IsItemClicked(ImGuiMouseButton.Right)) &&
            !ImGui.IsItemToggledOpen())
            context.Select(entity);

        // Subito dopo l'item: BeginPopupContextItem senza id usa l'id dell'ultimo disegnato,
        // cioè questa riga. Prima di scendere nei figli, che diventerebbero "l'ultimo item".
        DrawEntityContextMenu(entity);

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
