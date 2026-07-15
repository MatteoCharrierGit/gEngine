# gEngine — Guida all'uso

Come si costruisce un gioco con gEngine allo stato attuale. Documenta ciò che
**esiste ed è utilizzabile oggi** (non la roadmap futura — per quella vedi
[`ROADMAP.md`](ROADMAP.md)). I riferimenti ai file usano path relativi alla radice
del repo.

---

## 1. Filosofia architetturale

Due principi guidano il codice; conoscerli aiuta a capire *perché* le cose sono
disposte così.

**Split engine / gioco.** La libreria (`src/gEngine`) non sa nulla di *quale* gioco
gira. Il gioco (es. `samples/Sandbox`) implementa `IGame` e vive fuori dall'engine.

**Ports & adapters (indipendenza dalle librerie).** L'engine non chiama mai
direttamente librerie di terze parti (Raylib, in futuro Bepu) dalla sua logica.
Ogni libreria è nascosta dietro un'**interfaccia** (il *port*) con **una sola**
implementazione concreta (l'*adapter*), che è l'unico file a importarla:

| Ambito | Port (engine) | Adapter (unico a importare la lib) |
|---|---|---|
| Rendering | `IRenderer` | `RayLibRenderer` |
| Asset & audio | `IAssetBackend` | `RayLibAssetBackend` |

Regola pratica: **se stai per scrivere `using Raylib_cs;` in un file dell'engine,
fermati** — a meno che quel file non sia un adapter. Cambiare libreria deve voler
dire riscrivere solo gli adapter, non i system né i componenti.

> Nota onesta sullo stato: `Camera3D` espone ancora `CameraProjection` di raylib e il
> gioco `Sandbox` importa `Raylib_cs` per gli enum di input (`KeyboardKey`,
> `MouseButton`) — sono coupling residui noti, non ancora astratti.

---

## 2. Avvio: `Program` → `GameLoop` → `IGame`

Il punto d'ingresso costruisce un `GameLoop` con dimensioni finestra, titolo e
un'istanza del tuo gioco, poi chiama `Run()`:

```csharp
// samples/Sandbox/Program.cs
var gameLoop = new GameLoop(1920, 1080, "Game", new SandboxGame());
gameLoop.Run();
```

Il tuo gioco implementa `IGame` (`src/gEngine/Core/IGame.cs`):

```csharp
public interface IGame
{
    void Init(InputHandler inputHandler, AssetManager assets); // una volta, prima del loop
    void Update(float fixedDeltaTime, InputHandler input);     // 0..N volte per frame (fixed step)
    void Draw(IRenderer renderer);                             // una volta per frame
    void Shutdown();                                           // alla chiusura
}
```

`GameLoop` possiede la finestra, l'audio device, il `RayLibRenderer` e l'`AssetManager`
(tutti costruiti dopo `InitWindow`): passa il renderer a `Draw` e l'AssetManager a
`Init`. **Il gioco non tocca raylib per disegnare né costruisce gli adapter raylib.**

---

## 3. Il game loop (fixed timestep)

`src/gEngine/Core/GameLoop.cs` separa il passo di **simulazione** (a passo fisso) dal
**rendering** (a ogni frame):

```
ogni frame:
  accumulator += frameTime
  input.Update()
  while accumulator >= 1/60:      // FixedDeltaTime
      Game.Update(1/60, input)    // fisica/logica deterministiche
      accumulator -= 1/60
  Game.Draw(renderer)             // disegno, una volta per frame
```

Perché: la logica a `dt` costante è deterministica e stabile (niente esplosioni della
fisica a frame rate variabile), mentre il rendering gira libero. È il motivo per cui
i **RenderSystem** sono separati dagli altri (§5).

---

## 4. ECS: World, Entity, Component

L'ECS è mini e fatto a mano (`src/gEngine/Ecs/`).

- **Entity** (`Ecs/Base/Entity.cs`): `readonly record struct Entity(int Id)` — solo un id.
- **Component**: normali `struct`/`class` di **dati puri**, senza logica. Es.
  `TransformComponent`, `MeshRendererComponent`, `ParentComponent`.
- **World** (`Ecs/Base/World.cs`): contenitore. Ogni tipo di componente ha il suo
  storage (dizionario `entityId → component`).

```csharp
var world = new World();
var e = world.CreateEntity();

world.AddComponent(e, new TransformComponent
{
    Position = new Vector3(0, 1, 0),
    Rotation = Quaternion.Identity,
    Scale    = Vector3.One,
});

bool has   = world.HasComponent<TransformComponent>(e);
var  t     = world.GetComponent<TransformComponent>(e);
bool ok    = world.TryGetComponent<TransformComponent>(e, out var t2);
world.RemoveComponent<TransformComponent>(e);
```

### Query

Le query sono extension su `World` (`Ecs/Base/WorldQueries.cs`), da 1 a 4 componenti.
Restituiscono le entità che hanno **tutti** i tipi richiesti:

```csharp
foreach (var (entity, transform, mesh) in world.Query<TransformComponent, MeshRendererComponent>())
{
    // ...
}
```

### ⚠️ Gotcha: componenti `struct` = copia

Se un componente è una `struct`, `GetComponent`/la query restituiscono una **copia**.
Mutarla non tocca lo storage: devi **riscrivere** con `AddComponent` (fa da upsert):

```csharp
var t = world.GetComponent<TransformComponent>(e);
t.Position += velocity * dt;     // muta solo la COPIA locale
world.AddComponent(e, t);        // write-back: senza questa riga la modifica è persa
```

(`MeshRendererComponent` è oggi una `class`, quindi si comporta per riferimento — ma
non fare affidamento sul tipo: tratta il write-back come la regola.)

---

## 5. System: logica sui componenti

Un system contiene la logica; i componenti restano dati. Tutti derivano da `ISystem`
(`Ecs/Interfaces/System/`):

```csharp
public interface ISystem
{
    void OnCreate(World world);          // chiamato quando il system è registrato
    void OnUpdate(World world, float dt);
}
```

Ci sono **quattro sottotipi**, che determinano *quando* il system gira. I primi tre
girano nel passo fisso di `Update`, in quest'ordine; il quarto gira nel `Draw`:

| Interfaccia | Quando | A cosa serve |
|---|---|---|
| `IInputSystem` | fixed step, per primo | tradurre input in intenzioni/stato |
| `ISimulationSystem` | fixed step, poi | movimento, fisica, gameplay |
| `ILateSystem` | fixed step, per ultimo | correzioni dopo la simulazione (es. camera follow) |
| `IRenderSystem` | ogni frame in `Draw` | disegno; ha `OnRender(world, renderer, camera, dt)` |

`IInputSystem`/`ISimulationSystem`/`ILateSystem` sono marker vuoti (usano `OnUpdate`).
`IRenderSystem` aggiunge:

```csharp
public interface IRenderSystem : ISystem
{
    void OnRender(World world, IRenderer renderer, Camera3D camera, float frameDeltaTime);
}
```

(La `Camera3D` serve al render step per il frustum culling — e in prospettiva per
ordinare i trasparenti per distanza; vedi §7.)

### Registrare ed eseguire i system

**L'engine non ha uno scheduler**: è il gioco a tenere le liste per tipo ed eseguirle
nell'ordine giusto. Pattern usato in `SandboxGame`:

```csharp
// campi
private List<IInputSystem>? _inputSystems;
private List<ISimulationSystem>? _simulationSystems;
private List<ILateSystem>? _lateSystems;
private List<IRenderSystem>? _renderSystems;

// in Init(): AddSystem è overloaded per tipo, aggiunge alla lista giusta e chiama OnCreate
AddSystem(new MovementSystem());       // ISimulationSystem
AddSystem(new PlayerInputSystem(...));  // IInputSystem
AddSystem(new CameraFollowSystem(...)); // ILateSystem
AddSystem(new MeshRenderSystem());      // IRenderSystem  (fornito dall'engine)

// in Update(): input → simulation → late
foreach (var s in _inputSystems)      s.OnUpdate(_world, fixedDeltaTime);
foreach (var s in _simulationSystems) s.OnUpdate(_world, fixedDeltaTime);
foreach (var s in _lateSystems)       s.OnUpdate(_world, fixedDeltaTime);

// in Draw(): render (la camera serve al culling/ordinamento)
foreach (var s in _renderSystems) s.OnRender(_world, renderer, _camera, renderer.GetFrameTime());
```

---

## 6. Transform: locale, mondo, gerarchia

`TransformComponent` (`Ecs/Component/TransformComponent.cs`):

```csharp
public struct TransformComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;
}
```

Helper su singolo transform in `MathUtils/TransformExtensions.cs`:
`GetLocalMatrix()` (compone `Scale * Rotation * Translation`), `GetForward()`,
`GetRight()`, `GetUp()`.

### Gerarchia (parent/child)

Aggiungi un `ParentComponent` a un'entità per renderla **figlia**: da quel momento il
suo `TransformComponent` è interpretato come **locale rispetto al genitore**.

```csharp
var carro    = world.CreateEntity();
var torretta = world.CreateEntity();

world.AddComponent(torretta, new TransformComponent { Position = new Vector3(0, 2, 0), Rotation = Quaternion.Identity, Scale = Vector3.One });
world.AddComponent(torretta, new ParentComponent  { Parent = carro });
// muovi/ruota "carro": la torretta lo segue, senza toccare il suo transform locale.
```

La world matrix di un'entità si ottiene con l'extension su `World`
(`Ecs/Base/WorldTransforms.cs`):

```csharp
Matrix4x4 worldM = world.GetWorldMatrix(entity);
```

- Entità **root** (senza `ParentComponent`): `World == Local`.
- Entità **figlia**: `World = Local * GetWorldMatrix(parent)` — nota **il locale a
  sinistra** (convenzione row-vector di `System.Numerics`; §9).

Limiti attuali (vedi roadmap Fase 1): calcolo ricorsivo on-demand senza cache/dirty
flag, nessuna protezione dai cicli, e il `Parent` non è ancora esprimibile da file
scena (solo via codice).

---

## 7. Rendering

### `IRenderer` (il port)

`src/gEngine/Rendering/IRenderer.cs` è la facciata su tutto ciò che disegna. Il gioco
usa **solo** questo in `Draw`:

```csharp
public void Draw(IRenderer renderer)
{
    renderer.BeginFrame();
    renderer.ClearBackground(Color.White);

    renderer.DrawText("Hello", 20, 20, 30, Color.Black);   // UI 2D

    renderer.Begin3D(_camera);
    // ... disegno 3D (mesh) ...
    renderer.End3D();

    renderer.EndFrame();
}
```

`Color` (`Rendering/Color.cs`) è la struct colore dell'engine (niente
`Raylib_cs.Color`), con costanti (`White`, `Black`, `Red`, `Gray`, ...).

