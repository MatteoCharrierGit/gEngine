using System.Text.Json;
using gEngine.Scenes.Json;

namespace gEngine.Scenes;

/// <summary>
/// Carica una <see cref="Scene"/> da un file JSON con forma:
/// <code>
/// {
///   "name": "city",
///   "entities": [
///     { "components": { "Transform": { ... }, "MeshRenderer": { ... } } }
///   ]
/// }
/// </code>
/// I valori dei componenti restano <see cref="JsonElement"/> grezzi: non vengono
/// interpretati qui, ma dai binder in fase di istanziazione.
/// </summary>
public static class JsonSceneLoader
{
    public static Scene Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File di scena non trovato: {path}", path);

        var text = File.ReadAllText(path);

        var scene = JsonSerializer.Deserialize<Scene>(text, SceneJson.Options)
                    ?? throw new InvalidDataException($"Scena JSON vuota o non valida: {path}");

        return scene;
    }
}
