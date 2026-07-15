using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.MathUtils;
using gEngine.Rendering;

namespace gEngine.Ecs.System;

/// <summary>
/// Raccoglie le luci della scena (entità con <see cref="LightComponent"/> +
/// <see cref="TransformComponent"/>) e le carica nel renderer per il frame corrente.
/// ⚠️ Va registrato PRIMA del <see cref="MeshRenderSystem"/> tra gli
/// <see cref="IRenderSystem"/>, così le uniform delle luci sono impostate prima di
/// disegnare le mesh.
/// </summary>
public class LightingSystem : IRenderSystem
{
    // Deve combaciare con MAX_LIGHTS nello shader lit.fs. Le luci oltre questo numero
    // vengono ignorate.
    private const int MaxLights = 4;

    private readonly List<LightData> _lights = new();

    public void OnCreate(World world)
    {
    }

    public void OnUpdate(World world, float dt)
    {
    }

    public void OnRender(World world, IRenderer renderer, Camera3D camera, float frameDeltaTime)
    {
        _lights.Clear();

        foreach (var (_, transform, light) in world.Query<TransformComponent, LightComponent>())
        {
            if (_lights.Count >= MaxLights)
                break;

            _lights.Add(new LightData(
                light.Kind,
                transform.Position,        // usata dalle point
                transform.GetForward(),    // usata dalle direzionali
                light.Color,
                light.Intensity));
        }

        renderer.SetLighting(camera.Position, _lights);
    }
}