### Disegnare mesh: componente + system

Aggiungi un `MeshRendererComponent` (dati puri) accanto a un `TransformComponent`:

```csharp
public class MeshRendererComponent
{
    public MeshKind Kind;      // Cube | Plane | Grid | Model
    public Vector3 Size;
    public Color Tint;
    public bool Wireframe;
    public bool Visible;
    public RenderLayer Layer;  // Opaque | Transparent
    public int SortingOrder;   // ordine di disegno entro lo stesso layer
}
```

Il `MeshRenderSystem` (engine, `Ecs/System/MeshRenderSystem.cs`) fa il resto: interroga
`Query<TransformComponent, MeshRendererComponent>()`, calcola la world matrix
(gerarchia inclusa), costruisce un `DrawMeshCommand` e chiama `renderer.DrawMesh(...)`.
Parla solo con `IRenderer`, **mai** con raylib.

### Render order / layer

Il `MeshRenderSystem` non disegna nell'ordine della query: **raccoglie** tutti i
comandi, li **ordina** per `(Layer, SortingOrder)` e poi disegna. Quindi:

- `RenderLayer.Opaque` è disegnato prima di `RenderLayer.Transparent`.
- Entro lo stesso layer, `SortingOrder` più basso = disegnato prima.

Limite noto: i trasparenti **non** sono ancora ordinati per distanza dalla camera
(back-to-front). La camera è ormai disponibile in `OnRender`, quindi è un passo breve.

