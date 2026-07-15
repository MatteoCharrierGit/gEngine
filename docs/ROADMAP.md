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
- [ ] **World matrix** dal transform
  - [ ] Comporre `Matrix4x4 = Scale * Rotation * Translation`
  - [ ] Helper per direzioni (forward/right/up) dal quaternion
- [x] **Migrazione** del codice esistente da `Position` a `Transform`
  - [x] Aggiornare sample e sistemi (`Sandbox`, `gEngine.Sample` usano già `TransformComponent`)
  - [x] Ricordare il **write-back** dopo la mutazione (struct = copia) — pattern già seguito in `MovementSystem`/`SampleGame`
- [ ] *(rimandabile)* **Gerarchia di transform** (parent/child)
  - [ ] `Parent`/figli, transform locale vs mondo
  - [ ] Ricalcolo world matrix (dirty flag)

**Milestone:** entità con posizione/rotazione/scala 3D reali. ✅

---

## Fase 2 — Fondamenta rendering 3D 🟡🔴

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
- [ ] **Astrazione renderer (`src/gEngine/Rendering/`)**
  - [x] `Color.cs` — struct `Color(byte R,G,B,A)` con costanti statiche
    (White, Black, Red, Gray, DarkGray, LightGray); sostituisce
    `Raylib_cs.Color` in component/system engine-side
  - [x] `MeshKind.cs` — `enum { Cube, Plane, Grid, Model }`
  - [x] `DrawMeshCommand.cs` — `readonly record struct DrawMeshCommand(MeshKind Kind, Vector3 Position, Vector3 Size, Color Tint, bool Wireframe)`
  - [x] `IRenderer.cs` — facade su tutto ciò che oggi chiama raylib nei
    sample: `BeginFrame`, `EndFrame`, `Begin3D(Camera3D)`, `End3D`,
    `DrawMesh(in DrawMeshCommand)` (unico ingresso per le primitive:
    Cube/Plane/Grid/Model), `DrawText`, `DrawRectangle`/`DrawRectanglePro`,
    `GetScreenHeight/Width`, `GetFrameTime`/`GetTime`
  - [x] `RayLibRenderer.cs` — unica implementazione di `IRenderer`,
    converte `Color` → `Raylib_cs.Color` e chiama `Raylib.*`; `DrawMesh`
    dispatcha su `MeshKind` (`Cube`/`Plane`/`Grid` implementati, `Model`
    lancia `NotSupportedException` finché non c'è caricamento modelli)
  - [ ] `IGame.Draw()` diventa `Draw(IRenderer renderer)`; `GameLoop`
    costruisce il `RayLibRenderer` (dopo `InitWindow`) e lo possiede —
    i giochi non toccano più raylib direttamente in `Draw`
- [ ] **Componenti disegnabili**
  - [ ] `MeshRendererComponent` (`src/gEngine/Ecs/Component/`) — struct
    dati puri: `MeshKind Kind; Vector3 Size; Color Tint; bool Wireframe; bool Visible`
    (niente riferimenti a raylib)
- [ ] **`RenderSystem`**
  - [ ] `IRenderSystem` (`src/gEngine/Ecs/Interfaces/System/`) —
    `interface IRenderSystem : ISystem { void OnRender(World world, IRenderer renderer, float frameDt); }`,
    distinto da `IInputSystem`/`ISimulationSystem`/`ILateSystem` perché
    gira ogni frame in `Draw()`, non nel fixed-step `Update()`
  - [ ] `MeshRenderSystem` (`src/gEngine/Ecs/System/`) — implementazione di
    default fornita dall'engine: `world.Query<TransformComponent, MeshRendererComponent>()`
    → `renderer.DrawMesh(...)`. È il "system che cicla per mesh render
    component", ma parla solo con `IRenderer`, mai con raylib
  - [ ] Applica la world matrix e chiama `DrawModel`/`DrawMesh`
  - [ ] Depth test e back-face culling (default Raylib)
- [ ] **Render order / layer** (opachi vs trasparenti)
- [ ] Ambientazione statica (grid/plane del pavimento) resta un
  `DrawMeshCommand` costruito al volo dal gioco e passato a
  `renderer.DrawMesh(...)`, **non** un'entità ECS — non c'è ancora un caso
  d'uso per trattarla come dato di scena

**Milestone:** una scena di cubi/sfere navigabile con camera 3D. 🎉

---

## Fase 3 — Scene management & serializzazione 🔴 *(fase pivot)*

Rendere la scena un **file**. Precondizione dell'editor.

Vincolo di design: la scena **non** può conoscere a priori quali tipi di
componente esistono. Se il formato avesse campi fissi (es. un
`EntityDefinition` con proprietà `Transform`/`MeshRenderer` hardcoded), un
componente custom definito fuori dall'engine (es. `PlayerComponent`/
`VelocityComponent` in Sandbox) non potrebbe mai comparire in una scena
senza modificare il loader dentro l'engine. Si usa invece un **registry di
binder per tipo**, tenuto data-driven end-to-end:

