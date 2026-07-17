using System.Numerics;
using gEngine.Assets;

namespace gEngine.Rendering;

public readonly record struct DrawMeshCommand(
    MeshKind Kind,
    Matrix4x4 World,
    Vector3 Size,
    Color Tint,
    bool Wireframe,
    // Usato solo quando Kind == MeshKind.Model; per le primitive resta ModelHandle.None.
    // Ha un default così i call site delle primitive restano invariati (5 argomenti).
    ModelHandle Model = default,
    // Disegna senza illuminazione (texture già illuminata). Vedi MeshRendererComponent.Unlit.
    // Come sopra: default false per non toccare i call site delle primitive.
    bool Unlit = false
    );
