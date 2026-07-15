using gEngine.Ecs.Base;
using gEngine.Rendering;

namespace gEngine.Ecs.Interfaces.System;

public interface IRenderSystem : ISystem
{
    void OnRender(World world, IRenderer renderer, Camera3D camera, float frameDeltaTime);
}