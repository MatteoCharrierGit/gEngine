using System.Numerics;
using gEngine.Ecs.Component;
using gEngine.Rendering;

namespace gEngine.Ecs.Base;

/// <summary>
/// Il ponte fra la camera come <b>dato di scena</b> (<see cref="CameraComponent"/> +
/// <see cref="TransformComponent"/> su un'entità) e la camera come <b>oggetto di
/// matematica</b> (<see cref="Camera3D"/>), che è quella che sanno usare il renderer, il
/// frustum culling, il picking e i gizmi.
///
/// Sta qui accanto a <see cref="WorldTransforms"/>, e non come metodo di
/// <see cref="CameraComponent"/>, per lo stesso motivo: la posa di un'entità dipende dal
/// <b>World</b> — con un <see cref="ParentComponent"/> il suo Transform è locale, e senza
/// attraversare la gerarchia una camera figlia del player finirebbe nell'origine. Un
/// <c>CameraComponent.Resolve(transform)</c> non avrebbe modo di saperlo, e la firma
/// stessa inviterebbe a passargli il transform sbagliato (quello locale).
///
/// La <see cref="Camera3D"/> resta intatta: nessuna di queste conversioni riscrive la sua
/// matematica, che è verificata numericamente. Qui si <b>deriva</b> soltanto la posa.
/// </summary>
public static class WorldCameras
{
    /// <summary>
    /// La camera risolta dell'entità, o <c>null</c> se non è una camera (le serve sia il
    /// <see cref="CameraComponent"/> sia un <see cref="TransformComponent"/>: senza posa
    /// non c'è niente da cui guardare).
    ///
    /// ⚠️ La conversione attraversa due convenzioni diverse. Il Transform dà
    /// <c>Position + Rotation</c>, la <see cref="Camera3D"/> vuole
    /// <c>Position + Target + Up</c>: il ponte è <c>Target = Position + Forward</c>, con la
    /// convenzione già fissata dal progetto (<c>Forward = +UnitZ</c>, vedi
    /// <c>TransformExtensions.GetForward</c>).
    ///
    /// Assi presi dalla world matrix con <see cref="Vector3.TransformNormal"/> e
    /// normalizzati, invece che dalla sola <c>Rotation</c> locale: così la gerarchia entra
    /// nel conto e una <see cref="TransformComponent.Scale"/> non unitaria — che su una
    /// camera non vuol dire niente, ma nessuno vieta di scriverla — non allunga gli assi.
    ///
    /// <c>Up</c> è quello dell'entità e non un <c>+Y</c> fisso: è ciò che rende
    /// rappresentabile il <b>roll</b>. Per una camera dritta i due coincidono, e comunque
    /// <c>CreateLookAt</c> riortogonalizza — passare +Y darebbe la stessa view matrix ma
    /// butterebbe via l'unico dato che il quaternione ha in più.
    /// </summary>
    public static Camera3D? GetCamera(this World world, Entity entity)
    {
        if (!world.TryGetComponent<CameraComponent>(entity, out var camera) ||
            !world.HasComponent<TransformComponent>(entity))
            return null;

        var matrix = world.GetWorldMatrix(entity);

        var position = matrix.Translation;
        var forward = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, matrix));
        var up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, matrix));

        return new Camera3D
        {
            Position = position,
            Target = position + forward,
            Up = up,
            FovY = camera.FovY,
            Near = camera.Near,
            Far = camera.Far,
            Projection = camera.Projection
        };
    }

    /// <summary>
    /// La camera con cui si disegna il gioco: la prima marcata
    /// <see cref="CameraComponent.Primary"/>, altrimenti la prima camera qualunque.
    ///
    /// ⚠️ Restituisce <c>null</c> quando nel World non c'è nessuna camera, e chi chiama
    /// deve reggerlo. Da quando l'editor sa eliminare entità, "esiste sempre una camera"
    /// <b>non è più un invariante</b>: è la stessa lezione del player cancellato che faceva
    /// cadere l'HUD.
    ///
    /// Il fallback sulla "prima qualunque" evita che dimenticare <c>Primary: true</c>
    /// significhi schermo nero — con una camera sola la domanda non è ambigua.
    ///
    /// ⚠️ Si chiama <c>Get…</c> e non <c>TryGet…</c> apposta: in C# <c>TryGet</c> promette
    /// <c>bool</c> + parametro <c>out</c> (è la forma di <see cref="World.TryGetComponent"/>
    /// qui accanto), mentre qui l'assenza si dice col nullable. Il nome sbagliato aveva già
    /// prodotto una firma inventata nella documentazione: se l'API mente sulla sua forma, chi
    /// la scrive a memoria sbaglia — ed è il caso di chi legge i doc invece del sorgente.
    /// </summary>
    public static Camera3D? GetPrimaryCamera(this World world)
    {
        Entity? fallback = null;

        foreach (var (entity, _, camera) in world.Query<TransformComponent, CameraComponent>())
        {
            if (camera.Primary)
                return world.GetCamera(entity);

            fallback ??= entity;
        }

        return fallback is { } first ? world.GetCamera(first) : null;
    }
}
