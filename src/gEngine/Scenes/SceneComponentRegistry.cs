using System.Text.Json;
using gEngine.Ecs.Component;
using gEngine.Scenes.Json;

namespace gEngine.Scenes;

public class SceneComponentRegistry
{
    private readonly Dictionary<string, IComponentBinder> _componentBinders = new();

    /// <summary>Binder semplice: costruisce il componente dai soli dati JSON.</summary>
    public void Register<T>(string key, Func<JsonElement, T> parse)
    {
        _componentBinders[key] = new ComponentBinder<T>(parse);
    }

    /// <summary>
    /// Binder con contesto: per componenti che referenziano altre entità (Parent) o
    /// risorse per path (ModelPath). Riceve il <see cref="SceneBindContext"/>.
    /// </summary>
    public void Register<T>(string key, Func<JsonElement, SceneBindContext, T> parse)
    {
        _componentBinders[key] = new ContextComponentBinder<T>(parse);
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
        Register("Light", data => data.Deserialize<LightComponent>(SceneJson.Options));
        Register("RigidBody", data => data.Deserialize<RigidBodyComponent>(SceneJson.Options));

        // MeshRenderer: oltre ai campi standard, un opzionale "ModelPath" carica il modello
        // via AssetManager (dal contesto) e ne assegna l'handle. Così i modelli sono
        // autorabili da scena senza esporre gli handle nativi.
        Register("MeshRenderer", (data, ctx) =>
        {
            var mesh = data.Deserialize<MeshRendererComponent>(SceneJson.Options)!;
            if (data.TryGetProperty("ModelPath", out var modelPath) && modelPath.ValueKind == JsonValueKind.String)
                mesh.Model = ctx.Assets.LoadModel(modelPath.GetString()!);
            return mesh;
        });

        // Parent: il valore JSON è il "name" dell'entità genitore; lo risolviamo nella
        // Entity creata tramite il contesto (richiede l'istanziazione a due passate).
        Register("Parent", (data, ctx) =>
        {
            var parentName = data.GetString()
                             ?? throw new InvalidOperationException("Il componente 'Parent' dev'essere il nome (stringa) dell'entità genitore.");

            if (!ctx.EntitiesByName.TryGetValue(parentName, out var parent))
                throw new InvalidOperationException(
                    $"Parent: nessuna entità con name '{parentName}' nella scena. Assicurati che il genitore abbia un campo \"name\" corrispondente.");

            return new ParentComponent { Parent = parent };
        });
    }
}
