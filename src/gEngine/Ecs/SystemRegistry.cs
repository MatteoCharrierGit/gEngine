using gEngine.Ecs.Base;
using gEngine.Ecs.Interfaces.System;
using gEngine.Rendering;

namespace gEngine.Ecs;

/// <summary>
/// Esito della traceability di un system su un'entità. Vale sia per "agisce su"
/// (<see cref="SystemRegistry.MatchOn"/>) sia per "legge" (<see cref="SystemRegistry.ObserveOn"/>):
/// la domanda cambia, la forma della risposta no.
/// </summary>
public enum SystemMatch
{
    /// <summary>Il system non dichiara l'insieme interrogato: non si può sapere senza farlo
    /// girare. Da mostrare come "?" — non come "no".</summary>
    Unknown,

    /// <summary>L'entità ha tutti i componenti dichiarati dal system.</summary>
    Matching,

    /// <summary>Il system dichiara i suoi componenti e all'entità ne manca almeno uno.</summary>
    NotMatching,
}

/// <summary>Un system registrato e le fasi in cui è finito.</summary>
public readonly record struct RegisteredSystem(ISystem System, SystemPhase Phases);

/// <summary>
/// Possiede i system del gioco, li smista per fase e li esegue.
///
/// Prima questa roba stava in <c>SandboxGame</c>: quattro liste, quattro overload di
/// <c>AddSystem</c> e quattro cicli a mano in Update/Draw. Era logica dell'engine parcheggiata
/// nel sample, e finché stava lì ogni gioco doveva reimplementarsela — a partire dal gating
/// del Play/Stop, che è esattamente "non chiamare RunSimulation quando non si sta giocando"
/// e che senza un posto solo dove i system vivono non si può nemmeno scrivere.
///
/// Si chiama Registry e non Scheduler di proposito: <b>non</b> ordina niente. Le fasi sono
/// fisse e dentro una fase l'ordine è quello di registrazione — un ordine che il chiamante
/// deve ancora conoscere (⚠️ <c>LightingSystem</c> va aggiunto PRIMA di <c>MeshRenderSystem</c>,
/// altrimenti le uniform delle luci arrivano dopo le mesh che dovevano illuminare). Uno
/// scheduler vero risolverebbe le dipendenze da sé; chiamarlo così prometterebbe una cosa
/// che non fa.
/// </summary>
public sealed class SystemRegistry(World world)
{
    private readonly List<RegisteredSystem> _all = [];
    private readonly List<IInputSystem> _input = [];
    private readonly List<ISimulationSystem> _simulation = [];
    private readonly List<ILateSystem> _late = [];
    private readonly List<IRenderSystem> _render = [];

    /// <summary>Tutti i system registrati, in ordine di registrazione, con le loro fasi.</summary>
    public IReadOnlyList<RegisteredSystem> Systems => _all;

    // Viste per fase, nell'ordine reale di esecuzione: un pannello dell'editor deve poter
    // dire non solo "quali system ci sono" ma "in che ordine girano dentro la loro fase".
    public IReadOnlyList<IInputSystem> InputSystems => _input;
    public IReadOnlyList<ISimulationSystem> SimulationSystems => _simulation;
    public IReadOnlyList<ILateSystem> LateSystems => _late;
    public IReadOnlyList<IRenderSystem> RenderSystems => _render;

    /// <summary>
    /// Registra un system smistandolo su tutte le fasi che implementa, e chiama
    /// <c>OnCreate</c> una volta sola.
    ///
    /// ⚠️ Un system che implementa due fasi di update (es. <c>IInputSystem</c> +
    /// <c>ISimulationSystem</c>) si vedrà chiamare <c>OnUpdate</c> <b>due volte</b> per tick,
    /// una per fase: le due interfacce condividono lo stesso metodo. È voluto — è ciò che
    /// significa stare in due fasi — ma è una trappola se lo si è fatto per sbaglio.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se il system non implementa nessuna interfaccia di fase (non girerebbe mai, e in
    /// silenzio), o se l'istanza è già registrata (girerebbe due volte).
    /// </exception>
    public void Add(ISystem system)
    {
        if (_all.Any(registered => ReferenceEquals(registered.System, system)))
            throw new InvalidOperationException(
                $"System {system.GetType().Name} already registered");

        var phases = SystemPhase.None;

        if (system is IInputSystem inputSystem)
        {
            _input.Add(inputSystem);
            phases |= SystemPhase.Input;
        }

        if (system is ISimulationSystem simulationSystem)
        {
            _simulation.Add(simulationSystem);
            phases |= SystemPhase.Simulation;
        }

        if (system is ILateSystem lateSystem)
        {
            _late.Add(lateSystem);
            phases |= SystemPhase.Late;
        }

        if (system is IRenderSystem renderSystem)
        {
            _render.Add(renderSystem);
            phases |= SystemPhase.Render;
        }

        // Fail-fast: un ISystem "nudo" è quasi sempre una interfaccia di fase dimenticata.
        // Accettarlo vorrebbe dire un system che non gira mai e nessuno che lo dice.
        if (phases == SystemPhase.None)
            throw new InvalidOperationException(
                $"System {system.GetType().Name} implements no phase interface " +
                $"(IInputSystem/ISimulationSystem/ILateSystem/IRenderSystem)");

        _all.Add(new RegisteredSystem(system, phases));

        // Dopo lo smistamento: se lo smistamento fallisce, OnCreate non dev'essere girato.
        system.OnCreate(world);
    }

