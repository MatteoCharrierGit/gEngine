using System.Numerics;
using gEngine.Rendering;

namespace gEngine.Ecs.Component;

public class MeshRendererComponent
{
    public MeshKind Kind;
    public Vector3 Size;
    public Color Tint;
    public bool Wireframe;
    public bool Visible;
}