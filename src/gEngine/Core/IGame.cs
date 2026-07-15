using gEngine.Input;
using gEngine.Rendering;

namespace gEngine.Core;

public interface IGame
{
    void Init(InputHandler inputHandler);
    void Update(float fixedDeltaTime, InputHandler inputHandler);
    void Draw(IRenderer renderer);
    void Shutdown();
}