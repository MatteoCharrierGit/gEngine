namespace gEngine.Ecs;

/// <summary>
/// Le fasi in cui gira un system, nell'ordine in cui il gioco le esegue.
///
/// È un <c>[Flags]</c> e non un enum semplice perché un system può stare in più fasi: le
/// interfacce di fase sono ortogonali e nulla vieta a una classe di implementarne due (il
/// caso reale sono i render system, che sono <c>IRenderSystem</c> e potrebbero volere anche
/// un <c>OnUpdate</c> nella simulazione). Serve a mostrare la fase di un system nell'editor
/// senza doverne interrogare il tipo a mano.
/// </summary>
[Flags]
public enum SystemPhase
{
    None = 0,
    Input = 1 << 0,
    Simulation = 1 << 1,
    Late = 1 << 2,
    Render = 1 << 3,
}
