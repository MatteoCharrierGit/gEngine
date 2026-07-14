using Raylib_cs;

namespace gEngine.Input;

public readonly struct InputBinding
{
    public KeyboardKey? KKey { get; init; }
    public MouseButton? MButton { get; init; }
    
    
    public bool IsDown() => KKey.HasValue ? Raylib.IsKeyDown(KKey.Value) : Raylib.IsMouseButtonDown(MButton!.Value);
    public bool IsPressed() => KKey.HasValue ? Raylib.IsKeyPressed(KKey.Value) : Raylib.IsMouseButtonPressed(MButton!.Value);
    public bool IsReleased() => KKey.HasValue ? Raylib.IsKeyReleased(KKey.Value) : Raylib.IsMouseButtonReleased(MButton!.Value);
}
