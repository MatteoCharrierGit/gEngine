using gEngine.Ecs.Base;

namespace gEngine.Ecs.Interfaces.System;

public interface ISystem
{
    void OnCreate(World world);
    void OnUpdate(World world, float dt);
}