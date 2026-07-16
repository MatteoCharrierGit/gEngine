using System.Numerics;
using gEngine.Ecs.Component;
using gEngine.Scripting;

namespace Sandbox.Components;

/// <summary>
/// Velocità lineare, in unità al secondo. La applica il <c>MovementSystem</c>.
///
/// Vive nel <b>gioco</b> e non nell'engine, ed è il punto: non c'è niente di universale in
/// "questa entità si muove a velocità costante" — è una regola di Sandbox. Stava in
/// <c>src/gEngine/Ecs/Component/</c> perché all'inizio era comodo, ma nessun file dell'engine
/// lo ha mai usato: era un componente del gioco parcheggiato nel core, ed è esattamente il
/// genere di cosa che <see cref="GameComponentAttribute"/> esiste per rendere inutile.
/// </summary>
[GameComponent]
public struct VelocityComponent
{
    [EditorConfiguration] public Vector3 Velocity;

    /// <summary>
    /// Zero è l'unico default onesto: un'entità nuova non si muove da sé. Qui
    /// <c>default(T)</c> sarebbe pure andato bene — ma il metodo si scrive lo stesso, perché è
    /// la sua <b>presenza</b> a dire all'editor che il componente è aggiungibile, e
    /// dichiararlo è la decisione (vedi <c>SceneComponentRegistry.TryCreateDefault</c>).
    /// </summary>
    public static VelocityComponent CreateDefault() => new() { Velocity = Vector3.Zero };
}
