using System.Text.Json;
using gEngine.Ecs.Component;
using gEngine.Scenes.Json;

namespace gEngine.Scenes;

public class SceneComponentRegistry
{
    private readonly Dictionary<string, IComponentBinder> _componentBinders = new();

    public void Register<T>(string key, Func<JsonElement, T> parse)
    {
        _componentBinders[key] = new ComponentBinder<T>(parse);
    }

    public bool TryGet(string key, out IComponentBinder binder)
    {
        return _componentBinders.TryGetValue(key, out binder!);
    }

    /// <summary>
    /// Registra i componenti built-in dell'engine. I giochi estendono lo stesso
    /// registry con i propri componenti custom (es. Player, Velocity), riusando
    /// <see cref="SceneJson.Options"/> per mantenere identico il formato JSON.
    /// </summary>
    public void RegisterEngineDefaults()
    {
        Register("Transform", data => data.Deserialize<TransformComponent>(SceneJson.Options));
        Register("MeshRenderer", data => data.Deserialize<MeshRendererComponent>(SceneJson.Options));
    }
}