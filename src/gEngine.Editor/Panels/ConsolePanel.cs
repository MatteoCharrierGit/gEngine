using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Log;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Il log del gioco, dentro l'editor. <b>Tutto il flusso</b>, non solo gli errori — che è la
/// richiesta da cui è nato: un log che mostra solo i guasti non racconta cosa stava facendo il
/// gioco quando è successo, ed è proprio quella la domanda che ci si fa davanti a un guasto.
///
/// Non è un sink: <b>legge</b> la <see cref="LogHistory"/> dichiarata fra le Resource. La
/// differenza conta — un pannello-sink dovrebbe registrarsi e sregistrarsi con la propria vita,
/// e comunque nascerebbe troppo tardi per vedere l'avvio (la finestra logga prima che l'editor
/// esista). Leggendo un buffer che qualcun altro riempie dal primo istante, la console mostra
/// anche ciò che è successo prima che ci fosse una console.
/// </summary>
public class ConsolePanel : PanelBase
{
    public ConsolePanel() : base("Console", new Vector2(340, 700), new Vector2(760, 260))
    {
    }

    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 WarningColor = new(0.95f, 0.8f, 0.35f, 1f);
    private static readonly Vector4 DebugColor = new(0.55f, 0.55f, 0.58f, 1f);

    private const string TutteLeCategorie = "(tutte)";

    private bool _mostraDebug = true;
    private bool _mostraInfo = true;
    private bool _mostraWarning = true;
    private bool _mostraError = true;

    private string _cerca = string.Empty;
    private string _categoria = TutteLeCategorie;

    /// <summary>Seguire la coda del log, come farebbe un <c>tail -f</c>.</summary>
    private bool _segui = true;

    /// <summary>
    /// Quanti errori erano passati l'ultima volta che il pannello si è aperto da sé.
    ///
    /// ⚠️ Il confronto è con <see cref="LogHistory.TotalErrors"/>, che è monotòno: contare gli
    /// errori <i>presenti</i> non funzionerebbe, perché quando il buffer gira il conteggio
    /// scende e lo stesso errore farebbe scattare l'apertura una seconda volta.
    /// </summary>
    private int _erroriAnnunciati;

    // Riusate ogni frame: la lista delle categorie si ricalcola dal buffer a ogni disegno, e
    // allocarne una nuova 60 volte al secondo sarebbe spazzatura per niente.
    private readonly List<string> _categorie = [];
    private readonly List<LogMessage> _visibili = [];

    /// <summary>
    /// ⚠️ Come il <c>ScriptsPanel</c>, e per la stessa ragione: deve girare <b>anche da chiuso</b>
    /// per potersi riaprire da sé. <c>PanelBase.Draw</c> esce prima di disegnare se il pannello
    /// non è visibile, quindi il controllo va prima, qui.
    ///
    /// ⚠️ Si apre <b>una volta sola</b> per errore nuovo, e non a ogni frame in cui un errore
    /// esiste: un guasto che si ripete renderebbe il pannello impossibile da chiudere. È lo
    /// stesso motivo per cui il <c>ScriptsPanel</c> tiene il riferimento all'ultima
    /// compilazione annunciata.
    /// </summary>
    public override void Draw(World world, EditorContext context, IRenderer renderer)
    {
        if (context.LogHistory is { } history && history.TotalErrors > _erroriAnnunciati)
        {
            _erroriAnnunciati = history.TotalErrors;
            Visible = true;
        }

        base.Draw(world, context, renderer);
    }

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        if (context.LogHistory is not { } history)
        {
            // "Non lo so" e non "nessun messaggio": un gioco che costruisce il proprio loop
            // puo' non aver registrato la storia, e una console vuota che finge di essere
            // aggiornata e' peggio di una che ammette di non essere collegata.
            ImGui.TextDisabled("Storia del log non disponibile.");
            HelpMarker(
                "Il gioco non ha dichiarato una LogHistory fra le sue Resource. Il GameLoop\n" +
                "dell'engine la registra da se': se manca, questo gioco usa un loop suo.\n" +
                "Non e' \"nessun messaggio\": e' \"non lo so\".");
            return;
        }

