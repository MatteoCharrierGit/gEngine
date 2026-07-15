using System.Numerics;

namespace gEngine.Rendering;

public readonly record  struct DrawMeshCommand(
    MeshKind Kind,
    Vector3 Position,
    Vector3 Size,
    Color Tint,
    bool Wireframe
    );