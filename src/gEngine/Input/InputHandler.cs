using Raylib_cs;

namespace gEngine.Input;

public class InputHandler
{
    private GameActionContext ActiveContext { get; set; } = new();

    private Dictionary<GameAction, GameActionState> State { get; set; } = new();

    public InputHandler()
    {
        Init();
    }
    
    public void SetActiveContext(GameActionContext activeContext)
    {
        ActiveContext = activeContext;

        foreach (var (action, state) in State)
        {
            State[action] = GameActionState.Default;
        }
    }

    public void Update()
    {
        foreach (var (action, keys) in ActiveContext.Context)
        {
            GameActionState bestState = GameActionState.Default;

            foreach (var key in keys)
            {
                var state = GetKeyState(key);

                if (state > bestState)
                    bestState = state;
            }

            State[action] = bestState;
        }
    }
    
    private GameActionState GetKeyState(KeyboardKey key)
    {
        if (IsActionPressed(key))
            return GameActionState.KeyPressed;

        if (IsActionDown(key))
            return GameActionState.KeyDown;

        if (IsActionReleased(key))
            return GameActionState.KeyUp;

        return GameActionState.Default;
    }

    private bool IsActionPressed(KeyboardKey key)
    {
        return Raylib.IsKeyPressed(key);
    }

    private bool IsActionReleased(KeyboardKey key)
    {
        return Raylib.IsKeyReleased(key);
    }

    private bool IsActionDown(KeyboardKey key)
    {
        return Raylib.IsKeyDown(key);
    }

    private void Init()
    {
        foreach (var action in Enum.GetValues<GameAction>())
        {
            State.Add(action, GameActionState.Default);
        }
    }
    
    // Public API Input Handler
    
    public bool IsActionPressed(GameAction action)
    {
        return State.GetValueOrDefault(action, GameActionState.Default) ==  GameActionState.KeyPressed;
    }
    
    public bool IsActionDown(GameAction action)
    {
        return State.GetValueOrDefault(action, GameActionState.Default) ==  GameActionState.KeyDown;
    }
    
    public bool IsActionReleased(GameAction action)
    {
        return State.GetValueOrDefault(action, GameActionState.Default) ==  GameActionState.KeyUp;
    }
    
}



