### Frustum culling

Il `MeshRenderSystem` costruisce ogni frame il `Frustum` della camera
(`MathUtils/Frustum.cs`, dalla view-projection) e **salta la draw call** delle entità la
cui bounding sphere è interamente fuori dal volume visibile. Il test è conservativo
(niente falsi negativi: un oggetto visibile non viene mai scartato). I bound sono
calcolati assumendo l'ingombro del cubo unitario — con il caricamento modelli serviranno
i bound reali della mesh.

### Luci (illuminazione + PBR semplice)

Le luci sono entità con `LightComponent` (+ `TransformComponent`): posizione e direzione
vengono dal transform (posizione per le `Point`, forward per le `Directional`).

```csharp
var sun = world.CreateEntity();
world.AddComponent(sun, new TransformComponent {
    Rotation = Quaternion.CreateFromYawPitchRoll(0.6f, -0.9f, 0f), Scale = Vector3.One });
world.AddComponent(sun, new LightComponent {
    Kind = LightKind.Directional, Color = Color.White, Intensity = 1.0f });
```

Il `LightingSystem` (un `IRenderSystem`, da registrare **prima** del `MeshRenderSystem`)
raccoglie le luci e le carica nel renderer, che le passa allo shader PBR
(`Shaders/lit.vs`+`lit.fs`, GGX + Lambert). Massimo `MAX_LIGHTS = 4`. Il colore del
material (`MeshRenderer.Tint`) è l'albedo; metallic/roughness sono globali per ora. Le
primitive `Plane`/`Grid` disegnate immediate-mode restano non illuminate.

