# gEngine — Roadmap (3D)

## Fase 0 — Fondamenta & igiene 🟢

Piccole cose che userai ovunque, meglio averle prima.

- [x] **Logging** a livelli (Debug/Info/Warn/Error)
  - [x] Interfaccia `ILogger` + implementazione console (`ConsoleLogger`, `LogLevel`, `LogMessage`, `LogCategories`)
  - [x] Timestamp e categoria/tag per messaggio
  - [ ] Punto d'accesso comodo dall'engine — `GameLoop` istanzia `_logger` ma non lo passa mai a `IGame`/ai system; da esporre (es. via `Init`) prima di poterlo usare fuori da `GameLoop`
- [ ] **Unit test** sull'ECS (primo contatto col testing in C#)
  - [ ] Progetto di test (`tests/gEngine.Tests`, xUnit)
  - [ ] Test su `CreateEntity`, `AddComponent`/`GetComponent`, `Query<..>`
  - [ ] Test sul gotcha struct/copia (mutazione + write-back)
- [x] **Adozione math**: standardizzare su `System.Numerics`
  - [ ] Decidere convenzioni: sistema **right-handed, Y-up** (come Raylib) — implicito nell'uso attuale, non ancora scritto da nessuna parte
  - [ ] Note su unità (1 unità = 1 metro?) e scala di riferimento

**Milestone:** log leggibile a runtime + test verdi. *(log ✅, test ancora da scrivere)*

---

## Fase 1 — Transform 3D & math 🟢🟡

Il cuore matematico, già in ottica 3D.

- [x] **`TransformComponent`** che sostituisce `PositionComponent`
  - [x] `Position : Vector3`
  - [x] `Rotation : Quaternion` (in 2D useresti solo la Z)
  - [x] `Scale : Vector3`
- [x] **World matrix** dal transform (`src/gEngine/MathUtils/TransformExtensions.cs`)
  - [x] Comporre `Matrix4x4 = Scale * Rotation * Translation` — `GetLocalMatrix()`
    (extension method su `TransformComponent`; il componente resta dati puri)
  - [x] Helper per direzioni (forward/right/up) dal quaternion —
    `GetForward()`/`GetRight()`/`GetUp()` via `Vector3.Transform(UnitZ/UnitX/UnitY, Rotation)`;
    convenzione base fissata: Forward = `+UnitZ`, Right = `+UnitX`, Up = `+UnitY`
  - ⚠️ Nota: il namespace è `gEngine.MathUtils`, **non** `gEngine.Math` — quest'ultimo
    farebbe shadowing di `System.Math` in tutto il progetto
- [x] **Migrazione** del codice esistente da `Position` a `Transform`
  - [x] Aggiornare sample e sistemi (`Sandbox` usa già `TransformComponent`)
  - [x] Ricordare il **write-back** dopo la mutazione (struct = copia) — pattern
    seguito in `MovementSystem` (bug del write-back parziale che azzerava
    `Scale`/`Rotation` individuato e corretto)
- [ ] *(rimandabile)* **Gerarchia di transform** (parent/child)
  - [ ] `Parent`/figli, transform locale vs mondo
  - [ ] Ricalcolo world matrix (dirty flag)

**Milestone:** entità con posizione/rotazione/scala 3D reali. ✅

---

## Fase 2 — Fondamenta rendering 3D 🟢🟡

