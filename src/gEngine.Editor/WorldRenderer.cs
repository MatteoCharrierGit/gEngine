using gEngine.Rendering;

namespace gEngine.Editor;

/// <summary>
/// Come si disegna il mondo da un punto di vista qualunque. Lo fornisce il gioco, perché
/// è lui a possedere i render system e a sapere cosa significa "disegnare la scena";
/// l'editor sa solo che gli serve farlo <b>due volte con due camere diverse</b>, dentro
/// due render target.
///
/// L'implementazione include il proprio <c>Begin3D</c>/<c>End3D</c>: il blocco 3D
/// appartiene a chi disegna, e il viewport si occupa solo di dove finiscono i pixel.
/// </summary>
public delegate void WorldRenderer(IRenderer renderer, Camera3D camera);
