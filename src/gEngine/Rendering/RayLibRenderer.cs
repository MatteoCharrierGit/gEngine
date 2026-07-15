using System.Numerics;
using gEngine.Assets;
using Raylib_cs;

namespace gEngine.Rendering;

public unsafe class RayLibRenderer : IRenderer
{
    private const int MaxLights = 7; // deve combaciare con MAX_LIGHTS in Shaders/lit.fs

    private readonly Mesh _unitCubeMesh;
    private Material _defaultMaterial;

    // Sorgente dei modelli: l'altro adapter raylib. Serve a risolvere ModelHandle → Model
    // per il caso MeshKind.Model. Entrambi gli adapter condividono così la tabella modelli.
    private readonly RayLibAssetBackend _assetBackend;

    // Shader di illuminazione (PBR semplice) + cache delle location delle uniform, così
    // ogni frame settiamo i valori per indice senza ri-cercare le stringhe.
    private Shader _litShader;
    private int _viewPosLoc;
    private readonly int[] _lightEnabledLoc = new int[MaxLights];
    private readonly int[] _lightTypeLoc = new int[MaxLights];
    private readonly int[] _lightPositionLoc = new int[MaxLights];
    private readonly int[] _lightTargetLoc = new int[MaxLights];
    private readonly int[] _lightColorLoc = new int[MaxLights];
    private readonly int[] _lightIntensityLoc = new int[MaxLights];

    public RayLibRenderer(RayLibAssetBackend assetBackend)
    {
        _assetBackend = assetBackend;
        _unitCubeMesh = Raylib.GenMeshCube(1f, 1f, 1f);
        _defaultMaterial = Raylib.LoadMaterialDefault();

        SetupLightShader();

        // Il material di default (usato per i cubi) usa lo shader illuminato.
        _defaultMaterial.Shader = _litShader;
    }

    private void SetupLightShader()
    {
        var shaderDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        _litShader = Raylib.LoadShader(
            Path.Combine(shaderDir, "lit.vs"),
            Path.Combine(shaderDir, "lit.fs"));

        // matModel/matNormal/colDiffuse/mvp li aggancia raylib da sé (nomi standard).
        _viewPosLoc = Raylib.GetShaderLocation(_litShader, "viewPos");

        for (var i = 0; i < MaxLights; i++)
        {
            _lightEnabledLoc[i]   = Raylib.GetShaderLocation(_litShader, $"lights[{i}].enabled");
            _lightTypeLoc[i]      = Raylib.GetShaderLocation(_litShader, $"lights[{i}].type");
            _lightPositionLoc[i]  = Raylib.GetShaderLocation(_litShader, $"lights[{i}].position");
            _lightTargetLoc[i]    = Raylib.GetShaderLocation(_litShader, $"lights[{i}].target");
            _lightColorLoc[i]     = Raylib.GetShaderLocation(_litShader, $"lights[{i}].color");
            _lightIntensityLoc[i] = Raylib.GetShaderLocation(_litShader, $"lights[{i}].intensity");
        }

        // Parametri material globali (semplici: un solo set per tutta la scena, per ora).
        Raylib.SetShaderValue(_litShader, Raylib.GetShaderLocation(_litShader, "metallic"), 0.0f, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(_litShader, Raylib.GetShaderLocation(_litShader, "roughness"), 0.5f, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(_litShader, Raylib.GetShaderLocation(_litShader, "ambient"), new Vector3(0.03f, 0.03f, 0.03f), ShaderUniformDataType.Vec3);
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

    public void SetLighting(Vector3 cameraPosition, IReadOnlyList<LightData> lights)
    {
        Raylib.SetShaderValue(_litShader, _viewPosLoc, cameraPosition, ShaderUniformDataType.Vec3);

        for (var i = 0; i < MaxLights; i++)
        {
            if (i < lights.Count)
            {
                var light = lights[i];
                Raylib.SetShaderValue(_litShader, _lightEnabledLoc[i], 1, ShaderUniformDataType.Int);
                Raylib.SetShaderValue(_litShader, _lightTypeLoc[i], (int)light.Kind, ShaderUniformDataType.Int);
                Raylib.SetShaderValue(_litShader, _lightPositionLoc[i], light.Position, ShaderUniformDataType.Vec3);
                Raylib.SetShaderValue(_litShader, _lightTargetLoc[i], light.Direction, ShaderUniformDataType.Vec3);
                Raylib.SetShaderValue(_litShader, _lightColorLoc[i], ToVec3(light.Color), ShaderUniformDataType.Vec3);
                Raylib.SetShaderValue(_litShader, _lightIntensityLoc[i], light.Intensity, ShaderUniformDataType.Float);
            }
            else
            {
                Raylib.SetShaderValue(_litShader, _lightEnabledLoc[i], 0, ShaderUniformDataType.Int);
            }
        }
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
                if (_assetBackend.TryGetModel(command.Model, out var model))
                {
                    // Applica lo shader illuminato ai material del modello (idempotente).
                    for (var i = 0; i < model.MaterialCount; i++)
                        model.Materials[i].Shader = _litShader;

                    // Come per il cubo: la world matrix (row-major numerics) va trasposta
                    // nel layout column-major di raylib. Model.Transform è la matrice che
                    // raylib applica alla mesh; disegnando a origine con scala 1 il modello
                    // eredita l'intera world matrix.
                    model.Transform = Matrix4x4.Transpose(command.World);
                    Raylib.DrawModel(model, Vector3.Zero, 1f, color);
                }
                // Handle non valido / modello non trovato: skip silenzioso (niente crash).
                break;

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
        Raylib.UnloadShader(_litShader);
        Raylib.UnloadMesh(_unitCubeMesh);
        // NB: _defaultMaterial usa _litShader (già scaricato sopra). Non chiamiamo
        // UnloadMaterial per evitare un doppio-unload dello shader su alcune versioni
        // di raylib; al termine del processo la memoria è comunque liberata.
    }

    private Raylib_cs.Color ToRaylibColor(Color c)
    {
        return new Raylib_cs.Color(c.R, c.G, c.B, c.A);
    }

    private static Vector3 ToVec3(Color c)
    {
        return new Vector3(c.R/255f, c.G/255f, c.B/255f);
    }
}
