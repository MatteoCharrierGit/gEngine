using System.Numerics;

namespace gEngine.Rendering;

/// <summary>
/// Dato luce lib-independent passato al renderer per frame (via
/// <see cref="IRenderer.SetLighting"/>). Snapshot piatto: niente riferimenti a ECS o
/// raylib. Per una direzionale conta <see cref="Direction"/>; per una point conta
/// <see cref="Position"/>.
/// </summary>
public readonly record struct LightData(
    LightKind Kind,
    Vector3 Position,
    Vector3 Direction,
    Color Color,
    float Intensity);
