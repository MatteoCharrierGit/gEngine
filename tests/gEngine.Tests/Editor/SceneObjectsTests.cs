using gEngine.Core;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Editor;
using gEngine.Rendering;
using gEngine.Scenes;

namespace gEngine.Tests.Editor;

/// <summary>
/// Il catalogo "crea oggetto": cubo, sfera, luce, camera, vuoto.
///
/// Quel che c'è da verificare non è che i componenti si aggiungano — quello lo fa il World —
/// ma che l'oggetto nasca <b>come lo si è chiesto</b> e <b>annullabile</b>. Sono le due cose
/// che un menu può promettere e non mantenere senza che si veda.
/// </summary>
public class SceneObjectsTests
{
    [Fact]
    public void UnCubo_NasceConIlMeshRenderer()
    {
        var (world, context) = Setup();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Cube));

        Assert.NotNull(entity);
        Assert.True(world.HasComponent<TransformComponent>(entity.Value));
        Assert.True(world.TryGetComponent<MeshRendererComponent>(entity.Value, out var mesh));
        Assert.Equal(MeshKind.Cube, mesh.Kind);

        // Il default della Fase 4.7 dice "visibile", ed e' il motivo per cui aggiungere il
        // componente si vede. Se un giorno cambiasse, il cubo del menu diventerebbe invisibile
        // e sembrerebbe un bug del menu.
        Assert.True(mesh.Visible);
    }

    [Fact]
    public void UnaSfera_NasceConMeshKindSphere()
    {
        var (world, context) = Setup();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Sphere));

        Assert.True(world.TryGetComponent<MeshRendererComponent>(entity!.Value, out var mesh));
        Assert.Equal(MeshKind.Sphere, mesh.Kind);
    }

    /// <summary>
    /// ⚠️⚠️ Il test che protegge dal gotcha più caro di questo repo.
    /// <c>MeshRendererComponent</c> è l'<b>unica class</b> fra i componenti: se
    /// <c>TryCreateDefault</c> restituisse un'istanza condivisa invece di costruirne una nuova,
    /// creare una sfera trasformerebbe in sfera <b>anche tutti i cubi già in scena</b>, e la
    /// modifica arriverebbe senza che nessuno l'abbia chiesta.
    /// </summary>
    [Fact]
    public void CreareUnaSfera_NonTrasformaInSferaICubiGiaInScena()
    {
        var (world, context) = Setup();

        var cubo = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Cube))!.Value;
        SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Sphere));

        Assert.True(world.TryGetComponent<MeshRendererComponent>(cubo, out var mesh));
        Assert.Equal(MeshKind.Cube, mesh.Kind);
    }

    [Fact]
    public void UnaLuce_NasceIlluminando()
    {
        var (world, context) = Setup();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Light));

        Assert.True(world.TryGetComponent<LightComponent>(entity!.Value, out var light));
        Assert.True(light.Intensity > 0f, "Una luce a intensita' 0 sembrerebbe un menu rotto.");
    }

    /// <summary>
    /// ⚠️ La camera nuova NON è primaria, ed è una scelta dichiarata nel registry: rubare
    /// l'inquadratura a quella che sta già guardando il giocatore sarebbe l'effetto più
    /// spettacolare e meno voluto di tutto il menu.
    /// </summary>
    [Fact]
    public void UnaCamera_NasceNonPrimaria()
    {
        var (world, context) = Setup();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Camera));

        Assert.True(world.TryGetComponent<CameraComponent>(entity!.Value, out var camera));
        Assert.False(camera.Primary);
    }

    [Fact]
    public void UnVuoto_HaSoloIlTransformEIlNome()
    {
        var (world, context) = Setup();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Empty))!.Value;

        Assert.True(world.HasComponent<TransformComponent>(entity));
        Assert.True(world.HasComponent<NameComponent>(entity));
        Assert.False(world.HasComponent<MeshRendererComponent>(entity));
        Assert.False(world.HasComponent<LightComponent>(entity));
    }

    /// <summary>
    /// I nomi si rendono liberi: due entità omonime collassano nella mappa nome→Entity al
    /// reload della scena — vince l'ultima, senza un errore. Con un menu che crea cubi a
    /// ripetizione il caso arriva al secondo clic.
    /// </summary>
    [Fact]
    public void DueCubi_NonSiChiamanoUguale()
    {
        var (world, context) = Setup();

        var primo = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Cube))!.Value;
        var secondo = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Cube))!.Value;

        world.TryGetComponent<NameComponent>(primo, out var nomePrimo);
        world.TryGetComponent<NameComponent>(secondo, out var nomeSecondo);

        Assert.Equal("Cubo", nomePrimo.Value);
        Assert.NotEqual(nomePrimo.Value, nomeSecondo.Value);
    }

    [Fact]
    public void CreatoSottoUnGenitore_NeDiventaFiglio()
    {
        var (world, context) = Setup();
        var genitore = EntityOperations.Create(world);

        var figlio = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Cube), genitore)!.Value;

        Assert.True(world.TryGetComponent<ParentComponent>(figlio, out var parent));
        Assert.Equal(genitore, parent.Parent);
    }

    /// <summary>
    /// Creare è un'azione che si fa per sbaglio con un clic: senza Ctrl+Z l'unico rimedio
    /// sarebbe cercare l'entità nuova nella Hierarchy ed eliminarla a mano.
    /// </summary>
    [Fact]
    public void CreareEAnnullabile()
    {
        var (world, context) = Setup();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Cube))!.Value;
        Assert.True(context.Undo.CanUndo);

        context.Undo.Undo(world);

        Assert.False(world.Exists(entity));
    }

    // ------ SENZA IL REGISTRY DEL GIOCO ---------------------------------------------------

    /// <summary>
    /// Il registry lo dichiara il gioco e può mancare. In quel caso l'editor non sa con quali
    /// valori nasce un MeshRenderer, e <b>non se li inventa</b>.
    /// </summary>
    [Theory]
    [InlineData(SceneObjects.Kind.Cube)]
    [InlineData(SceneObjects.Kind.Sphere)]
    [InlineData(SceneObjects.Kind.Light)]
    [InlineData(SceneObjects.Kind.Camera)]
    public void SenzaRegistry_GliOggettiCheVoglionoUnDefaultNonSiCreano(SceneObjects.Kind kind)
    {
        var world = new World();
        var context = new EditorContext(); // niente Resources, quindi niente registry

        Assert.Null(SceneObjects.Create(world, context, Entry(kind)));
        Assert.Empty(world.AllEntities);
        Assert.False(context.Undo.CanUndo);
    }

    [Fact]
    public void SenzaRegistry_IlVuotoSiCreaLoStesso()
    {
        var world = new World();
        var context = new EditorContext();

        var entity = SceneObjects.Create(world, context, Entry(SceneObjects.Kind.Empty));

        Assert.NotNull(entity);
        Assert.True(world.HasComponent<TransformComponent>(entity.Value));
    }

    /// <summary>
    /// Il catalogo e la sua dichiarazione devono restare d'accordo: se un domani si aggiungesse
    /// un tipo senza segnarlo, la voce nascerebbe abilitata anche senza registry e produrrebbe
    /// un'entità monca invece di dire perché non si può.
    /// </summary>
    [Fact]
    public void SoloIlVuoto_DichiaraDiNonAverBisognoDelRegistry()
    {
        var senzaRegistry = SceneObjects.All.Where(entry => !entry.NeedsRegistry).ToList();

        Assert.Equal(SceneObjects.Kind.Empty, Assert.Single(senzaRegistry).Kind);
    }

    /// <summary>Le etichette e i tooltip finiscono sotto ImGui: solo Latin-1.</summary>
    [Fact]
    public void EtichetteETooltip_StannoDentroLatin1()
    {
        Assert.All(SceneObjects.All, entry =>
        {
            Assert.DoesNotContain(entry.Label, c => c > 0xFF);
            Assert.DoesNotContain(entry.Tooltip, c => c > 0xFF);
        });
    }

    // ------ FIXTURE -----------------------------------------------------------------------

    private static SceneObjects.Entry Entry(SceneObjects.Kind kind) =>
        SceneObjects.All.Single(entry => entry.Kind == kind);

    private static (World World, EditorContext Context) Setup()
    {
        var registry = new SceneComponentRegistry();
        registry.RegisterEngineDefaults();

        var resources = new Resources();
        resources.Add<SceneComponentRegistry>(registry);

        return (new World(), new EditorContext { Resources = resources });
    }
}
