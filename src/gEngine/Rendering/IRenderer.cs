using System.Numerics;

namespace gEngine.Rendering;

public interface IRenderer
{
    // Rendering
    void BeginFrame();
    void EndFrame();
    void Begin3D(Camera3D camera);
    void End3D();
    void ClearBackground(Color c);
    void Shutdown();


    // Primitives
    void DrawMesh(in DrawMeshCommand command);


    // Ui
    void DrawText(string text, int posX, int posY, int fontSize, Color color);
    void DrawRectangle(int posX, int posY, int width, int height, Color color);
    void DrawRectanglePro(Vector2 position, Vector2 size, Vector2 origin, float rotationDeg, Color color);


    // Helper
    int GetScreenHeight();
    int GetScreenWidth();

    float GetFrameTime();
    double GetTime();

}