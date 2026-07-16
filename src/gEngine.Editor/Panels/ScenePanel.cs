using gEngine.Ecs.Base;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Salvataggio e ricaricamento della scena. Pannello separato dalla Hierarchy perché
/// riguarda il <b>documento</b>, non le entità: la Hierarchy modifica il contenuto, questo
/// decide cosa finisce su disco.
/// </summary>
public class ScenePanel(SceneDocument? document) : IEditorPanel
{
    private string _status = string.Empty;
    private bool _statusIsError;

    public void Draw(World world, EditorContext context)
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(20, 520), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(280, 130), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Scena"))
        {
            ImGui.End();
            return;
        }

        if (document is null)
        {
            // Un gioco può usare l'editor senza dargli una scena su file (entità costruite
            // in codice): lì salvare non ha un bersaglio, e dirlo è meglio che mostrare un
            // bottone che non fa niente.
            ImGui.TextDisabled("Nessuna scena su file:\nsalvataggio non disponibile.");
            ImGui.End();
            return;
        }

        ImGui.TextDisabled(document.Path);
        ImGui.Separator();

        if (ImGui.Button("Salva"))
            Run(document.Save, "Scena salvata.");

        ImGui.SameLine();
        if (ImGui.Button("Ricarica"))
        {
            // La selezione punta a entità che il ricaricamento distrugge. Gli id non si
            // riusano, quindi resterebbe solo invalida invece che sbagliata — ma tanto
            // vale non lasciarla lì.
            context.ClearSelection();
            Run(document.Load, "Scena ricaricata da disco.");
        }

        if (_status.Length > 0)
        {
            ImGui.Separator();
            if (_statusIsError)
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _status);
            else
                ImGui.TextWrapped(_status);
        }

        ImGui.End();
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
