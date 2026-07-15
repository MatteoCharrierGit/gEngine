namespace gEngine.Rendering;

/// <summary>
/// Tipo di luce. ⚠️ I valori numerici devono combaciare con le costanti nello shader
/// (<c>Shaders/lit.fs</c>): <c>LIGHT_DIRECTIONAL 0</c>, <c>LIGHT_POINT 1</c>.
/// </summary>
public enum LightKind
{
    Directional = 0,
    Point = 1,
}
