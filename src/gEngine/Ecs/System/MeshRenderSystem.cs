using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.MathUtils;
using gEngine.Rendering;

namespace gEngine.Ecs.System;

public class MeshRenderSystem : IRenderSystem
{
    public IReadOnlyList<Type> MatchedComponents { get; } =
        [typeof(TransformComponent), typeof(MeshRendererComponent)];

    // Buffer riusato ogni frame per evitare un'allocazione a ogni OnRender.
    private readonly List<Entry> _drawList = new();

    // Gli 8 vertici del cubo unitario (spazio locale, centrato nell'origine): trasformati
    // dalla world matrix danno l'ingombro dell'oggetto nel mondo, da cui la bounding sphere.
    private static readonly Vector3[] UnitCubeCorners =
    {
        new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f),
        new(-0.5f,  0.5f, -0.5f), new(0.5f,  0.5f, -0.5f),
        new(-0.5f, -0.5f,  0.5f), new(0.5f, -0.5f,  0.5f),
        new(-0.5f,  0.5f,  0.5f), new(0.5f,  0.5f,  0.5f),
    };

    public void OnCreate(World world)
    {
    }

    public void OnUpdate(World world, float dt)
    {
    }

    public void OnRender(World world, IRenderer renderer, Camera3D camera, float frameDeltaTime)
    {
        _drawList.Clear();

        // Frustum della camera, ricostruito una volta per frame dalla view-projection.
        // GetRenderWidth/Height e non GetScreenWidth/Height: dentro un viewport dell'editor
        // si disegna su un render target grande quanto il pannello, e l'aspect della
        // finestra scarterebbe le entità sbagliate (visibili ai lati, cullate lo stesso).
        var aspect = (float)renderer.GetRenderWidth() / renderer.GetRenderHeight();
        var frustum = Frustum.FromViewProjection(camera.GetViewProjection(aspect));

        // 1) Raccogli i visibili E dentro il frustum, con la chiave di ordinamento.
        foreach (var (entity, _, meshRenderer) in world.Query<TransformComponent, MeshRendererComponent>())
        {
            if (!meshRenderer.Visible)
                continue;

            // GetRenderMatrix risale l'eventuale catena di ParentComponent (per una root
            // coincide col locale, per una figlia include il mondo del genitore) e ci
            // applica la Size. È condivisa con l'EntityPicker apposta: quel che si disegna
            // dev'essere quel che si clicca.
            var worldMatrix = world.GetRenderMatrix(entity, meshRenderer);

            // Frustum culling: se la bounding sphere è tutta fuori, salta la draw call.
            var (center, radius) = BoundingSphere(worldMatrix);
            if (!frustum.IntersectsSphere(center, radius))
                continue;

            var command = new DrawMeshCommand(
                meshRenderer.Kind, worldMatrix, Vector3.Zero, meshRenderer.Tint, meshRenderer.Wireframe,
                meshRenderer.Model, meshRenderer.Unlit);

            _drawList.Add(new Entry(meshRenderer.Layer, meshRenderer.SortingOrder, command));
        }

        // 2) Ordina: prima gli opachi, poi i trasparenti; a parità di layer, per
        //    SortingOrder crescente.
        _drawList.Sort(static (a, b) =>
        {
            var byLayer = a.Layer.CompareTo(b.Layer);
            return byLayer != 0 ? byLayer : a.Order.CompareTo(b.Order);
        });

        // 3) Disegna nell'ordine deciso.
        foreach (var entry in _drawList)
            renderer.DrawMesh(entry.Command);
    }

    // Bounding sphere in spazio mondo: centro = traslazione della world matrix, raggio =
    // distanza massima dai vertici del cubo unitario trasformati. Invariante alla
    // rotazione e corretta anche con scala non uniforme (conservativa).
    // NOTA: assume un ingombro "a cubo unitario" — vale per MeshKind.Cube. Con il
    // caricamento modelli servirà il bounds reale della mesh (Fase 5).
    private static (Vector3 Center, float Radius) BoundingSphere(Matrix4x4 world)
    {
        var center = world.Translation;

        var maxDistanceSq = 0f;
        foreach (var corner in UnitCubeCorners)
        {
            var worldCorner = Vector3.Transform(corner, world);
            var distanceSq = Vector3.DistanceSquared(worldCorner, center);
            if (distanceSq > maxDistanceSq)
                maxDistanceSq = distanceSq;
        }

        return (center, MathF.Sqrt(maxDistanceSq));
    }

    // NOTA (limite noto): i trasparenti NON sono ancora ordinati back-to-front per
    // distanza dalla camera. Ora che OnRender riceve la Camera3D, il passo successivo è:
    // dentro il layer Transparent, ordinare per distanza decrescente da camera.Position.
    private readonly record struct Entry(RenderLayer Layer, int Order, DrawMeshCommand Command);
}
