using gEngine.Ecs.Interfaces;

namespace gEngine.Ecs.Base;

public class World
{
    internal readonly Dictionary<int, Entity> Entities = [];
    internal readonly Dictionary<Type, IComponentStorage> Storages = [];

    private int _entityCounter = 0;

    /// <summary>
    /// Tutte le entità vive. Serve a chi deve ragionare sul World per intero invece che
    /// per componente — in pratica l'editor (la Hierarchy elenca anche le entità che non
    /// hanno nessun componente). I system normali usano <c>Query</c>: partire da qui
    /// significherebbe scandire tutto il World per poi scartare quasi tutto.
    /// </summary>
    public IReadOnlyCollection<Entity> AllEntities => Entities.Values;

    public bool Exists(Entity entity) => Entities.ContainsKey(entity.Id);

    /// <summary>
    /// Tutti gli storage esistenti, uno per tipo di componente incontrato finora.
    /// Serve a chi deve enumerare i componenti di un'entità <b>senza conoscerne i tipi</b>
    /// (l'Inspector dell'editor): si scorrono gli storage e si chiede a ciascuno
    /// <see cref="IComponentStorage.Has"/>. I system usano <c>Query</c>.
    /// </summary>
    public IEnumerable<IComponentStorage> ComponentStorages => Storages.Values;

    public Entity CreateEntity()
    {
        var entity = new Entity(GetNextId());
        Entities.Add(entity.Id, entity);
        return entity;
    }

    /// <summary>
    /// Distrugge l'entità e tutti i suoi componenti.
    ///
    /// Gli id <b>non vengono riusati</b> (<c>_entityCounter</c> cresce e basta): un
    /// <see cref="Entity"/> tenuto da parte dopo la distruzione non potrà mai riferirsi
    /// per sbaglio a un'entità nuova. È il motivo per cui qui non serve un contatore di
    /// generazione come negli ECS che riciclano gli id.
    ///
    /// I riferimenti pendenti restano possibili (un <see cref="Component.ParentComponent"/>
    /// che punta a un'entità distrutta) e sono gestiti a valle, non qui: chi risolve
    /// riferimenti verifica con <see cref="Exists"/>. Scandire tutti gli storage per
    /// ripulire i riferimenti costerebbe più di quanto valga.
    ///
    /// ⚠️ Il World non conosce risorse esterne: se un componente è il link a qualcosa che
    /// vive fuori (es. <c>PhysicsBodyComponent</c> → corpo Bepu), toglierlo di qui non lo
    /// libera. È il system proprietario a doversene accorgere — vedi la riconciliazione
    /// in <see cref="System.PhysicsSystem"/>.
    /// </summary>
    public void DestroyEntity(Entity entity)
    {
        if (!Entities.Remove(entity.Id))
            return;

        // Sicuro: Remove tocca il dizionario interno di ogni storage, non la collezione
        // Storages su cui stiamo iterando.
        foreach (var storage in Storages.Values)
            storage.Remove(entity.Id);
    }

    /// <summary>
    /// Svuota il World: nessuna entità, nessun componente. Serve a ricaricare una scena
    /// sopra quella corrente.
    ///
    /// Il contatore degli id <b>non</b> si azzera, per lo stesso motivo di
    /// <see cref="DestroyEntity"/>: gli id non si riusano mai, così un <see cref="Entity"/>
    /// rimasto in mano a qualcuno (la selezione dell'editor, per dire) dopo un ricaricamento
    /// resta invalido invece di puntare a un'entità nuova e sbagliata.
    ///
    /// ⚠️ Vale l'avvertenza di <see cref="DestroyEntity"/>: le risorse esterne referenziate
    /// dai componenti non vengono liberate qui — se ne accorge il system proprietario.
    /// </summary>
    public void Clear()
    {
        Entities.Clear();

        foreach (var storage in Storages.Values)
            storage.Clear();
    }

    public void AddComponent<T>(Entity entity, T component)
    {
        GetOrCreateStorage<T>().Add(entity.Id, component);
    }

