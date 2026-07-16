using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Rendering;
using gEngine.Scenes;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// L'inventario globale dei componenti: di quali tipi è fatta questa scena, quanti ce ne
/// sono, e quali l'editor sa aggiungere.
///
/// Il gemello del pannello Systems, con lo stesso verso: l'Inspector parte da un'entità e
/// dice cos'ha addosso, questo parte dal gioco e dice di cosa è fatto.
///
/// La sezione che giustifica il pannello è però la terza, <b>"nel World ma non registrati"</b>,
/// ed è un allarme che prima non aveva dove suonare: <c>SceneSerializer</c> <b>lancia</b>
/// davanti a un componente senza writer, quindi un tipo che finisce nel World senza essere
/// stato registrato rende la scena <b>non salvabile</b> — e lo si scopriva premendo Salva,
/// magari dopo mezz'ora di lavoro, con un messaggio d'errore al posto del file. Qui si vede
/// prima, che è l'unico momento in cui serve saperlo.
/// </summary>
public class ComponentsPanel : PanelBase
{
    public ComponentsPanel() : base("Components", new Vector2(420, 140), new Vector2(320, 320))
    {
        // Spento all'avvio, e si accende dal menu Panels. Il layout di default è a 5 pannelli
        // ed è quello con cui si lavora; questo è uno strumento diagnostico — si apre quando
        // ci si chiede di cosa è fatta la scena, non si tiene addosso.
        Visible = false;
    }

    /// <summary>Applicati a fine disegno: cambiano gli storage che stiamo scorrendo.</summary>
    private Type? _toAdd;
    private Type? _toRemoveFromAll;

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        // Contati una volta e non per riga: la riga della sezione "registrati" chiede il
        // conteggio di un tipo che negli storage potrebbe non esserci affatto (un tipo
        // registrato che nessuna entità usa), e cercarlo scandendo gli storage per ognuno
        // renderebbe quadratico un pannello che è solo un elenco.
        var counts = world.ComponentStorages.ToDictionary(
            storage => storage.ComponentType,
            storage => storage.Count);

        if (context.Components is not { } registry)
        {
            ImGui.TextDisabled("Registry dei componenti non disponibile.");
            HelpMarker(
                "Il gioco non ha dichiarato un SceneComponentRegistry fra le sue Resource.\n" +
                "Senza, non si può sapere quali tipi esistano né quali siano salvabili: non\n" +
                "è \"nessuno\".");

            DrawUnregistered(counts, registry: null);
            return;
        }

        DrawRegistered(world, context, registry, counts);
        DrawUnregistered(counts, registry);

