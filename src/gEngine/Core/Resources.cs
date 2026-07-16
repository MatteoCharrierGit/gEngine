namespace gEngine.Core;

/// <summary>
/// Contenitore type-safe delle <b>Resource</b>: l'infrastruttura singleton del gioco
/// (renderer, asset manager, mondo fisico, input handler), dichiarata in un posto solo
/// invece che sparsa tra i campi di chi capita.
///
/// Lo split è quello di Bevy, ed è la regola dell'engine:
/// <list type="bullet">
///   <item><b>Component</b> = dato di scena, vive su un'entità del World, finisce nel <c>.json</c>;</item>
///   <item><b>Resource</b> = infrastruttura singleton, vive FUORI dal World ma <b>dichiarata</b>.</item>
/// </list>
/// In una frase: <i>nessun dato di scena vive fuori dal World; l'infrastruttura è una
/// Resource registrata</i>. La parte che conta della regola è la seconda metà: "fuori dal
/// World" era già vero prima, "dichiarata" no — le dipendenze si passavano a mano nei
/// costruttori dei system e non esisteva nessun posto dove leggere di cosa vive il gioco.
///
/// PERCHÉ una Resource non può essere un Component (due ragioni concrete, non estetiche):
/// <list type="number">
///   <item><b>La serializzazione la seguirebbe.</b> <c>SceneSerializer</c> scorre
///   <c>World.ComponentStorages</c> e scrive tutto quel che trova: un <c>IRenderer</c>
///   messo nel World finirebbe dentro <c>demo.json</c>. Non è un caso da gestire con un
///   <c>[RuntimeState]</c> in più — è proprio l'idea di "salvare il renderer" a non
///   significare niente.</item>
///   <item><b>Il ciclo di vita è un altro.</b> Le risorse GPU sono legate alla finestra
///   (vivono tra <c>InitWindow</c> e <c>CloseWindow</c>), non alla scena. Nel World
///   sarebbero soggette a <c>World.Clear</c>: ricaricare una scena — o, appena arriva, uno
///   Stop che ripristina lo snapshot — spazzerebbe via il renderer insieme ai cubi.</item>
/// </list>
///
/// ⚠️ Pensato per l'infrastruttura, che è fatta di reference type. Un value type qui
/// verrebbe boxato e <see cref="Get{T}"/> ne restituirebbe una <b>copia</b>: mutarla non
/// toccherebbe il contenitore (lo stesso gotcha struct/copia del write-back nei system).
/// Se il dato è mutabile e condiviso, quasi sempre è un Component, non una Resource.
/// </summary>
public sealed class Resources
{
    private readonly Dictionary<Type, object> _resources = [];

    /// <summary>
    /// I tipi registrati, in ordine di registrazione. Serve a chi deve mostrare le Resource
    /// senza conoscerne i tipi a compile time (un pannello dell'editor), allo stesso modo in
    /// cui <c>World.ComponentStorages</c> serve all'Inspector.
    /// </summary>
    public IEnumerable<Type> RegisteredTypes => _resources.Keys;

    /// <summary>
    /// Registra una risorsa sotto <typeparamref name="T"/>.
    ///
    /// La chiave è <c>typeof(T)</c> e <b>non</b> <c>resource.GetType()</c>: è voluto. Così
    /// <c>Add&lt;IPhysicsWorld&gt;(new BepuPhysicsWorld(...))</c> si rilegge con
    /// <c>Get&lt;IPhysicsWorld&gt;()</c> — chi la consuma dipende dalla porta, non
    /// dall'adapter. Con la chiave presa dall'istanza si registrerebbe sotto
    /// <c>BepuPhysicsWorld</c> e ogni lettura per interfaccia fallirebbe.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se <typeparamref name="T"/> è già registrato. Fail-fast: due Add sullo stesso tipo
    /// vogliono dire che metà del gioco sta usando un'istanza e metà l'altra, ed è un bug
    /// che si manifesta lontano da qui.
    /// </exception>
    public void Add<T>(T resource) where T : notnull
    {
        if (!_resources.TryAdd(typeof(T), resource))
            throw new InvalidOperationException(
                $"Resource {typeof(T).Name} already registered");
    }

    /// <summary>
    /// La risorsa registrata sotto <typeparamref name="T"/>.
    /// Fail-fast come <c>World.GetComponent</c>: se manca è un errore di setup, e restituire
    /// null lo trasformerebbe in un NullReference a chilometri di distanza.
    /// </summary>
    public T Get<T>() where T : notnull
    {
        if (!_resources.TryGetValue(typeof(T), out var resource))
            throw new InvalidOperationException(
                $"No resource for {typeof(T).Name}");

        return (T)resource;
    }

    /// <summary>Variante non fatale, per chi può funzionare anche senza.</summary>
    public bool TryGet<T>(out T resource) where T : notnull
    {
        if (_resources.TryGetValue(typeof(T), out var value))
        {
            resource = (T)value;
            return true;
        }

        resource = default!;
        return false;
    }

    /// <summary>
    /// Variante <b>non generica</b>: il tipo si conosce solo a runtime.
    ///
    /// Serve a chi deve pescare una risorsa partendo da un <see cref="Type"/> che ha letto da
    /// qualche altra parte invece che scritto: <c>ScriptDiscovery</c>, che guarda i parametri
    /// del costruttore di un system e cerca qui dentro con cosa riempirli. È lo stesso bisogno
    /// (e la stessa forma) di <c>World.HasComponent(Entity, Type)</c> e
    /// <c>World.AddComponent(Entity, object)</c>: manipolare per tipo ciò di cui non si
    /// conosce il tipo a compile time.
    ///
    /// ⚠️ Cerca per <b>tipo esatto</b>, come l'overload generico: la chiave è quella con cui si
    /// è fatto <see cref="Add{T}"/>. Un costruttore che chiede <c>BepuPhysicsWorld</c> non
    /// trova un <c>IPhysicsWorld</c> registrato sotto la porta — e non deve, perché è il
    /// contrario della regola per cui la chiave è <c>typeof(T)</c>: chi consuma dipende dalla
    /// porta, non dall'adapter.
    /// </summary>
    public bool TryGet(Type type, out object resource)
    {
        return _resources.TryGetValue(type, out resource!);
    }

    public bool Has<T>() where T : notnull => _resources.ContainsKey(typeof(T));
}
