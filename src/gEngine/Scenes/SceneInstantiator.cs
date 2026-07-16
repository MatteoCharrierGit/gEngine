using gEngine.Assets;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;

namespace gEngine.Scenes;

/// <summary>
/// Istanzia una <see cref="Scene"/> dentro un <see cref="World"/>. È totalmente generico
/// (non conosce nessun tipo di componente) e procede in <b>due passate</b>:
/// <list type="number">
///   <item>crea tutte le entità e costruisce la mappa <c>name → Entity</c>;</item>
///   <item>applica i componenti, passando ai binder un <see cref="SceneBindContext"/> con
///   la mappa (per i riferimenti tra entità, es. <c>Parent</c>) e l'AssetManager (per le
///   risorse citate per path, es. <c>ModelPath</c>).</item>
/// </list>
/// Le due passate servono perché un componente può referenziare un'entità definita
/// <i>dopo</i> di sé nel file.
/// </summary>
public static class SceneInstantiator
{
    public static void Instantiate(Scene scene, World world, SceneComponentRegistry registry, AssetManager assets)
    {
        // Passata 1: crea le entità e mappa i nomi.
        var created = new List<(EntityDefinition Def, Entity Entity)>(scene.Entities.Count);
        var entitiesByName = new Dictionary<string, Entity>();

        foreach (var entityDef in scene.Entities)
        {
            var entity = world.CreateEntity();
            created.Add((entityDef, entity));

            if (!string.IsNullOrWhiteSpace(entityDef.Name))
            {
                entitiesByName[entityDef.Name] = entity;

                // Il nome entra anche nel World come NameComponent: serve all'editor per
                // etichettare le entità e al futuro salvataggio per riscrivere il campo.
                // Non è un componente dichiarabile nel bag "components" del file — deriva
                // dal campo Name dell'entità, che resta l'unico punto di verità.
                world.AddComponent(entity, new NameComponent { Value = entityDef.Name });
            }
        }

        var context = new SceneBindContext { EntitiesByName = entitiesByName, Assets = assets };

        // Passata 2: applica i componenti.
        foreach (var (entityDef, entity) in created)
        {
            foreach (var (key, data) in entityDef.Components)
            {
                if (!registry.TryGet(key, out var binder))
                    throw new InvalidOperationException(
                        $"Nessun binder registrato per il componente '{key}'. " +
                        $"Registralo nel SceneComponentRegistry (engine defaults o custom del gioco) prima di istanziare la scena '{scene.Name}'.");

                binder.Apply(world, entity, data, context);
            }
        }
    }
}
