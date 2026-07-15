using System.Text.Json;
using gEngine.Ecs.Base;

namespace gEngine.Scenes;

public interface IComponentBinder
{
    void Apply(World world, Entity entity, JsonElement data, SceneBindContext context);
}