        ApplyPending(world, context);
    }

    private void DrawRegistered(World world, EditorContext context, SceneComponentRegistry registry,
        Dictionary<Type, int> counts)
    {
        var registered = registry.RegisteredComponents.ToList();

        ImGui.SeparatorText($"Registrati ({registered.Count})");
        ImGui.TextDisabled("dichiarati dal gioco · salvabili");
        HelpMarker(
            "I tipi che il gioco ha registrato nel SceneComponentRegistry: l'editor sa come\n" +
            "si chiamano nel file, come si scrivono e - se hanno un default dichiarato - come\n" +
            "crearne uno.\n\n" +
            "Il numero è quante entità ce l'hanno addosso adesso. Zero è normale: un tipo si\n" +
            "registra perché esiste, non perché è in uso.");

        foreach (var component in registered)
        {
            ImGui.PushID(component.Key);

            var count = counts.GetValueOrDefault(component.Type);

            // Il nome è la chiave del file, non quello del tipo: è la parola con cui si
            // scrive nel .json ed è quella che serve a chi sta guardando la scena a mano.
            // Il tipo vero sta nel tooltip.
            //
            // ⚠️ Selectable e non TextUnformatted: un Text non ha id per ImGui, e il
            // BeginPopupContextItem() qui sotto ne ha bisogno — vedi la stessa nota in
            // SystemsPanel, dove la trappola è stata pagata.
            ImGui.Selectable(component.CanCreateDefault ? component.Key : $"{component.Key}  (no default)");

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            {
                ImGui.SetTooltip(component.CanCreateDefault
                    ? $"{component.Type.FullName}\n\nChiave nel file di scena: \"{component.Key}\""
                    : $"{component.Type.FullName}\n\nChiave nel file di scena: \"{component.Key}\"\n\n" +
                      "Non dichiara un valore di default: si salva e si carica, ma l'editor\n" +
                      "non sa crearne uno da zero. Per Parent è voluto (un genitore di\n" +
                      "default non esiste, ci si riparenta dalla Hierarchy).");
            }

            DrawRegisteredContextMenu(world, context, component, count);

            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 60f);
            ImGui.TextDisabled($"{count}");

            ImGui.PopID();
        }
    }

    private void DrawRegisteredContextMenu(World world, EditorContext context,
        RegisteredComponent component, int count)
    {
        if (!ImGui.BeginPopupContextItem())
            return;

        var selected = context.Selected is { } entity && world.Exists(entity) ? entity : (Entity?)null;

        // Tre motivi diversi per cui non si può aggiungere, e vanno distinti: non c'è un
        // bersaglio, il bersaglio ce l'ha già, o nessuno ha detto come si crea. Un solo
        // "spento" senza spiegazione manderebbe a cercare la causa sbagliata.
        var already = selected is { } target && world.HasComponent(target, component.Type);
        var canAdd = selected is not null && !already && component.CanCreateDefault;

        var label = selected is { } value
            ? $"Aggiungi a \"{Label(world, value)}\""
            : "Aggiungi (nessuna entità selezionata)";

        if (ImGui.MenuItem(label, null, false, canAdd))
            _toAdd = component.Type;

        if (already)
            ImGui.TextDisabled("   l'entità ce l'ha già");
        else if (!component.CanCreateDefault)
            ImGui.TextDisabled("   nessun default dichiarato");

        ImGui.Separator();

        if (ImGui.MenuItem($"Rimuovi da tutte ({count})", null, false, count > 0))
            _toRemoveFromAll = component.Type;

        // ⚠️ Non c'è undo da nessuna parte in questo editor, e questa è la voce che ne fa
        // sentire di più la mancanza: dirlo prima è tutto ciò che si può fare.
        HelpMarker(
            "Toglie questo componente da TUTTE le entità della scena, subito.\n\n" +
            "Attenzione: Non c'è undo. Il file su disco non è toccato: File > Open Scene rilegge\n" +
            "la scena da lì e rimette tutto - a patto di non aver salvato nel frattempo.");

        ImGui.EndPopup();
    }

    /// <summary>
    /// I tipi che vivono nel World e che il registry non conosce.
    ///
    /// Due sono sempre qui e vanno bene: <see cref="NameComponent"/> (nel file è il campo
    /// <c>name</c> dell'entità, non un componente) e chiunque sia marcato
    /// <see cref="RuntimeStateAttribute"/> (lo ricrea il system che lo possiede). Il
    /// <c>SceneSerializer</c> li salta prima di chiedere al registry, quindi la loro assenza
    /// non è un problema — e infatti sono elencati con la loro ragione accanto, non come
    /// avvisi.
    ///
    /// Chi resta è il caso vero: un componente registrato per errore da nessuna parte, che
    /// fa <b>fallire il salvataggio dell'intera scena</b>.
    /// </summary>
    private static void DrawUnregistered(Dictionary<Type, int> counts, SceneComponentRegistry? registry)
    {
        var known = registry?.RegisteredComponents.Select(component => component.Type).ToHashSet() ?? [];

        var unregistered = counts.Keys
            .Where(type => !known.Contains(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

        if (unregistered.Count == 0)
            return;

        ImGui.SeparatorText($"Nel World ma non registrati ({unregistered.Count})");

        foreach (var type in unregistered)
        {
            var reason = Excuse(type);

            if (reason is null)
            {
                // L'unico rosso del pannello, e se lo merita: questa riga è una scena che non
                // si salva. Il messaggio è quello che lancerebbe il serializer, ma qui arriva
                // prima di aver perso il lavoro.
                //
                // ⚠️ "(!)" e non un glifo di avviso: il font di default di ImGui non ha gli
                // emoji e disegnerebbe un "??" — succede davvero, si è visto in uno
                // screenshot. Nei commenti il ⚠️ resta: quelli non li legge ImGui.
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"{type.Name}  (!)");

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
                    ImGui.SetTooltip(
                        $"{type.FullName}\n\n" +
                        "Attenzione: Il salvataggio della scena FALLIRÀ finché questo tipo è addosso a\n" +
                        "un'entità: SceneSerializer non sa come si chiama nel file né come si\n" +
                        "scrive, e lancia invece di saltarlo in silenzio.\n\n" +
                        "Registralo nel SceneComponentRegistry, oppure marcalo [RuntimeState]\n" +
                        "se è stato di runtime che non deve essere salvato.");

                continue;
            }

            ImGui.TextDisabled($"{type.Name}");

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
                ImGui.SetTooltip($"{type.FullName}\n\n{reason}");
        }
    }

    /// <returns>Perché questo tipo può stare fuori dal registry, o null se non può.</returns>
    private static string? Excuse(Type type)
    {
        if (type == typeof(NameComponent))
            return "Va bene così: nel file il nome è il campo \"name\" dell'entità, non un\n" +
                   "componente. SceneSerializer lo salta apposta - scriverlo anche come\n" +
                   "componente darebbe due punti di verità che possono divergere.\n\n" +
                   "Attenzione: Il rovescio: non essendo registrato, l'editor non sa aggiungerlo. Un'entità\n" +
                   "a cui è stato tolto il nome non può riaverlo dall'Inspector.";

        if (type.IsDefined(typeof(RuntimeStateAttribute), inherit: false))
            return "Va bene così: è marcato [RuntimeState], cioè un link a qualcosa che vive\n" +
                   "fuori dall'ECS. SceneSerializer lo salta e il system che lo possiede lo\n" +
                   "ricrea al prossimo update.";

        return null;
    }

    private void ApplyPending(World world, EditorContext context)
    {
        if (_toAdd is { } toAdd)
        {
            if (context.Selected is { } entity && world.Exists(entity) &&
                context.Components is { } registry &&
                registry.TryCreateDefault(toAdd, out var created))
                world.AddComponent(entity, created);

            _toAdd = null;
        }

        if (_toRemoveFromAll is { } toRemove)
        {
            foreach (var storage in world.ComponentStorages)
            {
                if (storage.ComponentType == toRemove)
                    storage.Clear();
            }

            _toRemoveFromAll = null;
        }
    }

    private static string Label(World world, Entity entity)
    {
        return world.TryGetComponent<NameComponent>(entity, out var name) &&
               !string.IsNullOrWhiteSpace(name.Value)
            ? name.Value
            : $"Entity {entity.Id}";
    }

    /// <summary>Il "(?)" con la spiegazione sotto il puntatore. Vedi il gemello nell'Inspector.</summary>
    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
