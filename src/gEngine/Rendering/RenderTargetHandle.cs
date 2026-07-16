namespace gEngine.Rendering;

/// <summary>
/// Handle opaco verso un render target: una texture su cui disegnare invece che sulla
/// finestra. Stessa idea degli handle in <c>gEngine.Assets</c> — solo un id verso la
/// tabella interna del renderer, così chi lo usa non sa (e non deve sapere) che dietro
/// c'è una <c>RenderTexture2D</c> di raylib, un FBO OpenGL o altro.
///
/// Convenzione condivisa con gli altri handle: <c>Id == 0</c> significa "nessun target".
/// </summary>
public readonly record struct RenderTargetHandle(int Id)
{
    public static readonly RenderTargetHandle None = new(0);
    public bool IsValid => Id != 0;
}
