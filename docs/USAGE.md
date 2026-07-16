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
    void Init(Resources resources);                        // una volta, prima del loop
    void Update(float fixedDeltaTime, InputHandler input); // 0..N volte per frame (fixed step)
    void Draw(IRenderer renderer);                         // una volta per frame
    void Shutdown();                                       // alla chiusura
}
```

`GameLoop` possiede la finestra, l'audio device, il `RayLibRenderer` e l'`AssetManager`
(tutti costruiti dopo `InitWindow`), li registra nelle **`Resources`** (§4.5) e passa il
contenitore a `Init`. **Il gioco non tocca raylib per disegnare né costruisce gli adapter
raylib.**

```csharp
public void Init(Resources resources)
{
    var input  = resources.Get<InputHandler>();
    var assets = resources.Get<AssetManager>();
    // Ciò che crea il gioco, lo dichiara il gioco:
    resources.Add<IPhysicsWorld>(new BepuPhysicsWorld(new Vector3(0, -9.81f, 0)));
}
```

`Init` riceve il contenitore invece di una lista di parametri perché quella lista cresceva
a ogni servizio nuovo. `Update`/`Draw` invece continuano a ricevere `InputHandler`/`IRenderer`
**espliciti**, apposta: sono richiesti *sempre*, e il parametro lo dichiara nel tipo —
pescarli con `Get<T>()` fallirebbe a runtime invece che a compile time. È la stessa istanza:
la Resource è il punto di verità, il parametro è comodità.

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

## 4.5 Resources: l'infrastruttura, fuori dal World

La regola dell'engine, in una riga:

> **I dati di scena vivono nel World come Component. L'infrastruttura è una Resource
> registrata esplicitamente.**

Se una cosa ha una posa, appartiene a un'entità o va salvata nel file scena, è un
**Component**: camera, luce, mesh renderer, rigid body. Se è un servizio che il gioco usa ma
che non è "roba nella scena", è una **Resource**: il renderer, l'`AssetManager`, il mondo
fisico, l'`InputHandler`.

```csharp
resources.Add<IPhysicsWorld>(new BepuPhysicsWorld(new Vector3(0, -9.81f, 0)));

var physics = resources.Get<IPhysicsWorld>();          // fail-fast se manca
if (resources.TryGet<SystemRegistry>(out var systems)) { /* opzionale */ }
```

⚠️ La chiave è **`typeof(T)`**, non il tipo concreto: registra sempre con il tipo della
**porta** (`Add<IPhysicsWorld>(...)`). Con l'inferenza finirebbe sotto `BepuPhysicsWorld` e
nessuna lettura per porta lo troverebbe mai. Stessa trappola con i campi nullable: si
registrerebbe sotto `IRenderer?`.

**Perché il renderer non è un Component**, visto che "tutto è un'entità" sarebbe più elegante:
`SceneSerializer` scorre gli storage del World e scrive ciò che trova, quindi un
`RendererComponent` finirebbe dentro il tuo `.json`. L'unico modo per evitarlo sarebbe
marcarlo `[RuntimeState]` — cioè ammettere che non è dato d'autore. È lo split di Bevy
(`Component`/`Resource`), e vale anche al contrario: la camera **di gioco** è un'entità
(dato d'autore), la camera **di scena dell'editor** no (stato dell'editor). Vedi
`ROADMAP.md` Fase 4.5.

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

I system li possiede il **`SystemRegistry`** (`Ecs/SystemRegistry.cs`): un solo `Add`, che
smista il system su tutte le fasi che implementa e chiama `OnCreate` una volta sola.

```csharp
private readonly SystemRegistry _systems;   // new SystemRegistry(_world)

// in Init(): niente overload, la fase la deduce dalle interfacce implementate
_systems.Add(new PlayerInputSystem(input));   // IInputSystem
_systems.Add(new MovementSystem());           // ISimulationSystem
_systems.Add(new CameraFollowSystem());       // ILateSystem
_systems.Add(new LightingSystem());           // IRenderSystem — PRIMA di MeshRenderSystem
_systems.Add(new MeshRenderSystem());         // IRenderSystem

// in Update(): il World lo tiene il registry, tu passi solo il tempo
_systems.RunInput(fixedDeltaTime);
_systems.RunSimulation(fixedDeltaTime);
_systems.RunLate(fixedDeltaTime);

