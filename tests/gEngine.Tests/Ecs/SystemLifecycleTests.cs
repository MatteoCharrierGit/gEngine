using System.Numerics;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Ecs.System;
using gEngine.Log;
using gEngine.Physics;

namespace gEngine.Tests.Ecs;

/// <summary>
/// Il ciclo di vita di un system: <c>OnCreate</c> entrando, <c>OnDestroy</c> uscendo.
///
/// Era un debito teorico finché nessuno toglieva system. Il pannello Systems li fa togliere
/// <b>col mouse</b>, e quello ha reso raggiungibile un buco che prima non lo era: il
/// <c>PhysicsSystem</c> tiene una mappa privata entità→corpo Bepu, quindi con l'istanza
/// fuori dal registry quei corpi non erano più raggiungibili da nessuno.
/// </summary>
public class SystemLifecycleTests
{
    [Fact]
    public void Registrare_ChiamaOnCreate()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SpySystem();

        registry.Add(system);

        Assert.Equal(1, system.Created);
        Assert.Equal(0, system.Destroyed);
    }

    [Fact]
    public void Togliere_ChiamaOnDestroy()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SpySystem();
        registry.Add(system);

        Assert.True(registry.Remove(system));

        Assert.Equal(1, system.Destroyed);
    }

    /// <summary>
    /// ⚠️ Distruggere due volte è peggio che non distruggere: chi scrive <c>OnDestroy</c>
    /// assume di essere in pari con un <c>OnCreate</c> che qui non c'è mai stato.
    /// </summary>
    [Fact]
    public void ToglierloDueVolte_ChiamaOnDestroyUnaVoltaSola()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SpySystem();
        registry.Add(system);

        Assert.True(registry.Remove(system));
        Assert.False(registry.Remove(system));

        Assert.Equal(1, system.Destroyed);
    }

    [Fact]
    public void UnSystemMaiRegistrato_NonRiceveOnDestroy()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SpySystem();

        Assert.False(registry.Remove(system));

        Assert.Equal(0, system.Destroyed);
    }

    /// <summary>
    /// Togliere e rimettere dal pannello: i due contatori devono restare in pari. È la
    /// sequenza che il pannello Systems produce col mouse, ed è il motivo per cui
    /// <c>OnDestroy</c> va scritto simmetrico a <c>OnCreate</c>.
    /// </summary>
    [Fact]
    public void ToltoERimesso_ICreateEIDestroyRestanoInPari()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SpySystem();

        registry.Add(system);
        registry.Remove(system);
        registry.Add(system);

        Assert.Equal(2, system.Created);
        Assert.Equal(1, system.Destroyed);
    }

    [Fact]
    public void TogliendoPerTipo_OgniIstanzaRiceveIlSuoOnDestroy()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var primo = new SpySystem();
        var secondo = new SpySystem();
        registry.Add(primo);
        registry.Add(secondo);

        Assert.Equal(2, registry.Remove<SpySystem>());

        Assert.Equal(1, primo.Destroyed);
        Assert.Equal(1, secondo.Destroyed);
    }

    /// <summary>
    /// ⚠️ Il system smontato non deve poter ricevere un altro <c>OnUpdate</c>: il registry
    /// lo sfila dalle fasi <b>prima</b> di chiamare <c>OnDestroy</c>.
    /// </summary>
    [Fact]
    public void DopoOnDestroy_IlSystemNonGiraPiu()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SpySystem();
        registry.Add(system);

        registry.RunSimulation(0.016f);
        var giriPrima = system.Updates;

        registry.Remove(system);
        registry.RunSimulation(0.016f);

        Assert.Equal(1, giriPrima);
        Assert.Equal(giriPrima, system.Updates);
    }

    /// <summary>
    /// Un system che <b>non</b> dichiara <c>OnDestroy</c> deve continuare a compilare e a
    /// funzionare: il default interface member è vuoto apposta, perché la stragrande
    /// maggioranza dei system non possiede niente di esterno.
    /// </summary>
    [Fact]
    public void UnSystemSenzaOnDestroy_SiToglieLoStesso()
    {
        var world = new World();
        var registry = new SystemRegistry(world);
        var system = new SystemSenzaOnDestroy();
        registry.Add(system);

        Assert.True(registry.Remove(system));
        Assert.Empty(registry.Systems);
    }

    // ------ IL CASO VERO: IL PHYSICSSYSTEM ------------------------------------------------

    /// <summary>
    /// Il buco descritto nell'handoff, misurato: togliere il PhysicsSystem lasciava i corpi
    /// nel mondo Bepu. Adesso escono con lui.
    /// </summary>
    [Fact]
    public void TogliereIlPhysicsSystem_ToglieIComponentiCheHaCreato()
    {
        var physics = new FakePhysicsWorld();
        var (world, registry, system) = MondoConDueCorpi(physics);

        // Precondizione: i corpi ci sono davvero. Senza, il test passerebbe anche con un
        // fixture vuoto — cioè non verificherebbe niente.
        Assert.Equal(2, physics.AliveBodies);

        registry.Remove(system);

        Assert.Equal(0, physics.AliveBodies);
    }

    /// <summary>
    /// Il link stantio va via con il corpo. Se restasse, un PhysicsSystem rimesso in seguito
    /// leggerebbe <c>PhysicsBodyComponent</c> come "il corpo c'è già" e non ne ricreerebbe
    /// nessuno: l'entità avrebbe un RigidBody e non cadrebbe.
    /// </summary>
    [Fact]
    public void TogliereIlPhysicsSystem_NonLasciaLinkAUnCorpoMorto()
    {
        var physics = new FakePhysicsWorld();
        var (world, registry, system) = MondoConDueCorpi(physics);

        registry.Remove(system);

        Assert.All(world.AllEntities,
            entity => Assert.False(world.HasComponent<PhysicsBodyComponent>(entity)));
    }

    /// <summary>
    /// ⚠️ Il test che protegge la riga più facile da sbagliare. <c>IPhysicsWorld</c> è
    /// <c>IDisposable</c>, quindi disporlo in <c>OnDestroy</c> sembra la cosa ordinata da
    /// fare — e sarebbe un disastro: è una <b>Resource del gioco</b>, passata al costruttore.
    /// Toglierlo dal pannello per curiosità distruggerebbe il mondo fisico di tutti, e
    /// "Ripristina" restituirebbe un system agganciato a un oggetto morto.
    /// </summary>
    [Fact]
    public void TogliereIlPhysicsSystem_NonDistruggeIlMondoFisico_CheNonEDiSuaProprieta()
    {
        var physics = new FakePhysicsWorld();
        var (_, registry, system) = MondoConDueCorpi(physics);

        registry.Remove(system);

        Assert.False(physics.Disposed);
    }

    /// <summary>
    /// Il giro completo del pannello: togli, rimetti, e i corpi tornano. È ciò che rende
    /// <c>OnDestroy</c> una pulizia e non una mutilazione.
    /// </summary>
    [Fact]
    public void RimessoDopoEsserStatoTolto_IlPhysicsSystemRicreaISuoiCorpi()
    {
        var physics = new FakePhysicsWorld();
        var (world, registry, system) = MondoConDueCorpi(physics);

        registry.Remove(system);
        Assert.Equal(0, physics.AliveBodies);

        registry.Add(system);
        registry.RunSimulation(0.016f);

        Assert.Equal(2, physics.AliveBodies);
        Assert.All(world.AllEntities,
            entity => Assert.True(world.HasComponent<PhysicsBodyComponent>(entity)));
    }

    // ------ FIXTURE E DOPPI --------------------------------------------------------------

    /// <summary>Due entità con RigidBody, un giro di simulazione: i corpi esistono.</summary>
    private static (World World, SystemRegistry Registry, PhysicsSystem System) MondoConDueCorpi(
        IPhysicsWorld physics)
    {
        var world = new World();
        var registry = new SystemRegistry(world);

        for (var i = 0; i < 2; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent(entity, new TransformComponent
            {
                Position = new Vector3(0f, i * 2f, 0f),
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            });
            world.AddComponent(entity, new RigidBodyComponent
            {
                Shape = ColliderShape.Box,
                Size = Vector3.One,
                Mass = 1f,
                IsStatic = false
            });
        }

        var system = new PhysicsSystem(physics, new Logger());
        registry.Add(system);
        registry.RunSimulation(0.016f);

        return (world, registry, system);
    }

    private sealed class SpySystem : ISimulationSystem
    {
        public int Created { get; private set; }
        public int Destroyed { get; private set; }
        public int Updates { get; private set; }

        public void OnCreate(World world) => Created++;
        public void OnUpdate(World world, float dt) => Updates++;
        public void OnDestroy(World world) => Destroyed++;
    }

    /// <summary>Il caso "system scritto fuori dall'engine": non sa che OnDestroy esiste.</summary>
    private sealed class SystemSenzaOnDestroy : ISimulationSystem
    {
        public void OnCreate(World world) { }
        public void OnUpdate(World world, float dt) { }
    }
}
