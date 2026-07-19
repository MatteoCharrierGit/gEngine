using System.Numerics;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Interfaces.System;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// L'inventario globale dei system: quali girano, <b>in che ordine</b> e in quale fase.
///
/// È il gemello globale della traceability dell'Inspector, e la differenza è il verso della
/// domanda. Lì si parte da un'entità e si chiede chi la tocca; qui si parte dal gioco e si
/// chiede di cosa è fatto. Sono i due modi in cui ci si perde davanti a un ECS: "perché
/// questo cubo non cade" e "cosa gira, esattamente".
///
/// I system sono elencati <b>per fase</b> e nell'ordine reale di esecuzione, non in ordine
/// alfabetico né di registrazione: dentro una fase l'ordine <b>è</b> comportamento
/// (⚠️ <c>LightingSystem</c> prima di <c>MeshRenderSystem</c>, o le luci arrivano dopo le
/// mesh che dovevano illuminare) ed è esattamente ciò che un elenco può mostrare e il codice
/// no. Il <see cref="SystemRegistry"/> espone le viste per fase apposta.
///
/// ⚠️ Un system in due fasi compare <b>due volte</b>. Non è un bug della lista: è che gira
/// due volte per tick, una per fase (vedi <c>SystemRegistry.Add</c>). Vederlo una volta sola
/// nasconderebbe proprio la cosa che rende quel caso una trappola.
/// </summary>
public class SystemsPanel : PanelBase
{
    public SystemsPanel() : base("Systems", new Vector2(380, 100), new Vector2(320, 320))
    {
        // Spento all'avvio, e si accende dal menu Panels: vedi il gemello ComponentsPanel.
        Visible = false;
    }

    /// <summary>
    /// I system tolti in questa sessione, tenuti per poterli rimettere.
    ///
    /// Esistono perché l'editor non sa <b>costruire</b> un system: "aggiungi" nel senso di
    /// istanziarne uno nuovo non si può fare (vedi <see cref="DrawAdd"/>), quindi senza
    /// questa lista togliere un system sarebbe irreversibile fino al riavvio — e "spegni il
    /// PhysicsSystem e guarda cosa succede" è metà del motivo per cui questo pannello esiste.
    /// L'istanza c'è già: rimetterla non richiede di saperla creare.
    /// </summary>
    private readonly List<ISystem> _removed = [];

    /// <summary>Applicati a fine disegno: Add/Remove modificano le liste che stiamo scorrendo.</summary>
    private ISystem? _toRemove;
    private ISystem? _toRestore;

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        // Stessa regola della traceability nell'Inspector: il registry lo dichiara il gioco e
        // può non esserci. Un pannello vuoto direbbe "nessun system", che è una cosa falsa e
        // per giunta rassicurante.
        if (context.Systems is not { } registry)
        {
            ImGui.TextDisabled("Registry dei system non disponibile.");
            HelpMarker(
                "Il gioco non ha dichiarato un SystemRegistry fra le sue Resource.\n" +
                "L'editor non ha modo di sapere quali system girano: non è \"nessuno\".");
            return;
        }

        DrawPhase(registry, SystemPhase.Input, registry.InputSystems);
        DrawPhase(registry, SystemPhase.Simulation, registry.SimulationSystems);
        DrawPhase(registry, SystemPhase.Late, registry.LateSystems);
        DrawPhase(registry, SystemPhase.Render, registry.RenderSystems);

        DrawRemoved();

        ImGui.Spacing();
        DrawAdd();

