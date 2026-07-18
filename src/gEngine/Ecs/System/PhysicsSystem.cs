using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Physics;

namespace gEngine.Ecs.System;

/// <summary>
/// Ponte ECS ⇄ mondo fisico. Ogni update: rimuove i corpi rimasti orfani, crea quelli
/// mancanti, avanza la simulazione di un passo fisso, poi riscrive nel
/// <see cref="TransformComponent"/> la posa dei corpi dinamici. Gira come
/// <see cref="ISimulationSystem"/> nel fixed-step del GameLoop.
/// </summary>
public class PhysicsSystem : ISimulationSystem
{
    // Il RigidBody è ciò che decide su chi agisce: il PhysicsBodyComponent lo mette questo
    // system (è [RuntimeState]), quindi dichiararlo direbbe "agisco su chi ho già toccato".
    public IReadOnlyList<Type> MatchedComponents { get; } =
        [typeof(TransformComponent), typeof(RigidBodyComponent)];

    private readonly IPhysicsWorld _physics;

    // Materializza le entità da registrare PRIMA di modificare il world (non si aggiungono
    // componenti mentre si itera la stessa query).
    private readonly List<(Entity Entity, TransformComponent Transform, RigidBodyComponent Body)> _pending = new();

    // I corpi creati da questo system, per entità. Serve una mappa NOSTRA e non basta il
    // PhysicsBodyComponent: quando l'entità viene distrutta il componente sparisce con
    // lei, e con esso l'unico riferimento al corpo Bepu — che resterebbe a simulare per
    // sempre. Questa mappa è ciò che sopravvive alla distruzione e permette di accorgersene.
    private readonly Dictionary<int, BodyId> _bodiesByEntity = new();
    private readonly List<int> _orphaned = new();

    public PhysicsSystem(IPhysicsWorld physics)
    {
        _physics = physics;
    }

    public void OnCreate(World world)
    {
    }

    /// <summary>
    /// Il system esce di scena: i corpi che ha creato escono con lui.
    ///
    /// Senza questo, togliere il PhysicsSystem dal pannello lasciava i corpi a simulare in
    /// Bepu — e non "semplicemente non sincronizzati", come diceva il tooltip: la mappa
    /// <see cref="_bodiesByEntity"/> è privata di questa istanza, quindi con il system fuori
    /// dal registry quei corpi non erano più <b>raggiungibili da nessuno</b>. Continuavano a
    /// collidere contro un mondo che non li vede.
    ///
    /// Si toglie anche il <see cref="PhysicsBodyComponent"/> dalle entità vive, per lo stesso
    /// motivo per cui lo fa <see cref="RemoveOrphanedBodies"/>: è il link a un corpo che non
    /// esiste più, e lasciandolo lì un system rimesso in seguito lo leggerebbe come "il corpo
    /// c'è già" e non ne ricreerebbe nessuno. L'entità avrebbe un RigidBody e non cadrebbe.
    ///
    /// ⚠️ <b>Non</b> si tocca <see cref="_physics"/>: <c>IPhysicsWorld</c> è
    /// <c>IDisposable</c>, ma è una Resource che il <b>gioco</b> possiede e passa al
    /// costruttore. Disporlo qui vorrebbe dire che togliere un system dal pannello per
    /// curiosità distrugge il mondo fisico di tutti — e "Ripristina" restituirebbe un system
    /// agganciato a un oggetto morto. Si libera ciò che si è creato, non ciò che si è ricevuto.
    /// </summary>
    public void OnDestroy(World world)
    {
        foreach (var (entityId, body) in _bodiesByEntity)
        {
            _physics.RemoveBody(body);

            var entity = new Entity(entityId);
            if (world.Exists(entity))
                world.RemoveComponent<PhysicsBodyComponent>(entity);
        }

        // Simmetrico a OnCreate: la stessa istanza può essere rimessa dal pannello, e deve
        // ripartire senza ricordarsi di corpi che ha appena tolto.
        _bodiesByEntity.Clear();
        _pending.Clear();
        _orphaned.Clear();
    }

