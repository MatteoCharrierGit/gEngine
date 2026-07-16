using System.Text.Json;
using gEngine.Assets;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;

namespace gEngine.Scenes;

/// <summary>
/// Verso opposto di <see cref="SceneInstantiator"/>: legge un <see cref="World"/> e ne
/// ricava una <see cref="Scene"/> salvabile. Come l'istanziatore, non conosce nessun tipo
/// di componente: chiede al <see cref="SceneComponentRegistry"/> come si chiama e come si
/// scrive ogni tipo che trova negli storage.
///
/// Due categorie di componenti <b>non</b> finiscono nel file, per motivi diversi:
/// <list type="bullet">
///   <item>quelli marcati <see cref="RuntimeStateAttribute"/> — sono link a risorse create
///   a runtime (un corpo fisico), non dati d'autore: salvarli significherebbe salvare un
///   id valido solo per questa esecuzione;</item>
///   <item><see cref="NameComponent"/> — il nome esiste già nel formato, come campo
///   <c>Name</c> dell'entità. Scriverlo anche come componente creerebbe due punti di
///   verità che possono divergere.</item>
/// </list>
/// </summary>
public static class SceneSerializer
{
    /// <param name="source">
    /// La scena da cui il World è stato caricato, se c'è. Serve solo a <b>non perdere</b>
    /// ciò che il World non contiene: i <c>_comment</c> e ogni altra chiave che il formato
    /// non modella (vedi <see cref="EntityDefinition.Extra"/>) vivono nel file, non nei
    /// componenti, quindi senza il file d'origine il salvataggio li cancellerebbe.
    ///
    /// La fusione è per <b>nome</b>: un'entità rinominata o creata nell'editor non ha un
    /// originale da cui pescare e perde/non ha commenti. È un compromesso — l'alternativa
    /// sarebbe trattare i commenti come dati d'entità e portarseli nel World, che
    /// significherebbe inquinare l'ECS con metadati d'autore.
    /// </param>
    public static Scene ToScene(
        World world, SceneComponentRegistry registry, AssetManager assets, string sceneName, Scene? source = null)
    {
        var context = new SceneWriteContext
        {
            NamesByEntity = CollectNames(world),
            Assets = assets
        };

        var scene = new Scene { Name = sceneName };

        if (source is not null)
        {
            foreach (var (key, value) in source.Extra)
                scene.Extra[key] = value;
        }

        var extrasByName = CollectExtras(source);

        foreach (var entity in world.AllEntities)
        {
            var definition = ToDefinition(world, registry, context, entity);

            if (definition.Name is not null && extrasByName.TryGetValue(definition.Name, out var extra))
            {
                foreach (var (key, value) in extra)
                    definition.Extra[key] = value;
            }

            scene.Entities.Add(definition);
        }

        return scene;
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> CollectExtras(Scene? source)
    {
        var extras = new Dictionary<string, Dictionary<string, JsonElement>>();

        if (source is null)
            return extras;

        foreach (var definition in source.Entities)
        {
            if (!string.IsNullOrWhiteSpace(definition.Name) && definition.Extra.Count > 0)
                extras[definition.Name] = definition.Extra;
        }

        return extras;
    }

    private static Dictionary<int, string> CollectNames(World world)
    {
        var names = new Dictionary<int, string>();

        foreach (var (entity, name) in world.Query<NameComponent>())
        {
            if (!string.IsNullOrWhiteSpace(name.Value))
                names[entity.Id] = name.Value;
        }

        return names;
    }

    private static EntityDefinition ToDefinition(
        World world, SceneComponentRegistry registry, SceneWriteContext context, Entity entity)
    {
        var definition = new EntityDefinition();

        if (context.NamesByEntity.TryGetValue(entity.Id, out var name))
            definition.Name = name;

        foreach (var storage in world.ComponentStorages)
        {
            if (!storage.Has(entity.Id))
                continue;

            var type = storage.ComponentType;

            if (type == typeof(NameComponent))
                continue; // già scritto come definition.Name

            if (type.IsDefined(typeof(RuntimeStateAttribute), inherit: false))
                continue; // stato di runtime: lo ricrea il system al prossimo update

            if (!registry.TryGetWriter(type, out var key, out var write))
                throw new InvalidOperationException(
                    $"Nessun writer registrato per il componente '{type.Name}'. " +
                    "Registralo nel SceneComponentRegistry (engine defaults o custom del gioco) prima di salvare la scena. " +
                    "Se è stato di runtime e non deve essere salvato, marcalo con [RuntimeState].");

            var component = storage.GetBoxed(entity.Id);
            if (component is not null)
                definition.Components[key] = write(component, context);
        }

        return definition;
    }
}
