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
- [x] **Gerarchia di transform** (parent/child)
  - [x] `ParentComponent { Entity Parent }` (`src/gEngine/Ecs/Component/`) — dati puri,
    solo il riferimento verso l'alto; niente lista figli (un solo punto di verità)
  - [x] Transform locale vs mondo — `TransformComponent` diventa **locale** quando
    l'entità ha un `ParentComponent`; la world matrix si ricava con
    `World.GetWorldMatrix(entity)` (`src/gEngine/Ecs/Base/WorldTransforms.cs`)
  - [x] Composizione `World = Local * ParentWorld` (locale del figlio a SINISTRA, per la
    convenzione row-vector di `System.Numerics` — opposto del `Parent * Local` OpenGL)
  - [x] `MeshRenderSystem` usa la world matrix risolta invece del solo `GetLocalMatrix()`
  - [ ] *(rimandato)* Cache con **dirty flag**: oggi il ricalcolo è ricorsivo on-demand,
    senza cache e senza guard sui cicli — sufficiente per scene costruite a mano
  - [x] Authoring del `Parent` da **file scena**: risolto con istanziazione a due passate
    (mappa `name → Entity` nel `SceneBindContext`); `"Parent": "nomeGenitore"` nel JSON
    - ⚠️ Scoperto in Fase 4: il binder c'era e funzionava, ma **nessuna scena lo usava** —
      il path era di fatto non esercitato e avrebbe potuto regredire senza che nulla lo
      segnalasse. Ora `demo.json` ha `lamp-blue` figlia di `player` (l'unica entità con un
      `Parent`), quindi la gerarchia è coperta dalla scena di demo e visibile nell'albero
      della Hierarchy

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
- [x] **Render order / layer** (opachi vs trasparenti)
  - [x] `RenderLayer { Opaque, Transparent }` (`src/gEngine/Rendering/RenderLayer.cs`) +
    `Layer`/`SortingOrder` su `MeshRendererComponent`
  - [x] `MeshRenderSystem` raccoglie i `DrawMeshCommand`, li ordina per
    `(Layer, SortingOrder)` e poi disegna (opachi prima dei trasparenti)
  - [ ] *(rimandato)* Ordinamento **back-to-front per distanza** dei trasparenti:
    serve passare la camera (o la sua posizione) al render step, oggi assente da `OnRender`
- [ ] Ambientazione statica (grid/plane del pavimento) resta un
  `DrawMeshCommand` costruito al volo dal gioco e passato a
  `renderer.DrawMesh(...)`, **non** un'entità ECS — non c'è ancora un caso
  d'uso per trattarla come dato di scena

**Milestone:** una scena di cubi/sfere navigabile con camera 3D. 🎉 ✅
*(pipeline mesh via ECS completata; render order per layer/`SortingOrder` fatto —
resta solo il sort per distanza dei trasparenti, che richiede la camera nel render step)*

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

## Fase 4 — Editor MVP 🟡

UI immediate-mode dentro la finestra Raylib.

Scelta di layering: l'editor vive in un **progetto separato** (`src/gEngine.Editor/`,
referenzia `gEngine`), che è l'unico a dipendere da `ImGui.NET`/`rlImgui-cs`. Qui il
confine di isolamento è il **progetto**, non il file: diverso da `IRenderer`, dove la
regola è "un solo file importa `Raylib_cs`". Il motivo è che l'astrazione lì serve a
tenere raylib fuori dai *system* ECS, mentre un pannello è codice UI per natura —
avvolgere ImGui dietro un port darebbe poco e si romperebbe subito ai **gizmi**, perché
ImGuizmo vuole il contesto ImGui grezzo. Il beneficio vero (il core engine senza
dipendenze da ImGui, e un gioco che spedisce senza editor) lo dà già la separazione in
assembly.

