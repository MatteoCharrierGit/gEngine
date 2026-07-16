using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>Vedi <see cref="EntityDefinition.Extra"/>.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; } = new();
}

/// <summary>
/// Definizione di una singola entità: mappa nome-componente → dati JSON grezzi.
/// La chiave (es. <c>"Transform"</c>, <c>"MeshRenderer"</c>, <c>"Player"</c>) è
/// quella registrata nel <see cref="SceneComponentRegistry"/>.
/// </summary>
public class EntityDefinition
{
    /// <summary>
    /// Nome opzionale dell'entità nella scena. È il <b>bersaglio</b> dei riferimenti da
    /// altre entità (es. il campo <c>Parent</c>) e, quando valorizzato, viene copiato nel
    /// World come <see cref="gEngine.Ecs.Component.NameComponent"/> — così l'editor può
    /// etichettare l'entità e il salvataggio può riscrivere questo stesso campo.
    ///
    /// Resta un campo a sé e <b>non</b> una chiave del bag <see cref="Components"/>: il
    /// nome identifica l'entità nel file, quindi deve essere leggibile dalla prima passata
    /// di <see cref="SceneInstantiator"/>, prima che qualunque binder abbia girato.
    /// </summary>
    public string? Name { get; set; }

    public Dictionary<string, JsonElement> Components { get; set; } = new();

    /// <summary>
    /// Tutto ciò che sta nel file ma che il formato non modella — oggi in pratica i
    /// <c>_comment</c> con cui le scene si documentano.
    ///
    /// Esiste per una ragione precisa: da quando l'editor sa <b>salvare</b>, ciò che non
    /// viene letto viene <b>distrutto</b>. Senza questo bag, il primo Save cancellerebbe
    /// ogni commento scritto a mano nella scena — una perdita silenziosa e irreversibile
    /// di lavoro che il formato non aveva motivo di buttare via, solo di ignorare.
    ///
    /// Nota: in scrittura le chiavi extra finiscono <b>dopo</b> quelle dichiarate, quindi
    /// il primo salvataggio di un file scritto a mano sposta i <c>_comment</c> in fondo
    /// all'entità. Cambia il diff, non il contenuto.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; } = new();
}
