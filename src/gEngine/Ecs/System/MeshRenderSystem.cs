using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.MathUtils;
using gEngine.Rendering;

namespace gEngine.Ecs.System;

public class MeshRenderSystem : IRenderSystem
{
    public void OnCreate(World world)
    {
    }

    public void OnUpdate(World world, float dt)
    {
    }

    public void OnRender(World world, IRenderer renderer, float frameDeltaTime)
    {
        foreach (var (_, transform, meshRenderer) in world.Query<TransformComponent, MeshRendererComponent>())
        {
            if (!meshRenderer.Visible)
                continue;

            var worldMatrix = Matrix4x4.CreateScale(meshRenderer.Size) * transform.GetLocalMatrix();

            renderer.DrawMesh(new DrawMeshCommand(meshRenderer.Kind, worldMatrix, Vector3.Zero, meshRenderer.Tint, meshRenderer.Wireframe));
        }
    }
}
