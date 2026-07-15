using System.Numerics;

namespace gEngine.Rendering;

public readonly record struct DrawMeshCommand(
    MeshKind Kind,
    Matrix4x4 World,
    Vector3 Size,
    Color Tint,
    bool Wireframe
    );