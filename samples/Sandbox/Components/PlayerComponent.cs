using gEngine.Ecs.Component;
using gEngine.Scripting;

namespace Sandbox.Components;

/// <summary>
/// Marca l'entità guidata dal giocatore. La leggono il <c>PlayerInputSystem</c> (per muoverla)
/// e il <c>CameraFollowSystem</c> (per inseguirla).
///
/// Come <see cref="VelocityComponent"/>: vive nel gioco, perché "chi è il player" è una
/// domanda che ha senso solo dentro un gioco.
/// </summary>
[GameComponent]
public struct PlayerComponent
{
    [EditorConfiguration] public string Name;

    /// <summary>
    /// Nome vuoto: è dato d'autore e si scrive dall'Inspector. Non è <c>default(T)</c> per
    /// pigrizia — è che qui <c>default(T)</c> è la risposta giusta, e va detto.
    /// </summary>
    public static PlayerComponent CreateDefault() => new() { Name = string.Empty };
}
