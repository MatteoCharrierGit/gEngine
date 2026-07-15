using System.Text.Json;
using System.Text.Json.Serialization;

namespace gEngine.Scenes.Json;

/// <summary>
/// Opzioni <see cref="JsonSerializerOptions"/> condivise per il caricamento delle scene.
/// Le usano sia i binder built-in dell'engine (<see cref="SceneComponentRegistry.RegisterEngineDefaults"/>)
/// sia i giochi per registrare i propri componenti custom, così il formato JSON
/// resta identico ovunque.
///
/// Configurazione:
/// - <c>IncludeFields = true</c>: i componenti (es. <c>TransformComponent</c>) espongono
///   campi pubblici, non proprietà — senza questo la deserializzazione li ignorerebbe.
/// - <c>PropertyNameCaseInsensitive</c>: tolleranza sul casing delle chiavi.
/// - converter per i tipi math che STJ non gestisce nel formato "a dizionario"
///   voluto (<c>Vector3</c>, <c>Quaternion</c>, <c>Color</c>).
/// - <c>JsonStringEnumConverter</c>: gli enum (es. <c>MeshKind</c>) come stringa (<c>"Cube"</c>).
/// </summary>
public static class SceneJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        options.Converters.Add(new Vector3JsonConverter());
        options.Converters.Add(new QuaternionJsonConverter());
        options.Converters.Add(new ColorJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
