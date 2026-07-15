using System.Numerics;
using gEngine.Physics;

namespace gEngine.Ecs.Component;

/// <summary>
/// Descrive il corpo fisico di un'entità (dati puri). Il corpo vero e proprio è creato
/// dal <see cref="gEngine.Ecs.System.PhysicsSystem"/> al primo update, che poi aggiunge
/// un <see cref="PhysicsBodyComponent"/> come link runtime.
/// </summary>
public struct RigidBodyComponent
{
    public ColliderShape Shape;

    /// <summary>Box: estensioni piene (larghezza, altezza, profondità). Sfera: raggio = <c>Size.X</c>.</summary>
    public Vector3 Size;

    /// <summary>Massa per i corpi dinamici. Ignorata se <see cref="IsStatic"/>.</summary>
    public float Mass;

    /// <summary>Statico = non si muove (pavimento/muro); dinamico = soggetto a gravità e collisioni.</summary>
    public bool IsStatic;
}
