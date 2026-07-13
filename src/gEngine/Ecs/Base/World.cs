using gEngine.Ecs.Interfaces;

namespace gEngine.Ecs.Base;

public class World
{
    internal readonly Dictionary<int, Entity> Entities = [];
    internal readonly Dictionary<Type, IComponentStorage> Storages = [];

    private int _entityCounter = 0;

    public Entity CreateEntity()
    {
        var entity = new Entity(GetNextId());
        Entities.Add(entity.Id, entity);
        return entity;
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