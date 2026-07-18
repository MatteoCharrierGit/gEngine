using System.Numerics;
using gEngine.Assets;
using gEngine.Ecs.Base;
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
/// Il disco resta in <b>sola lettura</b>: niente creare/rinominare/eliminare. Non è pigrizia
/// travestita da prudenza — l'undo dell'editor copre il World, non il disco: per "elimina"
/// serve un cestino, che è una rete diversa e ancora da fare.
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
public class FileSystemPanel(string root)
    : PanelBase("File system", new Vector2(20, 520), new Vector2(360, 300))
{
    private readonly string _root = root;

    private string _current = root;

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

        ImGui.EndChild();
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
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - 170f));

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
            if (ImGui.Selectable($"[ ] {Path.GetFileName(directory)}", false,
                    ImGuiSelectableFlags.AllowDoubleClick) &&
                ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                Navigate(directory);
        }

        foreach (var file in Directory.EnumerateFiles(_current))
        {
            var kind = AssetDragDrop.Classify(file);
            var name = Path.GetFileName(file);

            // I file che non sappiamo caricare (un license.txt, uno scene.bin di un glTF)
            // restano visibili e spenti invece di sparire: la cartella è quella dell'utente e
            // nasconderne metà farebbe cercare i file che ci sono.
            if (kind is not { } assetKind)
            {
                ImGui.TextDisabled(name);
                continue;
            }

            // Selectable e non TextUnformatted: un testo nudo non è un item per ImGui e non può
            // essere una sorgente di trascinamento — è l'ultimo item disegnato che diventa
            // sorgente, e deve avere un id.
            ImGui.Selectable(name);
            BeginDrag(assetKind, file);
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

            if (DrawTile(Path.GetFileName(directory), TileKind.Folder, 0))
                Navigate(directory);
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

        if (hovered && path is not null)
            ImGui.SetTooltip($"{name}\n{Describe(path)}");
        else if (hovered && kind == TileKind.Folder)
            ImGui.SetTooltip($"{name}\nDoppio clic per entrare");

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
