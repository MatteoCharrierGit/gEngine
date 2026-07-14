using Raylib_cs;

namespace gEngine.Input;

public class GameActionContext
{
    public Dictionary<GameAction, List<InputBinding>> Context { get; set; } = new();

    public void AddToContext(List<InputBinding> keys, GameAction action)
    {
        Context.TryAdd(action, keys);
    }

    public void RemoveFromContext(GameAction action)
    {
        Context.Remove(action);
    }
}