        DrawBarra(history);
        ImGui.Separator();
        DrawRighe(history);
    }

    private void DrawBarra(LogHistory history)
    {
        if (ImGui.Button("Pulisci"))
            history.Clear();

        ImGui.SameLine();

        // I conteggi stanno SULLE spunte e non in una riga a parte: "ci sono 3 errori" e "gli
        // errori si vedono" sono la stessa domanda, e separarle vuol dire leggere due punti
        // per sapere se quello che si sta guardando e' tutto.
        var (debug, info, warning, error) = Conta(history);

        ImGui.Checkbox($"Debug ({debug})##fdebug", ref _mostraDebug);
        ImGui.SameLine();
        ImGui.Checkbox($"Info ({info})##finfo", ref _mostraInfo);
        ImGui.SameLine();
        ImGui.Checkbox($"Avvisi ({warning})##fwarn", ref _mostraWarning);
        ImGui.SameLine();
        ImGui.Checkbox($"Errori ({error})##ferr", ref _mostraError);

        ImGui.SameLine();
        ImGui.Checkbox("Segui##fsegui", ref _segui);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip("Resta incollato all'ultima riga quando ne arrivano di nuove.");

        DrawFiltroCategoria(history);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##cerca", ref _cerca, 128);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip("Filtra per testo del messaggio (maiuscole/minuscole indifferenti).");
    }

    /// <summary>
    /// Le categorie si ricavano da <b>cio' che c'e' nel buffer</b>, non dalle costanti di
    /// <c>LogCategories</c>. È voluto: alcune costanti non le usa ancora nessuno, e offrirle
    /// darebbe un filtro che non filtra niente — cioè far sembrare vuoto un log che invece non
    /// ha mai avuto quella categoria. E un gioco può inventarsi le proprie.
    /// </summary>
    private void DrawFiltroCategoria(LogHistory history)
    {
        _categorie.Clear();
        _categorie.Add(TutteLeCategorie);

        foreach (var message in history.Messages)
        {
            if (!_categorie.Contains(message.Category))
                _categorie.Add(message.Category);
        }

        // Una categoria puo' sparire dal buffer mentre e' selezionata (il buffer gira). Senza
        // questo, il filtro resterebbe attivo su qualcosa che non esiste piu' e la console
        // sembrerebbe vuota per sempre.
        if (!_categorie.Contains(_categoria))
            _categoria = TutteLeCategorie;

        ImGui.SetNextItemWidth(150);
        if (!ImGui.BeginCombo("##categoria", _categoria))
            return;

        foreach (var categoria in _categorie)
        {
            if (ImGui.Selectable(categoria, categoria == _categoria))
                _categoria = categoria;
        }

        ImGui.EndCombo();
    }

    private void DrawRighe(LogHistory history)
    {
        if (!ImGui.BeginChild("righe", Vector2.Zero, ImGuiChildFlags.None))
        {
            ImGui.EndChild();
            return;
        }

        _visibili.Clear();
        foreach (var message in history.Messages)
        {
            if (Passa(message))
                _visibili.Add(message);
        }

        if (_visibili.Count == 0)
        {
            // Due vuoti diversi, e confonderli manda a cercare un bug che non c'e'.
            ImGui.TextDisabled(history.Messages.Count == 0
                ? "Nessun messaggio."
                : "Nessun messaggio corrisponde ai filtri.");
        }

        foreach (var message in _visibili)
        {
            var riga = $"{message.Timestamp:HH:mm:ss} [{message.Level}] [{message.Category}] {message.Message}";

            // ⚠️ Le righe VANNO mandate a capo, e non è estetica: guardando il pannello la
            // prima volta, il messaggio dell'asset mancante era tagliato a "cercato in
            // 'C:\Users\ma" — cioè spariva proprio il path, che è l'unica ragione per cui quel
            // messaggio esiste. Un log che tronca a destra nasconde la coda dei messaggi, e la
            // coda è dove stanno i dettagli: path, numeri, nomi.
            //
            // A capo invece di una barra di scorrimento orizzontale perché un messaggio lungo
            // per volta non è il caso: sono lunghi tutti, e si finirebbe a scorrere avanti e
            // indietro per leggerne uno alla volta.
            var colore = Colore(message.Level);
            if (colore is { } c)
                ImGui.PushStyleColor(ImGuiCol.Text, c);

            ImGui.TextWrapped(riga);

            if (colore is not null)
                ImGui.PopStyleColor();
        }

        // Ci si incolla in fondo solo se ci si era: se l'utente ha scrollato indietro per
        // leggere qualcosa, trascinarlo via a ogni riga nuova renderebbe il log illeggibile
        // esattamente quando lo sta leggendo.
        if (_segui && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1f);

        ImGui.EndChild();
    }

    private bool Passa(in LogMessage message)
    {
        var livelloAmmesso = message.Level switch
        {
            LogLevel.Debug => _mostraDebug,
            LogLevel.Info => _mostraInfo,
            LogLevel.Warning => _mostraWarning,
            LogLevel.Error => _mostraError,
            _ => true
        };

        if (!livelloAmmesso)
            return false;

        if (_categoria != TutteLeCategorie && message.Category != _categoria)
            return false;

        return _cerca.Length == 0
               || message.Message.Contains(_cerca, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Debug, int Info, int Warning, int Error) Conta(LogHistory history)
    {
        var debug = 0;
        var info = 0;
        var warning = 0;
        var error = 0;

        foreach (var message in history.Messages)
        {
            switch (message.Level)
            {
                case LogLevel.Debug: debug++; break;
                case LogLevel.Info: info++; break;
                case LogLevel.Warning: warning++; break;
                case LogLevel.Error: error++; break;
            }
        }

        return (debug, info, warning, error);
    }

    /// <summary>
    /// <c>null</c> per l'Info: è il livello normale, e colorarlo vorrebbe dire che <b>tutto</b>
    /// è colorato — cioè che il colore non distingue più niente. Prende il colore del tema.
    /// </summary>
    private static Vector4? Colore(LogLevel level) => level switch
    {
        LogLevel.Error => ErrorColor,
        LogLevel.Warning => WarningColor,
        LogLevel.Debug => DebugColor,
        _ => null
    };

    /// <summary>Il "(?)" con la spiegazione sotto il puntatore. Vedi i gemelli negli altri pannelli.</summary>
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
