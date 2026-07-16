using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Sfoglia la cartella degli asset del gioco e ne <b>trascina</b> il contenuto negli slot
/// dell'Inspector (vedi <see cref="AssetDragDrop"/>).
///
/// Il disco resta in <b>sola lettura</b>: niente creare/rinominare/eliminare. Non è pigrizia
/// travestita da prudenza — è che qui sotto ci sono i file dell'utente e questo pannello non
/// ha né un undo né un cestino da offrirgli. Finché non ce li ha, "elimina" sarebbe un
/// bottone che distrugge lavoro senza rete.
///
/// Legge il disco a ogni frame (<c>EnumerateDirectories</c>/<c>EnumerateFiles</c> su una sola
/// cartella): come l'albero della Hierarchy, che si ricostruisce ogni frame. Se un giorno la
/// cartella diventerà grande abbastanza da farsi sentire, il posto giusto è una cache
/// invalidata da un FileSystemWatcher — non un elenco caricato una volta e mai più, che
/// mostrerebbe per sempre lo stato dell'avvio.
/// </summary>
/// <param name="root">
/// La radice oltre la quale non si sale. ⚠️ È un confine vero e va difeso: "su" dalla radice
/// porterebbe a sfogliare il disco dell'utente da un pannello che dice "assets".
/// </param>
public class FileSystemPanel(string root)
    : PanelBase("File system", new Vector2(20, 520), new Vector2(280, 200))
{
    private readonly string _root = root;

    private string _current = root;

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
            _current = _root;

        ImGui.TextDisabled(Relative(_current));
        ImGui.Separator();

        var atRoot = Path.GetFullPath(_current) == Path.GetFullPath(_root);

        ImGui.BeginDisabled(atRoot);
        if (ImGui.SmallButton(".."))
            _current = Directory.GetParent(_current)?.FullName ?? _root;
        ImGui.EndDisabled();

        ImGui.Separator();

        // Il disco può negare l'accesso o sparire da sotto: come per il salvataggio, qui
        // un'eccezione cadrebbe nel frame di disegno. Vedi MainMenuBar.Run — stesso motivo,
        // ma qui non c'è niente da rimediare per l'utente, solo da mostrare.
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(_current))
            {
                // Selectable e non TreeNode: l'albero ricorsivo aprirebbe la porta alle
                // cartelle profonde e alla loro cache. Qui si naviga una cartella per volta.
                if (ImGui.Selectable($"[ ] {Path.GetFileName(directory)}", false,
                        ImGuiSelectableFlags.AllowDoubleClick) &&
                    ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    _current = directory;
            }

            foreach (var file in Directory.EnumerateFiles(_current))
                DrawFile(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), ex.Message);
        }
    }

    /// <summary>
    /// Un file: il nome, e — se è un asset che sappiamo caricare — la possibilità di
    /// trascinarlo in uno slot dell'Inspector.
    ///
    /// I file che non sappiamo caricare (un license.txt, uno scene.bin di un glTF) restano
    /// <b>visibili e spenti</b> invece di sparire: la cartella è quella dell'utente e
    /// nasconderne metà farebbe cercare i file che ci sono.
    /// </summary>
    private void DrawFile(string file)
    {
        var kind = AssetDragDrop.Classify(file);
        var name = Path.GetFileName(file);

        if (kind is not { } assetKind)
        {
            ImGui.TextDisabled(name);
            return;
        }

        // Selectable e non TextUnformatted: un testo nudo non è un item per ImGui e non può
        // essere una sorgente di trascinamento — è l'ultimo item disegnato che diventa
        // sorgente, e deve avere un id.
        ImGui.Selectable(name);

        // ⚠️ Il path va relativo alla cartella asset, non assoluto: è la forma che
        // l'AssetManager si aspetta e quella che finisce nel file di scena. Funziona perché
        // le due radici coincidono — questo pannello deduce "assets" accanto all'eseguibile
        // con la stessa convenzione dell'AssetManager (vedi EditorHost.Setup). Se un giorno
        // divergeranno, è qui che il drop comincerà a cercare file inesistenti.
        AssetDragDrop.Source(assetKind, Path.GetRelativePath(_root, file).Replace('\\', '/'));

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip($"{assetKind} · trascinalo su un campo dell'Inspector");
    }

    private string Relative(string path)
    {
        var relative = Path.GetRelativePath(_root, path);
        return relative == "." ? Path.GetFileName(_root.TrimEnd(Path.DirectorySeparatorChar)) : relative;
    }
}