    /// <summary>
    /// Variante <b>non generica</b>: il tipo si conosce solo a runtime. Gemella di
    /// <see cref="HasComponent(Entity, Type)"/> e mossa dallo stesso bisogno — l'editor
    /// manipola componenti di cui non conosce i tipi a compile time. Serve ad "aggiungi
    /// componente": il tipo esce da un elenco (il <c>SceneComponentRegistry</c>), quindi
    /// non c'è nessun <c>T</c> da scrivere. I system usano l'overload generico.
    ///
    /// Come l'overload generico, <b>sovrascrive</b> se il componente c'è già.
    ///
    /// ⚠️ Lo storage è scelto col tipo <b>runtime</b> di <paramref name="component"/>, non
    /// col suo tipo statico: è l'unico disponibile qui, ma è anche un'asimmetria con
    /// l'overload generico, che usa <c>typeof(T)</c>. In pratica coincidono — i componenti
    /// sono tipi concreti, e uno struct boxato riporta il proprio tipo.
    /// </summary>
    public void AddComponent(Entity entity, object component)
    {
        GetOrCreateStorage(component.GetType()).SetBoxed(entity.Id, component);
    }

    public T GetComponent<T>(Entity entity)
    {
        var storage = GetStorage<T>()
                      ?? throw new InvalidOperationException(
                          $"No storage for {typeof(T).Name}");

        return storage.Get(entity.Id);
    }

    public bool TryGetComponent<T>(Entity entity, out T component)
    {
        component = default!;
        var storage = GetStorage<T>();
        
        return storage != null &&
               storage.TryGet(entity.Id, out component);
    }
    

    public bool HasComponent<T>(Entity entity)
    {
        var storage = GetStorage<T>();
        return storage != null && storage.Has(entity.Id);
    }

    /// <summary>
    /// Variante <b>non generica</b>: il tipo si conosce solo a runtime. Come
    /// <see cref="ComponentStorages"/>, serve a chi ragiona sui componenti senza conoscerne
    /// i tipi a compile time — la traceability dei system
    /// (<c>SystemRegistry.MatchOn</c>) confronta i <c>Type</c> dichiarati con questa. I
    /// system usano l'overload generico.
    /// </summary>
    public bool HasComponent(Entity entity, Type componentType) =>
        Storages.TryGetValue(componentType, out var storage) && storage.Has(entity.Id);

    public void RemoveComponent<T>(Entity entity)
    {
        if (Storages.TryGetValue(typeof(T), out var storage))
            ((ComponentStorage<T>)storage).Remove(entity.Id);
    }
    
    
    // ------ HELPER --------------------------------------------------------------------

    private int GetNextId()
    {
        _entityCounter++;
        return _entityCounter;
    }

    private ComponentStorage<T> GetOrCreateStorage<T>()
    {
        if (!Storages.TryGetValue(typeof(T), out var storage))
        {
            storage = new ComponentStorage<T>();
            Storages[typeof(T)] = storage;
        }

        return (ComponentStorage<T>)storage;
    }

    /// <summary>
    /// Come <see cref="GetOrCreateStorage{T}"/> ma col tipo noto solo a runtime: l'unico
    /// modo di costruire un <c>ComponentStorage&lt;T&gt;</c> senza scrivere quel T è
    /// chiuderne il generico a mano. Costa una <c>MakeGenericType</c> <b>una volta per
    /// tipo</b> — solo alla prima entità che riceve quel componente, non a ogni Add —
    /// e da lì in poi passa tutto dalla faccia non generica, che di reflection non ne ha.
    /// </summary>
    private IComponentStorage GetOrCreateStorage(Type componentType)
    {
        if (!Storages.TryGetValue(componentType, out var storage))
        {
            storage = (IComponentStorage)Activator.CreateInstance(
                typeof(ComponentStorage<>).MakeGenericType(componentType))!;

            Storages[componentType] = storage;
        }

        return storage;
    }
    
    internal ComponentStorage<T>? GetStorage<T>()
    {
        if (Storages.TryGetValue(typeof(T), out var storage))
            return (ComponentStorage<T>)storage;

        return null;
    }

}