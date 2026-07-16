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
    
    internal ComponentStorage<T>? GetStorage<T>()
    {
        if (Storages.TryGetValue(typeof(T), out var storage))
            return (ComponentStorage<T>)storage;

        return null;
    }

}