Da "il gioco disegna a mano" a "un sistema disegna la scena". Il punto
chiave: l'ECS non deve chiamare raylib direttamente, altrimenti cambiare
backend in futuro significa riscrivere i system. Si applica lo stesso
pattern "ports & adapters" già usato da `Camera3D` (`ToRaylibCamera3D()` è
l'unico punto di conversione): un'astrazione `IRenderer` nell'engine, con
un'unica implementazione concreta `RaylibRenderer` che è l'unico file a
importare `Raylib_cs`.

- [x] **Wrapper `Camera3D`**
  - [x] Position / Target / Up / FovY / Projection (perspective)
  - [x] Conversione verso `Raylib_cs.Camera3D`
  - [x] `BeginMode3D`/`EndMode3D` nel passo di rendering
- [x] **Camera di debug free-fly / orbit** (fondamentale per iterare in 3D)
  - [x] Movimento WASD + mouse look, oppure orbit attorno a un target
- [x] **Astrazione renderer (`src/gEngine/Rendering/`)**
  - [x] `Color.cs` — struct `Color(byte R,G,B,A)` con costanti statiche
    (White, Black, Red, Gray, DarkGray, LightGray); sostituisce
    `Raylib_cs.Color` in component/system engine-side
  - [x] `MeshKind.cs` — `enum { Cube, Plane, Grid, Model }`
  - [x] `DrawMeshCommand.cs` — `readonly record struct DrawMeshCommand(MeshKind Kind, Matrix4x4 World, Vector3 Size, Color Tint, bool Wireframe)`
    — porta la **world matrix** completa (scala/rotazione/posizione) invece della sola
    `Position`; `Size` resta usato solo dai path immediate-mode `Plane`/`Grid`
  - [x] `IRenderer.cs` — facade su tutto ciò che oggi chiama raylib nei
    sample: `BeginFrame`, `EndFrame`, `Begin3D(Camera3D)`, `End3D`,
    `DrawMesh(in DrawMeshCommand)` (unico ingresso per le primitive:
    Cube/Plane/Grid/Model), `DrawText`, `DrawRectangle`/`DrawRectanglePro`,
    `GetScreenHeight/Width`, `GetFrameTime`/`GetTime`
  - [x] `RayLibRenderer.cs` — unica implementazione di `IRenderer`,
    converte `Color` → `Raylib_cs.Color` e chiama `Raylib.*`; `DrawMesh`
    dispatcha su `MeshKind` (`Cube`/`Plane`/`Grid` implementati, `Model`
    lancia `NotSupportedException` finché non c'è caricamento modelli)
    - [x] `Cube`: genera **una volta** una mesh cubo unitaria (`GenMeshCube(1,1,1)`)
      + material di default nel costruttore (riusati ogni frame, colore mutato via
      `Maps[Albedo].Color`), poi `Raylib.DrawMesh(mesh, material, world)`; il
      `Wireframe` ridisegna la stessa mesh con `Rlgl.EnableWireMode()`
    - [x] `Shutdown()` libera mesh/material (chiamato da `GameLoop` prima di
      `CloseWindow`); il progetto richiede `AllowUnsafeBlocks` per scrivere
      `Material.Maps` (puntatore nativo)
    - ⚠️ Nota appresa: `Raylib.DrawMesh` è un P/Invoke diretto e vuole matrici
      **column-major** (layout raylib), mentre `System.Numerics.Matrix4x4` è
      row-major → la world matrix va **trasposta** prima della chiamata nativa
      (verificato: `raylib == transpose(numerics)`)
    - ⚠️ Nota: il wireframe della mesh mostra anche le **diagonali** delle facce
      (il cubo è triangolato, 2 triangoli per faccia) — diverso dai 12 spigoli
      del vecchio `DrawCubeWires`
  - [x] `IGame.Draw()` diventa `Draw(IRenderer renderer)`; `GameLoop`
    costruisce il `RayLibRenderer` (dopo `InitWindow`) e lo possiede —
    i giochi non toccano più raylib direttamente in `Draw`
- [x] **Componenti disegnabili**
  - [x] `MeshRendererComponent` (`src/gEngine/Ecs/Component/`) — dati puri:
    `MeshKind Kind; Vector3 Size; Color Tint; bool Wireframe; bool Visible`
    (niente riferimenti a raylib). *(attualmente `class`, non `struct`)*
- [x] **`RenderSystem`**
  - [x] `IRenderSystem` (`src/gEngine/Ecs/Interfaces/System/`) —
    `interface IRenderSystem : ISystem { void OnRender(World world, IRenderer renderer, float frameDt); }`,
    distinto da `IInputSystem`/`ISimulationSystem`/`ILateSystem` perché
    gira ogni frame in `Draw()`, non nel fixed-step `Update()`
  - [x] `MeshRenderSystem` (`src/gEngine/Ecs/System/`) — implementazione di
    default fornita dall'engine: `world.Query<TransformComponent, MeshRendererComponent>()`
    → `renderer.DrawMesh(...)` (salta se `!Visible`). Parla solo con
    `IRenderer`, mai con raylib
  - [x] Applica la world matrix: `Matrix4x4.CreateScale(Size) * transform.GetLocalMatrix()`
    per ogni entità, passata dentro il `DrawMeshCommand`
  - [x] Depth test e back-face culling (default Raylib)
  - [x] `SandboxGame` ora crea i 6 edifici + il player come **entità** con
    `Transform`+`MeshRenderer` (in `Init()`), e in `Draw()` itera i
    `_renderSystems` invece di disegnare i building a mano; la creazione
    delle entità resta nel gioco (che conosce la scena), **non** nel system
- [ ] **Render order / layer** (opachi vs trasparenti)
- [ ] Ambientazione statica (grid/plane del pavimento) resta un
  `DrawMeshCommand` costruito al volo dal gioco e passato a
  `renderer.DrawMesh(...)`, **non** un'entità ECS — non c'è ancora un caso
  d'uso per trattarla come dato di scena

**Milestone:** una scena di cubi/sfere navigabile con camera 3D. 🎉 ✅
*(pipeline mesh via ECS completata; restano da fare solo `render order`/layer
opachi-vs-trasparenti)*

---

## Fase 3 — Scene management & serializzazione 🟢 *(fase pivot)*

Rendere la scena un **file**. Precondizione dell'editor.

Vincolo di design: la scena **non** può conoscere a priori quali tipi di
componente esistono. Se il formato avesse campi fissi (es. un
`EntityDefinition` con proprietà `Transform`/`MeshRenderer` hardcoded), un
componente custom definito fuori dall'engine (es. `PlayerComponent`/
`VelocityComponent` in Sandbox) non potrebbe mai comparire in una scena
senza modificare il loader dentro l'engine. Si usa invece un **registry di
binder per tipo**, tenuto data-driven end-to-end:

- [x] **Registry dei componenti** (`src/gEngine/Scenes/`, no reflection —
  registrazione esplicita)
  - [x] `IComponentBinder.cs` — `interface IComponentBinder { void Apply(World world, Entity entity, JsonElement data); }`
  - [x] `ComponentBinder<T>.cs` — `class ComponentBinder<T>(Func<JsonElement, T> parse) : IComponentBinder`,
    applica il componente con `world.AddComponent(entity, parse(data))`
  - [x] `SceneComponentRegistry.cs` — `Dictionary<string, IComponentBinder>`
    con `Register<T>(string key, Func<JsonElement, T> parse)` e
    `bool TryGet(string key, out IComponentBinder binder)`; la chiave è il nome
    usato nel JSON (`"Transform"`, `"MeshRenderer"`, `"Player"`, `"Velocity"`, ...)
  - [x] L'engine registra i propri built-in (`Transform`, `MeshRenderer`)
    via `SceneComponentRegistry.RegisterEngineDefaults()`; **Sandbox
    estende lo stesso registry** con i propri componenti custom in
    `SandboxGame.Init()`, prima di caricare la scena — l'estensibilità
    vive fuori dall'engine, senza toccare `gEngine.Scenes`
- [x] **Serializzazione** (JSON con `System.Text.Json`)
  - [x] `Scene.cs` — `class Scene { string Name; List<EntityDefinition> Entities; }`,
    `class EntityDefinition { Dictionary<string, JsonElement> Components; }`
    — nessun campo fisso, solo un bag chiave→dati grezzi
  - [x] `JsonSceneLoader.cs` — `Scene Load(string path)`, deserializza
    `{ name, entities: [ { components: { "Transform": {...}, "MeshRenderer": {...} } } ] }`
    senza interpretare i valori (restano `JsonElement` finché un binder
    non li legge)
  - [x] Converter per `Vector3` / `Quaternion` / `Color` (`src/gEngine/Scenes/Json/`)
    nel formato "a dizionario" (`{"x":..,"y":..,"z":..}`), più
    `SceneJson.Options` condivise (`IncludeFields=true`, enum come stringa
    via `JsonStringEnumConverter`) usate sia dai built-in sia dai custom
- [x] **`SceneInstantiator`** (sostituisce il concetto di `SceneManager`
  per il caricamento — attivazione/switch di scena resta da valutare
  quando servirà davvero più di una scena)
  - [x] `SceneInstantiator.cs` — `static void Instantiate(Scene scene, World world, SceneComponentRegistry registry)`:
    per ogni entity crea `world.CreateEntity()`, poi per ogni coppia
    `(key, data)` cerca il binder nel registry e applica il componente.
    Non conosce nessun tipo di componente specifico: è totalmente generico.
    Binder mancante → **fail-fast** (`InvalidOperationException`), non skip silenzioso
- [x] **Refactor** del gioco
  - [x] `SandboxGame` **carica la scena da file** (`JsonSceneLoader` +
    `SceneInstantiator`) invece di costruire l'array `Buildings` in codice
  - [x] `samples/Sandbox/assets/scenes/city.json` — scena d'esempio
    versionata, con `Player`/`Velocity` custom nello stesso file a dimostrare
    che i componenti di Sandbox funzionano data-driven. *(scena attuale:
    "crown-city", 12 torri ad anello + 4 pilastri ruotati + player — anche
    banco di prova delle rotazioni via quaternione)*

**Milestone:** modifichi un file `.json` e la scena cambia senza ricompilare. ✅

---

## Fase 4 — Editor MVP 🔴 

UI immediate-mode dentro la finestra Raylib.

- [ ] **Integrazione ImGui**
  - [ ] `ImGui.NET` + `rlImGui-cs` agganciati al loop
  - [ ] Docking/layout base dei pannelli
- [ ] **Pannello Hierarchy**
  - [ ] Lista entità della scena attiva, selezione
  - [ ] Crea/duplica/elimina entità
- [ ] **Pannello Inspector** (reflection-driven)
  - [ ] Mostra i componenti dell'entità selezionata
  - [ ] Editing campi Transform (position/rotation/scale)
  - [ ] Editing generico dei campi (float/int/bool/Vector3/enum)
  - [ ] **Aggiungi/Rimuovi componente** da UI
- [ ] **Viewport & manipolazione**
  - [ ] Rendering della scena nel viewport dell'editor
  - [ ] **Picking** (clic per selezionare un'entità)
  - [ ] **Gizmi** move/rotate/scale (valuta **ImGuizmo.NET**) 🔴
- [ ] **Persistenza**
  - [ ] Bottoni Save/Load scena
- [ ] **Play/Stop**
  - [ ] Esegui i system dentro l'editor, con pausa/stop

**Milestone:** aggiungi entità, modifichi transform e componenti e salvi, **senza toccare codice**. 🏆

---

## Fase 5 — Profondità 3D: asset, materiali, luci, fisica 🔴

Da "cubi colorati" a "scena 3D vera".

- [ ] **Caricamento modelli**
  - [ ] Import glTF/OBJ (`LoadModel`)
  - [ ] Gestione texture/material dei modelli
  - [ ] Integrazione con l'AssetManager (cache, unload)
- [ ] **Materiali & shader**
  - [ ] Material con colore/albedo/texture
  - [ ] Shader base (Blinn-Phong o PBR minimale)
- [ ] **Illuminazione**
  - [ ] Luce direzionale (sole) + luci punto
  - [ ] *(avanzato)* ombre
- [ ] **Fisica 3D → BepuPhysics v2** (Aether è 2D-only)
  - [ ] Rigid body + collider (box/sphere/capsule/mesh)
  - [ ] Sync mondo fisico ⇄ `Transform` dell'ECS
  - [ ] Raycast (utile anche per il picking dell'editor)
- [ ] **Frustum culling** (non disegnare ciò che è fuori camera)

**Milestone:** modelli importati, illuminati e con fisica 3D.

---

## Fase 6 — Qualità & avanzato 🔴

Il "poi" che rende l'engine piacevole da usare.

- [ ] **Undo/Redo** nell'editor (command pattern)
- [ ] **Prefab** (template di entità istanziabili)
- [ ] **Asset browser** + **hot reload**
- [ ] **Animazione**
  - [ ] Skeletal animation per modelli 3D
  - [ ] State machine / blending
- [ ] **Particelle**
- [ ] **Profiling / stats overlay** (FPS, draw call, ms per sistema)
- [ ] **Perf ECS**: valutare storage ad **archetipi / array densi** se serve
- [ ] **Audio 3D** (sfx, canali, spatial audio)

---

## Librerie di riferimento

| Ambito | Scelta consigliata | Note |
|--------|--------------------|------|
| Finestra/render/audio | **Raylib-cs** (già in uso) | `Camera3D`, `Model`, `Mesh`, shader |
| Math | **System.Numerics** | `Vector3`/`Quaternion`/`Matrix4x4`, SIMD, zero dipendenze |
| Serializzazione | **System.Text.Json** | converter custom per i tipi math |
| UI editor | **ImGui.NET** + **rlImGui-cs** | immediate-mode dentro Raylib |
| Gizmi 3D | **ImGuizmo.NET** | move/rotate/scale handles |
| Fisica 3D | **BepuPhysics v2** | sostituisce Aether (2D) |
| Test | **xUnit** | progetto `tests/` |

---

## Riepilogo milestone

1. ~~**Fase 2** → scena di primitive navigabile in 3D.~~ ✅
2. ~~**Fase 3** → scena caricata da file (data-driven).~~ ✅
3. **Fase 4** → editor: hierarchy + inspector + gizmi + save/load 
4. **Fase 5** → modelli, luci e fisica 3D reali.
