using gEngine.Assets;
using gEngine.Ecs.Base;

namespace gEngine.Scenes;

/// <summary>
/// Contesto passato ai binder durante l'istanziazione della scena, per risolvere
/// riferimenti che un singolo <see cref="System.Text.Json.JsonElement"/> non può esprimere
/// da solo:
/// <list type="bullet">
///   <item><see cref="EntitiesByName"/>: nome (dal campo <c>name</c> dell'entità) →
///   <see cref="Entity"/> creata. Serve ai componenti che referenziano altre entità,
///   es. <c>Parent</c>.</item>
///   <item><see cref="Assets"/>: per caricare risorse citate per path nella scena
///   (es. <c>ModelPath</c> di un <c>MeshRenderer</c>).</item>
/// </list>
/// </summary>
public sealed class SceneBindContext
{
    public required IReadOnlyDictionary<string, Entity> EntitiesByName { get; init; }
    public required AssetManager Assets { get; init; }
}