    public void OnUpdate(World world, float dt)
    {
        // 0) Rimuovi i corpi rimasti orfani.
        RemoveOrphanedBodies(world);

        // 1) Registra i corpi per le entità con RigidBody ma ancora senza corpo fisico.
        _pending.Clear();
        foreach (var (entity, transform, body) in world.Query<TransformComponent, RigidBodyComponent>())
        {
            if (!world.HasComponent<PhysicsBodyComponent>(entity))
                _pending.Add((entity, transform, body));
        }

        foreach (var (entity, transform, body) in _pending)
        {
            var id = body.Shape == ColliderShape.Sphere
                ? _physics.AddSphere(transform.Position, transform.Rotation, body.Size.X, body.Mass, body.IsStatic)
                : _physics.AddBox(transform.Position, transform.Rotation, body.Size, body.Mass, body.IsStatic);

            world.AddComponent(entity, new PhysicsBodyComponent { Body = id });
            _bodiesByEntity[entity.Id] = id;
        }

        // 2) Avanza la simulazione di un passo.
        _physics.Step(dt);

        // 3) Sync fisica → Transform (solo i dinamici restituiscono una posa).
        foreach (var (entity, _, physicsBody) in world.Query<TransformComponent, PhysicsBodyComponent>())
        {
            if (!_physics.TryGetPose(physicsBody.Body, out var position, out var orientation))
                continue;

            var transform = world.GetComponent<TransformComponent>(entity);
            transform.Position = position;
            transform.Rotation = orientation;
            world.AddComponent(entity, transform); // write-back (TransformComponent è struct)
        }
    }

    /// <summary>
    /// Toglie dalla simulazione i corpi che non hanno più un motivo di esistere. Due casi,
    /// entrambi resi possibili dall'editor:
    /// <list type="bullet">
    ///   <item>l'<b>entità è stata distrutta</b> — il suo <c>PhysicsBodyComponent</c> è
    ///   sparito con lei, quindi nessuna query può più trovare il corpo: senza questo
    ///   passaggio resterebbe a simulare e collidere da fantasma;</item>
    ///   <item>il <b>RigidBody è stato rimosso</b> ma l'entità vive — "non è più un corpo
    ///   fisico" deve valere anche per Bepu, non solo per l'ECS.</item>
    /// </list>
    /// È una riconciliazione a polling invece che un evento <c>OnEntityDestroyed</c> sul
    /// World: l'ECS resta ignaro della fisica (e di qualunque risorsa esterna), e il costo
    /// è una scansione dei soli corpi vivi, non di tutto il World.
    /// </summary>
    private void RemoveOrphanedBodies(World world)
    {
        _orphaned.Clear();

        foreach (var (entityId, _) in _bodiesByEntity)
        {
            var entity = new Entity(entityId);

            if (world.Exists(entity) && world.HasComponent<RigidBodyComponent>(entity))
                continue;

            _orphaned.Add(entityId);
        }

        // Fuori dal ciclo: stiamo modificando la stessa mappa che scorrevamo.
        foreach (var entityId in _orphaned)
        {
            _physics.RemoveBody(_bodiesByEntity[entityId]);
            _bodiesByEntity.Remove(entityId);

            // Caso "RigidBody rimosso": l'entità vive ancora e si porterebbe dietro un
            // PhysicsBodyComponent che punta a un corpo ormai morto. Va tolto anche quello,
            // altrimenti rimettere un RigidBody in seguito non ricreerebbe nulla — il
            // system leggerebbe il link stantio come "il corpo esiste già".
            var entity = new Entity(entityId);
            if (world.Exists(entity))
                world.RemoveComponent<PhysicsBodyComponent>(entity);
        }
    }
}
