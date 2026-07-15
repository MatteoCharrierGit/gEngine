using System.Text.Json;
using gEngine.Ecs.Base;

namespace gEngine.Scenes;

public class ComponentBinder<T>(Func<JsonElement, T> parse) : IComponentBinder
{
    public void Apply(World world, Entity entity, JsonElement data)
    {
        world.AddComponent(entity, parse(data));
    }
}