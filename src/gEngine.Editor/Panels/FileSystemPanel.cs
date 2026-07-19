using System.Numerics;
using gEngine.Assets;
using gEngine.Ecs.Base;
using gEngine.Editor.Files;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Sfoglia la cartella degli asset del gioco e ne <b>trascina</b> il contenuto negli slot
/// dell'Inspector (vedi <see cref="AssetDragDrop"/>).
///
/// Due disposizioni, e la griglia è quella di default: un elenco di nomi è la forma giusta per
/// leggere dei file, non per <b>riconoscere</b> degli asset. Chi cerca una texture la cerca
/// guardandola. La lista resta perché sui nomi lunghi e sulle cartelle profonde è ancora la più
/// leggibile, e perché toglierla sarebbe sostituire una preferenza con un'altra.
///
/// Il disco si può <b>modificare</b>: creare cartelle, rinominare, eliminare. ⚠️ E queste sono
/// le uniche azioni dell'editor che toccano file veri dell'utente, cioè le uniche in cui
/// sbagliare non si annulla: l'undo copre il World, non il disco. La rete è un'altra e sta
/// sotto "elimina" — il <b>cestino</b> del sistema (<see cref="IFileTrash"/>), senza il quale
/// il comando non viene nemmeno offerto. Le regole (cosa è un nome legale, cosa vuol dire
/// "dentro la cartella asset") stanno in <see cref="AssetFiles"/>, dove un test le può
/// interrogare senza aprire una finestra.
///
/// Legge il disco a ogni frame (<c>EnumerateDirectories</c>/<c>EnumerateFiles</c> su una sola
/// cartella): come l'albero della Hierarchy. ⚠️ È anche ciò che rende il pannello <b>vivo</b> —
/// un file copiato da Explorer compare senza riavviare. Non era vero prima, ma per un motivo
/// che non stava qui: l'editor guardava la copia degli asset dentro <c>bin/</c>. Vedi
/// <c>ContentRoot</c>.
/// </summary>
/// <param name="root">
/// La radice oltre la quale non si sale. ⚠️ È un confine vero e va difeso: "su" dalla radice
/// porterebbe a sfogliare il disco dell'utente da un pannello che dice "assets".
/// </param>
/// <param name="trash">
/// Dove finisce quel che si elimina. ⚠️ È una dipendenza <b>dichiarata</b> e non un dettaglio
/// interno: è l'unica rete sotto l'unica operazione irreversibile del pannello. Se manca, il
/// comando "Elimina" non si accende.
/// </param>
public class FileSystemPanel(string root, IFileTrash trash)
    : PanelBase("File system", new Vector2(20, 520), new Vector2(360, 300))
{
    private readonly string _root = root;
    private readonly IFileTrash _trash = trash;

    private string _current = root;

    /// <summary>
    /// L'azione chiesta col mouse, applicata a <b>fine frame</b>.
    ///
    /// ⚠️ Non è pedanteria: creare o eliminare mentre si sta scorrendo
    /// <c>EnumerateDirectories</c>/<c>EnumerateFiles</c> sulla stessa cartella è il modo sicuro
    /// per far cadere il frame di disegno. Stesso schema del pannello Systems, e per lo stesso
    /// motivo: là si modificavano le liste che si stavano scorrendo, qui il disco.
    /// </summary>
    private Action? _pending;

    /// <summary>L'ultimo errore del disco, mostrato finché non si fa qualcosa che riesce.</summary>
    private string _error = string.Empty;

    // Il bersaglio delle finestrelle modali. Il path e non l'indice: fra il clic che apre il
    // popup e il clic che conferma passano dei frame, e la cartella nel frattempo puo' essere
    // cambiata da fuori (il pannello rilegge il disco a ogni frame).
    private string? _renaming;
    private string? _deleting;

    // I buffer di ImGui vogliono una taglia fissa. 260 e' sotto il limite classico dei 260
    // caratteri di un path Windows: qui e' un nome, non un percorso, quindi avanza.
    private string _renameBuffer = string.Empty;
    private string _newFolderBuffer = string.Empty;

    /// <summary>
    /// La modale da aprire, aperta a <b>livello di finestra</b> e non dove è stata chiesta.
    ///
    /// ⚠️⚠️ Non è un vezzo, è l'unica cosa che la fa funzionare, e si vede solo provandola.
    /// <c>OpenPopup</c> e <c>BeginPopupModal</c> si accordano su un <b>id calcolato nell'ID
    /// stack corrente</b>: chiamando <c>OpenPopup("Elimina")</c> da dentro il menu contestuale
    /// — che è a sua volta un popup, quindi un livello più in basso — l'id non è lo stesso che
    /// <c>BeginPopupModal("Elimina")</c> calcola a livello di finestra, e <b>la modale non si
    /// apre mai</b>. Nessun errore, nessun log: il comando semplicemente non fa niente.
    ///
    /// Trovato pilotando la finestra viva: dalla barra degli strumenti funzionava (là si è già
    /// a livello di finestra), dal menu contestuale no. Rileggendo il codice i due rami sono
    /// identici.
    /// </summary>
    private string? _opening;

    /// <summary>
    /// Griglia di default: vedi il commento della classe. È uno stato del pannello e non una
    /// preferenza salvata — ImGui si ricorda posizione e taglia in <c>imgui.ini</c>, non questo.
    /// </summary>
    private bool _grid = true;

    /// <summary>
    /// Lato del riquadro in pixel. Il minimo non è arrotondato per caso: sotto i ~48px il nome
    /// sotto l'icona non ci sta in nessun modo e la griglia diventa un mosaico anonimo.
    /// </summary>
    private float _tileSize = 84f;

    private ThumbnailCache? _thumbnails;

    /// <summary>
    /// Le anteprime nascono al primo disegno e non nel costruttore: servono l'AssetManager (che
    /// arriva dalle Resource del gioco) e un contesto grafico vivo. ⚠️ E possono <b>non</b>
    /// esserci — un gioco può non dichiarare l'AssetManager: in quel caso si vedono i riquadri,
    /// che è una degradazione, non un errore.
    /// </summary>
    private ThumbnailCache? Thumbnails(EditorContext context)
    {
        if (_thumbnails is not null)
            return _thumbnails;

        return context.Assets is { } assets ? _thumbnails = new ThumbnailCache(assets) : null;
    }

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        if (!Directory.Exists(_root))
        {
            // Un gioco può girare senza cartella assets (entità costruite in codice, mesh
            // primitive): dirlo è meglio di un pannello vuoto che sembra un bug.
            ImGui.TextDisabled($"Cartella non trovata:\n{_root}");
            return;
        }

        // La cartella corrente può sparire mentre la si guarda (cancellata da fuori):
        // tornare alla radice è meglio che far cadere il frame di disegno su ogni Enumerate.
        if (!Directory.Exists(_current))
            Navigate(_root);

        Thumbnails(context)?.BeginFrame();

        DrawToolbar();
        DrawError();
        ImGui.Separator();

        // Il contenuto in una regione a sé: così la barra qui sopra resta ferma mentre il
        // contenuto scorre, invece di sparire in cima allo scroll.
        if (!ImGui.BeginChild("contenuto", Vector2.Zero, ImGuiChildFlags.None))
        {
            ImGui.EndChild();
            return;
        }

        // Il disco può negare l'accesso o sparire da sotto: come per il salvataggio, qui
        // un'eccezione cadrebbe nel frame di disegno. Vedi MainMenuBar.Run — stesso motivo,
        // ma qui non c'è niente da rimediare per l'utente, solo da mostrare.
        try
        {
            if (_grid)
                DrawGrid(context);
            else
                DrawList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), ex.Message);
        }

        // Il menu contestuale dello sfondo: clic destro nel vuoto per creare. È dentro il
        // BeginChild perché è quella la regione che si vede come "la cartella".
        DrawBackgroundContextMenu();

        ImGui.EndChild();

        // Qui, e non dove il comando e' stato scelto: vedi _opening.
        if (_opening is { } popup)
        {
            ImGui.OpenPopup(popup);
            _opening = null;
        }

        // Le modali fuori dal child: una finestra dentro una regione a scorrimento verrebbe
        // ritagliata e potrebbe nascere fuori dalla vista.
        DrawRenameModal();
        DrawDeleteModal();
        DrawNewFolderModal();

        // A fine frame, quando nessuna enumerazione è più in volo.
        var pending = _pending;
        _pending = null;
        pending?.Invoke();
    }

    /// <summary>
    /// L'esito dell'ultima operazione fallita. Resta finché non ne riesce una: un errore che
    /// sparisce al frame dopo non è leggibile, e qui il frame dopo arriva sempre (il pannello
    /// si ridisegna di continuo).
    /// </summary>
    private void DrawError()
    {
        if (_error.Length == 0)
            return;

        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(new Vector4(1f, 0.45f, 0.45f, 1f), _error);
        ImGui.PopTextWrapPos();

        ImGui.SameLine();
        if (ImGui.SmallButton("Chiudi##errore"))
            _error = string.Empty;
    }

    /// <summary>Esegue e tiene il messaggio se è andata male. Un posto solo per tre comandi.</summary>
    private void Apply(Func<FileResult> operation)
    {
        var result = operation();
        _error = result.Ok ? string.Empty : result.Error;

        // Le anteprime sono indicizzate per percorso: dopo un rinomina o un elimina almeno una
        // chiave è bugiarda. Buttarle tutte è sovrabbondante e costa il ricaricamento pigro di
        // una cartella, che è già il caso normale quando si cambia cartella.
        _thumbnails?.Clear();
    }

    /// <summary>
    /// Cambiare cartella <b>butta le anteprime</b>: sono texture GPU di file che non si stanno
    /// più guardando. È l'unico posto in cui si sa che è successo, quindi la navigazione passa
    /// tutta di qui invece di assegnare <c>_current</c> a mano in tre punti.
    /// </summary>
    private void Navigate(string directory)
    {
        if (string.Equals(directory, _current, StringComparison.OrdinalIgnoreCase))
            return;

        _current = directory;
        _thumbnails?.Clear();
    }

    private void DrawToolbar()
    {
        // Il percorso come briciole cliccabili: da "models/SummonersRift/Textures" si torna a
        // "models" con un clic invece che con tre ".." — e intanto si vede dove si è, che un
        // ".." da solo non dice.
        DrawBreadcrumb();

        ImGui.SameLine();
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - 285f));

        if (ImGui.SmallButton("Nuova cartella"))
            OpenNewFolder();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip($"Crea una cartella dentro {Path.GetFileName(_current)}");

        ImGui.SameLine();

        if (ImGui.SmallButton(_grid ? "Lista" : "Griglia"))
            _grid = !_grid;

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip(_grid
                ? "Passa all'elenco per nome"
                : "Passa ai riquadri con anteprima");

        if (!_grid)
            return;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90f);

        // Slider e non due bottoni +/-: la taglia giusta dipende da quanto è largo il pannello
        // e da cosa si sta cercando, quindi è una manopola, non due scatti.
        ImGui.SliderFloat("##taglia", ref _tileSize, 48f, 160f, "");

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip("Dimensione dei riquadri");
    }

    /// <summary>
    /// Il percorso spezzato in segmenti cliccabili. La radice si chiama col proprio nome
    /// ("assets") e non "." o "/": è il nome che l'utente vede in Explorer.
    /// </summary>
    private void DrawBreadcrumb()
    {
        var relative = Path.GetRelativePath(_root, _current);
        var rootName = Path.GetFileName(_root.TrimEnd(Path.DirectorySeparatorChar));

        if (ImGui.SmallButton(rootName))
            Navigate(_root);

        if (relative == ".")
            return;

        var walked = _root;

        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            walked = Path.Combine(walked, segment);

            ImGui.SameLine(0f, 4f);
            ImGui.TextDisabled(">");
            ImGui.SameLine(0f, 4f);

            // PushID sul percorso: due cartelle omonime a livelli diversi ("Source/Textures" e
            // "Textures") sarebbero lo stesso bottone per ImGui, che guarda l'etichetta.
            ImGui.PushID(walked);
            if (ImGui.SmallButton(segment))
                Navigate(walked);
            ImGui.PopID();
        }
    }

    // ---------------------------------------------------------------- lista

    private void DrawList()
    {
        if (!AtRoot())
        {
            if (ImGui.Selectable("..", false, ImGuiSelectableFlags.AllowDoubleClick) &&
                ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                Navigate(Directory.GetParent(_current)?.FullName ?? _root);
        }

        foreach (var directory in Directory.EnumerateDirectories(_current))
        {
            // PushID sul percorso: due elementi omonimi in liste diverse sarebbero lo stesso
            // item per ImGui, che guarda l'etichetta — e il menu contestuale finirebbe
            // agganciato al posto sbagliato.
            ImGui.PushID(directory);

            if (ImGui.Selectable($"[ ] {Path.GetFileName(directory)}", false,
                    ImGuiSelectableFlags.AllowDoubleClick) &&
                ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                Navigate(directory);

            DrawItemContextMenu(directory);
            ImGui.PopID();
        }

        foreach (var file in Directory.EnumerateFiles(_current))
        {
            var kind = AssetDragDrop.Classify(file);
            var name = Path.GetFileName(file);

            ImGui.PushID(file);

            // I file che non sappiamo caricare (un license.txt, uno scene.bin di un glTF)
            // restano visibili e spenti invece di sparire: la cartella è quella dell'utente e
            // nasconderne metà farebbe cercare i file che ci sono.
            //
            // ⚠️ Spenti col COLORE, non con un TextDisabled: erano un Text nudo, e un Text non
            // ha un id — <c>BeginPopupContextItem()</c> ci andrebbe in IM_ASSERT, che su
            // Windows è una dialog modale nativa e sembra un hang del game loop. Da quando c'è
            // il menu contestuale devono essere item veri: un license.txt si elimina come tutto
            // il resto.
            if (kind is not { } assetKind)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                ImGui.Selectable(name);
                ImGui.PopStyleColor();

                DrawItemContextMenu(file);
                ImGui.PopID();
                continue;
            }

            // Selectable e non TextUnformatted: un testo nudo non è un item per ImGui e non può
            // essere una sorgente di trascinamento — è l'ultimo item disegnato che diventa
            // sorgente, e deve avere un id.
            ImGui.Selectable(name);
            BeginDrag(assetKind, file);
            DrawItemContextMenu(file);

            ImGui.PopID();
        }
    }

    // ---------------------------------------------------------------- griglia

    /// <summary>
    /// I riquadri, disposti in orizzontale e mandati a capo a mano.
    ///
    /// ⚠️ ImGui non ha un layout a flusso: il ritorno a capo è un conto nostro
    /// (<c>SameLine</c> finché ci si sta). Farlo con le colonne di una tabella sembrerebbe più
    /// pulito ma legherebbe il numero di riquadri per riga alla larghezza <b>di quando la
    /// tabella è nata</b>, e questo pannello si ridimensiona di continuo.
    /// </summary>
    private void DrawGrid(EditorContext context)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var available = ImGui.GetContentRegionAvail().X;

        // Almeno una colonna: con un pannello più stretto di un riquadro il conto darebbe
        // zero, e una divisione per zero più avanti sarebbe un riquadro per riga di larghezza
        // infinita. Meglio uno che sfora.
        var columns = Math.Max(1, (int)((available + spacing) / (_tileSize + spacing)));

        var drawn = 0;

        if (!AtRoot())
        {
            if (DrawTile("..", TileKind.Parent, 0))
                Navigate(Directory.GetParent(_current)?.FullName ?? _root);

            // Conta come riquadro anche se non e' passato da Wrap: e' il primo della riga.
            drawn++;
        }

        foreach (var directory in Directory.EnumerateDirectories(_current))
        {
            Wrap(ref drawn, columns);

            if (DrawTile(Path.GetFileName(directory), TileKind.Folder, 0, directory))
                Navigate(directory);

            DrawItemContextMenu(directory);
        }

        var thumbnails = Thumbnails(context);

        foreach (var file in Directory.EnumerateFiles(_current))
        {
            Wrap(ref drawn, columns);

            var kind = AssetDragDrop.Classify(file);
            var name = Path.GetFileName(file);

            // L'anteprima si chiede solo alle immagini: per tutto il resto non esiste un modo
            // di guardare il file che costi meno che aprirlo, e aprire un modello per
            // disegnarlo grande 84 pixel vorrebbe dire caricarlo davvero. Il riquadro con
            // l'estensione dice quel che serve senza pagare niente.
            var preview = kind == AssetKind.Texture && thumbnails is not null
                ? thumbnails.GetOrRequest(file)
                : 0;

            DrawTile(name, kind is null ? TileKind.Unknown : TileKind.Asset, preview, file);

            if (kind is { } assetKind)
                BeginDrag(assetKind, file);

            // Dopo il drag: vedi la nota in fondo a DrawTile.
            DrawItemContextMenu(file);
        }
    }

    /// <summary>
    /// Manda a capo se questo riquadro apre una riga nuova.
    ///
    /// ⚠️ Il conto si fa <b>prima</b> di incrementare, e l'ordine inverso è un fuori-di-uno che
    /// si vede: contando dopo, si ragiona sulla posizione 1-based e la <b>prima riga</b> ne
    /// riceve uno in meno di tutte le altre — due riquadri sopra, tre sotto, con la seconda
    /// riga che parte anche più a sinistra (il <c>SameLine</c> di troppo). Trovato guardando
    /// uno screenshot, non rileggendo il codice.
    /// </summary>
    private static void Wrap(ref int drawn, int columns)
    {
        if (drawn % columns != 0)
            ImGui.SameLine();

        drawn++;
    }

    private enum TileKind { Parent, Folder, Asset, Unknown }

    /// <summary>
    /// Un riquadro: sfondo, anteprima (o il colore del genere con l'estensione sopra) e il
    /// nome sotto, troncato.
    ///
    /// ⚠️ È disegnato col <b>draw list</b> e non con widget: le icone vere vorrebbero un font
    /// di icone, e il font di default (ProggyClean) copre solo Latin-1 — un glifo fuori da lì
    /// esce come "?". Finché il font non cambia (è un punto a sé del piano), un rettangolo
    /// colorato con dentro "PNG" dice il genere meglio di qualunque carattere disponibile.
    /// </summary>
    /// <returns>true su doppio clic, che è "entraci".</returns>
    private bool DrawTile(string name, TileKind kind, nint preview, string? path = null)
    {
        ImGui.PushID(path ?? name);

        var start = ImGui.GetCursorScreenPos();
        var labelHeight = ImGui.GetTextLineHeightWithSpacing();
        var size = new Vector2(_tileSize, _tileSize + labelHeight);

        // Il Selectable per primo e grande quanto tutto: è lui l'item (hover, selezione,
        // sorgente del trascinamento, doppio clic). Il disegno gli va SOPRA.
        var activated = ImGui.Selectable("##riquadro", false,
            ImGuiSelectableFlags.AllowDoubleClick, size);

        var hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList();

        var iconMin = start;
        var iconMax = start + new Vector2(_tileSize, _tileSize);

        if (preview != 0)
        {
            // Sfondo scuro sotto l'immagine: una texture con trasparenza su un pannello scuro
            // sparisce a metà, e non si capisce se è l'immagine o l'anteprima a essere rotta.
            draw.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.09f, 0.09f, 0.10f, 1f)), 4f);
            draw.AddImageRounded(preview, iconMin, iconMax, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(Vector4.One), 4f);
        }
        else
        {
            draw.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(TileColor(kind, name)), 4f);
            DrawTileGlyph(draw, iconMin, iconMax, kind, name);
        }

        if (hovered)
            draw.AddRect(iconMin, iconMax, ImGui.GetColorU32(ImGuiCol.NavCursor), 4f);

        DrawTileLabel(draw, start, name, labelHeight);

        // ⚠️ Il ramo si sceglie sul GENERE, non su "il path c'è": da quando anche le cartelle
        // ricevono il proprio percorso (gli serve per il menu contestuale), un test su
        // `path is not null` manderebbe una cartella dentro Describe — che fa
        // `new FileInfo(path).Length` e su una cartella lancia. Sarebbe finita nel catch del
        // pannello e la griglia si sarebbe spenta in un messaggio d'errore.
        if (hovered)
        {
            if (kind is TileKind.Folder)
                ImGui.SetTooltip($"{name}\nDoppio clic per entrare - clic destro per rinominare o eliminare");
            else if (kind is not TileKind.Parent && path is not null)
                ImGui.SetTooltip($"{name}\n{Describe(path)}");
        }

        // ⚠️ Il menu contestuale NON si aggancia qui, e non è una svista: sia
        // <c>BeginPopupContextItem</c> sia <c>BeginDragDropSource</c> si riferiscono
        // all'<b>ultimo item disegnato</b>, e il trascinamento lo registra il chiamante subito
        // dopo questa chiamata. Aprendo il popup qui dentro, "l'ultimo item" al ritorno non
        // sarebbe più il riquadro e il drag&drop degli asset — che funziona ed è stato
        // verificato pilotando la finestra viva — smetterebbe di funzionare. Il chiamante lo
        // aggancia dopo il drag.
        ImGui.PopID();

        return activated && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
    }

    /// <summary>
    /// Il nome sotto il riquadro, troncato con "..." se non ci sta.
    ///
    /// ⚠️ Tagliato a mano e non con <c>TextWrapped</c>: mandare a capo farebbe crescere il
    /// riquadro in altezza e la griglia diventerebbe una scala, perché ogni riga sarebbe alta
    /// quanto il nome più lungo che contiene.
    /// </summary>
    private void DrawTileLabel(ImDrawListPtr draw, Vector2 tileStart, string name, float labelHeight)
    {
        var text = name;

        if (ImGui.CalcTextSize(text).X > _tileSize)
        {
            while (text.Length > 1 && ImGui.CalcTextSize(text + "...").X > _tileSize)
                text = text[..^1];

            text += "...";
        }

        var width = ImGui.CalcTextSize(text).X;
        var position = tileStart + new Vector2((_tileSize - width) * 0.5f, _tileSize + labelHeight * 0.1f);

        draw.AddText(position, ImGui.GetColorU32(ImGuiCol.Text), text);
    }

    /// <summary>
    /// L'estensione al centro del riquadro (senza punto, maiuscola). Per le cartelle un
    /// simbolo disegnato, non una lettera: una cartella non ha un'estensione da mostrare, e
    /// scriverci "DIR" sarebbe gergo.
    /// </summary>
    private static void DrawTileGlyph(ImDrawListPtr draw, Vector2 min, Vector2 max, TileKind kind, string name)
    {
        if (kind is TileKind.Folder or TileKind.Parent)
        {
            // Una linguetta e un corpo: due rettangoli, ed è riconoscibile a 48 pixel.
            var size = max - min;
            var body = new Vector2(size.X * 0.62f, size.Y * 0.44f);
            var origin = min + new Vector2((size.X - body.X) * 0.5f, (size.Y - body.Y) * 0.5f);
            var color = ImGui.GetColorU32(new Vector4(0.80f, 0.72f, 0.48f, 1f));

            draw.AddRectFilled(origin + new Vector2(0f, -body.Y * 0.28f),
                origin + new Vector2(body.X * 0.42f, 0f), color, 2f);
            draw.AddRectFilled(origin, origin + body, color, 3f);

            if (kind == TileKind.Parent)
            {
                // Una freccia in su dentro la cartella: "questa è la cartella sopra".
                var center = min + size * 0.5f;
                draw.AddTriangleFilled(
                    center + new Vector2(0f, -size.Y * 0.06f),
                    center + new Vector2(-size.X * 0.09f, size.Y * 0.06f),
                    center + new Vector2(size.X * 0.09f, size.Y * 0.06f),
                    ImGui.GetColorU32(new Vector4(0.15f, 0.14f, 0.10f, 1f)));
            }

            return;
        }

        var extension = Path.GetExtension(name).TrimStart('.').ToUpperInvariant();

        if (extension.Length == 0)
            return;

        var textSize = ImGui.CalcTextSize(extension);
        var position = (min + max) * 0.5f - textSize * 0.5f;

        draw.AddText(position, ImGui.GetColorU32(new Vector4(0.92f, 0.92f, 0.94f, 0.95f)), extension);
    }

    /// <summary>
    /// Il colore del riquadro <b>dice il genere</b>: modelli, audio, scene e script si
    /// distinguono con la coda dell'occhio, prima di leggere l'estensione. I file che non
    /// sappiamo caricare restano grigi — spenti, non nascosti.
    /// </summary>
    private static Vector4 TileColor(TileKind kind, string name)
    {
        if (kind is TileKind.Folder or TileKind.Parent)
            return new Vector4(0.16f, 0.16f, 0.17f, 1f);

        return AssetDragDrop.Classify(name) switch
        {
            AssetKind.Model => new Vector4(0.20f, 0.32f, 0.42f, 1f),
            AssetKind.Texture => new Vector4(0.34f, 0.26f, 0.44f, 1f),
            AssetKind.Music => new Vector4(0.20f, 0.38f, 0.30f, 1f),
            _ => Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".json" => new Vector4(0.42f, 0.34f, 0.18f, 1f),
                ".cs" => new Vector4(0.34f, 0.22f, 0.24f, 1f),
                _ => new Vector4(0.17f, 0.17f, 0.18f, 1f)
            }
        };
    }

    private static string Describe(string path)
    {
        var kind = AssetDragDrop.Classify(path);
        var size = new FileInfo(path).Length;

        var readable = size < 1024 ? $"{size} B"
            : size < 1024 * 1024 ? $"{size / 1024.0:F0} KB"
            : $"{size / (1024.0 * 1024.0):F1} MB";

        return kind is { } assetKind
            ? $"{assetKind} · {readable} · trascinalo su un campo dell'Inspector"
            : $"{readable} · non e' un asset che l'engine sa caricare";
    }

    // ---------------------------------------------------------------- mutazioni

    /// <summary>
    /// Il menu contestuale di un elemento: rinomina, elimina.
    ///
    /// ⚠️ Va chiamato subito dopo un item con un <b>id</b> — un <c>Selectable</c>, non un
    /// <c>Text</c>. <c>BeginPopupContextItem()</c> senza argomenti usa l'id dell'ultimo item, e
    /// un <c>Text</c> non ne ha: su Windows l'<c>IM_ASSERT</c> che ne segue è una dialog modale
    /// nativa, quindi il gioco non crasha e non logga — si pianta al primo frame e sembra un
    /// hang del game loop. È già costato mezza sessione in Fase 4.7. È il motivo per cui anche
    /// i file non riconosciuti qui sotto sono disegnati come <c>Selectable</c> spenti.
    /// </summary>
    private void DrawItemContextMenu(string path)
    {
        if (!ImGui.BeginPopupContextItem())
            return;

        // Il nome in cima: la finestrella nasce sotto il puntatore e senza questa riga non si
        // vede su cosa si sta per agire, che con "Elimina" nel menu non e' un dettaglio.
        ImGui.TextDisabled(Path.GetFileName(path));
        ImGui.Separator();

        if (ImGui.MenuItem("Rinomina"))
            OpenRename(path);

        // Spento e col motivo, invece che assente: un comando che sparisce fa cercare la
        // ragione nel posto sbagliato. Stessa regola di "Aggiungi system".
        if (!_trash.Available)
            ImGui.BeginDisabled();

        if (ImGui.MenuItem("Elimina"))
            OpenDelete(path);

        if (!_trash.Available)
            ImGui.EndDisabled();

        HelpMarker(_trash.Available
            ? "Manda il file nel cestino del sistema, da dove si puo' recuperare.\n\n" +
              "Attenzione: l'annulla dell'editor (Ctrl+Z) NON copre il disco - un comando in\n" +
              "memoria non resuscita un file. Il cestino e' l'unica rete che c'e' qui."
            : _trash.UnavailableReason);

        ImGui.EndPopup();
    }

    /// <summary>Clic destro nel vuoto: crea, senza dover mirare la barra in alto.</summary>
    private void DrawBackgroundContextMenu()
    {
        if (!ImGui.BeginPopupContextWindow("sfondo",
                ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            return;

        ImGui.TextDisabled(Path.GetFileName(_current));
        ImGui.Separator();

        if (ImGui.MenuItem("Nuova cartella"))
            OpenNewFolder();

        ImGui.EndPopup();
    }

    // I tre "Open*" armano la modale e la aprono. Separati dal disegno perche' l'apertura puo'
    // partire da due posti (la barra e il menu contestuale) e ImGui vuole che OpenPopup e
    // BeginPopupModal usino lo stesso nome.

    private void OpenRename(string path)
    {
        _renaming = path;
        _renameBuffer = Path.GetFileName(path);
        _opening = "Rinomina";
    }

    private void OpenDelete(string path)
    {
        _deleting = path;
        _opening = "Elimina";
    }

    private void OpenNewFolder()
    {
        _newFolderBuffer = string.Empty;
        _opening = "Nuova cartella";
    }

    private void DrawRenameModal()
    {
        var open = true;
        if (!ImGui.BeginPopupModal("Rinomina", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        if (_renaming is not { } path)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        ImGui.TextDisabled(Path.GetFileName(path));

        // Il fuoco alla casella all'apertura: si è arrivati qui per scrivere un nome, e senza
        // questa riga il primo carattere digitato si perde.
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        var confirmed = ImGui.InputText("##nome", ref _renameBuffer, 260,
            ImGuiInputTextFlags.EnterReturnsTrue);

        if (Path.GetExtension(path).Length > 0)
        {
            HelpMarker(
                "Attenzione: rinominare un asset NON aggiorna le scene che lo citano: il\n" +
                "ModelPath nel file di scena e' questo percorso, e nessuno lo riscrive.\n" +
                "Il modello diventa bianco al prossimo caricamento della scena.");
        }

        if (ImGui.Button("Rinomina") || confirmed)
        {
            var nuovo = _renameBuffer;
            _pending = () => Apply(() => AssetFiles.Rename(_root, path, nuovo));
            _renaming = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Annulla"))
        {
            _renaming = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    /// <summary>
    /// La conferma prima di eliminare.
    ///
    /// ⚠️ Dice <b>quanto</b> sta per sparire quando è una cartella, e non è cortesia: dal
    /// riquadro una cartella con dentro 405 texture e una vuota si assomigliano.
    /// </summary>
    private void DrawDeleteModal()
    {
        var open = true;
        if (!ImGui.BeginPopupModal("Elimina", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        if (_deleting is not { } path)
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        ImGui.Text($"Mandare nel cestino {Path.GetFileName(path)}?");

        if (Directory.Exists(path))
            ImGui.TextDisabled(DescribeFolderContents(path));

        ImGui.TextDisabled("Si recupera dal cestino del sistema. Ctrl+Z non arriva al disco.");

        if (ImGui.Button("Elimina"))
        {
            var bersaglio = path;
            _pending = () => Apply(() => AssetFiles.Delete(_root, bersaglio, _trash));
            _deleting = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Annulla"))
        {
            _deleting = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawNewFolderModal()
    {
        var open = true;
        if (!ImGui.BeginPopupModal("Nuova cartella", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextDisabled($"dentro {Path.GetFileName(_current)}");

        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        var confirmed = ImGui.InputText("##nome", ref _newFolderBuffer, 260,
            ImGuiInputTextFlags.EnterReturnsTrue);

        if (ImGui.Button("Crea") || confirmed)
        {
            var nome = _newFolderBuffer;
            var dove = _current;
            _pending = () => Apply(() => AssetFiles.CreateFolder(_root, dove, nome, out _));
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Annulla"))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    /// <summary>
    /// "3 cartelle, 405 file" — quanto pesa il gesto. Il conteggio è ricorsivo e succede solo
    /// quando la finestrella è aperta, cioè una volta per frame su una sola cartella: è il
    /// momento in cui vale la pena pagarlo, non a ogni riquadro disegnato.
    /// </summary>
    private static string DescribeFolderContents(string path)
    {
        try
        {
            var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
            var folders = Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Count();

            if (files == 0 && folders == 0)
                return "La cartella e' vuota.";

            return $"Con dentro {folders} cartelle e {files} file.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "Non si riesce a leggere cosa contiene.";
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

    private bool AtRoot() => Path.GetFullPath(_current) == Path.GetFullPath(_root);

    /// <summary>
    /// ⚠️ Il path va relativo alla cartella asset, non assoluto: è la forma che l'AssetManager
    /// si aspetta e quella che finisce nel file di scena. Regge perché le due radici
    /// coincidono — pannello e AssetManager passano entrambi da <c>ContentRoot</c>. Se un
    /// giorno divergeranno, è qui che il drop comincerà a cercare file inesistenti.
    /// </summary>
    private void BeginDrag(AssetKind kind, string file)
    {
        AssetDragDrop.Source(kind, Path.GetRelativePath(_root, file).Replace('\\', '/'));
    }
}
