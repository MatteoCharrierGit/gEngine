using gEngine.Ecs.Base;
using gEngine.Ecs.System;

namespace gEngine.Ecs.Component.Event;

public struct Collision2DComponentEvent
{
    public List<Collision2DInfo> Collisions;
}

public struct Collision2DInfo
{
    public Collision2DAxis Axis;
    public float OverlapX;
    public float OverlapY;
    public Entity Other;
}