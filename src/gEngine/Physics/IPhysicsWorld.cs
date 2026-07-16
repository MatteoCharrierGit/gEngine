using System.Numerics;

namespace gEngine.Physics;

/// <summary>
/// Port (astrazione) verso il motore fisico. L'engine dipende solo da questa interfaccia;
/// l'unica implementazione concreta (<see cref="BepuPhysicsWorld"/>) è l'unico file che
/// importa <c>BepuPhysics</c>. Stesso schema ports &amp; adapters di renderer e asset:
/// per cambiare motore fisico si riscrive solo l'adapter.
///
/// I corpi statici non si muovono (pavimenti, muri); i dinamici sì e vanno letti dopo
/// ogni <see cref="Step"/> per sincronizzare il Transform dell'ECS.
/// </summary>
public interface IPhysicsWorld : IDisposable
{
    BodyId AddBox(Vector3 position, Quaternion orientation, Vector3 size, float mass, bool isStatic);
    BodyId AddSphere(Vector3 position, Quaternion orientation, float radius, float mass, bool isStatic);

    /// <summary>Avanza la simulazione di <paramref name="dt"/> secondi (passo fisso).</summary>
    void Step(float dt);

    /// <summary>
    /// Posa corrente di un corpo. Restituisce false per handle non validi o corpi statici
    /// (che non hanno bisogno di sync).
    /// </summary>
    bool TryGetPose(BodyId id, out Vector3 position, out Quaternion orientation);

    /// <summary>
    /// Toglie il corpo dalla simulazione. Serve a chiunque distrugga un'entità: il mondo
    /// fisico non sa niente dell'ECS, quindi un corpo non rimosso continuerebbe a
    /// simulare (e a collidere) per un'entità che non esiste più.
    ///
    /// Handle non valido o già rimosso = <b>no-op sicuro</b>, come da convenzione degli
    /// handle opachi.
    /// </summary>
    void RemoveBody(BodyId id);
}
