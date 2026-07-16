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

    /// <summary>
    /// Scrive la scena su file. <c>WriteIndented</c> perché un file di scena è
    /// <b>sorgente versionato</b>: deve restare leggibile e produrre diff sensati in git,
    /// non essere compatto.
    ///
    /// Scrittura atomica (file temporaneo + move): un salvataggio interrotto a metà
    /// lascerebbe la scena troncata — cioè distruggerebbe il lavoro che stava salvando.
    /// </summary>
    public static void Save(Scene scene, string path)
    {
        var options = new JsonSerializerOptions(SceneJson.Options) { WriteIndented = true };
        var text = JsonSerializer.Serialize(scene, options);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, text);
        File.Move(tempPath, path, overwrite: true);
    }
}
