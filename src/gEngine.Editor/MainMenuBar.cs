using System.Numerics;
using gEngine.Ecs.Base;
using ImGuiNET;

namespace gEngine.Editor;

/// <summary>
/// La barra dei menu in cima all'editor: le azioni sul <b>documento</b> (File) e
/// l'accensione dei pannelli (Panels).
///
/// Le azioni su file stavano in un pannello "File scena" con due bottoni. Un pannello
/// dedicato a Salva/Ricarica occupava un riquadro del layout per due comandi che si usano
/// una volta ogni tanto, e per giunta si scontrava col viewport per il nome "Scena" (vedi
/// <see cref="IEditorPanel.Title"/>: quel bug è la ragione del commento lì). Il menu è il
/// posto dove chi apre un editor va a cercarle.
/// </summary>
/// <param name="document">
/// La scena su file, o null se il gioco non ne ha una (entità costruite in codice): in quel
/// caso il menu File c'è ma le voci sono spente, che è più onesto di un menu che sparisce.
/// </param>
/// <param name="playMode">
/// Play/Pausa/Stop. I comandi stanno qui e non in un pannello loro per lo stesso motivo per
/// cui ci sono finite le azioni su file: sono <b>globali</b> — non parlano di un'entità né di
/// una vista — e un pannello dedicato a tre bottoni si porterebbe via un riquadro del layout.
/// </param>
public class MainMenuBar(SceneDocument? document, PlayMode playMode)
{
    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);

    /// <summary>Verde/rosso dei comandi di Play: un colore per dire "questo è lo stato", non per far bello.</summary>
    private static readonly Vector4 PlayingColor = new(0.3f, 0.75f, 0.35f, 1f);

    private const string OpenPopup = "Apri scena";

    /// <summary>
    /// Dove cercare le scene da aprire, dedotta <b>una volta sola</b> dal file di partenza:
    /// il path del documento cambia con Apri e si svuota con Nuova, ma la cartella delle
    /// scene del gioco resta quella.
    /// </summary>
    private readonly string _scenesFolder =
        document is null ? string.Empty : Path.GetDirectoryName(document.Path) ?? string.Empty;

    private readonly List<string> _found = [];

    private string _status = string.Empty;
    private bool _statusIsError;

    private bool _openRequested;

    public void Draw(World world, EditorContext context, IReadOnlyList<IEditorPanel> panels)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                DrawFileMenu(world, context);
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Edit"))
            {
                DrawEditMenu(world, context);
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Panels"))
            {
                DrawPanelsMenu(panels);
                ImGui.EndMenu();
            }

            DrawPlayControls(world, context);
            DrawStatus();

            ImGui.EndMainMenuBar();
        }

        // ⚠️ OpenPopup va chiamato allo stesso livello di stack del BeginPopup, non dentro il
        // menu: un popup aperto da dentro un BeginMenu eredita l'id del menu, e quando il
        // menu si chiude (subito, appena si clicca la voce) il popup se ne va con lui. Da qui
        // il rimando di un frame tramite il flag.
        if (_openRequested)
        {
            _openRequested = false;
            RefreshScenes();
            ImGui.OpenPopup(OpenPopup);
        }

        DrawOpenPopup(context);
    }

    /// <summary>
    /// Annulla / Rifai, con dentro <b>il nome di ciò che si annulla</b>.
    ///
    /// Non è decorazione: "Annulla" da solo obbliga a premerlo per scoprire cosa fa, e in un
    /// editor dove l'ultima azione può essere stata un trascinamento visto con la coda
    /// dell'occhio è esattamente l'informazione che serve prima di premere. Le voci restano
    /// <b>visibili e spente</b> quando non c'è niente da annullare, come le voci di File: un
    /// menu che cambia forma è un menu in cui non si trova più niente.
    /// </summary>
    private static void DrawEditMenu(World world, EditorContext context)
    {
        var undo = context.Undo;

        ImGui.BeginDisabled(!undo.CanUndo);
        if (ImGui.MenuItem(undo.CanUndo ? $"Annulla {undo.UndoLabel}" : "Annulla", "Ctrl+Z"))
            undo.Undo(world);
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!undo.CanRedo);
        if (ImGui.MenuItem(undo.CanRedo ? $"Rifai {undo.RedoLabel}" : "Rifai", "Ctrl+Y"))
            undo.Redo(world);
        ImGui.EndDisabled();
    }

    private void DrawFileMenu(World world, EditorContext context)
    {
        if (ImGui.MenuItem("New Scene"))
        {
            world.Clear();
            context.ClearSelection();

            // ⚠️ La storia va buttata con la scena: i comandi parlano di entità che non
            // esistono più, e il loro Undo le farebbe rinascere dentro una scena a cui non
            // appartengono. Vedi UndoStack.Clear.
            context.Undo.Clear();

            if (document is not null)
            {
                // ⚠️ Il limite, dichiarato: una scena nuova non ha un file, e qui non c'è un
                // document model che sappia rappresentare "senza titolo" (un Save As non è in
                // scope). Path vuoto è la resa più onesta possibile: Save lo vede e si rifiuta
                // invece di sovrascrivere in silenzio la scena da cui si era partiti, che è il
                // solo esito davvero inaccettabile. Chi vuole ripartire da un file usa Open.
                document.Path = string.Empty;
                document.Source = null;
            }

            _status = "Scena nuova (senza file): usa Open Scene per riaprirne una.";
            _statusIsError = false;
        }

        // Niente scena su file = niente da aprire e niente su cui salvare. Le voci restano
        // visibili e spente: dire "qui non c'è" è meglio di un menu che cambia forma.
        ImGui.BeginDisabled(document is null || _scenesFolder.Length == 0);
        if (ImGui.MenuItem("Open Scene..."))
            _openRequested = true;
        ImGui.EndDisabled();

        ImGui.Separator();

        ImGui.BeginDisabled(document is null);
        if (ImGui.MenuItem("Save Scene"))
            Run(document!.Save, $"Scena salvata: {document!.Path}");
        ImGui.EndDisabled();
    }

    private static void DrawPanelsMenu(IReadOnlyList<IEditorPanel> panels)
    {
        foreach (var panel in panels)
        {
            // Il titolo è la stessa stringa che il pannello passa a Begin: è ciò che rende la
            // spunta e la X della finestra lo stesso interruttore. Vedi IEditorPanel.Title.
            var visible = panel.Visible;
            if (ImGui.MenuItem(panel.Title, string.Empty, ref visible))
                panel.Visible = visible;
        }
    }

    /// <summary>
    /// Play / Pausa / Stop.
    ///
    /// I bottoni non spariscono mai e non cambiano posto: cambia quale è acceso. Una barra che
    /// si ricompone a ogni stato costringe a rileggerla invece che a premere.
    /// </summary>
    private void DrawPlayControls(World world, EditorContext context)
    {
        ImGui.Separator();

        var playing = playMode.State == PlayState.Playing;
        var stopped = playMode.State == PlayState.Editing;

        // Il motivo si chiede una volta: è anche l'unica cosa che rende leggibile un Play
        // spento. Vedi PlayMode.BlockedReason.
        var blocked = playMode.BlockedReason(context);

        ImGui.BeginDisabled(playing || blocked is not null);

        // Verde solo mentre gira: è l'unico posto dell'editor che dice, senza leggere,
        // "attento, questo non è più il tuo lavoro — è il gioco".
        if (!stopped)
            ImGui.PushStyleColor(ImGuiCol.Text, PlayingColor);

        // "Riprendi" e non "Play" da Paused: sono due cose diverse e la seconda volta non si
        // riprende uno snapshot, si continua quello di prima.
        if (ImGui.SmallButton(playMode.State == PlayState.Paused ? "Riprendi" : "Play"))
        {
            // Niente Run: Start non lancia piu' (lo chiama anche F1, che non saprebbe dove
            // mostrare un'eccezione). Il motivo del rifiuto sta in LastError.
            _statusIsError = !playMode.Start(world, context);
            _status = _statusIsError
                ? playMode.LastError ?? "Play non riuscito."
                : "In esecuzione - Stop riporta la scena com'era al Play.";
        }

        if (!stopped)
            ImGui.PopStyleColor();

        ImGui.EndDisabled();

        if (blocked is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip | ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(blocked);

        ImGui.SameLine();
        ImGui.BeginDisabled(!playing);
        if (ImGui.SmallButton("Pausa"))
            playMode.Pause();
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(stopped);
        if (ImGui.SmallButton("Stop"))
        {
            // Niente Run: Stop non lancia, per costruzione — ciò che poteva andare storto è
            // già stato scoperto al Play. Vedi PlayMode.
            playMode.Stop(world, context);
            _status = "Scena ripristinata com'era al Play.";
            _statusIsError = false;
        }
        ImGui.EndDisabled();
    }

    private void DrawStatus()
    {
        if (_status.Length == 0)
            return;

        ImGui.Separator();

        if (_statusIsError)
            ImGui.TextColored(ErrorColor, _status);
        else
            ImGui.TextDisabled(_status);
    }

    /// <summary>
    /// L'elenco dei file, riletto a ogni apertura del popup e non tenuto: fra un'apertura e
    /// l'altra la cartella cambia (qualcuno salva da fuori), e un elenco stantio offrirebbe
    /// file che non ci sono più.
    /// </summary>
    private void RefreshScenes()
    {
        _found.Clear();

        try
        {
            if (Directory.Exists(_scenesFolder))
                _found.AddRange(Directory.EnumerateFiles(_scenesFolder, "*.json"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status = ex.Message;
            _statusIsError = true;
        }
    }

    /// <summary>
    /// Un elenco dei .json nella cartella delle scene, e basta. <b>Non</b> è un file dialog:
    /// niente path a mano, niente navigazione, niente libreria tirata dentro per aprire una
    /// finestra di sistema. Le scene del gioco stanno in una cartella sola — questo le mostra
    /// tutte, ed è tutto ciò che serve per aprirne una.
    /// </summary>
    private void DrawOpenPopup(EditorContext context)
    {
        if (!ImGui.BeginPopup(OpenPopup))
            return;

        ImGui.TextDisabled(_scenesFolder);
        ImGui.Separator();

        if (_found.Count == 0)
            ImGui.TextDisabled("Nessuna scena .json qui.");

        foreach (var file in _found)
        {
            if (!ImGui.Selectable(Path.GetFileName(file)))
                continue;

            // La selezione punta a entità che il caricamento distrugge. Gli id non si riusano,
            // quindi resterebbe solo invalida invece che sbagliata — ma tanto vale non
            // lasciarla lì.
            context.ClearSelection();

            document!.Path = file;
            Run(document.Load, $"Scena caricata: {file}");

            // Stessa ragione di New Scene: il World è stato sostituito in blocco.
            context.Undo.Clear();

            ImGui.CloseCurrentPopup();
            break; // _found può essere ricostruita da qui in poi: non si continua a iterarla
        }

        ImGui.EndPopup();
    }

    /// <summary>
    /// Salvare e caricare toccano il disco e la scena può essere non salvabile (un
    /// riferimento verso un'entità senza nome, un componente senza writer): un'eccezione
    /// qui butterebbe giù il gioco durante il frame di disegno. L'errore va mostrato
    /// nell'editor — è lì che l'utente può rimediare.
    /// </summary>
    private void Run(Action action, string successMessage)
    {
        try
        {
            action();
            _status = successMessage;
            _statusIsError = false;
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            _statusIsError = true;
        }
    }
}
