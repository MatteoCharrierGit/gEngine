using System.Numerics;
using gEngine.Physics;

namespace gEngine.Tests.Ecs;

/// <summary>
/// Mondo fisico finto: tiene solo il <b>conto</b> dei corpi vivi, che è l'unica cosa che
/// serve per verificare chi li libera e chi no. Con Bepu vero servirebbe una simulazione
/// intera per rispondere a una domanda di ciclo di vita.
/// </summary>
internal sealed class FakePhysicsWorld : IPhysicsWorld
{
    private readonly HashSet<int> _alive = [];
    private int _nextId;

    /// <summary>I corpi ancora nella simulazione. È la misura del leak.</summary>
    public int AliveBodies => _alive.Count;

    /// <summary>
    /// Se qualcuno ha chiamato <see cref="Dispose"/>. Serve al test che pretende che
    /// <c>OnDestroy</c> <b>non</b> lo faccia: il mondo fisico è una Resource del gioco, non
    /// roba del system.
    /// </summary>
    public bool Disposed { get; private set; }

    public BodyId AddBox(Vector3 position, Quaternion orientation, Vector3 size, float mass, bool isStatic) =>
        Add();

    public BodyId AddSphere(Vector3 position, Quaternion orientation, float radius, float mass, bool isStatic) =>
        Add();

    private BodyId Add()
    {
        _alive.Add(++_nextId);
        return new BodyId(_nextId);
    }

    public void Step(float dt) { }

    // I test sul ciclo di vita non guardano le pose: restituire false tiene il
    // PhysicsSystem fuori dal ramo di sync, che qui non è quel che si sta verificando.
    public bool TryGetPose(BodyId id, out Vector3 position, out Quaternion orientation)
    {
        position = Vector3.Zero;
        orientation = Quaternion.Identity;
        return false;
    }

    public void RemoveBody(BodyId id) => _alive.Remove(id.Id);

    public void Dispose() => Disposed = true;
}