- [ ] **Registry dei componenti** (`src/gEngine/Scenes/`, no reflection —
  registrazione esplicita)
  - [ ] `IComponentBinder.cs` — `interface IComponentBinder { void Apply(World world, Entity entity, JsonElement data); }`
  - [ ] `ComponentBinder<T>.cs` — `class ComponentBinder<T>(Func<JsonElement, T> parse) : IComponentBinder`,
    applica il componente con `world.AddComponent(entity, parse(data))`
  - [ ] `SceneComponentRegistry.cs` — `Dictionary<string, IComponentBinder>`
    con `Register<T>(string key, Func<JsonElement, T> parse)` e
    `TryGet(string key, out IComponentBinder)`; la chiave è il nome usato
    nel JSON (`"Transform"`, `"MeshRenderer"`, `"Player"`, `"Velocity"`, ...)
  - [ ] L'engine registra i propri built-in (`Transform`, `MeshRenderer`)
    via `SceneComponentRegistry.RegisterEngineDefaults()`; **Sandbox
    estende lo stesso registry** con i propri componenti custom in
    `SandboxGame.Init()`, prima di caricare la scena — l'estensibilità
    vive fuori dall'engine, senza toccare `gEngine.Scenes`
- [ ] **Serializzazione** (JSON con `System.Text.Json`)
  - [ ] `Scene.cs` — `class Scene { string Name; List<EntityDefinition> Entities; }`,
    `class EntityDefinition { Dictionary<string, JsonElement> Components; }`
    — nessun campo fisso, solo un bag chiave→dati grezzi
  - [ ] `JsonSceneLoader.cs` — `Scene Load(string path)`, deserializza
    `{ name, entities: [ { components: { "Transform": {...}, "MeshRenderer": {...} } } ] }`
    senza interpretare i valori (restano `JsonElement` finché un binder
    non li legge)
  - [ ] Converter per `Vector3` / `Quaternion` (usati dai parser dei
    binder built-in, es. `TransformComponent`)
- [ ] **`SceneInstantiator`** (sostituisce il concetto di `SceneManager`
  per il caricamento — attivazione/switch di scena resta da valutare
  quando servirà davvero più di una scena)
  - [ ] `SceneInstantiator.cs` — `static void Instantiate(Scene scene, World world, SceneComponentRegistry registry)`:
    per ogni entity crea `world.CreateEntity()`, poi per ogni coppia
    `(key, data)` cerca il binder nel registry e applica il componente.
    Non conosce nessun tipo di componente specifico: è totalmente generico
- [ ] **Refactor** del gioco
  - [ ] `SandboxGame` **carica la scena da file** (`JsonSceneLoader` +
    `SceneInstantiator`) invece di costruire l'array `Buildings` in codice
  - [ ] `samples/Sandbox/assets/scenes/city.json` — scena d'esempio
    versionata: i 6 edifici attuali + il player, con `Player`/`Velocity`
    inclusi per dimostrare che i componenti custom di Sandbox funzionano
    nello stesso file

**Milestone:** modifichi un file `.json` e la scena cambia senza ricompilare.

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

1. **Fase 2** → scena di primitive navigabile in 3D.
2. **Fase 3** → scena caricata da file (data-driven).
3. **Fase 4** → editor: hierarchy + inspector + gizmi + save/load 
4. **Fase 5** → modelli, luci e fisica 3D reali.