        ApplyPending(registry);
    }

    private void DrawPhase(SystemRegistry registry, SystemPhase phase, IEnumerable<ISystem> systems)
    {
        var inPhase = systems.ToList();

        ImGui.SeparatorText($"{phase} ({inPhase.Count})");

        if (inPhase.Count == 0)
        {
            ImGui.TextDisabled("nessuno");
            return;
        }

        // ⚠️ L'id della fase avvolge quelli delle righe. Senza, la riga 0 di Input e la riga 0
        // di Simulation avrebbero lo stesso id — ImGui non guarda in che SeparatorText stai,
        // guarda lo stack degli id — e i loro menu contestuali sarebbero lo stesso menu.
        // Nemmeno il nome del tipo basterebbe: registrare due istanze dello stesso system è
        // legittimo. L'indice dentro la fase invece è unico per costruzione.
        ImGui.PushID((int)phase);

        for (var i = 0; i < inPhase.Count; i++)
        {
            var system = inPhase[i];

            ImGui.PushID(i);

            // Il numero è l'informazione: dice dove sta questo system nella catena della sua
            // fase, che è ciò che decide se le sue scritture arrivano prima o dopo.
            //
            // ⚠️ Selectable e non TextUnformatted, e non è estetica: un Text per ImGui non ha
            // un id, e BeginPopupContextItem() senza id usa quello dell'ultimo item — su un
            // Text trova 0 e va in IM_ASSERT. Su Windows quell'assert è una dialog modale
            // nativa, quindi il gioco non "crasha": si pianta al primo frame, in silenzio,
            // aspettando un clic su una finestra che nessuno ha chiesto. Il Selectable dà
            // anche l'evidenziazione al passaggio, che è ciò che dice "questa riga fa
            // qualcosa" a chi non sa che c'è un menu contestuale.
            ImGui.Selectable($"{i + 1}. {system.GetType().Name}");

            // Il tooltip prima del menu: legge IsItemHovered, che parla dell'ultimo item
            // disegnato. BeginPopupContextItem non ne aggiunge uno, quindi l'ordine oggi non
            // conta — ma dipenderci sarebbe fidarsi di un dettaglio di ImGui invece che
            // dell'ovvio.
            DrawDeclaredComponents(registry, system);
            DrawSystemContextMenu(system);

            ImGui.PopID();
        }

        ImGui.PopID();
    }

    /// <summary>
    /// Cosa il system <b>dichiara</b> di toccare. Sotto il puntatore e non in riga: sono
    /// elenchi lunghi, e la riga serve a leggere l'ordine.
    /// </summary>
    private static void DrawDeclaredComponents(SystemRegistry registry, ISystem system)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            return;

        var phases = registry.Systems
            .FirstOrDefault(registered => ReferenceEquals(registered.System, system))
            .Phases;

        ImGui.BeginTooltip();
        ImGui.TextUnformatted(system.GetType().FullName ?? system.GetType().Name);
        ImGui.TextDisabled($"Fasi: {phases}");
        ImGui.Separator();

        DrawTypeList("Agisce su (MatchedComponents)", system.MatchedComponents);
        DrawTypeList("Legge (ObservedComponents)", system.ObservedComponents);

        ImGui.EndTooltip();
    }

    private static void DrawTypeList(string title, IReadOnlyList<Type> types)
    {
        ImGui.TextUnformatted(title);

        // ⚠️ "non dichiarato" e non "nessuno": è la stessa distinzione di SystemMatch.Unknown.
        // Un elenco vuoto qui vuol dire che il system non l'ha scritto, non che non tocchi
        // niente — vedi ISystem.MatchedComponents.
        if (types.Count == 0)
        {
            ImGui.TextDisabled("   non dichiarato");
            return;
        }

        foreach (var type in types)
            ImGui.TextDisabled($"   {type.Name}");
    }

    private void DrawSystemContextMenu(ISystem system)
    {
        if (!ImGui.BeginPopupContextItem())
            return;

        if (ImGui.MenuItem("Rimuovi"))
            _toRemove = system;

        // ⚠️ Questo tooltip diceva "non è un OnDestroy - ISystem non ce l'ha". Adesso ce l'ha
        // (il registry lo chiama uscendo), quindi la frase è cambiata invece di restare a
        // mentire. Il limite vero è un altro ed è dichiarato qui sotto: quel che il system
        // libera dipende da lui.
        HelpMarker(
            "Toglie il system da tutte le sue fasi: smette di girare subito, e riceve\n" +
            "OnDestroy per liberare quel che possiede fuori dall'ECS. Il PhysicsSystem\n" +
            "toglie i suoi corpi dal mondo Bepu.\n\n" +
            "Attenzione: OnDestroy libera solo ciò che il system ha CREATO, non ciò che ha\n" +
            "ricevuto: le Resource del gioco (il mondo fisico, l'input) restano in piedi,\n" +
            "o rimettere il system lo aggancerebbe a un oggetto morto.\n\n" +
            "Attenzione: un system scritto senza OnDestroy non libera niente - il default\n" +
            "dell'interfaccia e' vuoto. Toglierlo e' sicuro per l'ECS, non per quel che\n" +
            "il system tiene da parte.\n\n" +
            "L'istanza resta qui sotto, in \"Rimossi\", e si può rimettere.");

        ImGui.EndPopup();
    }

    private void DrawRemoved()
    {
        if (_removed.Count == 0)
            return;

        ImGui.SeparatorText($"Rimossi ({_removed.Count})");

        ImGui.TextDisabled("non girano più");
        HelpMarker(
            "Attenzione: Ripristina rimette il system IN FONDO alla sua fase, non dov'era: il registry\n" +
            "smista in ordine di registrazione e non sa da dove veniva. Dentro una fase\n" +
            "l'ordine conta - ripristinare il LightingSystem lo mette dopo il MeshRenderSystem,\n" +
            "cioè le luci arriverebbero dopo le mesh che dovevano illuminare.\n\n" +
            "L'elenco qui sopra lo mostra subito: il numero della riga è la verità.\n\n" +
            "Ripristina richiama OnCreate sulla stessa istanza. Non è più la stonatura che\n" +
            "era: adesso Rimuovi chiama OnDestroy, quindi i due si fanno il paio e l'istanza\n" +
            "riparte da uno stato pulito - il PhysicsSystem ricrea i corpi al primo update.");

        // "removed" e non l'indice nudo: questa lista convive nella stessa finestra con le
        // righe delle fasi, che usano già gli indici. Vedi il commento in DrawPhase.
        ImGui.PushID("removed");

        for (var i = 0; i < _removed.Count; i++)
        {
            ImGui.PushID(i);

            if (ImGui.SmallButton("Ripristina"))
                _toRestore = _removed[i];

            ImGui.SameLine();
            ImGui.TextDisabled(_removed[i].GetType().Name);

            ImGui.PopID();
        }

        ImGui.PopID();
    }

    /// <summary>
    /// Il buco dichiarato: non c'è "aggiungi".
    ///
    /// Non è rimandato per tempo, è che non si può fare come si fa per i componenti. Là
    /// l'engine non conosceva i tipi e il gioco li dichiara una volta nel
    /// <c>SceneComponentRegistry</c>, default compreso. Qui il problema è un altro e più
    /// duro: un system non è un dato, ha delle <b>dipendenze</b>. <c>PlayerInputSystem</c>
    /// vuole un <c>InputHandler</c>, <c>PhysicsSystem</c> un <c>IPhysicsWorld</c> — non
    /// esiste un "system di default" da costruire, e un <c>Activator.CreateInstance</c>
    /// fallirebbe sul primo costruttore con un parametro.
    ///
    /// La strada, se servirà, è la stessa del registry dei componenti: il gioco dichiara le
    /// factory dei suoi system, che è l'unico posto dove le dipendenze si sanno. Non è stato
    /// fatto perché non c'è ancora un caso d'uso — si tolgono i system per capire chi fa
    /// cosa, non se ne aggiungono di nuovi da un pannello.
    /// </summary>
    private static void DrawAdd()
    {
        // ⚠️ -30 e non -1 (tutta la larghezza): l'HelpMarker qui sotto fa un SameLine, e con
        // il bottone fino al bordo il "(?)" finiva fuori dal pannello — cioè un bottone spento
        // senza il motivo accanto, che è l'unica cosa che questa riga ha da dire. Stesso
        // inciampo dello slot degli asset nell'Inspector.
        ImGui.BeginDisabled();
        ImGui.Button("Aggiungi system", new Vector2(-30f, 0f));
        ImGui.EndDisabled();

        HelpMarker(
            "Non implementato, e non per dimenticanza: un system ha delle dipendenze\n" +
            "(PlayerInputSystem vuole un InputHandler, PhysicsSystem un IPhysicsWorld),\n" +
            "quindi non esiste un \"system di default\" che l'editor possa costruire - al\n" +
            "contrario dei componenti, che sono dati e il cui default il gioco dichiara nel\n" +
            "SceneComponentRegistry.\n\n" +
            "Servirebbe che il gioco dichiarasse anche le factory dei suoi system. I system\n" +
            "che il gioco registra all'avvio si tolgono e si rimettono da qui.");
    }

    private void ApplyPending(SystemRegistry registry)
    {
        if (_toRemove is { } toRemove)
        {
            if (registry.Remove(toRemove))
                _removed.Add(toRemove);

            _toRemove = null;
        }

        if (_toRestore is { } toRestore)
        {
            // Il controllo prima dell'Add: il registry lancia su un'istanza già registrata
            // (girerebbe due volte), e un'eccezione qui cadrebbe nel frame di disegno. Non
            // dovrebbe succedere — si arriva qui solo da una Remove riuscita — ma il gioco
            // resta libero di riaggiungere il system per conto suo mentre l'editor guarda.
            if (!registry.Systems.Any(registered => ReferenceEquals(registered.System, toRestore)))
                registry.Add(toRestore);

            _removed.Remove(toRestore);
            _toRestore = null;
        }
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
