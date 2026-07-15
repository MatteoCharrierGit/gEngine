using gEngine.Ecs.Base;

namespace gEngine.Scenes;

/// <summary>
/// Istanzia una <see cref="Scene"/> dentro un <see cref="World"/>: per ogni
/// <see cref="EntityDefinition"/> crea una entità e applica i suoi componenti
/// tramite i binder del <see cref="SceneComponentRegistry"/>. È totalmente
/// generico — non conosce nessun tipo di componente specifico.
/// </summary>
public static class SceneInstantiator
{
    public static void Instantiate(Scene scene, World world, SceneComponentRegistry registry)
    {
        foreach (var entityDef in scene.Entities)
        {
            var entity = world.CreateEntity();

            foreach (var (key, data) in entityDef.Components)
            {
                if (!registry.TryGet(key, out var binder))
                    throw new InvalidOperationException(
                        $"Nessun binder registrato per il componente '{key}'. " +
                        $"Registralo nel SceneComponentRegistry (engine defaults o custom del gioco) prima di istanziare la scena '{scene.Name}'.");

                binder.Apply(world, entity, data);
            }
        }
    }
}
