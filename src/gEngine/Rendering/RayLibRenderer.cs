using System.Numerics;
using Raylib_cs;

namespace gEngine.Rendering;

public unsafe class RayLibRenderer : IRenderer
{
    private readonly Mesh _unitCubeMesh;
    private Material _defaultMaterial;

    public RayLibRenderer()
    {
        _unitCubeMesh = Raylib.GenMeshCube(1f, 1f, 1f);
        _defaultMaterial = Raylib.LoadMaterialDefault();
    }

    public void BeginFrame()
    {
        Raylib.BeginDrawing();
    }

    public void EndFrame()
    {
        Raylib.EndDrawing();
    }

    public void Begin3D(Camera3D camera)
    {
        Raylib.BeginMode3D(camera.ToRaylibCamera3D());
    }

    public void End3D()
    {
        Raylib.EndMode3D();
    }

    public void DrawMesh(in DrawMeshCommand command)
    {
        var color = ToRaylibColor(command.Tint);

        switch (command.Kind)
        {
            case MeshKind.Cube:
                // Raylib usa matrici column-major (traslazione nella 4a colonna), mentre
                // System.Numerics.Matrix4x4 è row-major (traslazione nella 4a riga): le due
                // convenzioni sono l'una la trasposta dell'altra. DrawMesh è un P/Invoke diretto
                // sull'API nativa, quindi la matrice va trasposta prima di passarla.
                var raylibWorld = Matrix4x4.Transpose(command.World);

                _defaultMaterial.Maps[(int)MaterialMapIndex.Albedo].Color = color;
                Raylib.DrawMesh(_unitCubeMesh, _defaultMaterial, raylibWorld);

                if (command.Wireframe)
                {
                    _defaultMaterial.Maps[(int)MaterialMapIndex.Albedo].Color = Raylib_cs.Color.Black;
                    Rlgl.EnableWireMode();
                    Raylib.DrawMesh(_unitCubeMesh, _defaultMaterial, raylibWorld);
                    Rlgl.DisableWireMode();
                }
                break;

            case MeshKind.Plane:
                Raylib.DrawPlane(command.World.Translation, new Vector2(command.Size.X, command.Size.Z), color);
                break;

            case MeshKind.Grid:
                Raylib.DrawGrid((int)command.Size.X, command.Size.Y);
                break;

            case MeshKind.Model:
                throw new NotSupportedException("MeshKind.Model non è ancora supportato da DrawMesh (nessun caricamento modelli implementato).");

            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "MeshKind non riconosciuto.");
        }
    }

    public void DrawText(string text, int posX, int posY, int fontSize, Color color)
    {
        Raylib.DrawText(text, posX, posY, fontSize, ToRaylibColor(color));
    }

    public void DrawRectangle(int posX, int posY, int width, int height, Color color)
    {
        Raylib.DrawRectangle(posX, posY, width, height, ToRaylibColor(color));
    }

    public void DrawRectanglePro(Vector2 position, Vector2 size, Vector2 origin, float rotationDeg, Color color)
    {
        var rect = new Rectangle(position.X, position.Y, size.X, size.Y);
        Raylib.DrawRectanglePro(rect, origin, rotationDeg, ToRaylibColor(color));
    }

    public int GetScreenHeight()
    {
        return Raylib.GetScreenHeight();
    }

    public int GetScreenWidth()
    {
        return Raylib.GetScreenWidth();
    }

    public float GetFrameTime()
    {
        return Raylib.GetFrameTime();
    }

    public double GetTime()
    {
        return Raylib.GetTime();
    }

    public void ClearBackground(Color c)
    {
        Raylib.ClearBackground(ToRaylibColor(c));
    }

    public void Shutdown()
    {
        Raylib.UnloadMaterial(_defaultMaterial);
        Raylib.UnloadMesh(_unitCubeMesh);
    }

    private Raylib_cs.Color ToRaylibColor(Color c)
    {
        return new Raylib_cs.Color(c.R, c.G, c.B, c.A);
    }
}
