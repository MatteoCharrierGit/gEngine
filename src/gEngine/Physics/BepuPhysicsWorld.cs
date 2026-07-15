using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

namespace gEngine.Physics;

/// <summary>
/// Adapter BepuPhysics v2 di <see cref="IPhysicsWorld"/>: l'UNICO file che importa
/// <c>BepuPhysics</c>. Mappa i <see cref="BodyId"/> opachi agli handle nativi di Bepu.
///
/// ⚠️ Le firme dei callback (<see cref="NarrowPhaseCallbacks"/>,
/// <see cref="PoseIntegratorCallbacks"/>) e alcune API di <c>Simulation</c> dipendono
/// dalla versione esatta del pacchetto (qui: BepuPhysics 2.4.0). Se dopo il restore il
/// compilatore segnala una firma diversa, è qui che va allineata.
/// </summary>
public class BepuPhysicsWorld : IPhysicsWorld
{
    private readonly BufferPool _bufferPool;
    private readonly Simulation _simulation;

    private readonly Dictionary<int, BodyHandle> _dynamicBodies = new();
    private readonly Dictionary<int, StaticHandle> _staticBodies = new();
    private int _nextId = 1; // 0 = BodyId.None

    public BepuPhysicsWorld(Vector3 gravity)
    {
        _bufferPool = new BufferPool();
        _simulation = Simulation.Create(
            _bufferPool,
            new NarrowPhaseCallbacks(),
            new PoseIntegratorCallbacks(gravity),
            new SolveDescription(velocityIterationCount: 8, substepCount: 1));
    }

    public BodyId AddBox(Vector3 position, Quaternion orientation, Vector3 size, float mass, bool isStatic)
    {
        var box = new Box(size.X, size.Y, size.Z);
        var shapeIndex = _simulation.Shapes.Add(box);
        return isStatic
            ? AddStatic(position, orientation, shapeIndex)
            : AddDynamic(position, orientation, shapeIndex, box.ComputeInertia(mass));
    }

    public BodyId AddSphere(Vector3 position, Quaternion orientation, float radius, float mass, bool isStatic)
    {
        var sphere = new Sphere(radius);
        var shapeIndex = _simulation.Shapes.Add(sphere);
        return isStatic
            ? AddStatic(position, orientation, shapeIndex)
            : AddDynamic(position, orientation, shapeIndex, sphere.ComputeInertia(mass));
    }

    private BodyId AddDynamic(Vector3 position, Quaternion orientation, TypedIndex shapeIndex, BodyInertia inertia)
    {
        var pose = new RigidPose(position, orientation);
        var handle = _simulation.Bodies.Add(
            BodyDescription.CreateDynamic(pose, inertia, new CollidableDescription(shapeIndex), new BodyActivityDescription(0.01f)));

        var id = _nextId++;
        _dynamicBodies[id] = handle;
        return new BodyId(id);
    }

    private BodyId AddStatic(Vector3 position, Quaternion orientation, TypedIndex shapeIndex)
    {
        var handle = _simulation.Statics.Add(new StaticDescription(new RigidPose(position, orientation), shapeIndex));

        var id = _nextId++;
        _staticBodies[id] = handle;
        return new BodyId(id);
    }

    public void Step(float dt)
    {
        _simulation.Timestep(dt);
    }

    public bool TryGetPose(BodyId id, out Vector3 position, out Quaternion orientation)
    {
        if (_dynamicBodies.TryGetValue(id.Id, out var handle))
        {
            var pose = _simulation.Bodies[handle].Pose;
            position = pose.Position;
            orientation = pose.Orientation;
            return true;
        }

        // Corpi statici (o handle non valido): niente sync necessario.
        position = default;
        orientation = default;
        return false;
    }

    public void Dispose()
    {
        _simulation.Dispose();
        _bufferPool.Clear();
    }
}

/// <summary>
/// Callback della narrow phase: consente tutte le collisioni che coinvolgono almeno un
/// corpo dinamico e assegna un materiale di contatto standard (attrito + molla di
/// risoluzione). Versione minimale dai demo di Bepu.
/// </summary>
internal struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public void Initialize(Simulation simulation)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        // Almeno uno dei due deve essere dinamico (statico-statico non genera contatti).
        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = 1f;
        pairMaterial.MaximumRecoveryVelocity = 2f;
        pairMaterial.SpringSettings = new SpringSettings(30, 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        return true;
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Callback dell'integratore di posa: applica la gravità alla velocità dei corpi a ogni
/// substep. Usa il layout SIMD "wide" di Bepu (più corpi per bundle).
/// </summary>
internal struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public Vector3 Gravity;
    private Vector3Wide _gravityWideDt;

    public PoseIntegratorCallbacks(Vector3 gravity) : this()
    {
        Gravity = gravity;
    }

    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public readonly bool AllowSubstepsForUnconstrainedBodies => false;
    public readonly bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
        // Pre-moltiplica gravità * dt una volta per bundle.
        _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
    }

    public void IntegrateVelocity(
        Vector<int> bodyIndices,
        Vector3Wide position,
        QuaternionWide orientation,
        BodyInertiaWide localInertia,
        Vector<int> integrationMask,
        int workerIndex,
        Vector<float> dt,
        ref BodyVelocityWide velocity)
    {
        velocity.Linear += _gravityWideDt;
    }
}
