using gEngine.Ecs.Base;

namespace gEngine.Editor;

/// <summary>
/// Un pannello dell'editor. Viene disegnato una volta per frame, dentro il blocco
/// ImGui aperto da <see cref="EditorHost"/>: l'implementazione apre la propria
/// finestra e basta, non deve gestire begin/end del frame ImGui.
/// </summary>
public interface IEditorPanel
{
    void Draw(World world, EditorContext context);
}
