using Raylib_cs;

namespace gEngine.Input;

public class GameActionContext
{
    public Dictionary<GameAction, List<KeyboardKey>> Context { get; set; } = new();

    public void AddToContext(List<KeyboardKey> keys, GameAction action)
    {
        Context.TryAdd(action, keys);
    }

    public void RemoveFromContext(GameAction action)
    {
        Context.Remove(action);
    }
}