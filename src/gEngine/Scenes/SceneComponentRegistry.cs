using System.Text.Json;
using gEngine.Ecs.Component;
using gEngine.Scenes.Json;

namespace gEngine.Scenes;

public class SceneComponentRegistry
{
    private readonly Dictionary<string, IComponentBinder> _componentBinders = new();

    // Verso opposto: dal tipo del componente alla chiave JSON + come scriverlo. Indicizzato
    // per Type e non per chiave perché chi salva parte da uno storage (che conosce il suo
    // Type) e deve scoprire come si chiama nel file.
    private readonly Dictionary<Type, ComponentWriter> _componentWriters = new();

    private sealed record ComponentWriter(string Key, Func<object, SceneWriteContext, JsonElement> Write);

    /// <summary>Binder semplice: costruisce il componente dai soli dati JSON.</summary>
    public void Register<T>(string key, Func<JsonElement, T> parse, Func<T, SceneWriteContext, JsonElement>? write = null)
    {
        _componentBinders[key] = new ComponentBinder<T>(parse);
        RegisterWriter(key, write);
    }

    /// <summary>
    /// Binder con contesto: per componenti che referenziano altre entità (Parent) o
    /// risorse per path (ModelPath). Riceve il <see cref="SceneBindContext"/>.
    /// </summary>
    public void Register<T>(string key, Func<JsonElement, SceneBindContext, T> parse, Func<T, SceneWriteContext, JsonElement>? write = null)
    {
        _componentBinders[key] = new ContextComponentBinder<T>(parse);
        RegisterWriter(key, write);
    }

    /// <summary>
    /// Il writer è <b>opzionale</b> alla registrazione: per la stragrande maggioranza dei
    /// componenti "serializza i campi con le opzioni condivise" è già l'inverso corretto
    /// del parse di default, quindi chiederlo ogni volta sarebbe cerimonia. Va passato
    /// solo quando la lettura fa qualcosa di asimmetrico che la scrittura deve disfare
    /// (Parent risolve un nome in Entity; MeshRenderer trasforma un path in handle).
    /// </summary>
    private void RegisterWriter<T>(string key, Func<T, SceneWriteContext, JsonElement>? write)
    {
        _componentWriters[typeof(T)] = new ComponentWriter(
            key,
            write is null
                ? (component, _) => JsonSerializer.SerializeToElement((T)component, SceneJson.Options)
                : (component, context) => write((T)component, context));
    }

    public bool TryGet(string key, out IComponentBinder binder)
    {
        return _componentBinders.TryGetValue(key, out binder!);
    }

    /// <summary>Come si chiama e come si scrive questo tipo di componente nel file di scena.</summary>
    public bool TryGetWriter(Type componentType, out string key, out Func<object, SceneWriteContext, JsonElement> write)
    {
        if (_componentWriters.TryGetValue(componentType, out var writer))
        {
            key = writer.Key;
            write = writer.Write;
            return true;
        }

        key = string.Empty;
        write = null!;
        return false;
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
        Register("MeshRenderer",
            (data, ctx) =>
            {
                var mesh = data.Deserialize<MeshRendererComponent>(SceneJson.Options)!;
                if (data.TryGetProperty("ModelPath", out var modelPath) && modelPath.ValueKind == JsonValueKind.String)
                    mesh.Model = ctx.Assets.LoadModel(modelPath.GetString()!);
                return mesh;
            },
            (mesh, ctx) =>
            {
                var node = JsonSerializer.SerializeToNode(mesh, SceneJson.Options)!.AsObject();

                // Il campo Model è un handle: un id opaco valido solo per questa
                // esecuzione. Scriverlo sarebbe peggio che inutile — al reload verrebbe
                // deserializzato come handle buono e punterebbe a un modello a caso.
                // Al suo posto va il path, che è l'unica cosa che sopravvive al riavvio.
                node.Remove(nameof(MeshRendererComponent.Model));

                if (mesh.Model.IsValid && ctx.Assets.TryGetModelPath(mesh.Model, out var path))
                    node["ModelPath"] = path;

                return JsonSerializer.SerializeToElement(node, SceneJson.Options);
            });

        // Parent: il valore JSON è il "name" dell'entità genitore; lo risolviamo nella
        // Entity creata tramite il contesto (richiede l'istanziazione a due passate).
        Register("Parent",
            (data, ctx) =>
            {
                var parentName = data.GetString()
                                 ?? throw new InvalidOperationException("Il componente 'Parent' dev'essere il nome (stringa) dell'entità genitore.");

                if (!ctx.EntitiesByName.TryGetValue(parentName, out var parent))
                    throw new InvalidOperationException(
                        $"Parent: nessuna entità con name '{parentName}' nella scena. Assicurati che il genitore abbia un campo \"name\" corrispondente.");

                return new ParentComponent { Parent = parent };
            },
            // Scrittura: dall'Entity si torna al nome. Gli id non sopravvivono al reload
            // (si riassegnano da capo), i nomi sì.
            (parent, ctx) => JsonSerializer.SerializeToElement(ctx.RequireName(parent.Parent, "Parent"), SceneJson.Options));
    }
}
