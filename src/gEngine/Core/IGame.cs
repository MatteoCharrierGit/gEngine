using gEngine.Assets;
using gEngine.Input;
using gEngine.Rendering;

namespace gEngine.Core;

public interface IGame
{
    // L'AssetManager è creato e posseduto dal GameLoop (che possiede il ciclo di vita
    // degli adapter raylib) e passato qui: il gioco lo usa per caricare, non per crearlo.
    void Init(InputHandler inputHandler, AssetManager assets);
    void Update(float fixedDeltaTime, InputHandler inputHandler);
    void Draw(IRenderer renderer);
    void Shutdown();
}