// in Draw(): la camera serve al culling/ordinamento
_systems.RunRender(renderer, camera, renderer.GetFrameTime());
```

Si chiama **Registry e non Scheduler** apposta: non ordina niente. Le fasi sono fisse, ma
dentro una fase vale l'**ordine di registrazione** — che devi ancora conoscere tu.

⚠️ `LightingSystem` va aggiunto **prima** di `MeshRenderSystem`, altrimenti le uniform delle
luci arrivano dopo le mesh che dovevano illuminare.

⚠️ Un system che implementa **due** fasi di update si vede chiamare `OnUpdate` **due volte**
per tick, una per fase: le interfacce condividono lo stesso metodo. È voluto — è cosa
significa stare in due fasi — ma è una trappola se l'hai fatto per sbaglio.

Fail-fast: un `ISystem` che non implementa **nessuna** interfaccia di fase è un errore
(non girerebbe mai, in silenzio), e registrare due volte la stessa istanza pure.

### Traceability: chi agisce su questa entità

Ogni system può **dichiarare** i componenti su cui agisce. È ciò che permette all'Inspector
di rispondere a "perché questa entità non si muove?" senza far girare niente:

```csharp
public IReadOnlyList<Type> MatchedComponents { get; } =
    [typeof(TransformComponent), typeof(VelocityComponent)];
```

È un *default interface member* con default vuoto: i system esistenti non si rompono, ma
chi non dichiara resta **`SystemMatch.Unknown`** — "non si sa", che l'UI mostra come `?` e
non come "no".

Ci sono **due verbi**, e la differenza conta:

```csharp
// Su chi il system AGISCE (l'insieme che decide chi processa):
public IReadOnlyList<Type> MatchedComponents { get; } = [typeof(CameraComponent), typeof(TransformComponent)];

// Cosa GUARDA senza toccarlo (opzionale, default vuoto):
public IReadOnlyList<Type> ObservedComponents { get; } = [typeof(PlayerComponent), typeof(TransformComponent)];
```

Il caso reale è `CameraFollowSystem`: **agisce** sulla camera, ma **legge** il player. Senza
il secondo verbo, chi si chiede "perché la camera non mi segue?" guarda il player e non trova
il system. Si interrogano con `SystemsActingOn(entity)` e `SystemsObserving(entity)`.

Si chiama `Observed` e non `Read` perché "legge" non distingue niente: un system legge *anche*
i componenti che matcha. Dichiaralo **solo** se il system guarda entità che non tocca — una
dichiarazione inventata è peggio di nessuna (nel progetto ce n'è **una sola**).

⚠️ Entrambe le liste sono **metadata scritta a mano**: possono mentire e andare fuori sincrono
col codice. `ObservedComponents` è anzi *meno* affidabile perché è opzionale — una sezione
vuota non prova che nessuno legga l'entità. È un aiuto diagnostico, **non una prova**.

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

Una camera **d'autore** è un'entità: `CameraComponent` (solo l'ottica — `FovY`, `Near`,
`Far`, `Projection`, `Primary`) più il `TransformComponent`, che dà la **posa**. È il
modello Unity: la camera non duplica `Position`/`Target`, li eredita dal suo Transform
(gerarchia inclusa). Così finisce nel file scena, si seleziona nella Hierarchy e si muove
col gizmo come qualunque altra entità.

```csharp
var camera = world.CreateEntity();
world.AddComponent(camera, new TransformComponent { Position = new Vector3(0, 18, 28) });
world.AddComponent(camera, new CameraComponent { FovY = 60f, Primary = true });

