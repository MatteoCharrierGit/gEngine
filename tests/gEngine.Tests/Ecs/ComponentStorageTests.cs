using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces;
using gEngine.Editor;
using gEngine.Rendering;

namespace gEngine.Tests.Ecs;

/// <summary>
/// ⚠️⚠️ <b>Il gotcha struct/copia, finalmente sotto test.</b> È aperto dalla Fase 0, ha morso
/// cinque volte (<c>MovementSystem</c>, l'Inspector, il gizmo, e altre due), ed è la voce col
/// rapporto costo/danno peggiore di tutto il repo: sbagliarlo non dà un errore, dà una modifica
/// che sembra applicata e non è stata salvata.
///
/// Qui si verifica il meccanismo <b>esatto</b> su cui si regge l'Inspector — <c>GetBoxed</c>,
/// muta la scatola, <c>SetBoxed</c> — perché quello è codice dentro un <c>DrawContent</c> e non
/// si può chiamare senza ImGui. Testare lo strato sotto è il modo di coprirlo lo stesso.
/// </summary>
public class ComponentStorageTests
{
    /// <summary>
    /// La metà che ha morso cinque volte: la copia si muta e <b>lo storage non se ne accorge</b>.
    /// </summary>
    [Fact]
    public void GetBoxed_DaUnaCopia_MutarlaNonToccaLoStorage()
    {
        var (world, entity) = MondoConCubo();
        var storage = StorageDi<MeshRendererComponent>(world);

        var boxed = (MeshRendererComponent)storage.GetBoxed(entity.Id)!;
        boxed.Kind = MeshKind.Sphere;

        Assert.Equal(MeshKind.Cube, world.GetComponent<MeshRendererComponent>(entity).Kind);
    }

    /// <summary>E l'altra metà: col write-back la modifica arriva. È la riga dell'Inspector.</summary>
    [Fact]
    public void SetBoxed_RiscriveLaCopiaMutata()
    {
        var (world, entity) = MondoConCubo();
        var storage = StorageDi<MeshRendererComponent>(world);

        var boxed = (MeshRendererComponent)storage.GetBoxed(entity.Id)!;
        boxed.Kind = MeshKind.Sphere;
        storage.SetBoxed(entity.Id, boxed);

        Assert.Equal(MeshKind.Sphere, world.GetComponent<MeshRendererComponent>(entity).Kind);
    }

    /// <summary>
    /// Lo stesso giro fatto <b>per reflection</b>, che è come lo fa davvero l'Inspector: non
    /// conosce i tipi a compile time e scrive i campi con <c>FieldInfo.SetValue</c> sulla
    /// scatola. ⚠️ Conta che sia un test a sé: <c>SetValue</c> su uno struct <i>boxato</i> muta
    /// la scatola, ma la stessa chiamata su un valore non boxato si perderebbe nel nulla — ed è
    /// la differenza che rende l'Inspector corretto.
    /// </summary>
    [Fact]
    public void PerReflection_IlGiroDellInspectorFunziona()
    {
        var (world, entity) = MondoConCubo();
        var storage = StorageDi<MeshRendererComponent>(world);

        var boxed = storage.GetBoxed(entity.Id)!;
        typeof(MeshRendererComponent)
            .GetField(nameof(MeshRendererComponent.Wireframe))!
            .SetValue(boxed, true);
        storage.SetBoxed(entity.Id, boxed);

        Assert.True(world.GetComponent<MeshRendererComponent>(entity).Wireframe);
    }

    /// <summary>
    /// Vale anche per il <c>TransformComponent</c>, che è il componente su cui il gotcha ha
    /// morso più volte (il write-back parziale di <c>MovementSystem</c> azzerava
    /// <c>Scale</c>/<c>Rotation</c>).
    ///
    /// ⚠️ E il write-back va fatto <b>intero</b>: si riscrive il componente che si è letto e
    /// mutato, non uno costruito da zero coi soli campi che interessano.
    /// </summary>
    [Fact]
    public void WriteBack_DelTransform_NonPerdeGliAltriCampi()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TransformComponent
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.CreateFromYawPitchRoll(1f, 0f, 0f),
            Scale = new Vector3(2f, 2f, 2f)
        });

        var transform = world.GetComponent<TransformComponent>(entity);
        transform.Position = new Vector3(5f, 0f, 0f);
        world.AddComponent(entity, transform);

        var riletto = world.GetComponent<TransformComponent>(entity);
        Assert.Equal(new Vector3(5f, 0f, 0f), riletto.Position);
        Assert.Equal(new Vector3(2f, 2f, 2f), riletto.Scale);
        Assert.NotEqual(Quaternion.Identity, riletto.Rotation);
    }

    /// <summary>
    /// ⚠️ Il test di regressione del bug della Fase 4.8: duplicando un'entità, originale e copia
    /// condividevano lo <b>stesso</b> <c>MeshRendererComponent</c> — dipingere di rosso la copia
    /// dipingeva anche l'originale.
    ///
    /// Oggi non può più succedere (quel componente è uno struct), ma il test resta: è la prova
    /// che <c>Duplicate</c> produce entità <b>indipendenti</b>, che è la sua ragione di esistere
    /// e non dipende da come sono fatti i componenti di oggi.
    /// </summary>
    [Fact]
    public void Duplicate_DaEntitaIndipendenti()
    {
        var (world, originale) = MondoConCubo();

        var copia = EntityOperations.Duplicate(world, originale);

        var mesh = world.GetComponent<MeshRendererComponent>(copia);
        mesh.Tint = Color.Red;
        world.AddComponent(copia, mesh);

        Assert.Equal(Color.White, world.GetComponent<MeshRendererComponent>(originale).Tint);
        Assert.Equal(Color.Red, world.GetComponent<MeshRendererComponent>(copia).Tint);
    }

    private static (World World, Entity Entity) MondoConCubo()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.AddComponent(entity, new TransformComponent
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        world.AddComponent(entity, new MeshRendererComponent
        {
            Kind = MeshKind.Cube,
            Size = Vector3.One,
            Tint = Color.White,
            Visible = true
        });

        return (world, entity);
    }

    private static IComponentStorage StorageDi<T>(World world) =>
        world.ComponentStorages.Single(storage => storage.ComponentType == typeof(T));
}
