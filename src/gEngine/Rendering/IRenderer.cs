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


    // Render target: disegnare su una texture invece che sulla finestra. Serve all'editor,
    // che mostra la scena dentro un pannello (e due volte, con due camere diverse) invece
    // che a tutto schermo con la UI sopra.
    RenderTargetHandle CreateRenderTarget(int width, int height);
    void DestroyRenderTarget(RenderTargetHandle target);

    // Fra Begin e End tutto il disegno finisce sul target invece che sulla finestra.
    // Non sono annidabili: il target attivo è al massimo uno.
    void BeginRenderTarget(RenderTargetHandle target);
    void EndRenderTarget();

    // Id della texture del target nel formato che vuole il backend della UI, da dare a
    // ImGui per mostrarla. È l'unica crepa voluta nel port: un'immagine ImGui è per
    // definizione un handle GPU grezzo, e inventare un tipo intermedio nasconderebbe solo
    // il fatto che i due lati devono comunque parlare della stessa texture.
    nint GetRenderTargetTextureId(RenderTargetHandle target);


    // Lighting: carica le luci del frame (posizione camera + fino a MAX_LIGHTS luci).
    // Va chiamato dentro il blocco 3D, prima di disegnare le mesh illuminate.
    void SetLighting(Vector3 cameraPosition, IReadOnlyList<LightData> lights);


    // Primitives
    void DrawMesh(in DrawMeshCommand command);


    // Ui
    void DrawText(string text, int posX, int posY, int fontSize, Color color);
    void DrawRectangle(int posX, int posY, int width, int height, Color color);
    void DrawRectanglePro(Vector2 position, Vector2 size, Vector2 origin, float rotationDeg, Color color);


    // Helper
    int GetScreenHeight();
    int GetScreenWidth();

    // Dimensioni della superficie su cui si sta disegnando ORA: il render target attivo,
    // oppure la finestra se non ce n'è uno. Diverse da GetScreenWidth/Height, che sono
    // sempre e solo la finestra.
    //
    // La distinzione non è pedanteria: chi calcola un aspect ratio (proiezione, frustum)
    // deve usare queste. Dentro un viewport dell'editor la finestra è larga il doppio del
    // pannello, e prendere la sua misura schiaccerebbe la vista.
    int GetRenderWidth();
    int GetRenderHeight();

    float GetFrameTime();
    double GetTime();

}