    /// <summary>
    /// Toglie un system da tutte le fasi. Non esiste un <c>OnDestroy</c> su
    /// <see cref="ISystem"/>: un system che possiede risorse esterne (il
    /// <c>PhysicsSystem</c> e i suoi corpi Bepu) non le libera togliendolo di qui.
    /// </summary>
    public bool Remove(ISystem system)
    {
        var index = _all.FindIndex(registered => ReferenceEquals(registered.System, system));
        if (index < 0)
            return false;

        _all.RemoveAt(index);

        if (system is IInputSystem inputSystem) _input.Remove(inputSystem);
        if (system is ISimulationSystem simulationSystem) _simulation.Remove(simulationSystem);
        if (system is ILateSystem lateSystem) _late.Remove(lateSystem);
        if (system is IRenderSystem renderSystem) _render.Remove(renderSystem);

        return true;
    }

    /// <summary>
    /// Toglie <b>tutte</b> le istanze di <typeparamref name="T"/> (registrare due volte lo
    /// stesso tipo è legittimo: due istanze configurate diversamente).
    /// </summary>
    /// <returns>Quanti system sono stati tolti.</returns>
    public int Remove<T>() where T : ISystem
    {
        // Materializza prima: Remove modifica la lista che stiamo scorrendo.
        var targets = _all.Where(registered => registered.System is T)
                          .Select(registered => registered.System)
                          .ToList();

        foreach (var system in targets)
            Remove(system);

        return targets.Count;
    }

    // ------ ESECUZIONE DELLE FASI -------------------------------------------------------
    //
    // Input/Simulation/Late girano nel passo fisso (dt = FixedDeltaTime), Render una volta
    // per frame. Il World lo tiene il registry: il chiamante passa solo il tempo, così non
    // può sbagliare mondo a metà tick.

    public void RunInput(float dt)
    {
        foreach (var system in _input)
            system.OnUpdate(world, dt);
    }

    public void RunSimulation(float dt)
    {
        foreach (var system in _simulation)
            system.OnUpdate(world, dt);
    }

    public void RunLate(float dt)
    {
        foreach (var system in _late)
            system.OnUpdate(world, dt);
    }

    /// <summary>
    /// ⚠️ Non apre nessun blocco 3D: chi disegna possiede il proprio Begin3D/End3D (con
    /// l'editor questa fase gira due volte per frame, una per viewport, con camere diverse).
    /// </summary>
    public void RunRender(IRenderer renderer, Camera3D camera, float frameDeltaTime)
    {
        foreach (var system in _render)
            system.OnRender(world, renderer, camera, frameDeltaTime);
    }

    // ------ TRACEABILITY ----------------------------------------------------------------

    /// <summary>
    /// I system che agiscono su questa entità, secondo ciò che dichiarano
    /// (<see cref="ISystem.MatchedComponents"/>). Chi non dichiara nulla resta fuori: vedi
    /// <see cref="MatchOn"/> se serve distinguere "no" da "non si sa".
    /// </summary>
    public IEnumerable<RegisteredSystem> SystemsActingOn(Entity entity) =>
        _all.Where(registered => MatchOn(registered.System, world, entity) == SystemMatch.Matching);

    /// <summary>
    /// I system che <b>leggono</b> questa entità senza agirci
    /// (<see cref="ISystem.ObservedComponents"/>).
    ///
    /// Volutamente separato da <see cref="SystemsActingOn"/> e non un flag su un elenco solo:
    /// fondere i due gruppi cancellerebbe l'unica cosa che questa distinzione porta —
    /// "questo system tocca l'entità" contro "questo system la guarda per toccarne un'altra".
    /// Lo stesso system può comparire nei due elenchi di <b>entità diverse</b>
    /// (<c>CameraFollowSystem</c>: agisce sulla camera, legge il player), e in linea di
    /// principio anche della stessa — la dichiarazione è libera di dirlo.
    /// </summary>
    public IEnumerable<RegisteredSystem> SystemsObserving(Entity entity) =>
        _all.Where(registered => ObserveOn(registered.System, world, entity) == SystemMatch.Matching);

    /// <summary>
    /// Se il system agisca su questa entità. Confronto puramente dichiarativo: non fa girare
    /// niente, guarda solo i componenti dichiarati contro quelli dell'entità.
    /// </summary>
    public static SystemMatch MatchOn(ISystem system, World world, Entity entity) =>
        Match(system.MatchedComponents, world, entity);

    /// <summary>
    /// Se il system legga questa entità senza agirci.
    ///
    /// ⚠️ <see cref="SystemMatch.Unknown"/> qui è <b>quasi sempre</b> il valore giusto e non
    /// significa "chissà cosa legge": <see cref="ISystem.ObservedComponents"/> è opzionale e
    /// la stragrande maggioranza dei system non legge nulla di esterno. Chi lo interroga non
    /// deve mostrare tutti quei "?" come sospetti — solo <see cref="SystemMatch.Matching"/>
    /// dice qualcosa.
    /// </summary>
    public static SystemMatch ObserveOn(ISystem system, World world, Entity entity) =>
        Match(system.ObservedComponents, world, entity);

    private static SystemMatch Match(IReadOnlyList<Type> declared, World world, Entity entity)
    {
        if (declared.Count == 0)
            return SystemMatch.Unknown;

        foreach (var componentType in declared)
        {
            if (!world.HasComponent(entity, componentType))
                return SystemMatch.NotMatching;
        }

        return SystemMatch.Matching;
    }
}
