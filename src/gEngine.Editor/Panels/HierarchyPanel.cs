using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Editor.Undo;
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
    private enum Command { None, Create, CreateChild, Duplicate, Destroy, Reparent }

    private Command _command = Command.None;

    /// <summary>
    /// Il nuovo genitore di <see cref="_target"/> per <see cref="Command.Reparent"/>, o null
    /// per "portalo a radice". Campo a sé e non un <c>Entity</c> con un valore convenzionale:
    /// "nessun genitore" è uno stato legittimo, non un'entità speciale.
    /// </summary>
    private Entity? _reparentTo;

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

        DrawRootDropZone(world);

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

    /// <summary>
    /// Il bersaglio "portami a radice", che <b>esiste solo mentre si trascina</b> qualcosa che
    /// può diventare radice.
    ///
    /// È una riga vera e non lo spazio vuoto del pannello per un motivo di scopribilità: il
    /// vuoto sotto l'albero non dice di essere un bersaglio, e un utente che vuole staccare un
    /// figlio non ha nessun posto ovvio dove lasciarlo cadere. Mostrarla solo durante il
    /// trascinamento è il compromesso: la riga appare quando serve e non occupa il pannello il
    /// resto del tempo.
    ///
    /// ⚠️ Un <c>Selectable</c> e non un <c>Text</c>: serve un item con un id, perché
    /// <c>BeginDragDropTarget</c> lavora sull'ultimo item disegnato — e un <c>Text</c> non ne
    /// ha uno (è la stessa trappola che con <c>BeginPopupContextItem</c> arriva fino
    /// all'assert nativo).
    /// </summary>
    private void DrawRootDropZone(World world)
    {
        if (!EntityDragDrop.TryPeek(out var dragged) ||
            !EntityOperations.CanReparent(world, dragged, null))
            return;

        ImGui.Selectable("Rilascia qui: diventa radice");

        if (!EntityDragDrop.Target(out var dropped))
            return;

        _command = Command.Reparent;
        _target = dropped;
        _reparentTo = null;
    }

    private void ApplyPendingCommand(World world, EditorContext context)
    {
        var command = _command;
        _command = Command.None;

        if (command == Command.None)
            return;

        if (command == Command.Create)
        {
            context.Select(Created(world, context, "crea entità", () => EntityOperations.Create(world)));
            return;
        }

        // Il bersaglio è stato scelto un frame fa, e nel frattempo il mondo ha girato: un
        // system (o il caricamento di un'altra scena) può averlo distrutto.
        if (!world.Exists(_target))
            return;

        switch (command)
        {
            case Command.CreateChild:
                context.Select(Created(world, context, "crea entità figlia",
                    () => EntityOperations.Create(world, _target)));
                break;

            case Command.Duplicate:
                context.Select(Created(world, context, $"duplica {Label(world, _target)}",
                    () => EntityOperations.Duplicate(world, _target)));
                break;

            // Il valore di ritorno si ignora apposta: false significa che nel frame trascorso
            // fra il rilascio e adesso la mossa è diventata illegale (il genitore è stato
            // distrutto, qualcun altro ha riparentato). Non è un errore da mostrare, è una
            // mossa che non si fa — e la Hierarchy del frame successivo lo fa già vedere.
            //
            // Il riparentamento tocca i componenti di UNA entità (il ParentComponent sta tutto
            // sul figlio, e il Transform ricalcolato pure), quindi entra nell'undo come una
            // qualunque modifica: se non ha cambiato niente, Run non registra niente.
            case Command.Reparent:
                context.Undo.Run(world, _target, $"sposta {Label(world, _target)}",
                    () => EntityOperations.Reparent(world, _target, _reparentTo));
                break;

            case Command.Destroy:
                var doomed = EntityOperations.Descendants(world, _target);

                context.Undo.Push(EntityLifetimeCommand.ForDestruction(
                    world, $"elimina {Label(world, _target)}", doomed,
                    () => EntityOperations.DestroyRecursive(world, _target)));

                // Solo se la selezione è finita nel sottoalbero eliminato: il destro su
                // un'entità seleziona, ma "elimina questa" non è "dimentica quell'altra".
                if (context.Selected is { } selected && !world.Exists(selected))
                    context.ClearSelection();

                break;
        }
    }

    /// <summary>
    /// Crea passando per l'undo. Il nome dell'entità si legge <b>dopo</b>: prima non esiste.
    /// </summary>
    private static Entity Created(World world, EditorContext context, string label, Func<Entity> factory)
    {
        var command = EntityLifetimeCommand.ForCreation(world, label, factory);
        context.Undo.Push(command);
        return command.Entity;
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

        var label = Label(world, entity);
        var open = ImGui.TreeNodeEx(label, flags);

        // Trascinamento: sorgente e bersaglio lavorano entrambi sull'ultimo item disegnato,
        // quindi stanno qui, prima di qualunque altra cosa che possa diventare "l'ultimo item".
        // ⚠️ Vale anche per il menu contestuale qui sotto: l'adiacenza che quel commento chiede
        // regge perché nessuno di questi due disegna un item nella finestra corrente (la
        // sorgente disegna l'anteprima dentro un tooltip, che è un'altra finestra).
        EntityDragDrop.Source(entity, label);

        // Il bersaglio si offre solo se la mossa è legale: un riparentamento su sé stessi o
        // dentro un proprio discendente non si illumina nemmeno. Vedi EntityOperations.CanReparent
        // per il perché il controllo è qui e non dopo il rilascio.
        if (EntityDragDrop.TryPeek(out var dragged) &&
            EntityOperations.CanReparent(world, dragged, entity) &&
            EntityDragDrop.Target(out var dropped))
        {
            _command = Command.Reparent;
            _target = dropped;
            _reparentTo = entity;
        }

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
