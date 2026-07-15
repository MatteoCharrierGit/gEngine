using System.Text.Json;

namespace gEngine.Scenes;

/// <summary>
/// Rappresentazione in memoria di una scena caricata da file, prima
/// dell'istanziazione nel <c>World</c>. Non conosce nessun tipo di componente:
/// ogni entità è solo un bag chiave→dati grezzi (<see cref="JsonElement"/>),
/// interpretati dai binder del <see cref="SceneComponentRegistry"/> solo in fase
/// di <see cref="SceneInstantiator.Instantiate"/>.
/// </summary>
public class Scene
{
    public string Name { get; set; } = string.Empty;
    public List<EntityDefinition> Entities { get; set; } = new();
}

/// <summary>
/// Definizione di una singola entità: mappa nome-componente → dati JSON grezzi.
/// La chiave (es. <c>"Transform"</c>, <c>"MeshRenderer"</c>, <c>"Player"</c>) è
/// quella registrata nel <see cref="SceneComponentRegistry"/>.
/// </summary>
public class EntityDefinition
{
    /// <summary>
    /// Nome opzionale dell'entità nella scena. Serve solo come <b>bersaglio</b> di
    /// riferimenti da altre entità (es. il campo <c>Parent</c>); non finisce nel World.
    /// </summary>
    public string? Name { get; set; }

    public Dictionary<string, JsonElement> Components { get; set; } = new();
}
