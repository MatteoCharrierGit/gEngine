using gEngine.Input;

namespace gEngine.Core;

public interface IGame
{
    void Init(InputHandler inputHandler);
    void Update(float fixedDeltaTime, InputHandler inputHandler);
    void Draw();
    void Shutdown();
}