- [x] **Integrazione ImGui**
  - [x] `ImGui.NET` 1.91.6.1 + `rlImgui-cs` 3.2.0 agganciati al loop
    - ⚠️ Nota: `rlImgui-cs` dichiara `Raylib-cs >= 7.0.1` (range aperto) ed è compilato
      contro il 7, ma gira contro il **8.0.0** che usa l'engine — verificato a runtime,
      nessun problema. NuGet unifica su 8.0.0, niente downgrade.
  - [x] Docking/layout base dei pannelli — `rlImGui.Setup(darkTheme: true, enableDocking: true)`
    + `ImGui.DockSpaceOverViewport(...)`. ImGui.NET 1.91.6.1 include il **branch docking**
    (`ImGuiConfigFlags.DockingEnable` esiste), non serve un pacchetto separato
    - ⚠️ Nota: il dockspace va creato con `ImGuiDockNodeFlags.PassthruCentralNode`,
      altrimenti la finestra host copre lo schermo col proprio sfondo opaco e la scena 3D
      sparisce
    - ⚠️ Nota: ImGui salva il layout in un **`imgui.ini`** che scrive nella *working
      directory* — cioè nel sorgente, non in `bin/`. Aggiunto al `.gitignore`: è stato
      locale della finestra, non del progetto
  - [x] **Chi possiede l'editor: il gioco, non il `GameLoop`.** I tre agganci che ImGui
    vuole esistono già in `IGame` con le garanzie giuste — `Init` gira dopo `InitWindow`
    (rlImGui carica l'atlas dei font: serve contesto grafico), `Draw` è già dentro
    `BeginFrame`/`EndFrame`, `Shutdown` gira prima di `CloseWindow`. Quindi **zero
    modifiche al contratto `IGame`** e nessun `#if EDITOR`: chi non vuole l'editor non
    referenzia il progetto
  - [x] Guard sull'input conteso: `EditorHost.WantsMouse`/`WantsKeyboard`
    (`ImGui.GetIO().WantCaptureMouse/Keyboard`) — senza, un clic su un pannello ruota
    anche la camera. `SandboxGame` li interroga prima di far girare gli input system e
    il `FreeFlyCamera3DController`
  - [x] Toggle dell'editor con **F1** (`GameAction.ToggleEditor`)
    - ⚠️ Gotcha appreso: il toggle sta in `Draw`, **non** in `Update`. `Update` è a passo
      fisso e in un frame lento gira più volte, mentre l'`InputHandler` fa polling una
      volta per frame: un input edge-triggered letto lì verrebbe consumato a ogni
      iterazione, ribaltando l'editor due volte (= nessun effetto). Vale per qualunque
      azione "premuto una volta"
- [x] **Pannello Hierarchy**
  - [x] Lista entità della scena attiva, selezione — `HierarchyPanel`
    (`src/gEngine.Editor/Panels/`), albero che rispetta i `ParentComponent`
  - [x] `EditorContext` — selezione condivisa fra i pannelli (`Entity? Selected`).
    Separato dai pannelli apposta: la selezione sopravvive alla singola finestra e
    servirà a Inspector e gizmi
  - [x] `NameComponent` (`src/gEngine/Ecs/Component/`) — l'etichetta delle righe.
    Prima il `name` del file scena serviva **solo** a risolvere i riferimenti `Parent` e
    per scelta esplicita "non finiva nel World": la Hierarchy però ha bisogno di meglio di
    "Entity 7", e il Save dovrà riscrivere quel campo. Ora `SceneInstantiator` lo copia
    nel World. Resta un campo a sé e non una chiave di `components`: il nome identifica
    l'entità nel file, quindi va letto nella **prima** passata, prima di ogni binder
  - [x] `World.AllEntities` + `World.Exists(entity)` — la Hierarchy elenca anche le entità
    senza componenti, quindi non può partire da una `Query`
  - ⚠️ Nota: l'albero è ricostruito da zero ogni frame. `ParentComponent` tiene solo il
    riferimento verso l'alto (un solo punto di verità), quindi la direzione padre→figli va
    comunque derivata, e una cache andrebbe invalidata a ogni modifica dell'editor. Se un
    giorno pesa, il posto giusto è lo stesso dirty flag già rimandato per le world matrix
  - [x] Crea/duplica/elimina entità — toolbar del pannello + `EntityOperations`
    (`src/gEngine.Editor/`). Stanno nell'editor e non nel `World` perché sono **politiche**,
    non meccanismi: "duplicare = copiare tutto tranne lo stato di runtime" e "eliminare =
    portarsi via i figli" sono decisioni dell'editor; il World offre i mattoni e non ha
    opinioni
    - [x] `World.DestroyEntity` — gli id **non si riusano** (`_entityCounter` cresce e
      basta), quindi un `Entity` tenuto da parte non può aliasare un'entità nuova: è il
      motivo per cui qui non serve un contatore di generazione. I riferimenti pendenti
      restano possibili e sono gestiti a valle (`GetWorldMatrix` ricade su identità, la
      Hierarchy tratta il figlio come radice)
    - [x] `[RuntimeState]` (`src/gEngine/Ecs/Component/RuntimeStateAttribute.cs`) —
      duplicare un'entità copiando *tutto* copierebbe anche `PhysicsBodyComponent`, con
      **due entità sullo stesso corpo Bepu**. La regola viveva solo in un commento ("è
      stato di runtime, non dato d'autore"); ora è un attributo che l'editor legge, così
      il prossimo componente-link non ri-scopre il bug
    - [x] Il duplicato prende un **nome libero** (`falling-cube-red (1)`). Non è estetica:
      il `name` è il bersaglio dei riferimenti `Parent` nel file scena, e due omonime al
      reload collasserebbero nella mappa `name → Entity` — vince l'ultima, in silenzio
    - ⚠️ Scoperto abilitando l'eliminazione: `SandboxGame.Draw` faceva `.First()` sulla
      query del player, quindi **cancellare il player faceva crashare il gioco**. "Esiste
      sempre un player" non è più un invariante da quando c'è un editor: ora l'HUD degrada
      a "Nessun player"
- [x] **Pannello Inspector** (reflection-driven) — `src/gEngine.Editor/Panels/InspectorPanel.cs`
  - [x] Mostra i componenti dell'entità selezionata
  - [x] Editing campi Transform (position/rotation/scale)
  - [x] Editing generico dei campi — `float`/`int`/`bool`/`Vector3`/`Quaternion`/`Color`/
    `string`/`enum`. Tipi fuori elenco (`Entity`, `ModelHandle`) mostrati in **sola
    lettura**: sono riferimenti, non numeri — un DragInt sull'id di un'entità è un piede
    nel fucile. Serviranno widget dedicati (un picker)
  - [x] **Accesso non generico ai componenti** (`IComponentStorage`): l'Inspector deve
    chiedere "quali componenti ha l'entità X" senza conoscerne i tipi a compile time, ma
    l'interfaccia esponeva solo `Count`. Aggiunti `ComponentType`, `Has`, `GetBoxed`,
    `SetBoxed`, `Remove`, più `World.ComponentStorages`. I system non passano di qui:
    continuano a usare `Query<T>`, tipizzata e senza boxing
    - ⚠️ **Il write-back torna a mordere qui.** `GetBoxed` su uno struct restituisce una
      **copia** boxed: l'Inspector muta la scatola via reflection e la riscrive con
      `SetBoxed`. Senza quella riscrittura il pannello sembrerebbe funzionare e non
      salverebbe niente. `MeshRendererComponent` è una `class`, quindi lì la mutazione è
      già in loco e il `SetBoxed` è una riscrittura innocua dello stesso riferimento:
      trattarli uguale evita di distinguere i due casi
  - [x] Rotazione mostrata in **gradi** (`EulerAngles`), non come quaternione grezzo:
    nessuno legge `(x:0.38, y:0, z:0, w:0.92)`. Convenzione **yaw(Y)→pitch(X)→roll(Z)**,
    la stessa di `Quaternion.CreateFromYawPitchRoll`, così andata e ritorno usano lo
    stesso ordine — verificata numericamente (200k rotazioni casuali con |pitch| ≤ 89°,
    errore max `1-|dot|` ≈ 3e-7) e poi sui dati veri: il player di `demo.json`
    (`w:0.7071, y:-0.7071`) legge `-89.999°`
    - ⚠️ A pitch ±90° (gimbal lock) la scomposizione è ambigua e i numeri possono saltare
      mentre trascini, pur restando corretta la rotazione. La cura vera è un Eulero "di
      lavoro" per-entità invece di ri-derivarlo ogni frame; non serve finché non dà fastidio
  - [~] **Aggiungi/Rimuovi componente** da UI — **Rimuovi** fatto (bottone `X`
    sull'header). **Aggiungi** no: serve un elenco dei tipi istanziabili, che l'engine per
    definizione non ha. La strada probabile è estendere il `SceneComponentRegistry` (che
    già mappa nome→tipo per le scene) con una factory del valore di default, così un
    componente custom diventa aggiungibile registrandolo una volta sola
  - ⚠️ Nota di layout: i pannelli nascono con `SetNextWindowPos/Size(..., ImGuiCond.FirstUseEver)`.
    Senza, ImGui dà a ogni finestra la stessa posizione di default e al primo avvio i
    pannelli si sovrappongono (successo davvero). `FirstUseEver` e non `Always`: dopo il
    primo avvio comanda il layout salvato dall'utente in `imgui.ini`
- [ ] **Viewport & manipolazione** — *deciso: due viste render-to-texture, come Unity*
  - [ ] Rendering della scena nel viewport dell'editor: la scena va disegnata su
    **RenderTexture** e mostrata come pannello ImGui, invece che a tutto schermo con la UI
    sopra. Richiede di estendere `IRenderer` con i render target
  - [ ] **Vista Scena e vista Game separate**, visibili insieme, ognuna con la sua camera
    - ⚠️ Da sciogliere: oggi la `Camera3D` è **una sola** e se la contendono il
      `FreeFlyCamera3DController` (editor) e il `CameraFollowSystem` (gioco). La camera di
      scena appartiene all'editor, quella di gioco alla scena: vanno separate prima di
      poter mostrare due viste
  - [ ] **Picking** (clic per selezionare un'entità) — ha bisogno del viewport per avere
    coordinate mouse sensate, e di `IPhysicsWorld.Raycast` (già rimandato in Fase 5)
  - [ ] **Gizmi** move/rotate/scale (**ImGuizmo.NET**) 🔴 — l'unico punto che giustifica di
    non aver avvolto ImGui dietro un port: vuole il contesto ImGui grezzo
- [x] **Persistenza** — pannello "Scena" (`ScenePanel`) + `SceneDocument`
  - [x] Bottoni Save/Load scena
  - [x] **`SceneSerializer`** (`src/gEngine/Scenes/`) — verso opposto di
    `SceneInstantiator`, e come lui non conosce nessun tipo: chiede al registry come si
    chiama e come si scrive ogni tipo che trova negli storage. Round-trip verificato sul
    `demo.json` reale: **zero differenze semantiche** (le uniche differenze sono i default
    impliciti resi espliciti — `{"w":1}` → `{"x":0,"y":0,"z":0,"w":1}`)
  - [x] `SceneWriteContext` — lo specchio di `SceneBindContext`, con le mappe percorse al
    contrario: `Entity → nome` (gli id non sopravvivono al reload, i nomi sì) e
    `handle → path` via `AssetManager.TryGetModelPath` (che **mancava**: la cache era solo
    `path → handle`)
  - [x] Writer opzionale nel registry: per quasi tutti i componenti "serializza i campi con
    `SceneJson.Options`" è già l'inverso corretto del parse (i converter math implementano
    `Write`). Va scritto a mano solo dove la lettura è **asimmetrica**: `Parent` (nome →
    Entity) e `MeshRenderer` (path → handle)
  - [x] Il campo `Model` (handle) **non** viene scritto, al suo posto va `ModelPath`:
    scriverlo sarebbe peggio che inutile — al reload verrebbe riletto come handle valido e
    punterebbe a un modello a caso
  - [x] I componenti `[RuntimeState]` non finiscono nel file (verificato: `PhysicsBody`
    assente dal salvato anche salvando a fisica già avviata). Stesso attributo della
    duplicazione: la regola "non è dato d'autore" ora ha **un solo posto** dove vive
  - ⚠️ **Trovato salvando**: il primo Save cancellava tutti i `_comment` con cui le scene si
    documentano — da quando l'editor scrive, ciò che il formato non legge lo **distrugge**.
    Risolto con `[JsonExtensionData]` su `Scene`/`EntityDefinition` + fusione per nome con
    la scena d'origine in `SceneSerializer.ToScene(source:)`. Limite: un'entità rinominata o
    creata nell'editor non ha un originale da cui pescare il commento
  - ⚠️ Salvataggio **atomico** (file temporaneo + move): un Save interrotto a metà
    troncherebbe la scena, cioè distruggerebbe il lavoro che stava salvando
  - [x] `World.Clear` per il Load. Il contatore degli id **non** si azzera (come in
    `DestroyEntity`): una selezione rimasta in mano all'editor dopo un reload resta
    invalida invece di puntare a un'entità nuova e sbagliata
- [ ] **Play/Stop** — *deciso: Stop **ripristina** lo stato pre-Play, come Unity*
  - [ ] Stato `Editing / Playing / Paused`. Fuori da Play i system **non girano**: la
    fisica dev'essere ferma, non solo invisibile
  - [ ] Snapshot all'ingresso in Play, ripristino allo Stop. **È il motivo per cui la
    Persistenza viene prima**: lo snapshot *è* la serializzazione — `SceneSerializer` in
    memoria invece che su file. Non è un punto separato della lista, è lo stesso lavoro
    - Prova che serve: salvando dopo appena 5 frame di simulazione, il file si era già
      portato dietro i cubi caduti (`y: 10` → `y: 8.08`). Senza snapshot, giocare e salvare
      cementa la scena a metà caduta
  - [ ] I system oggi li possiede e li cicla a mano `SandboxGame` (quattro liste + quattro
    overload di `AddSystem`): è logica dell'engine che vive nel sample, e finché sta lì
    ogni gioco deve reimplementare da sé il gating del Play. Va portata nell'engine prima
    di appenderci sopra il Play/Stop

**Milestone raggiunta:** aggiungi entità, modifichi transform e componenti e salvi,
**senza toccare codice**. 🏆
*(crei/duplichi/elimini entità, ne modifichi i componenti dall'Inspector, premi Salva e il
`.json` sul disco cambia — poi Ricarica e la scena torna dal file.)*

**Resta della Fase 4:** le due viste render-to-texture, il Play/Stop con snapshot, il
picking e i gizmi (vedi sopra). Più un buco piccolo ma fastidioso: **aggiungere un
componente** a un'entità dall'UI — serve una factory del valore di default, che l'engine
per definizione non ha; la strada probabile è estendere il `SceneComponentRegistry`, che
già mappa nome→tipo.

---

## Fase 5 — Profondità 3D: asset, materiali, luci, fisica 🔴

Da "cubi colorati" a "scena 3D vera".

- [x] **Caricamento modelli** *(animazioni rimandate, come da piano)*
  - [x] Import glTF/OBJ via `AssetManager.LoadModel` → `ModelHandle` opaco
    (`RayLibAssetBackend.LoadModel` = `Raylib.LoadModel`)
  - [x] Integrazione con l'AssetManager (cache path→handle, `UnloadAll` dal `GameLoop`)
  - [x] Disegno via `MeshKind.Model`: `MeshRendererComponent.Model` (handle) →
    `DrawMeshCommand.Model` → `RayLibRenderer` risolve `ModelHandle → Model` tramite
    `RayLibAssetBackend.TryGetModel` (i due adapter raylib condividono la tabella modelli,
    creati e collegati dal `GameLoop`)
  - [~] Texture/material dei modelli: quelli **embedded** nel file (es. glTF) li carica
    raylib da sé; material/shader custom = prossimo punto (luci/PBR)
  - [ ] *(rimandato)* Animazioni scheletriche
  - [ ] *(rimandato)* Bounds reali della mesh per il frustum culling (ora: cubo unitario)
- [~] **Materiali & shader**
  - [x] Shader di illuminazione PBR semplice (`src/gEngine/Shaders/lit.vs`+`lit.fs`,
    GLSL 330: GGX + Lambert, metallic/roughness) caricato e gestito da `RayLibRenderer`;
    applicato al material dei cubi e ai material dei modelli
  - [~] Material con colore/albedo: il colore (`Tint`→`colDiffuse`) e la texture albedo
    dei modelli funzionano; metallic/roughness sono **globali** (un solo set), non ancora
    per-material — prossimo affinamento
- [x] **Illuminazione**
  - [x] `LightComponent` (Directional/Point) + `LightingSystem` che raccoglie le luci e
    le carica via `IRenderer.SetLighting`; `RayLibRenderer` setta le uniform dello shader
    (fino a `MAX_LIGHTS = 4`)
  - [ ] *(rimandato)* ombre
- [~] **Fisica 3D → BepuPhysics v2** (Aether è 2D-only)
  - [x] Port `IPhysicsWorld` + adapter `BepuPhysicsWorld` (unico file che importa Bepu),
    stesso schema ports & adapters di renderer/asset
  - [x] Rigid body + collider **box/sphere** (`RigidBodyComponent`, statici e dinamici);
    capsule/mesh rimandati
  - [x] Sync mondo fisico → `Transform` dell'ECS (`PhysicsSystem`, fixed-step)
  - [x] **Rimozione dei corpi** (`IPhysicsWorld.RemoveBody`) — servita alla Fase 4, ma è
    un buco della fisica, non dell'editor: senza, un'entità distrutta lasciava un corpo a
    simulare e collidere da fantasma
    - [x] `PhysicsSystem` **riconcilia** a ogni update: toglie i corpi la cui entità non
      esiste più, o che hanno perso il `RigidBody`. Serve una mappa `entityId → BodyId`
      interna al system, perché il `PhysicsBodyComponent` sparisce insieme all'entità e
      con lui l'unico riferimento al corpo. È polling e non un evento `OnEntityDestroyed`
      sul World: così l'ECS resta ignaro della fisica (e di ogni risorsa esterna), e si
      scandiscono i soli corpi vivi invece di tutto il World
    - ⚠️ Bug trovato di sponda: `AddBox`/`AddSphere` fanno `Shapes.Add` per **ogni** corpo
      e non rimuovevano mai la shape. Invisibile con scene statiche, non più con un editor
      che crea/elimina: ora lo shapeIndex è tracciato per `BodyId` e rimosso col corpo.
      (`Remove` basta per le convesse; con mesh/compound servirà `RemoveAndDispose`)
  - [ ] *(rimandato)* Raycast (utile anche per il picking dell'editor)
- [x] **Frustum culling** (non disegnare ciò che è fuori camera)
  - [x] `Frustum` (`src/gEngine/MathUtils/Frustum.cs`) — 6 piani estratti dalla
    view-projection (Gribb–Hartmann, adattato alla convenzione row-vector di
    `System.Numerics`), test conservativo `IntersectsSphere(center, radius)`
  - [x] `Camera3D` espone `Near`/`Far` + `GetView/Projection/ViewProjection(aspect)`
    (matematica pura, indipendente da raylib)
  - [x] `IRenderSystem.OnRender` ora riceve la `Camera3D`; `MeshRenderSystem` costruisce
    il frustum una volta per frame e scarta le entità la cui bounding sphere è tutta fuori
  - [ ] *(rimandato)* Bounds per-mesh reali col caricamento modelli (ora si assume
    l'ingombro del cubo unitario, valido per `MeshKind.Cube`)

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
| UI editor | **ImGui.NET** + **rlImgui-cs** | immediate-mode dentro Raylib; solo in `gEngine.Editor` |
| Gizmi 3D | **ImGuizmo.NET** | move/rotate/scale handles |
| Fisica 3D | **BepuPhysics v2** | sostituisce Aether (2D) |
| Test | **xUnit** | progetto `tests/` |

---

## Riepilogo milestone

1. ~~**Fase 2** → scena di primitive navigabile in 3D.~~ ✅
2. ~~**Fase 3** → scena caricata da file (data-driven).~~ ✅
3. **Fase 4** → editor: ~~hierarchy~~ + ~~inspector~~ + gizmi + save/load *(hierarchy ✅, inspector ✅)*
4. **Fase 5** → modelli, luci e fisica 3D reali. *(quasi tutta fatta, mancano le rifiniture [~])*
