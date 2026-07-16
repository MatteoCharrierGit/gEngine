using gEngine.Ecs.Interfaces;

namespace gEngine.Ecs.Base;

public class ComponentStorage<T> : IComponentStorage
{
    private readonly Dictionary<int, T> _components = [];
    
    /*
     * Queries optimization:
     *      Expose EntityId and Components count 
     */
    
    public IEnumerable<int> Keys => _components.Keys;
    public int Count => _components.Count;
    public IEnumerable<KeyValuePair<int, T>> Items => _components;

    public void Add(int entityId, T component)
    {
        _components[entityId] = component;
    }

    public bool Has(int entityId)
    {
        return _components.ContainsKey(entityId);
    }

    public T Get(int entityId)
    {
        return _components[entityId];
    }

    public bool TryGet(int entityId, out T component)
    {
        return _components.TryGetValue(entityId, out component!);
    }

    public void Remove(int entityId)
    {
        _components.Remove(entityId);
    }


    // ------ FACCIA NON GENERICA (IComponentStorage) --------------------------------
    // Usata solo dall'editor, che non conosce i tipi a compile time. Vedi IComponentStorage.

    public Type ComponentType => typeof(T);

    public void Clear()
    {
        _components.Clear();
    }

    public object? GetBoxed(int entityId)
    {
        return _components.TryGetValue(entityId, out var component) ? component : null;
    }

    public void SetBoxed(int entityId, object component)
    {
        _components[entityId] = (T)component;
    }
}