// La camera "risolta" per disegnare/proiettare: derivata da Transform + CameraComponent.
// Entrambe restituiscono Camera3D? — null se l'entità non è una camera / se non ce n'è nessuna.
Camera3D? cam     = world.GetCamera(camera);
Camera3D? primary = world.GetPrimaryCamera();   // la Primary, con fallback sulla prima
```

`Camera3D` (`Rendering/Camera3D.cs`) resta la camera **risolta**, cioè l'oggetto di
matematica pura (`GetViewMatrix`, `GetRay`, `WorldToViewport`, frustum): non si crea a mano
per il gioco, la si **deriva** dal World. Va letta **per frame** — è un valore calcolato,
non un oggetto persistente a cui restare agganciati.

⚠️ «Esiste sempre una camera» non è un invariante: da quando l'editor può cancellare entità,
`GetPrimaryCamera` può fallire e chi disegna deve degradare, non crashare.

La camera con cui si **naviga nell'editor** è un'altra cosa e sta apposta fuori dal World
(`EditorHost.SceneCamera`, mossa dal `FreeFlyCamera3DController` in `Rendering/Editor/`): è
stato dell'editor, non dato di scena. Vedi `ROADMAP.md` Fase 4.5.

---

## 8. Scene da file (data-driven)

Una scena è un file JSON; il caricamento non conosce a priori i tipi di componente —
usa un **registry di binder** (`Scenes/`). Vedi `ROADMAP.md` Fase 3 per il razionale.

```csharp
// 1) registra i binder: built-in dell'engine + eventuali custom del gioco
var registry = new SceneComponentRegistry();
registry.RegisterEngineDefaults();  // Transform, MeshRenderer, Light, RigidBody, Camera, Parent

// createDefault è opzionale e serve all'EDITOR: senza, il componente si salva e si carica
// ma "Aggiungi componente" non sa come crearne uno (vedi §8.1).
registry.Register("Player", data => data.Deserialize<PlayerComponent>(SceneJson.Options),
    createDefault: () => new PlayerComponent { Name = string.Empty });
registry.Register("Velocity", data => data.Deserialize<VelocityComponent>(SceneJson.Options),
    createDefault: () => new VelocityComponent { Velocity = Vector3.Zero });

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

### 8.1 Rendere un componente aggiungibile dall'editor

Il registry non serve solo al file: è anche **l'elenco dei tipi di componente che questo
gioco ha**, ed è da lì che l'editor prende la lista di "Aggiungi componente". Perché una
factory dichiarata e non un `default(T)` automatico: vedi `ROADMAP.md` Fase 4.7 — in breve,
i componenti sono struct di dati nudi e `default(T)` è un default *rotto* (Transform con
`Scale = 0` è invisibile, Light con `Intensity = 0` non illumina), quindi l'utente vedrebbe
"componente aggiunto" e nessun effetto.

```csharp
registry.Register("Velocity", data => data.Deserialize<VelocityComponent>(SceneJson.Options),
    createDefault: () => new VelocityComponent { Velocity = Vector3.Zero });
```

- **Il default va scelto perché si veda**, non perché compili: è il criterio con cui sono
  scritti quelli dell'engine (`RegisterEngineDefaults`).
- ⚠️ Se il componente è una **class** (come `MeshRendererComponent`), la factory deve
  costruirne uno **nuovo a ogni chiamata**: restituire un'istanza condivisa farebbe editare
  lo stesso oggetto a tutte le entità che l'aggiungono.
- Senza `createDefault` il componente **resta nell'elenco dell'editor ma spento, col
  motivo**: è il caso di `Parent`, dove è voluto (un genitore di default non esiste — ci si
  riparenta dalla Hierarchy).
- ⚠️ `NameComponent` **non** è registrato: nel file il nome è il campo `name` dell'entità,
  non un componente. Il rovescio è che l'editor non sa aggiungerlo.

Perché l'editor lo veda, il registry va **dichiarato fra le Resources** (come il
`SystemRegistry`, e per lo stesso motivo — vedi §4.5):

```csharp
resources.Add(registry);
```

### 8.2 Campi che puntano a un asset

Un campo che tiene un handle (`ModelHandle`) **non** va esposto con
`[EditorConfiguration]`: l'handle è un id di cache valido solo per questa esecuzione, e
mostrarlo darebbe da editare un numero che al riavvio punta a un modello a caso. Il dato
d'autore è il **path**. Si marca invece con `[EditorAsset]`, e l'editor mostra uno **slot**
in cui trascinare un file dal pannello File system:

```csharp
[EditorAsset(AssetKind.Model)] public ModelHandle Model;
```

Il tipo dichiarato **è** la validazione: un `.mp3` sopra uno slot `Model` non si può proprio
lasciar cadere. ⚠️ Lo slot non conosce gli altri campi del componente: assegnare un modello a
un `MeshRenderer` con `Kind = Cube` riempie il campo e non cambia cosa si vede (il tooltip
dello slot lo dice).

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
public void Init(Resources resources)
{
    var assets = resources.Get<AssetManager>();

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
resources.Add(physics);              // chi lo crea lo dichiara: qui il mondo fisico è del gioco
_systems.Add(new PhysicsSystem(physics));
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
