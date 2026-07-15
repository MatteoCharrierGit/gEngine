using System.Text.Json;
using gEngine.Ecs.Base;

namespace gEngine.Scenes;

/// <summary>
/// Binder semplice: deserializza il componente dai soli dati JSON, ignorando il contesto.
/// Copre la maggioranza dei componenti (Transform, Light, Velocity, ...).
/// </summary>
public class ComponentBinder<T>(Func<JsonElement, T> parse) : IComponentBinder
{
    public void Apply(World world, Entity entity, JsonElement data, SceneBindContext context)
    {
        world.AddComponent(entity, parse(data));
    }
}

/// <summary>
/// Binder che ha bisogno del <see cref="SceneBindContext"/> per costruire il componente:
/// riferimenti ad altre entità (es. <c>Parent</c>) o risorse per path (es. <c>ModelPath</c>).
/// </summary>
public class ContextComponentBinder<T>(Func<JsonElement, SceneBindContext, T> parse) : IComponentBinder
{
    public void Apply(World world, Entity entity, JsonElement data, SceneBindContext context)
    {
        world.AddComponent(entity, parse(data, context));
    }
}