### Camera

`Camera3D` (`Rendering/Camera3D.cs`): `Position`, `Target`, `Up`, `FovY`,
`Projection`. Per una camera di debug WASD + mouse look c'è
`FreeFlyCamera3DController` (`Rendering/Editor/`).

---

## 8. Scene da file (data-driven)

Una scena è un file JSON; il caricamento non conosce a priori i tipi di componente —
usa un **registry di binder** (`Scenes/`). Vedi `ROADMAP.md` Fase 3 per il razionale.

```csharp
// 1) registra i binder: built-in dell'engine + eventuali custom del gioco
var registry = new SceneComponentRegistry();
registry.RegisterEngineDefaults();  // Transform, MeshRenderer
registry.Register("Player",   data => data.Deserialize<PlayerComponent>(SceneJson.Options));
registry.Register("Velocity", data => data.Deserialize<VelocityComponent>(SceneJson.Options));

// 2) carica e istanzia (l'AssetManager serve ai binder che caricano risorse, es. ModelPath)
var scene = JsonSceneLoader.Load(path);      // Scene: nome + lista di EntityDefinition
SceneInstantiator.Instantiate(scene, _world, registry, assets);
```

L'istanziazione è a **due passate**: prima crea tutte le entità (mappando il campo
opzionale `"name"` → `Entity`), poi applica i componenti passando un `SceneBindContext`
(la mappa dei nomi + l'`AssetManager`). Questo abilita i binder che referenziano
**altre entità** o **risorse per path**:

- **Gerarchia**: `"Parent": "nomeGenitore"` su un'entità la rende figlia di quella con
  `"name": "nomeGenitore"` (il suo `Transform` diventa locale).
- **Modelli**: `"ModelPath": "models/x.gltf"` dentro `MeshRenderer` carica il modello via
  `AssetManager` e assegna l'handle (con `"Kind": "Model"`).

Formato JSON (bag chiave→dati grezzi, nessun campo fisso):

```json
{
  "name": "city",
  "entities": [
    { "components": {
        "Transform":    { "position": {"x":0,"y":0,"z":0}, "rotation": {...}, "scale": {"x":1,"y":1,"z":1} },
        "MeshRenderer": { "kind": "Cube", "tint": {...}, "visible": true }
    } }
  ]
}
```

I nuovi campi `Layer`/`SortingOrder` sono opzionali: se assenti valgono i default
(`Opaque`, `0`). I tipi math (`Vector3`, `Quaternion`, `Color`) hanno converter
dedicati (`Scenes/Json/`); gli enum sono stringhe (`"Cube"`, `"Transparent"`).
Un componente citato nel JSON ma senza binder registrato → errore **fail-fast**.

---

## 9. Convenzioni matematiche

- **Sistema right-handed, Y-up** (come Raylib).
- Math via **`System.Numerics`** (`Vector3`, `Quaternion`, `Matrix4x4`).
- Direzioni base: Forward = `+Z`, Right = `+X`, Up = `+Y`.
- **Matrici row-vector**: `Vector3.Transform(v, M)` calcola `v * M`. Conseguenze:
  - composizione locale = `Scale * Rotation * Translation`;
  - composizione gerarchica = `Local * ParentWorld` (**figlio a sinistra**, opposto del
    `Parent * Local` dei tutorial column-vector/OpenGL).
- ⚠️ `Raylib.DrawMesh` (P/Invoke nativo) vuole matrici **column-major**: la world matrix
  va **trasposta** prima della chiamata. Questa trasposizione vive dentro il
  `RayLibRenderer`, non nei system.

---

## 10. Input

Il gioco definisce un `GameActionContext` mappando binding (tasti/mouse) ad azioni
astratte (`GameAction`), poi lo attiva sull'`InputHandler` (§`Init`):

```csharp
var ctx = new GameActionContext();
ctx.AddToContext([new InputBinding { KKey = KeyboardKey.W }],       GameAction.MoveUp);
ctx.AddToContext([new InputBinding { MButton = MouseButton.Right }], GameAction.CameraFreeFly);
inputHandler.SetActiveContext(ctx);
```

`GameLoop` chiama `inputHandler.Update()` una volta per frame. I system di input
leggono lo stato delle azioni dall'`InputHandler` (che ricevono via costruttore). Il
vantaggio del livello d'astrazione: la logica ragiona su `GameAction.MoveUp`, non sul
tasto `W`, e il rebinding non tocca i system.

---

## 11. Asset & audio

`AssetManager` (`Assets/AssetManager.cs`) è **indipendente dalla libreria**: carica per
path (con cache: stesso file = un solo load) e restituisce **handle opachi**
(`TextureHandle`/`SoundHandle`/`MusicHandle`/`ModelHandle`), delegando tutto a un
`IAssetBackend`. Lo costruisce e lo possiede il `GameLoop` (che possiede gli adapter
raylib); il gioco lo **riceve** in `Init`:

```csharp
public void Init(InputHandler input, AssetManager assets)
{
    MusicHandle intro = assets.LoadMusicStream("audio/intro.mp3");
    assets.PlayMusic(intro);

    ModelHandle robot = assets.LoadModel("models/robot.glb");
    // poi su un'entità: meshRenderer.Kind = MeshKind.Model; meshRenderer.Model = robot;
}
// in Draw()/Update(): assets.UpdateMusic(intro);
// UnloadAll() lo chiama il GameLoop alla chiusura — non il gioco.
```

Gli handle sono solo id: `Id == 0` è `None` (handle non valido), e le operazioni su un
handle non valido sono no-op sicuri. Per cambiare libreria audio/grafica basta passare
un altro `IAssetBackend` al `GameLoop`: `AssetManager` non cambia. Il caso `MeshKind.Model`
è l'unico punto in cui i due adapter raylib (backend asset + renderer) condividono la
tabella modelli — vedi `RayLibRenderer`/`RayLibAssetBackend.TryGetModel`.

---

## 12. Fisica (3D, BepuPhysics)

Stesso schema ports & adapters: il port `IPhysicsWorld` (`Physics/`) è l'astrazione;
`BepuPhysicsWorld` è l'unico file che importa `BepuPhysics`. Un'entità diventa fisica
aggiungendole un `RigidBodyComponent` (dati puri):

```csharp
world.AddComponent(caisse, new RigidBodyComponent {
    Shape = ColliderShape.Box, Size = new Vector3(1, 1, 1), Mass = 1f, IsStatic = false });
```

Il gioco crea il mondo fisico e registra il `PhysicsSystem` (un `ISimulationSystem`,
gira nel fixed-step):

```csharp
IPhysicsWorld physics = new BepuPhysicsWorld(new Vector3(0, -9.81f, 0));
AddSystem(new PhysicsSystem(physics));
// in Shutdown(): physics.Dispose();
```

Ogni update il `PhysicsSystem`: (1) crea i corpi mancanti (e aggiunge un
`PhysicsBodyComponent` come link runtime), (2) avanza la simulazione, (3) riscrive la
posa dei corpi **dinamici** nel `TransformComponent`. I corpi `IsStatic` non si muovono
(pavimenti/muri). Collider oggi: box e sphere. Raycast/capsule/mesh: rimandati.

## 13. Logging

`ILogger` + `ConsoleLogger` (`Log/`) con livelli (Debug/Info/Warn/Error), timestamp e
categoria. `GameLoop` istanzia un logger internamente; l'esposizione comoda a
`IGame`/ai system è ancora da completare (vedi `ROADMAP.md` Fase 0).
