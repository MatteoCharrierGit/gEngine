using gEngine.Assets;
using gEngine.Ecs.Base;

namespace gEngine.Scenes;

/// <summary>
/// Contesto passato ai writer durante il salvataggio: lo specchio esatto di
/// <see cref="SceneBindContext"/>, con le due mappe percorse al contrario.
/// <list type="bullet">
///   <item><see cref="NamesByEntity"/>: <see cref="Entity"/> → nome. Un id di entità non
///   significa niente al prossimo caricamento (gli id si riassegnano da capo), quindi un
///   riferimento fra entità va scritto come <b>nome</b> — l'inverso di
///   <c>EntitiesByName</c>.</item>
///   <item><see cref="Assets"/>: per risalire dall'handle opaco al path da scrivere
///   (<c>ModelPath</c>) — l'inverso di <c>LoadModel</c>.</item>
/// </list>
/// </summary>
public sealed class SceneWriteContext
{
    public required IReadOnlyDictionary<int, string> NamesByEntity { get; init; }
    public required AssetManager Assets { get; init; }

    /// <summary>
    /// Nome dell'entità referenziata, o eccezione se non ne ha uno. Un riferimento verso
    /// un'entità senza nome <b>non è scrivibile</b>: al reload non ci sarebbe modo di
    /// ritrovarla. Meglio fallire qui, dicendo quale entità va nominata, che salvare un
    /// file che si ricarica monco.
    /// </summary>
    public string RequireName(Entity entity, string referenceKind)
    {
        if (NamesByEntity.TryGetValue(entity.Id, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;

        throw new InvalidOperationException(
            $"Impossibile salvare il riferimento '{referenceKind}' verso l'entità {entity.Id}: non ha un nome. " +
            "I riferimenti fra entità nel file di scena sono per nome — dalle un nome (campo Name nell'Inspector) e risalva.");
    }
}
