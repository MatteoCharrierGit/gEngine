# gEngine — Diario delle decisioni

> **Cos'è questo file.** L'archivio del **perché**, fase per fase, in ordine cronologico.
> Era `ROADMAP.md` fino alla riorganizzazione: si è visto che il 90% del contenuto non era
> "cosa fare" ma il razionale di ciò che era già stato fatto — le alternative scartate, i
> numeri delle verifiche, i bug trovati costruendo. Quella parte è la cosa più preziosa del
> repo e non si tocca; è solo stata separata da ciò che è ancora aperto.
>
> **Come si legge:**
> - Cosa resta da fare → [`ROADMAP.md`](ROADMAP.md). **La verità sullo stato aperto è lì.**
> - Cosa non va dimenticato → [`DA_RICORDARE.md`](DA_RICORDARE.md)
> - Come si usa l'engine → [`USAGE.md`](USAGE.md)
> - Il perché di una scelta già presa → **questo file**
>
> ⚠️ **Le caselle `- [ ]` qui dentro sono congelate**: fotografano lo stato *al tempo della
> fase*, non oggi. Non spuntarle e non fidartene — sono state travasate in `ROADMAP.md`, che
> è l'unico posto dove l'aperto è aggiornato. Due punti di verità sullo stato divergerebbero,
> ed è esattamente l'errore che questa separazione toglie di mezzo.
>
> **Si scrive ancora qui**: chiudendo un lavoro, il racconto del perché va in fondo a questo
> file come nuova fase. È parte del lavoro, non un extra.

---

## Fase 0 — Fondamenta & igiene 🟢

Piccole cose che userai ovunque, meglio averle prima.

- [x] **Logging** a livelli (Debug/Info/Warn/Error)
  - [x] Interfaccia `ILogger` + implementazione console (`ConsoleLogger`, `LogLevel`, `LogMessage`, `LogCategories`)
  - [x] Timestamp e categoria/tag per messaggio
  - [ ] Punto d'accesso comodo dall'engine — `GameLoop` istanzia `_logger` ma non lo passa mai a `IGame`/ai system; da esporre (es. via `Init`) prima di poterlo usare fuori da `GameLoop`
- [ ] **Unit test** sull'ECS (primo contatto col testing in C#)
  - [x] Progetto di test (`tests/gEngine.Tests`, xUnit) — creato in **Fase 4.86**, che però copre
    la **serializzazione**, non l'ECS: si è testato per primo il pezzo che ne regge tre (Salva,
    Play/Stop, hot-reload), non quello elencato qui
  - [ ] Test su `CreateEntity`, `AddComponent`/`GetComponent`, `Query<..>`
  - [ ] Test sul gotcha struct/copia (mutazione + write-back) — ⚠️ ha già morso **cinque** volte,
    ed è la voce con il rapporto costo/danno peggiore di tutta la lista
- [x] **Adozione math**: standardizzare su `System.Numerics`
  - [ ] Decidere convenzioni: sistema **right-handed, Y-up** (come Raylib) — implicito nell'uso attuale, non ancora scritto da nessuna parte
  - [ ] Note su unità (1 unità = 1 metro?) e scala di riferimento

**Milestone:** log leggibile a runtime + test verdi. *(log ✅; i test esistono e sono verdi da
Fase 4.86, ma sulla serializzazione — l'ECS di questa fase è ancora scoperto)*

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
  - [x] **L'inverso**: `World.SetWorldPose(entity, position, rotation)` (Fase 4.5) — scrivere
    una posa **mondo** su un'entità, cioè `Local = World * inverse(ParentWorld)`
    - Verificato numericamente su 50k pose, gerarchie a 2-3 livelli: ordine giusto **2.5e-5**
      (posizione) / **8.6e-7** (orientamento); **l'ordine opposto** `inverse(ParentWorld) * World`
      sbaglia di **675** / **2.0** (il massimo possibile per un quaternione). Sette ordini di
      grandezza: come per l'ordine dei quaternioni dei gizmi, **la verifica discrimina davvero**
      invece di passare comunque. Caso root: identico **bit a bit** alla scrittura diretta
    - `Matrix4x4.Invert` **può fallire** (genitore a scala 0): ritorna `false` e non scrive.
      Non ricade sull'identità — leggere una posa qualunque è tollerabile, **scriverla**
      teletrasporterebbe l'entità nell'origine
    - ⚠️ **Limite noto, trovato misurando**: con genitore a scala **non uniforme** la posizione
      resta esatta (7.7e-6) ma l'**orientamento sbaglia di ~90° in mediana**, *in silenzio*.
      Non è arrotondamento: in row-vector la 3x3 è `S_figlio * R_figlio * S_genitore * R_genitore`,
      e la scala del genitore cade **in mezzo** alle due rotazioni — commuta solo se uniforme.
      Con shear, il quaternione locale che darebbe quell'orientamento **non esiste**. Oggi non
      morde (camere root, l'unico genitore vivo di `demo.json` ha scala uniforme), ma è un
      compromesso consapevole e non una svista: il commento iniziale diceva "l'orientamento è
      il migliore disponibile" ed è stato corretto perché **la misura l'ha smentito**
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

## Fase 4 — Editor MVP 🟢

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
  - [x] **Riparentare trascinando** — `EntityDragDrop` + `EntityOperations.Reparent`/`CanReparent`
    - ⚠️ **Era un buco che cinque punti dichiaravano chiuso**: `ParentComponent` non è esposto
      nell'Inspector *«perché ci si riparenta dalla Hierarchy»*, il registry non gli dà un
      default per lo stesso motivo, e **due tooltip mostrati all'utente** ripetevano la frase —
      mentre la Hierarchy leggeva `ParentComponent` solo per **disegnare** l'albero. L'editor
      mandava a fare una cosa che non si poteva fare. Chiuso il buco, non corrette le frasi
    - Stessa meccanica di `AssetDragDrop` con un payload diverso (un `Entity` invece di un
      path), e per lo stesso motivo: **il nome del payload è il tipo**, quindi un modello non
      illumina una riga dell'albero e un'entità non illumina lo slot di un asset
    - [x] **Il bersaglio non viene offerto se la mossa è illegale** (`TryPeek` + `CanReparent`
      *prima* di `BeginDragDropTarget`): su sé stessi o dentro un proprio discendente la riga
      **non si illumina nemmeno**. È il principio del payload tipizzato portato un passo più in
      là — lì è ImGui a non accoppiare due tipi, qui siamo noi a non offrire ciò che
      rifiuteremmo. Il controllo è **ripetuto** nell'applicazione del comando: fra il rilascio e
      l'esecuzione passa un frame, e il mondo ha girato
    - ⚠️ **I cicli non sono un dato strano, sono un blocco totale**: `GetWorldMatrix` risale i
      genitori ricorsivamente **senza guard**. È lo stesso pericolo del visited set di
      `DestroyRecursive` visto dall'altro lato — lì ci si difende da un ciclo esistente, qui si
      impedisce di crearlo (e `IsDescendantOf` ha comunque il suo visited set, perché una scena
      scritta a mano può già contenerne uno)
    - [x] **A radice si toglie il `ParentComponent`**, non si mette a `Entity(0)`: la Hierarchy
      tratterebbe comunque l'entità come radice, ma con dentro un riferimento rotto fino al
      salvataggio. Il bersaglio "diventa radice" è una riga che **compare solo durante il
      trascinamento**: il vuoto sotto l'albero non dice di essere un bersaglio
    - [x] **La posa di mondo è mantenuta** (semantica Unity, *decisa dal proprietario*): a
      cambiare è il Transform **locale**, via `World.SetWorldPose`. Chi riordina l'albero non
      sta chiedendo di spostare l'oggetto, e vederlo saltare sembrerebbe un bug del
      trascinamento
      - Verificato numericamente su **20k** riparentamenti fra gerarchie a 3 livelli:
        pos **3.5e-5**, orientamento **1.2e-7**. Il controllo "riparenta e basta" (senza
        ricalcolo) sbaglia di **348** / **1.0** — sette ordini di grandezza, quindi **la
        verifica discrimina** invece di passare comunque. A radice: **0 esatto** (nessuna
        inversione, come già fa `SetWorldPose`)
      - ⚠️ **Eredita il debito di `SetWorldPose`**: con genitore a scala **non uniforme** la
        posizione resta esatta (1e-5) ma l'orientamento sbaglia **in silenzio** — misurato qui:
        `1-|dot|` **0.47 mediano**, cioè ~117°. Finora il caso non mordeva perché i genitori
        vivi avevano scala uniforme; **trascinando a mano si arriva ovunque**, quindi qui è
        molto più raggiungibile che altrove
    - Verificato **a video** col rig della Fase 4.7: la zona "diventa radice" che si illumina
      col colore acceso del tema mentre `lamp-blue` esce da `player` (che torna una foglia), e
      `lamp-green` che diventa figlia di `falling-cube-red`. Il rifiuto del ciclo è stato
      guardato **dal vivo** (log temporaneo: *«sopra lamp-green con falling-cube-red in mano,
      legale=False»*), non solo dedotto. La catena fino al file è verificata a parte: dopo il
      riparentamento il json esce con `"Parent": "floor"` — **per nome**, come vuole il formato
    - ⚠️ **Trappola del rig, non del prodotto** (pagata una volta): rlImGui campiona il mouse
      **a livello, una volta per frame**, quindi se la pressione sintetica e i primi spostamenti
      cadono nello stesso frame ImGui registra la pressione sulla posizione **finale** — si
      afferra la riga sbagliata e sembra che il trascinamento non funzioni. La pressione vuole
      un frame tutto suo, con margine
- [x] **Pannello Inspector** (reflection-driven) — `src/gEngine.Editor/Panels/InspectorPanel.cs`
  - [x] Mostra i componenti dell'entità selezionata
  - [x] **`[EditorConfiguration]`** (`src/gEngine/Ecs/Component/EditorConfigurationAttribute.cs`) —
    l'Inspector mostrava **tutti** i campi pubblici: il default era "esponi" e nessuno l'aveva
    deciso, così ogni campo nuovo (un accumulatore, una cache) si prendeva un DragFloat
    nell'UI. Ora il default si inverte — si vede solo ciò che è marcato — e la scelta torna a
    chi scrive il componente, l'unico che sa quali dei suoi campi sono dati d'autore
    - È il gemello a livello di **membro** di `[RuntimeState]`, che esclude un componente
      intero: stessa idea (l'editor manipola dati che non conosce e ha bisogno che il tipo gli
      dica cosa toccare), granularità diversa. Sta nell'engine e **non** in `gEngine.Editor`
      per lo stesso motivo: l'attributo è *letto* dall'editor ma *scritto* da chi definisce i
      dati, e un gioco che spedisce senza editor non può perdere gli attributi dei suoi
      componenti
    - Vale su **campi e proprietà**: l'Inspector leggeva solo `GetFields`, quindi decorare una
      proprietà avrebbe compilato senza mostrare niente — una trappola silenziosa, cioè
      esattamente ciò che l'attributo doveva togliere di mezzo. Le proprietà passano dal loro
      accessor, quindi un setter con dentro validazione/clamp viene rispettato
    - Parametro `label` opzionale (`[EditorConfiguration("Massa")]`). **Niente flag readonly**:
      un campo `readonly` o una proprietà senza setter lo dicono già da sé, e ricadono nella
      stessa vista in sola lettura dei tipi fuori elenco. Un secondo modo di dire la stessa
      cosa sarebbe solo un modo in più di dirla storta
    - Un componente senza nessun membro marcato resta **visibile come header** ("nessuna
      proprietà esposta"): sparire nasconderebbe che il componente c'è, che è metà
      dell'informazione. È il caso di `Parent` (riferimento a un'entità: si riparenta
      dall'albero della Hierarchy, non battendo un id a mano) e di `MeshRenderer.Model`
      (handle: il dato d'autore è il *path*, vedi `SceneWriteContext` — serve un asset picker)
  - [x] Editing campi Transform (position/rotation/scale)
    - ⚠️ **"Non si riesce a scrivere numeri nei fields"** non era un bug nostro: un clic
      semplice su un `Drag*` **trascina**, e per digitare ImGui vuole il **Ctrl+Click**. Una
      scorciatoia che non si vede da nessuna parte. Curato col cartello (tooltip
      "Ctrl+Click per digitare"), perché non esiste un `ImGuiSliderFlags` "clic = digita" —
      l'unico in tema è `NoInput`, che toglie *anche* il Ctrl+Click — e sostituire i Drag con
      `InputFloat` pagherebbe il trascinamento, che su un transform è il modo normale di
      lavorare
    - Il sospetto ovvio era un altro ed è stato **scartato coi fatti**, non per fiducia:
      rlImgui-cs 3.2.0 (decompilato: il nuget ha solo la DLL) pompa `GetCharPressed()` in
      `io.AddInputCharacter` **senza guard** su `WantCaptureKeyboard` — il guard che si temeva
      non esiste in questa versione. Guidando la finestra con eventi sintetici: i caratteri
      arrivano in `io.InputQueueCharacters`, un clic su un `InputText` lo attiva e ci si
      scrive (campo Nome: `player` → `player-EDIT`, write-back incluso), e col Ctrl+Click
      anche i Drag accettano la digitazione. La catena ImGui/rlImgui è **sana**
    - ⚠️ Gotcha del banco di prova, non del prodotto: un `WM_KEYUP` sintetico con `lParam=0`
      GLFW lo legge come *press* (`KF_UP` sta in `HIWORD(lParam)`), quindi il Ctrl restava
      premuto per sempre — e ImGui **ignora i caratteri mentre Ctrl è giù**. Sembrava
      "Ctrl+Click apre il campo ma non ci si scrive": era il banco. Col `lParam` giusto
      (`0xC01D0001`) il valore si digita e commit
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
  - [x] **Aggiungi/Rimuovi componente** da UI — chiuso in **Fase 4.7**: la strada ipotizzata
    qui (estendere il `SceneComponentRegistry` con una factory del default) è quella che è
    stata presa. **Rimuovi** non è più il bottone `X` sull'header ma un menu contestuale
  - ⚠️ Nota di layout: i pannelli nascono con `SetNextWindowPos/Size(..., ImGuiCond.FirstUseEver)`.
    Senza, ImGui dà a ogni finestra la stessa posizione di default e al primo avvio i
    pannelli si sovrappongono (successo davvero). `FirstUseEver` e non `Always`: dopo il
    primo avvio comanda il layout salvato dall'utente in `imgui.ini`
- [x] **Viewport & manipolazione** — *deciso: due viste render-to-texture, come Unity*
  - [x] Rendering della scena nel viewport dell'editor: la scena è disegnata su
    **RenderTexture** e mostrata come pannello ImGui (`ViewportPanel`), invece che a tutto
    schermo con la UI sopra. `IRenderer` esteso con i render target
    (`RenderTargetHandle` + `CreateRenderTarget`/`BeginRenderTarget`/`EndRenderTarget`/
    `DestroyRenderTarget`/`GetRenderTargetTextureId`), stesso schema di handle opachi degli
    asset. A tutto schermo si torna con F1 (editor chiuso), che è il gioco vero
    - [x] `GetRenderWidth/Height` **distinti** da `GetScreenWidth/Height`: chi calcola un
      aspect ratio (proiezione, frustum) deve usare la superficie su cui sta disegnando.
      `MeshRenderSystem` prendeva la misura della finestra e dentro un pannello largo la
      metà avrebbe cullato le entità sbagliate
    - [x] `GetRenderTargetTextureId` restituisce un `nint`: è l'unica crepa voluta nel port
      — un'immagine ImGui **è** un handle GPU grezzo, e un tipo intermedio nasconderebbe
      solo che i due lati devono parlare della stessa texture
    - ⚠️ Il target insegue la taglia del pannello con **un frame di ritardo**: si riempie
      prima di aprire il frame ImGui, ma la taglia del pannello si sa solo mentre ImGui lo
      dispone, cioè dopo. Durante un resize la vista è vecchia di un frame, non rotta
    - ⚠️ Trovato guardando i pixel: l'immagine alta quanto lo spazio disponibile fa sforare
      il contenuto e ImGui tira su la **scrollbar verticale**, che si mangia ~14px di
      `GetContentRegionAvail().X` — il frame dopo il target si ricrea più stretto, cioè la
      vista si restringe da sola e butta via una render texture a ogni comparsa della barra.
      `ImGuiWindowFlags.NoScrollbar`: una vista 3D non scrolla, si ridimensiona
    - ⚠️ Le render texture di raylib (OpenGL) hanno l'origine in **basso** a sinistra e
      ImGui in alto: la V va invertita (`uv0=(0,1)`, `uv1=(1,0)`) o la vista è capovolta
    - ⚠️ ImGui identifica le finestre **per titolo**: `ScenePanel` faceva `Begin("Scena")` e
      il viewport pure, quindi non erano due pannelli ma lo **stesso pannello riempito due
      volte** — i bottoni Salva/Ricarica finivano dentro la vista 3D, senza un warning.
      Il pannello del documento ora è `"File scena"`: il nome "Scena" spetta alla vista, e
      quel pannello parla del file (come diceva già il suo commento)
  - [x] **Vista Scena e vista Game separate**, visibili insieme, ognuna con la sua camera
    - [x] La camera di scena appartiene ora all'editor (`EditorHost.SceneCamera`, mossa dal
      `FreeFlyCamera3DController` che l'editor possiede), quella di gioco **al World**
      (entità `game-camera` in `demo.json`, mossa dal `CameraFollowSystem`). Convivevano solo
      perché non si guardavano mai insieme: appena le viste diventano due, la contesa si vede
      — navigare la scena sposterebbe l'inquadratura del giocatore
      - ⚠️ Aggiornato in Fase 4.5: la camera di gioco era `SandboxGame._gameCamera`, un campo
        del sample. Ora è dati di scena (vedi Fase 4.5), e l'asimmetria con la camera di scena
        **è voluta**: la prima è dato d'autore, la seconda è stato dell'editor
    - ⚠️ **Il guard `WantsMouse` non regge più** per la camera di scena. Da quando il 3D sta
      dentro un pannello, il puntatore sopra la vista **è** sopra una finestra ImGui, quindi
      `WantCaptureMouse` è sempre vero e la camera non si muoverebbe mai. Il gate giusto è
      "il puntatore è sopra la vista Scena", e lo sa solo il viewport → `EditorHost.Update`.
      `WantsMouse`/`WantsKeyboard` restano validi per tutto il resto
    - ⚠️ Il free-fly ha ora un **latch**: il gate vale solo sull'**inizio** del volo. Durante
      il volo il cursore è bloccato al centro della finestra, quindi "sono ancora sopra il
      viewport?" smetterebbe di essere vero proprio mentre lo si usa
    - ⚠️ Bug di sponda, trovato riscrivendo il controller: `Math.Clamp(Pitch, -89f, 89f)`
      clampava **radianti** con un limite in gradi (±89 rad ≈ ±5000°), cioè non clampava
      niente — oltre il polo la camera si ribaltava, perché `Up` resta fisso a +Y
  - [x] **Picking** (clic per selezionare un'entità) — `EntityPicker` + `Camera3D.GetRay` +
    `Ray.IntersectsUnitCube` (metodo delle slab, `src/gEngine/MathUtils/Ray.cs`)
    - ⚠️ **`IPhysicsWorld.Raycast` era la dipendenza sbagliata**, non solo "rimandata": si
      seleziona ciò che si **vede**, e metà della scena non ha un `RigidBody` (le luci, la
      lampada figlia del player) — col mondo fisico sarebbero invisibili al clic pur stando
      lì sullo schermo. E i corpi Bepu esistono solo mentre la fisica gira, mentre l'editor
      deve selezionare a simulazione ferma, cioè sempre prima del Play. L'ingombro giusto è
      quello che usa già `MeshRenderSystem` per il culling, e infatti il picker ricalcola la
      **stessa identica world matrix**: quel che si disegna è quel che si clicca. Eredita lo
      stesso limite noto (cubo unitario finché non ci sono i bounds per-mesh)
    - Verificato: proiettando il centro di ogni entità sul suo pixel e risparandoci una
      semiretta, **7 entità su 7 riprendono se stesse**, e un pixel d'angolo non colpisce
      niente
  - [x] **Gizmi** move/rotate/scale — `TransformGizmo`, **scritti a mano** 🔴
    - ⚠️ **ImGuizmo.NET non è agganciabile a questo stack**, ed era dato per scontato:
      `Twizzle.ImGuizmo.NET` (1.89.4) dipende da un *altro* binding di ImGui, mentre qui c'è
      `ImGui.NET` 1.91.6.1 con `rlImgui-cs` compilato contro di esso — affiancarli dà due
      assembly che definiscono entrambi `ImGuiNET.ImGui` e due `cimgui.dll` native che si
      sovrascrivono, e 1.89/1.91 hanno un `ImGuiContext` con ABI diverso, che è proprio la
      struttura che `SetImGuiContext` dovrebbe condividere. Il bundle autoconsistente
      (`Twizzle.ImGui-Bundle.NET`) risolve il conflitto fra nativi ma lascia rlImgui-cs
      legato all'assembly `ImGui.NET`: due contesti ImGui in volo. `Hexa.NET.ImGuizmo` è di
      un altro ecosistema e non ha un backend raylib. Restava o forkare rlImgui-cs per
      sempre, o scrivere la matematica — che il progetto ha già in casa
    - ⚠️ **Cade quindi la motivazione scritta qui sopra** ("l'unico punto che giustifica di
      non aver avvolto ImGui dietro un port: vuole il contesto grezzo"). La conclusione
      regge — un pannello è codice UI per natura — ma non per quel motivo
    - [x] Disegno sul **draw list** di ImGui: le maniglie sono linee 2D proiettate con
      `Camera3D.WorldToViewport` (l'inverso esatto di `GetRay`), non geometria 3D.
      L'interazione invece è in 3D, con le stesse semirette del picking
    - [x] Assi **locali dell'oggetto** (il pivot "Local" di Unity). Non è solo una scelta
      d'uso: rende rotazione e scala esatte per costruzione, perché `Rotation` e `Scale` sono
      già in quello spazio e il genitore non entra nel conto. L'unico che deve attraversare
      la gerarchia è lo spostamento, perché `Position` vive nello spazio del **genitore**
    - ⚠️ Ordine dei quaternioni verificato numericamente invece che per fiducia (200k
      rotazioni casuali): `Concatenate(delta, start)` ruota attorno all'asse locale con
      errore max 3e-7 — e l'**ordine opposto sbaglia di 0.558**, quindi la verifica
      discrimina davvero. Stessa famiglia dei gotcha del transpose e del row-vector
    - ⚠️ Il write-back delle struct morde anche qui: `TryGetComponent` dà una **copia** del
      `TransformComponent`, e senza `AddComponent` finale il gizmo sembrerebbe funzionare
      senza muovere niente. Terzo posto dopo `MovementSystem` e l'Inspector
    - ⚠️ Misurato di sponda: `GetRay`→`WorldToViewport` non torna esatto ma sbaglia di
      **~0.24px** con `Near=0.01`/`Far=1000` (i default di raylib). È precisione float
      nell'invertire la proiezione, non un errore di formula: l'errore è **piatto rispetto
      alla distanza** (0.24px a 2 unità come a 190) e scala solo col rapporto near/far
      (0.1/1000 → 0.03px; 1/100 → 0.002px). Sotto il pixel non tocca né picking né maniglie
    - [ ] *(non esercitato)* Il **trascinamento** è verificato solo nella sua matematica
      (punto più vicino fra asse e semiretta confrontato con una minimizzazione a forza
      bruta su ~20k casi): manca una prova col mouse in mano
- [x] **Persistenza** — `SceneDocument` (+ menu **File**, vedi Fase 4.6)
  - [x] Save/Load scena — erano bottoni in un pannello `ScenePanel` provvisorio, ora sono
    voci del menu File; il pannello è stato **eliminato** (vedi Fase 4.6)
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
- [x] **Play/Stop** — *deciso: Stop **ripristina** lo stato pre-Play, come Unity*.
  **Fatto in Fase 4.7bis**, dove sta tutto il razionale
  - [x] Stato `Editing / Playing / Paused`. Fuori da Play i system **non girano**: la
    fisica dev'essere ferma, non solo invisibile
  - [x] Snapshot all'ingresso in Play, ripristino allo Stop. **È il motivo per cui la
    Persistenza viene prima**: lo snapshot *è* la serializzazione — `SceneSerializer` in
    memoria invece che su file. Non è un punto separato della lista, è lo stesso lavoro
    - Prova che serve: salvando dopo appena 5 frame di simulazione, il file si era già
      portato dietro i cubi caduti (`y: 10` → `y: 8.08`). Senza snapshot, giocare e salvare
      cementa la scena a metà caduta
  - [x] I system oggi li possiede e li cicla a mano `SandboxGame` (quattro liste + quattro
    overload di `AddSystem`): è logica dell'engine che vive nel sample, e finché sta lì
    ogni gioco deve reimplementare da sé il gating del Play. Va portata nell'engine prima
    di appenderci sopra il Play/Stop — fatto in Fase 4.5 (`SystemRegistry`), ed è servito
    esattamente a questo

**Milestone raggiunta:** aggiungi entità, modifichi transform e componenti e salvi,
**senza toccare codice**. 🏆
*(crei/duplichi/elimini entità, ne modifichi i componenti dall'Inspector, premi Salva e il
`.json` sul disco cambia — poi Ricarica e la scena torna dal file.)*

**Milestone raggiunta:** l'editor **ha la forma di un editor**. 🏆
*(due viste render-to-texture affiancate — Scena navigabile col free-fly e Gioco con
l'inquadratura del giocatore, ognuna con la sua camera — clicchi nel viewport e selezioni
l'entità, e la trascini con le maniglie move/rotate/scale. F1 chiude tutto e resta il gioco
a tutto schermo.)*

**Della Fase 4 non resta niente:** il buco "aggiungere un componente dall'UI" è chiuso in
Fase 4.7, il **Play/Stop con snapshot** in Fase 4.7bis. 🎉

---

## Fase 4.5 — Rigore ECS: Component vs Resource 🟢🟡

Il punto di partenza era la richiesta "converti tutti i manager (Camera, Audio, Input,
Renderer) in Component, e **tutto ciò che non sta nel World non deve esistere**". La regola
è stata **respinta nella forma assoluta** e riscritta, perché il purismo si rompe su un caso
concreto che questo progetto ha già in casa:

> `SceneSerializer` scorre `World.ComponentStorages` e scrive ciò che trova. Un
> `RendererComponent` finirebbe dentro `demo.json`. L'unico modo per evitarlo sarebbe
> marcarlo `[RuntimeState]` — cioè **ammettere che non è dato d'autore**, che è esattamente
> il punto.

La regola adottata è lo split di Bevy (`Component` / `Resource`), più preciso e *davvero*
imponibile:

> **I dati di scena vivono nel World come Component. L'infrastruttura è una Resource
> registrata esplicitamente.** Niente dati di scena fuori dal World; niente singleton sparsi.

- [x] **`Resources`** (`src/gEngine/Core/Resources.cs`) — contenitore type-safe
  (`Add<T>`/`Get<T>`/`TryGet<T>`/`Has<T>`/`RegisteredTypes`), fail-fast come `World.GetComponent`
  - Sta in `Core/` e **non** in `Ecs/Base/`: quella cartella è casa del World, e metterci un
    contenitore che per definizione vive *fuori* dal World direbbe l'opposto della regola
  - ⚠️ La chiave è `typeof(T)`, **non** `resource.GetType()`: `Add<IPhysicsWorld>(new BepuPhysicsWorld(...))`
    si rilegge dalla **porta**, non dall'adapter. Con l'inferenza si registrerebbe sotto
    `BepuPhysicsWorld` e nessuna lettura per porta lo troverebbe mai
  - ⚠️ Stesso morso in `GameLoop`: i campi sono nullable (esistono solo dopo `InitWindow`),
    quindi l'inferenza registrerebbe sotto `IRenderer?`/`AssetManager?`. Tipi espliciti
- [x] **`IGame.Init(Resources)`** — il contratto cambia: il `GameLoop` popola le Resources
  (`InputHandler`, `AssetManager`, `IRenderer`) **dopo `InitWindow`** e prima di `Init`
  - Il motivo non è estetico: `IRenderer` era **l'unica Resource fuori posto**, registrata
    pigramente al primo frame di `Draw` con un `if (!Has<IRenderer>())`, perché `Init` non lo
    riceveva. Una regola che nasce con un'eccezione strutturale non è una regola
  - `Update(float, InputHandler)`/`Draw(IRenderer)` restano **invariati** di proposito: quei
    servizi sono richiesti *sempre*, e un parametro esplicito lo dichiara nel tipo — pescarli
    da `Get<T>()` fallirebbe a runtime invece che a compile time. La Resource resta il punto
    di verità (stessa istanza), il parametro è comodità. `Init` è il caso opposto: lì la
    firma cresceva a ogni servizio nuovo, ed è ciò che il contenitore risolve
  - `IPhysicsWorld` lo crea e lo registra il **gioco**: le Resources sono un contenitore
    condiviso, non impongono chi crea cosa
- [x] **`SystemRegistry`** (`src/gEngine/Ecs/`) — la proprietà dei system passa all'engine
  - Chiude il buco già annotato in Fase 4 (Play/Stop): `SandboxGame` teneva **quattro liste**
    e quattro overload di `AddSystem`, cioè logica dell'engine che viveva nel sample — e
    finché stava lì, ogni gioco avrebbe dovuto reimplementarsi il gating del Play
  - Un solo `Add(ISystem)` che smista sulle interfacce implementate (`SystemPhase` è `[Flags]`:
    un system può stare in più fasi)
  - **`SystemRegistry` e non `SystemScheduler`**: non ordina niente. Dentro una fase vale
    l'ordine di registrazione, e chi chiama deve ancora sapere che `LightingSystem` va prima
    di `MeshRenderSystem`. "Scheduler" prometterebbe una risoluzione di dipendenze inesistente
- [x] **`Camera3D` → dati di scena** — `CameraComponent` (solo ottica: `FovY`/`Near`/`Far`/
  `Projection`/`Primary`) + la **posa dal `TransformComponent`**, modello Unity
  - ⚠️ **Asimmetria voluta**: la camera di **gioco** è un'entità del World (dato d'autore:
    serializzata, selezionabile, col gizmo); la camera di **scena** dell'editor resta fuori
    dal World. Non è un'incoerenza, è la regola applicata: la scene camera è stato
    dell'editor — nel World verrebbe serializzata in `demo.json` e comparirebbe in Hierarchy
  - `Camera3D` **resta** come camera "risolta"/matematica (la usano `EntityPicker`,
    `TransformGizmo`, il frustum, `Begin3D`): quella matematica è verificata numericamente e
    non si riscrive. Si **deriva** con `World.GetCamera(Entity)` — e non un
    `CameraComponent.Resolve(transform)`, perché la posa dipende dal World (col `ParentComponent`
    il Transform è locale) e quella firma inviterebbe a passare il transform sbagliato
  - ⚠️ **Il trucco del riferimento `class` non funziona più.** `EditorHost.Setup` prendeva una
    `Camera3D` e il riferimento restava agganciato a ciò che il gioco le faceva ogni frame.
    Ora la camera si **ricava** dal World: va letta **per frame** (`Func<Camera3D?>`)
  - ⚠️ «Esiste sempre una camera» **non è un invariante** — stessa lezione del player
    cancellabile (Fase 4): senza `CameraComponent` nel World si degrada, non si crasha
  - Verifica numerica sulla posa storica `(0,18,28)→(0,6,0)` di `demo.json`: pos err **0**,
    dir err **4.8e-7** (arrotondamento del quaternione a 6 cifre nel file), view matrix err
    **1.6e-5**. Round-trip World→file→World: **0 esatto**
    - ⚠️ Sottigliezza che un confronto Position/Target avrebbe **nascosto**: l'`Up` derivato
      *non* è `(0,1,0)` ma l'up inclinato complanare a forward/worldUp. Non è un errore —
      `CreateLookAt` riortogonalizza, e infatti la view matrix combacia. Ma il campo grezzo
      differisce: confrontare i campi d'ingresso non è confrontare la camera
  - [x] ~~`CameraFollowSystem` scrive la posa come se la camera fosse **root**~~ — **risolto**
    con `World.SetWorldPose` (vedi Fase 1: l'inverso di `GetWorldMatrix`). `Position` vive
    nello spazio del **genitore**: finché la camera era root funzionava per coincidenza
    - ⚠️ **La frase "il gizmo ha lo stesso limite dal lato opposto" era FALSA**, ed era un
      commento nel codice ripetuto per fiducia invece che verificato. `TransformGizmo.ToParentSpace`
      divide **già** per il mondo del genitore, con `TransformNormal` (giusto: il drag è un
      **delta**, non un punto) e con l'`Invert` fallibile già gestito. Non fattorizzato sulla
      nuova API apposta: `ToParentSpace` lavora su un delta direzionale, `SetWorldPose` su una
      posa assoluta — sono due conti diversi che si somigliano
    - [x] Bonus trovato di sponda: `CameraFollowSystem` leggeva la posizione del player da
      `Position` **grezza** (locale). Ora usa `GetWorldMatrix(...).Translation` — "il player è
      root" non era un invariante più di quanto lo fosse "la camera è root"
  - [ ] *(limite noto)* Più camere `Primary`: vince la prima incontrata. Documentato, non imposto
- [x] **Traceability**: ogni `ISystem` dichiara `MatchedComponents` (default interface member,
  default vuoto → non rompe i system esterni). Dichiarativo e non `Matches(World, Entity)`:
  risponde **senza far girare i system**, e documenta di cosa parla il system
  - ⚠️ **`SystemMatch { Unknown, Matching, NotMatching }`**: "non dichiara nulla" resta
    distinguibile da "no". Appiattire a un booleano farebbe **mentire** l'UI
  - [x] **Due verbi, non uno**: `MatchedComponents` (agisce su) + `ObservedComponents`
    (**guarda ma non tocca**, opzionale, default vuoto). Nasce da un caso vivo: il `player`
    non mostrava `CameraFollowSystem`, perché quel system *legge* il player e *agisce* sulla
    camera — e chi si chiede "perché la camera non mi segue?" guarda il player
    - Il nome è `Observed` e non `Read` perché "legge" non distingue niente: un system legge
      **anche** i componenti che matcha (il `TransformComponent` che poi riscrive)
    - Dichiarato **una volta sola** in tutto il progetto, e solo dove è reale: gli altri 6
      system non leggono entità che non toccano (`LightingSystem`/`MeshRenderSystem` ricevono
      la camera come **parametro** di `OnRender`, non da una query — non è lettura di
      un'entità). Una dichiarazione inventata sarebbe peggio di nessuna
  - ⚠️ **Resta un'approssimazione, e i due verbi NON la curano**: è **metadata scritta a mano**,
    che può mentire e andare fuori sincrono col codice. `ObservedComponents` è anzi *meno*
    affidabile, perché essendo opzionale una "sezione vuota" non prova che nessuno legga
    l'entità. La cura vera sarebbe **derivare i match dalle query reali** dei system; finché
    non c'è, l'elenco è un aiuto diagnostico, **non una prova** — e l'UI lo dice
  - [x] ⚠️ **Il ramo `Unknown` non era mai stato renderizzato** e infatti **conteneva un bug**:
    l'intestazione usciva `Non si sa (?) (?)` — l'etichetta aveva già un `(?)` letterale e
    `HelpMarker` gliene aggiungeva un altro. Tutti i system del sample dichiarano i propri
    componenti, quindi quella sezione non si disegna mai: la logica era verificata sul dato,
    il **disegno** no. Esercitato con un system fittizio senza dichiarazioni + screenshot
    (`Raylib.TakeScreenshot`) **guardato davvero**, poi corretto e ri-fotografato. È la lezione
    generale: un ramo che nessun percorso reale attraversa non è "probabilmente a posto"
- **Audio**: nella richiesta iniziale era fra i manager "da convertire", ma **non esiste
  codice audio da convertire** — c'è solo `AssetManager.PlayMusic`. Quando arriverà (Fase 6),
  la regola lo assegna già: `AudioSource`/`AudioListener` sono Component (hanno una posa,
  sono dati di scena), il device audio è una Resource

**Milestone:** la regola è **imponibile invece che aspirazionale** — un dato di scena senza
entità non ha dove stare, e l'infrastruttura ha un posto dichiarato invece di essere un campo
sparso in un sample. 🏆

---

## Fase 4.6 — Lo scheletro della UI: menu bar e pannelli 🟡

L'editor aveva la *forma* di un editor (due viste, gizmi, picking) ma non la sua **struttura**:
niente barra dei menu, pannelli non richiudibili, azioni sparse su bottoni dentro finestre
provvisorie. Questa fase fa lo scheletro su cui si innestano context menu, pannelli globali e
asset browser — non le funzioni, l'ossatura.

- [x] **`IEditorPanel` ha identità e visibilità**: `Title` + `Visible`
  - Il `Title` è **l'unica fonte di verità**: stesso valore per `ImGui.Begin` e per la voce del
    menu Panels. ⚠️ Non è pedanteria — ImGui identifica le finestre **per titolo**, e due
    `Begin` con lo stesso nome sono lo stesso pannello riempito due volte, senza un warning
    (è già successo: vedi la nota su `Begin("Scena")` in Fase 4)
  - `ImGui.Begin(title, ref visible)`: la **X** della finestra e la **spunta** del menu devono
    scrivere lo **stesso** flag. Due stati paralleli divergono al primo clic
- [x] **`PanelBase`** — il preambolo (`SetNextWindowPos/Size` + `Begin` + il ramo "collassato →
  `End` e torna") era già copiato identico in ogni pannello: aggiungere `Visible` a mano avrebbe
  voluto dire quattro copie del punto più delicato. `Visible` sta **nell'interfaccia** (l'host
  li usa in modo polimorfico: il menu Panels elenca pannelli senza sapere cosa siano),
  l'implementazione in **una** classe base
  - Due hook per non stravolgere il `ViewportPanel`, che ha esigenze reali: `WindowFlags`/
    `WindowPadding` (i suoi `NoScrollbar` restano) e `OnNotDrawn()` — un viewport **chiuso**
    deve smettere di riempire il render target, non solo di mostrarlo
- [x] **Top Menu Bar** (`MainMenuBar.cs`, fuori da `EditorHost`: l'host orchestra, non fa UI)
  - **File**: New / Open… / Save. **Panels**: elenco spuntabile di tutti i pannelli, da cui si
    riaprono quelli chiusi
  - ⚠️ **Ordine con il dockspace**: la barra va disegnata **prima** di `DockSpaceOverViewport`.
    `BeginMainMenuBar` sottrae la propria altezza alla work area del viewport, ed è su quella
    che il dockspace si dispone — invertendo, i pannelli dockati in alto finiscono **sotto** la
    barra
  - **Open** è un popup ImGui che elenca i `.json` della cartella scene, **non** un file dialog
    di sistema e nessuna libreria in più: ImGui non ha un file dialog, e tirarne dentro uno per
    aprire una scena sarebbe una dipendenza per un menu
  - Il try/catch di `ScenePanel` (salvare tocca il disco: un'eccezione nel frame di disegno
    butta giù il gioco, l'errore va **mostrato**, è lì che l'utente rimedia) è stato **portato**,
    non riscritto. La barra dei menu è già una status bar: gli errori si vedono lì
- [x] **Pannello "File scena" eliminato**: era provvisorio, le sue azioni sono nel menu File
- [x] **Layout di default a 5 pannelli**: Scena, Gioco, Inspector, File system, Hierarchy
  - ⚠️ Il layout salvato in `imgui.ini` (working directory, gitignorato) **vince** sui default
    `FirstUseEver`: per provare davvero il primo avvio va cancellato
- [~] **`FileSystemPanel`** — stub navigabile (elenca `assets`, doppio clic per entrare, `..`,
  radice difesa). Il **drag&drop** è arrivato in Fase 4.7; le mutazioni su disco no. Deduce
  `assets` da `AppContext.BaseDirectory` (stessa convenzione di `SandboxGame.Init`) invece di
  aggiungere un parametro a `Setup`; se un gioco tiene gli asset altrove il pannello **lo
  dice** invece di fingere
- [ ] *(limite noto)* **`New Scene` lascia il documento senza percorso** e non esiste un
  "Save As": `SceneDocument.Save` lancia con un messaggio esplicito invece di scrivere a caso.
  La guardia sta nel document e non nel menu, così il chiamante non deve ricordarsene
- [ ] *(buco di verifica)* Popup Open, New e Save sono verificati **per costruzione**, non a
  mano. Il resto della barra è stato guardato in uno screenshot. ⚠️ "Non c'era modo di
  cliccarli" **non è più vero**: la Fase 4.7 pilota la finestra viva con `PostMessage` da
  PowerShell — questi tre non sono ancora passati di lì, ma ora si può

**Milestone:** l'editor ha una **struttura** invece di una collezione di finestre — c'è una
barra dei menu, i pannelli si chiudono e si riaprono, e il layout d'avvio è quello giusto. 🎉

---

## Fase 4.7 — L'editor si usa: context menu, inventari, asset 🟢

Lo scheletro della 4.6 ora ha i muscoli. Tre cose che l'editor prometteva e non faceva:
manipolare un'entità dove la si guarda, sapere di cosa è fatto il gioco, e mettere un asset
in un campo.

### La decisione: da dove nasce un componente

Il buco annotato dalla Fase 4 (**aggiungere un componente da UI**) non era un lavoro
rimandato, era una **domanda senza risposta**: l'engine non conosce l'elenco dei tipi
istanziabili, e senza un valore di partenza "aggiungi" non significa niente.

- **Scartato: `Activator.CreateInstance` / `default(T)`.** I componenti sono struct di dati
  nudi, quindi "creane uno" senza dire come dà **tutti i campi a zero** — che non è un default
  neutro, è un default **rotto**, in modo diverso per ogni tipo: `Transform` con `Scale = 0` è
  invisibile e la sua rotazione non è nemmeno un quaternione valido `(0,0,0,0)`; `Camera` con
  `FovY = Near = Far = 0` non inquadra niente; `Light` con `Intensity = 0` non illumina;
  `MeshRenderer` nasce `Visible = false`. L'utente avrebbe visto "componente aggiunto" e
  **nessun effetto**: il bug più caro da cercare è quello che assomiglia a un no-op.
- **Scartato: scandire gli assembly** per trovare i tipi. Offrirebbe di aggiungere anche i
  tipi di una libreria di terze parti.
- **Preso: il default lo dichiara chi registra il componente**, dentro il
  `SceneComponentRegistry` — la strada già ipotizzata in Fase 4. È lo stesso principio di
  `[EditorConfiguration]`: l'editor manipola dati che non conosce, e ha bisogno che sia **il
  tipo** a dirglielo. Un componente custom diventa aggiungibile registrandolo una volta sola
  (vedi `SandboxGame.Init`: `createDefault:` accanto a parse e write).
  - Sta nel registry delle **scene** e non in un secondo registry perché la domanda è la
    stessa che quello risponde già: *di quali componenti è fatto questo gioco e come si
    chiamano*. Un secondo elenco = un secondo posto in cui dimenticarsi il componente nuovo
  - `createDefault` è **opzionale**, e chi non ce l'ha **resta nell'elenco ma spento, col
    motivo**: sparire manderebbe a cercare la registrazione mancante nel posto sbagliato.
    ⚠️ Per `Parent` è **voluto e definitivo**: un genitore di default non esiste (`Entity(0)`
    non è un'entità), ci si riparenta dalla Hierarchy
  - I default dell'engine sono scelti perché **si vedano**: Transform neutro, Light bianca a
    intensità 1 e *Directional* (una point nell'origine sarebbe dentro il pavimento),
    RigidBody dinamico massa 1, MeshRenderer cubo bianco visibile, Camera 60°/0.01/1000 e
    `Primary = false` (non ruba l'inquadratura a quella che sta già guardando)
- `World.AddComponent(Entity, object)` — la controparte **non generica**, gemella di
  `HasComponent(Entity, Type)` e mossa dallo stesso bisogno: il tipo esce da un elenco, non
  c'è nessun `T` da scrivere. Chiude `ComponentStorage<>` a runtime una volta per tipo

### Fatto

- [x] **Context menu al posto dei bottoni** (Task 3). Via la toolbar Nuova/Duplica/Elimina
  della Hierarchy e la `X` sull'header dei componenti — chiedevano di selezionare prima e
  cliccare poi, e la `X` era piazzata con un `SameLine` su una coordinata calcolata a mano
  - Destro su un'entità → Crea entità figlia / Duplica / Elimina; destro nel vuoto → Crea
    entità; destro su un header dell'Inspector → Rimuovi componente
  - Il bersaglio del comando è **la riga cliccata**, non la selezione: sono due cose che
    possono divergere. Che il destro selezioni anche è un servizio all'Inspector
  - ⚠️ `ImGuiPopupFlags.NoOpenOverItems` sul popup della finestra: senza, il destro su una
    riga apre **anche** quello — due id diversi, quindi ImGui non segnala niente e si vede il
    menu sbagliato esattamente dove ci si aspetta l'altro
  - `EntityOperations.Create` — la creazione è **politica dell'editor** come Duplicate e
    DestroyRecursive, quindi sta lì. ⚠️ Ci ha portato un bug vero: due entità create di
    seguito si chiamavano entrambe "Nuova entità", e due omonime **collassano** nella mappa
    nome→Entity di `SceneInstantiator` al reload (vince l'ultima, senza un errore). Ora il
    nome è reso libero, come già faceva `Duplicate`
- [x] **Pannelli globali `Systems` e `Components`** (Task 4), spenti all'avvio e accesi dal
  menu Panels: sono diagnostici, non roba da tenere addosso
  - **Systems**: elenca **per fase e nell'ordine reale di esecuzione**, perché dentro una fase
    l'ordine *è* comportamento (⚠️ `LightingSystem` prima di `MeshRenderSystem`) ed è ciò che
    un elenco mostra e il codice no. Un system in due fasi compare **due volte**: gira due
    volte. Tooltip con `MatchedComponents`/`ObservedComponents`, destro → Rimuovi
  - **Rimossi in questa sessione** + Ripristina: l'editor non sa *costruire* un system (ha
    dipendenze — `PlayerInputSystem` vuole un `InputHandler`), quindi senza la lista togliere
    un system sarebbe irreversibile fino al riavvio. ⚠️ Ripristina rimette il system **in
    fondo** alla sua fase, non dov'era, e richiama `OnCreate` sulla stessa istanza (oggi tutti
    vuoti, quindi non si vede)
  - [ ] *(limite dichiarato)* **Aggiungi system non c'è**, e il bottone è spento **col motivo
    nel tooltip** invece che assente: servirebbe che il gioco dichiarasse le factory dei suoi
    system, ed è l'unico posto dove le dipendenze si sanno
  - **Components**: i tipi registrati con quante entità li usano e se sono aggiungibili. La
    sezione che giustifica il pannello è la terza — **"nel World ma non registrati"**: un tipo
    lì dentro rende la scena **non salvabile** (`SceneSerializer` lancia), e prima lo si
    scopriva premendo Salva. `NameComponent` e i `[RuntimeState]` sono elencati **con la loro
    ragione accanto**, non come allarmi
- [x] **Drag&drop tipizzato** (Task 6): un asset dal File system a uno slot dell'Inspector
  - La **validazione è il payload stesso**, non un controllo dopo il drop: ImGui accoppia
    sorgente e bersaglio confrontando la stringa che identifica il payload, quindi un `.mp3`
    sopra lo slot Model non si illumina nemmeno. È la differenza fra validare e rendere
    irrappresentabile. ⚠️ ImGui tiene quel nome in un `char[32+1]` e **asserisce** se è più
    lungo — da qui il prefisso corto
  - `[EditorAsset(AssetKind.Model)]` — il **terzo attributo** della famiglia
    (`[RuntimeState]` esclude un componente, `[EditorConfiguration]` espone un valore, questo
    espone uno **slot**). Nell'engine, non nell'editor: un componente si descrive senza
    referenziare l'editor. `MeshRenderer.Model` era **volutamente non esposto** in attesa di
    questo: il dato d'autore è il *path*, l'handle è un id di cache che al reload punta a un
    modello a caso
  - Il payload porta il **path relativo alla cartella asset** (non l'handle: a quel punto il
    modello non è caricato, e sfogliare una cartella non deve caricarne il contenuto).
    ⚠️ Regge perché le due radici coincidono — il pannello deduce `assets` con la stessa
    convenzione dell'`AssetManager`
  - `AllowUnsafeBlocks` acceso in `gEngine.Editor`: `AcceptDragDropPayload` torna un wrapper
    su un `ImGuiPayload*` che è **null** quando non è stato rilasciato niente, e quel
    confronto in C# vuole `unsafe`. Un blocco solo, in `AssetDragDrop.TryRead`
  - ⚠️ **Limite dichiarato**: lo slot non conosce gli altri campi del componente. Assegnare un
    modello a un `MeshRenderer` con `Kind = Cube` riempie il campo e **non cambia cosa si
    vede**. L'editor lo dice **nel tooltip dello slot** invece di aggiustare `Kind` da sé —
    un'UI generica che indovina i campi correlati indovina anche quando non deve, e la strada
    "setter con logica" cambierebbe il comportamento del **caricamento** (un file con
    `Kind = Cube` e un `ModelPath` verrebbe sovrascritto)

### Il buco di verifica della 4.6 è chiuso

I popup non erano mai stati **cliccati** ("non c'era modo di automatizzare l'input"). Ora c'è:
si pilota la finestra viva con `PostMessage` (WM_MOUSEMOVE / WM_?BUTTONDOWN / UP) da
PowerShell, mentre il gioco si fa gli screenshot da solo a frame prestabiliti. Verificati **a
video**: i quattro menu contestuali, la popup Aggiungi componente (che offre Camera/Light/
RigidBody, esclude quelli già addosso e mostra Parent spento), e il drag&drop completo — uno
slot passato da "(nessun modello)" al path, col valore che resta dopo il rilascio.

⚠️ Limiti del rig, non del codice: il **doppio clic** sintetico non si riesce a riprodurre
(rlImGui campiona lo stato del mouse **a livello, una volta per frame**, e i tempi non tornano),
e un trascinamento che parte da un punto vuoto **sposta la finestra ImGui** invece dell'item.

### Trappole pagate in questa sessione

- ⚠️⚠️ **`BeginPopupContextItem()` dopo un `Text` fa `IM_ASSERT`**: senza id usa quello
  dell'ultimo item, e **un `Text` non ha id** (trova 0). Su Windows quell'assert è una
  **dialog modale nativa**: il gioco non crasha e non logga — si **pianta al primo frame**,
  aspettando un clic su una finestra che nessuno ha chiesto. Sembrava un hang del game loop.
  La cura è un `Selectable` (che ha un id, e per giunta si illumina al passaggio). Era in
  **due** pannelli
- ⚠️ **Le stringhe che passano a ImGui possono usare solo Latin-1** (`0x20–0xFF`): è la
  copertura del font di default (`GetGlyphRangesDefault`), tutto il resto esce come `?`. Non è
  "niente emoji" — quella era la formulazione stretta, e infatti ha lasciato passare il
  problema vero: la **lineetta lunga `—`** (U+2014), che si scrive senza pensarci ed era già
  in un tooltip della Fase 4. Il `·` (U+00B7) invece va bene: è dentro Latin-1. Nei
  **commenti** il `⚠️` resta (quelli non li legge ImGui); nelle stringhe: "Attenzione:", "-",
  "(!)". Il modo di trovarli tutti è scandire i sorgenti per i caratteri `> 0xFF` nelle righe
  non di commento — non rileggerli sperando di accorgersene
- ⚠️ Un bottone a **larghezza piena** (`-1`) seguito da `SameLine` spinge l'etichetta **fuori
  dal pannello**. Lo slot degli asset è nato **senza nome** così. Per allinearsi agli altri
  campi si usa `ImGui.CalcItemWidth()`, che è la larghezza che lasciano i `DragFloat`
- ⚠️ L'id della **fase** deve avvolgere quelli delle righe nel pannello Systems: ImGui non
  guarda in che `SeparatorText` stai, guarda lo stack degli id — la riga 0 di Input e la riga
  0 di Simulation sarebbero lo **stesso** item

**Milestone:** l'editor non si guarda più, si **usa**: si crea e si manipola dove si guarda,
si vede di cosa è fatto il gioco, e un modello si mette in una scena trascinandolo. 🎉

---

## Fase 4.7bis — Play / Stop: la Fase 4 è chiusa 🟢

L'ultimo pezzo, e arrivato buon ultimo perché **dipendeva da tutto il resto**. Play/Stop è
fatto di due metà, e nessuna delle due è nuova:

1. **Il gating** — "non far girare i system quando non si sta giocando". Serviva un posto solo
   dove i system vivono: il `SystemRegistry`, nato apposta in Fase 4.5. Il gating **non** sta
   nel registry né nell'editor: sta nel **gioco**, che è l'unico a chiamare le fasi. L'engine
   non sa che esiste un editor, l'editor non sa quali fasi il gioco faccia girare. L'editor
   espone solo la verità su cui si decide (`EditorHost.ShouldSimulate`).
2. **Lo snapshot** — "rimetti tutto com'era". Non è una funzione nuova: **è la
   serializzazione, in memoria invece che su file**. Giocare e poi fermarsi è un
   Salva-senza-file seguito da un Apri-senza-file. È il motivo per cui il salvataggio inverso
   (Fase 3) valeva la pena anche prima che qualcuno volesse premere Salva.

### Le scelte

- **Lo snapshot si prende PRIMA di partire, e se fallisce non si parte.** È la scelta che
  rende la cosa sicura: una scena non serializzabile (un componente senza writer, un genitore
  senza nome) romperebbe lo Stop, cioè si scoprirebbe di non poter tornare indietro *dopo*
  aver giocato. Fallendo al Play si perde un clic; fallendo allo Stop si perde il lavoro.
  L'errore passa dallo stesso `MainMenuBar.Run` del salvataggio — è lo stesso errore. Il
  pannello Components (Fase 4.7) lo dice ancora prima, a riposo
- **Render non è gated**: in Editing la scena si deve **vedere**, ferma. Solo
  Input/Simulation/Late si fermano
- **Il free-fly della camera di scena non è gated**: navigare è dell'editor, non del gioco —
  ed è in Editing che serve di più
- **La selezione si ritrova per nome**, non per `Entity`: ⚠️ lo Stop distrugge e ricrea, quindi
  **gli id cambiano** (verificato: `Entity 4` → `Entity 17`, stessa entità). Il nome è l'unica
  identità che sopravvive al giro — è già così che i riferimenti attraversano il file. Un'entità
  senza nome non si ritrova: è lo stesso limite del formato, non uno in più
- **`[RuntimeState]` non torna indietro, ed è giusto**: il corpo Bepu dello snapshot punterebbe
  a un corpo che non c'è più. Lo ricrea il `PhysicsSystem` al primo update. ⚠️ Ma in Editing
  quel system non gira, quindi la pulizia degli orfani arriva al Play successivo
- **Pausa** c'è perché una volta che lo stato esiste costa tre righe, e "congela e guarda" è
  metà del motivo per cui si preme Play in un editor
- [x] **F1 entra in Play** *(deciso dal proprietario, fra tre alternative)*. `ShouldSimulate`
  resta `!Visible || Playing`, ma il `!Visible` ora è una **rete**, non la regola:
  `EditorHost.SetVisible` prende lo snapshot ed entra in Playing quando si chiude l'editor.
  - **Il problema che chiude**: prima si usciva con F1 restando in Editing, il gioco girava
    **senza snapshot**, e rientrando si trovava la scena mossa e nessuno Stop da premere. Chi
    preme F1 sta chiedendo di giocare, e chi gioca deve poter tornare indietro
  - **Le altre due strade, scartate**: lasciare com'era (buco aperto); far rispettare a F1 lo
    stato, cioè mostrare la scena **congelata** a schermo intero — più puro, ma sembra un gioco
    rotto e rompe l'invariante *editor chiuso = il gioco vero*
  - ⚠️ Il prezzo, dichiarato: **F1 fa due cose** (nasconde l'UI e fa partire il gioco) e si
    rientra in uno stato che non si era chiesto. Lo **Stop acceso** nella barra è ciò che lo
    ricorda
  - ⚠️ **Non è simmetrico**: rientrare non ferma. Fosse un'anteprima usa-e-getta si giocherebbe,
    succederebbe qualcosa di interessante, si rientrerebbe per guardarlo — e non ci sarebbe più
  - ⚠️ Il `!Visible` resta per quando il Play **non parte** (registry non dichiarato, scena non
    serializzabile): lì si gioca senza snapshot. Sembra il buco di prima e non lo è — un gioco
    che non sa serializzare la propria scena non aveva niente da ripristinare, né qui né con un
    Salva
  - `Visible` è diventata `private set`: nasconderlo ha una conseguenza, quindi non può essere
    un assegnamento. E `PlayMode.Start` non lancia più (torna `bool` + `LastError`): lo chiamano
    due posti, e F1 non saprebbe dove mostrare un'eccezione
  - ⚠️ **Verificato solo a metà**: si vede l'uscita e la simulazione che parte, ma il rig non
    riesce a pilotare F1 in **rientro** (i tasti sintetici non passano come i clic). Lo Stop
    acceso al rientro è verificato **per costruzione** — primo controllo da fare a mano

### Verificato a video (non per costruzione)

Col rig della Fase 4.7, guardando `falling-cube-red` (autorato a `y = 10`, cade a `y ≈ 0.5`):

| stato | Position.Y | cosa dimostra |
|---|---|---|
| Editing | `10.000` | i system stanno fermi: il gating funziona |
| Play | `0.727` | il gioco gira davvero |
| Stop | `10.000` | lo snapshot ha rimesso tutto com'era |

Allo Stop l'entità è tornata selezionata pur essendo diventata `Entity 17`, e `PhysicsBody` è
sparito dall'Inspector — esattamente ciò che il design prevede.

### Il tema (`EditorTheme`)

Prima c'era il dark di default di rlImGui, cioè il tema di ImGui — quello che ha **qualunque
cosa** costruita con ImGui, incluse le finestre di debug buttate lì in un pomeriggio. Non è
brutto: è **anonimo**, e un editor che assomiglia a un pannello di debug viene usato come un
pannello di debug. Le regole, che contano più dei numeri:

- **Una sola scala di grigi** neutra, con i piani separati dalla **luminosità** e non dai bordi:
  la finestra è più scura dei suoi campi, che sono più scuri di quelli sotto il puntatore. Così
  "cosa è cliccabile" si legge senza cercare un contorno. Un bordo per riquadro, in un editor
  pieno di riquadri, è una griglia che non aiuta a leggere niente
- **Un solo accento**, desaturato, speso solo dove significa: selezione, spunte, maniglie. Il
  blu di ImGui è saturo e finisce ovunque — con dodici pannelli aperti diventa rumore. I bottoni
  partono dal grigio dei campi: un bottone blu ogni tre righe trasforma l'Inspector in un
  semaforo
- **La barra del titolo è più scura della finestra**, non più chiara: il pannello attivo si
  distingue per il testo e il bordo, non per una fascia luminosa che tira l'occhio su ogni
  finestra aperta
- **Raggi piccoli e uguali** (4px, 2px per le maniglie): il default mescola angoli vivi e
  arrotondati, e l'incoerenza si nota anche quando non si sa cosa si sta notando
- ⚠️ **Due colori restano accesi e saturi apposta**: il bersaglio del drag&drop e il cursore di
  navigazione. Sono gli unici che devono **interrompere** — uno slot che accetta un modello si
  deve vedere *mentre* lo si trascina sopra, non dopo
- [ ] ⚠️ **Il limite più grosso resta il font**: ProggyClean, il bitmap font di default, è ciò
  che fa sembrare "prototipo" più di ogni colore. Cambiarlo vuol dire spedire un `.ttf` — peso,
  licenza, e un path che non sia quello di Windows. È una decisione a sé, non un ritocco

**Milestone:** la **Fase 4 è chiusa**. L'editor autora una scena, la fa girare, e la rimette
com'era. 🎉

---

## Fase 4.9 — Script: si scrivono negli asset, il motore li trova 🟢🟡

*Richiesta del proprietario: «devo poter scrivere script custom, e chi usa l'editor non tocca i
file core dell'ECS». Deciso: si va verso i `.cs` sotto `assets/` compilati a runtime — ma
quello è lo strato **sopra** questo.*

Il problema non era la verbosità di `_systems.Add(new MovementSystem())`: era che **scrivere
uno script significava modificare un file che non è lo script**. Due punti lontani (il
`Game.Init` e il registry delle scene), e dimenticarne uno **non dà un errore di
compilazione** — dà un system che non gira mai, o una scena che non si salva.

- [x] **`[GameSystem(Order)]`** — la classe si dichiara e `ScriptDiscovery` la trova
  - Le **dipendenze da costruttore** si risolvono dalle `Resources` per tipo
    (`PlayerInputSystem` vuole un `InputHandler`). È **il** caso d'uso per cui le Resource
    esistono: un contenitore dove è *dichiarato* di cosa vive il gioco. Ciò che non è lì dentro
    non è iniettabile, e la scoperta lo dice col nome di quel che manca invece di costruire un
    system mezzo rotto. Ha richiesto `Resources.TryGet(Type, out object)`, gemella non generica
  - ⚠️ **`Order` esiste perché la riflessione non ha un ordine**: `GetTypes()` cambia
    ricompilando, e dentro una fase l'ordine *è* comportamento. Senza, aggiungere uno script
    potrebbe riordinarne altri due e non lo direbbe nessuno
  - ⚠️ **Dove si chiama `RegisterSystems` conta**: l'`Order` ordina gli script *fra loro*, non
    rispetto ai system che il gioco registra a mano. Resta una riga visibile in `Init` — è
    l'unico punto in cui la decisione si prende, e nasconderla dentro la scoperta sarebbe
    peggio che scriverla
  - Si chiama `GameSystem` e non `System` per un motivo stupido e insormontabile: `[System]`
    collide col namespace `System` e non compila
- [x] **`[GameComponent(key?)]`** — chiave dedotta dal nome del tipo meno "Component" (la
  stessa convenzione degli header dell'Inspector). Chiavi in conflitto → **fail-fast**: due
  componenti sotto lo stesso nome vorrebbero dire caricare una scena istanziandone uno a caso
  - Il default resta **dichiarato**, con un `public static T CreateDefault()` trovato per
    convenzione: ⚠️ **niente `Activator.CreateInstance` come ripiego** — per uno struct di dati
    nudi darebbe zeri, cioè il default rotto travestito da neutro contro cui è stata presa la
    decisione della Fase 4.7. Chi non lo dichiara resta spento col motivo, come prima
  - ⚠️ **Non sostituisce `RegisterEngineDefaults` e non deve**: i componenti dell'engine hanno
    binder **asimmetrici** che un attributo non può esprimere (`MeshRenderer` converte un path
    in handle, `Parent` un nome in `Entity`). L'attributo copre il caso normale, che è quello
    di quasi tutti i componenti di un gioco
- [x] **`PlayerComponent`/`VelocityComponent` fuori dall'engine** → `samples/Sandbox/Components/`.
  Erano componenti del **gioco parcheggiati nel core** — nessun file dell'engine li ha mai
  usati (solo un commento dell'Inspector li citava come esempio). È esattamente ciò che
  l'attributo esiste per rendere inutile, e finché stavano lì la regola era smentita dal
  sample che doveva dimostrarla
- [x] **`ScriptDiscovery` prende un `Assembly` e non gli importa da dove venga** — è il perno:
  quando arriverà la compilazione a runtime, le si passerà l'assembly prodotto e questi stessi
  metodi funzioneranno. Costruire questo strato prima è ciò che rende l'altro un'aggiunta
  invece che una riscrittura. `ReflectionTypeLoadException` è già gestita: con un assembly
  compilato insieme al gioco non capita quasi mai, ma con gli script a runtime sarà il caso
  **normale**
- [x] **Compilazione a runtime dei `.cs` sotto `assets/scripts/`** — Roslyn
  (`Microsoft.CodeAnalysis.CSharp`) in `gEngine.Editor`, **non** nel core: sono ~10MB che
  servono solo mentre si sviluppa, e un gioco spedito non deve compilare i propri script (li ha
  già compilati chi ha usato l'editor). Stessa regola di ImGui. Il core resta con gli attributi
  e `ScriptDiscovery`, che è ciò che serve a un gioco spedito
  - **La prova**: `MovementSystem`, `PlayerInputSystem` e `CameraFollowSystem` sono usciti dal
    `.csproj` e vivono in `assets/scripts/`. Il gioco compila **senza di loro**, e il pannello
    Systems a runtime li mostra al posto giusto — identico a quando erano sei righe a mano
  - ⚠️ **`<Compile Remove="assets\**" />`** nel csproj: senza, il glob dell'SDK compila i `.cs`
    degli script **anche** dentro il gioco, e ScriptDiscovery troverebbe due copie di ogni tipo.
    Per il progetto uno script è un **dato**, come un modello
  - ⚠️ **La freccia del tempo**: uno script può nominare i tipi del gioco (`using
    Sandbox.Components;` — l'assembly del gioco è fra le reference), il gioco **non** può
    nominare i tipi di uno script, perché quando è stato compilato non esistevano. Non è un
    limite da aggirare: è il motivo per cui i system possono essere script (nessuno li nomina,
    li trova la scoperta) e un componente che l'HUD interroga per nome no
  - ⚠️ **Un errore di compilazione non è un crash**: `ScriptCompiler` non lancia mai. Torna
    l'esito, e il `ScriptsPanel` lo mostra **aprendosi da solo** — l'unico pannello a cui è
    concesso, perché ha da dire una cosa che l'utente non sa ancora di dover chiedere. Il
    sintomo altrimenti sarebbe "il mio system non funziona", e si cercherebbe il bug dentro il
    system invece che nella riga che non compila
  - ⚠️⚠️ **Trappola pagata: gli implicit usings.** Uno script è identico a un file del
    progetto, ma il csproj ci mette dentro un `GlobalUsings.g.cs` generato e Roslyn su file
    grezzi no: 9 CS0246 su `IReadOnlyList`/`Type` che sembravano riferimenti mancanti. Vanno
    aggiunti a mano, con l'elenco dell'SDK — un elenco diverso sarebbe un dialetto di C# da
    imparare
  - ⚠️ **E le reference sono gli assembly CARICATI, non quelli referenziati**: gli implicit
    usings promettono `System.Net.Http`, che nessuno carica mai → CS0234 dentro un file che non
    esiste. Un `Assembly.Load` esplicito. La regola generale resta: una libreria che il gioco
    referenzia senza mai toccare non è caricata, quindi gli script non la vedono
  - `AssemblyLoadContext` **collezionabile** anche se oggi non si scarica niente: la scelta non
    si può cambiare dopo, costa nulla ora e sarebbe da rifare tutto poi
- [ ] **Ricaricare gli script senza riavviare** (hot-reload). ⚠️ Il punto duro non è
  ricompilare: è **cosa succede alla scena quando un tipo sparisce o cambia forma** mentre il
  World ne tiene istanze. La strada è la stessa del Play/Stop — snapshot → ricompila →
  reistanzia — perché **lo snapshot è JSON**, cioè parla di chiavi e non di tipi, quindi
  sopravvive al cambio. `PlayMode` è già metà del lavoro. ⚠️ Ostacolo vero già individuato:
  `World.Clear()` **non toglie gli storage**, che tengono i `Type` del vecchio assembly e ne
  impediscono lo scaricamento. Vedi `HANDOFF.md`

**Verificato a video**, in due passaggi. Prima con gli script ancora nel progetto: il pannello
Systems mostra `Input: PlayerInput, CameraFollow` · `Simulation: Movement, Physics` · `Render:
Lighting, MeshRender` — **identico** alle sei righe scritte a mano di prima, e la scena resta
illuminata (cioè le luci arrivano ancora prima delle mesh). Poi con i tre system **spostati in
`assets/scripts/` e tolti dal `.csproj`**: il gioco compila senza di loro e il pannello li
mostra uguali. Nel mezzo, il `ScriptsPanel` si è aperto da solo a dire che 9 righe non
compilavano — che è esattamente il suo mestiere.

**Milestone:** un system si scrive in **un file dentro gli asset**, e il motore lo trova. Chi
usa l'editor non tocca né il core dell'ECS né il progetto. 🎉

**Resta** il ricaricamento a caldo: oggi gli script si compilano **all'avvio**, quindi cambiarne
uno vuol dire riavviare il gioco.

---

## Fase 4.8 — Undo/Redo: l'editor si può esplorare 🟢

Fino a qui ogni azione distruttiva era **definitiva** — Elimina, "Rimuovi da tutte", ogni
trascinamento del gizmo — e si sopravviveva solo perché il disco non veniva toccato: riaprire
la scena era l'annulla del poveraccio. ⚠️ Ed è il motivo per cui questa fase viene **prima**
del FileSystem scrivibile: quel pannello aggiunge "elimina file", e lì quella rete non c'è.

### La decisione: grana fine, non snapshot

*Presa dal proprietario fra due strade.* L'handoff dava lo snapshot a grana grossa per "quasi
gratis, con quello che esiste" — riusare `PlayMode`, che serializza la scena in memoria.
**Guardandolo da vicino non regge**, e non per costo:

> `PlayMode.Stop` è `World.Clear()` + `Instantiate`. Usarlo come undo vuol dire che **annullare
> la digitazione di un numero ricostruisce l'intera scena**: tutti gli id cambiano, i
> `[RuntimeState]` spariscono e vengono ricreati, la selezione di un'entità senza nome si perde,
> la scena sbatte. E la serializzazione **può fallire** — un undo che lancia è peggio di niente.

- **Preso: command stack a grana fine.** Esatto, istantaneo, gli id restano quelli, non può
  fallire. È anche ciò che la Fase 6 già prevedeva (*command pattern*)
- ⚠️ **La regola che tiene insieme il disegno**: un comando si costruisce **attorno a
  un'operazione già avvenuta**. Chi modifica continua a modificare come prima — l'Inspector
  scrive nello storage, la Hierarchy chiama `EntityOperations` — e il comando fotografa il prima
  e il dopo. Niente dispatcher da cui far passare tutto: quella strada ha **un solo modo di
  rompersi**, dimenticare un punto, e il sintomo è una scena che si muove e uno stack che non lo
  sa. Ne segue che `Redo` è idempotente e che `Push` non esegue niente
- **Tre comandi, non cinque**: `EntityStateCommand` ("i componenti di questa entità sono passati
  da così a così") copre campo dell'Inspector, gizmo, slot d'asset, aggiungi/rimuovi componente
  **e riparentamento**; `EntityLifetimeCommand` copre creazione ed eliminazione **nello stesso
  tipo** (sono la stessa frase letta al contrario); `CompositeCommand` tiene insieme ciò che per
  l'utente è stato un clic solo. Uno per verbo sarebbe stato quattro modi di scrivere la stessa
  coppia di fotografie — e quattro posti in cui sbagliarla
- [x] **`World.RestoreEntity(id)`**: le entità tornano con **lo stesso id**. Ricrearle con uno
  nuovo renderebbe l'annullamento una bugia — ogni riferimento resterebbe rotto (il `Parent` di
  un figlio, la selezione, i comandi più vecchi) e si sarebbe rimesso in scena un **sosia**.
  ⚠️ Non viola "gli id non si riusano": quella regola vieta di dare l'id di un'entità morta a una
  **diversa**, qui è la stessa che torna. Lancia se l'id è vivo, che sarebbe l'aliasing esatto
  che la regola esiste per impedire
- [x] **Il confine del gesto**: un `DragFloat` riscrive il componente a **ogni frame**, quindi
  senza un confine lo stack prenderebbe sessanta comandi al secondo — un annulla che disfa un
  millimetro alla volta è peggio di nessun annulla. L'Inspector lo deduce da `IsAnyItemActive`
  letto **prima** di disegnare (cioè riferito al frame precedente); il gizmo ce l'ha esplicito
  (afferra/rilascia). ⚠️ Vale per **entrambe** le strade: non è un dettaglio della grana fine
- [x] **La storia si butta quando il World è sostituito in blocco**: Play, Stop, Apri, Nuova.
  ⚠️ Non è prudenza, è correttezza — dopo un `World.Clear` i comandi parlano di entità che non
  esistono, e il loro Undo le farebbe **rinascere** dentro una scena a cui non appartengono
- [x] Menu **Edit** con dentro **il nome** di ciò che si annulla ("Annulla sposta lamp-green"),
  Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z. ⚠️ Le scorciatoie tacciono se `WantTextInput`: dentro un
  `InputText` l'annulla è quello di ImGui, che disfa le **lettere** — rubargli il tasto vorrebbe
  dire che sbagliare una lettera rinominando annulla qualcos'altro nella scena
- [ ] *(fuori portata, per scelta)* **Il disco non è coperto**: un comando in memoria non può
  resuscitare un file. Quando il FileSystem saprà cancellare servirà una rete diversa — il
  Cestino di Windows, non lo stack

### Il bug trovato mentre si costruiva

⚠️⚠️ **`GetBoxed` ha due metà e ne era stata scritta una sola.** Il commento diceva "se il
componente è uno struct, il boxing ne fa una copia" — vero, ma l'altra metà è che **se è una
`class` si ottiene il riferimento**. `EntityOperations.Duplicate` copiava così, e il commento lì
diceva *«i componenti sono struct (o piccole class di dati), quindi `GetBoxed` basta»*: la
parentesi era proprio il caso in cui non bastava.

**Misurato**: duplicando un'entità, originale e copia condividevano lo stesso
`MeshRendererComponent` — dipingere di rosso la copia dipingeva anche l'originale
(`ReferenceEquals` = true; lo struct di controllo restava indipendente, quindi è esattamente il
caso `class`). Ora c'è `ComponentCopy.Shallow` (`MemberwiseClone` per reflection: prende anche i
campi privati ed ereditati, che un giro di `GetFields` sbaglierebbe in silenzio), usata dalla
duplicazione **e** dagli snapshot dell'undo — che avevano lo stesso problema in forma peggiore:
un "prima" che è lo stesso oggetto del "dopo" non è una storia, è un alias.

### Verificato

Sequenza di **sei** azioni vere (crea, duplica, riparenta, modifica un componente, aggiungi un
componente, elimina un sottoalbero), poi indietro tutto e avanti tutto, confrontando l'**impronta
del World** (entità per id, componenti per tipo, campi per nome):

| | esito |
|---|---|
| undo di tutto == stato iniziale | ✅ |
| redo di tutto == stato finale | ✅ |
| **controllo**: ripristino *senza rimuovere* i componenti comparsi | ❌ **come deve** |
| l'entità eliminata torna con lo stesso id (3 → 3), e la successiva non collide (4) | ✅ |
| componente `class` mutato in loco: il "prima" non è stato sovrascritto | ✅ |

⚠️ Il controllo è la metà che conta: rimettere i componenti fotografati **senza togliere quelli
comparsi nel frattempo** è l'errore facile, e l'undo di "aggiungi componente" sembrerebbe
funzionare — i valori tornano indietro e il componente resta lì.

**A video** (rig della Fase 4.7): riparentamento → **Ctrl+Z da tastiera** che lo disfa → menu
Edit che mostra *"Annulla sposta lamp-green"* con *Rifai* spento → clic sulla voce che disfa →
**Ctrl+Y** che rifà. ⚠️ Cade qui una convinzione della Fase 4.7bis (*«il rig non riesce a
pilotare i tasti»*): i tasti passano, **col `lParam` giusto** — è la stessa trappola già pagata
per il `WM_KEYUP` (il bit di transizione sta nell'HIWORD), applicata anche al KEYDOWN.

**Milestone:** l'editor si può **esplorare**. Ogni azione si disfa, e il pannello Components non
ha più bisogno di un tooltip che avverte. 🎉

---

## Fase 4.85 — Gli asset: dove sono, e come si guardano 🟢🟡

### Il bug che non era dove sembrava

*«Se copio un file in assets da Windows, in gioco non si aggiorna»*. Sembrava un refresh
mancante nel pannello, e il pannello non aveva colpa: rilegge il disco **a ogni frame**.
Guardava un'altra cartella.

> Il csproj copia `assets/**` nell'output (`CopyToOutputDirectory`), e sia l'`AssetManager` sia
> il pannello risolvevano `assets` da `AppContext.BaseDirectory`. L'eseguibile leggeva una
> **copia** fatta al momento della build.

Due conseguenze, e la seconda è peggiore: un file nuovo non si vedeva (e **non bastava
riavviare** — serviva ricompilare), e soprattutto **il Salva dell'editor scriveva dentro
`bin/`**. Il lavoro d'autore finiva nell'output di compilazione: perso alla prima pulizia, e
invisibile a git nel frattempo.

- [x] **`ContentRoot`** (`src/gEngine/Core/ContentRoot.cs`) — regola di Unity: *la cartella di
  progetto **è** la cartella asset*. Risale dall'eseguibile cercando una cartella con **sia** un
  `.csproj` **sia** una `assets/`; se non la trova (gioco spedito) resta accanto all'exe
  - ⚠️ Servono **entrambi** i segni: solo il `.csproj` aggancerebbe un progetto qualunque
    risalendo, solo `assets/` aggancerebbe la copia nell'output — cioè proprio quella da cui si
    sta scappando
  - Verificato in modo discriminante: file creato **solo** nel sorgente, **nessuna build** in
    mezzo, eseguibile lanciato direttamente → compare nel pannello. E il log conferma i
    caricamenti da `samples/Sandbox/assets/`

### Il pannello File system: da elenco a griglia

*Richiesta del proprietario: «gli item allineabili anche in orizzontale, con anteprime — così
è orribile da vedere e da usare».*

- [x] **Griglia di default**, con interruttore per tornare alla lista e slider della dimensione.
  Un elenco di nomi è la forma giusta per **leggere** dei file, non per **riconoscere** degli
  asset: una texture si cerca guardandola
- [x] **Anteprime vere per le immagini**, riquadro colorato per genere (con l'estensione sopra)
  per tutto il resto
  - ⚠️ **Le miniature si riducono PRIMA di salire sulla GPU** (`LoadTextureThumbnail`:
    `LoadImage` → `ImageResize` → `LoadTextureFromImage`). Caricare intero e disegnare piccolo
    sarebbe stato una riga in meno e gigabyte di memoria video: `SummonersRift/Textures` ha
    **405** file `.dds`
  - ⚠️ **Pigre, poche per frame (2), buttate cambiando cartella.** Tre difese contro tre modi
    diversi di rovinare l'editor: caricare una cartella intera all'apertura, bloccare la
    finestra per secondi, non restituire mai la memoria video. Anche i **fallimenti** vanno in
    cache, o un file illeggibile verrebbe riletto a ogni frame per sempre
  - ⚠️ **Niente anteprime dei modelli**, e non è un rinvio pigro: generarle significa
    **caricare** il modello, e `SummonersRift.obj` è enorme. Vorrebbe caricamento pigro con
    budget e una cache su disco — è un lavoro a sé
- [x] **Breadcrumb cliccabile** al posto del solo `..`; nome troncato con tooltip (genere, peso,
  cosa farci)
- ⚠️ **Le icone sono disegnate col draw list**, non con un font di icone: ProggyClean copre solo
  Latin-1 e qualunque glifo fuori da lì esce `?`. Un rettangolo colorato con dentro `PNG` dice
  il genere meglio di ogni carattere disponibile — finché il font non cambia (punto a sé)
- ⚠️ **Fuori-di-uno trovato guardando uno screenshot**: il ritorno a capo contava la posizione
  *dopo* aver incrementato, quindi la **prima riga** aveva un riquadro in meno di tutte le altre
  e la seconda partiva più a sinistra. Rileggendo il codice non si vedeva; nell'immagine sì

### Trovato di sponda: raylib non decodifica i JPEG — e ora sì 🟢

Un `.jpeg` valido (magic `FFD8FFE0`, 88 KB) resta un riquadro. Nel log:
`FILEIO: File loaded successfully` seguito da `WARNING: IMAGE: Failed to load image data` — i
byte si leggono, il decoder no. **Questa build di raylib-cs non ha il supporto JPEG.**

Escluse le cause banali prima di intervenire: il file è **baseline** (non progressivo, che stb
non legge), **YCbCr a 3 componenti** (non CMYK), 8 bit, 1024x1024. Un JPEG del tutto ordinario —
quindi è il nativo a essere compilato senza `SUPPORT_FILEFORMAT_JPG`.

Non riguardava solo le anteprime: una texture `.jpg` **non si caricava affatto**, e un modello
che la referenzia veniva disegnato **bianco** (albedo = la texture di default 1x1). È la causa
vera del "gli obj restano bianchi" da cui era partita la sessione.

- [x] **Ricaduta su `StbImageSharp`** (MIT) in `RayLibAssetBackend`: la porta in C# dello
  **stesso** `stb_image` che raylib usa dentro — non un secondo modo di leggere le immagini, ma
  un pezzo spento nel nativo che si riaccende. Si prova **prima** raylib e si ricade solo se
  fallisce: i formati che il nativo gestisce (comprese le `.dds` compresse, che stb non tocca)
  restano sulla strada veloce, e il giorno che la native avrà il JPEG questa ricaduta smetterà
  da sé di essere usata
  - ⚠️ La memoria dei pixel si chiede a **`Raylib.MemAlloc`** e non a `Marshal`: quell'`Image`
    finisce in mano a raylib, e `UnloadImage` la libera col **suo** allocatore. Mescolare i due
    corrompe l'heap in un punto che non c'entra niente
- [x] **`RepairFailedAlbedo`**, e senza questo il resto non sarebbe servito a niente nel caso
  vero: le texture di un **modello** non passano da noi — le apre il loader di raylib mentre
  legge il `.mtl`. Quindi un modello con albedo jpg restava bianco *anche col decoder in casa*.
  Dopo `LoadModel` si cercano i material la cui albedo è la texture di default (fallita), si
  ripesca il `map_Kd` dal `.mtl` e si ricarica con la ricaduta
  - ⚠️ **Solo OBJ**: per glTF servirebbe leggere il json (o il chunk binario di un `.glb`) e
    seguire material→texture→image. È un parser, non una riparazione — un glTF con albedo jpg
    resta bianco, e il commento nel codice dice perché
  - ⚠️ Si patcha **solo se il numero di material combacia** con quello dei `newmtl`: si assume
    che raylib crei i material nell'ordine del file, e se i conti non tornano l'assunzione non
    regge. Meglio un modello bianco che uno con le texture scambiate, che sembrerebbe un errore
    d'autore

**Verificato sul caso reale**, non per costruzione: messo un JPEG dove il `.mtl` di
`FuturisticGirl.obj` lo cerca e il modello in scena, il log mostra
`WARNING: IMAGE: Failed to load image data` (raylib) seguito **dalla riga dopo** da
`TEXTURE: Texture loaded successfully (1024x1024 | R8G8B8A8)` — la ricaduta — e a video il
modello è **texturizzato** invece che bianco.

---

## Fase 4.86 — Il primo test, e cosa ha detto sui test 🟢

La Fase 0 prevedeva `tests/gEngine.Tests` (xUnit) e non è mai esistito: le verifiche numeriche
del progetto sono state fatte con app scratch temporanee e buttate. Il primo test permanente
copre il **round-trip di serializzazione** — `World → Scene → World`, e confronta.

Perché proprio quello, fra tutto ciò che non è coperto: quel codice regge **tre** cose insieme.
Il Salva dell'editor, il Play/Stop (lo snapshot **è** il serializer) e, domani, l'hot-reload
degli script (snapshot → ricompila → reistanzia). Finora è stato verificato guardando dei cubi
cadere.

### ⚠️⚠️ Il round-trip da solo NON verifica niente. Misurato, non temuto.

Scritto il giro e visto verde, si è **sabotato il serializer** per controllare che il test
mordesse — è lo standard del progetto (*«una verifica che passa anche con l'implementazione
sbagliata non sta verificando niente»*). Il primo sabotaggio è stato togliere la scrittura di
`ModelPath` dal writer del `MeshRenderer`, cioè **far perdere il modello a ogni salvataggio**.

**I test restavano tutti verdi.**

Il motivo, e vale per qualunque round-trip: il giro è cieco a una perdita **simmetrica**. Se il
writer non scrive `ModelPath`, la prima scena non ce l'ha → il World rientrante non carica
nessun modello → la seconda scena non ce l'ha uguale. Le due coincidono, il confronto è
soddisfatto, e il dato è sparito. Lo stesso vale per `Parent` e per ogni altro campo
asimmetrico.

Da qui la forma definitiva, che è **a due gambe** e non si può ridurre a una:
- `UnGiroCompleto_NonPerdeNienteDiCioCheEDatoDAutore` verifica la **stabilità** (il giro è un
  punto fisso);
- `LaScenaScritta_ContieneDavveroIDatiDAutore` verifica la **fedeltà** (nel file c'è
  `ModelPath` e non l'handle, c'è `"Giocatore"` e non un id, il nome è un campo e non un
  componente).

Con la seconda gamba, il sabotaggio del `ModelPath` fallisce. Provati anche: `Parent` scritto
per **id** invece che per nome (4 test rossi) e `[RuntimeState]` **non** saltato (10 rossi).

### Cosa c'è nei test

- [x] **`tests/gEngine.Tests`** (xUnit, net10.0), nella soluzione sotto `/tests/`
- [x] **`FakeAssetBackend`** — il round-trip del `MeshRenderer` passa dall'`AssetManager`
  (path→handle e ritorno), quindi non è testabile senza. Col backend raylib servirebbero una
  finestra aperta e i file veri: due dipendenze estranee a ciò che si verifica
- [x] **`SceneComparison`** — elenca le differenze invece di dire sì/no, perché un round-trip
  rotto deve dire **quale campo** non è tornato
  - ⚠️ Confronto **per indice**, non per nome: l'ordine delle entità è ciò che decide il diff in
    git di una scena versionata. Un giro che conserva i dati ma rimescola le righe è una
    regressione, e per nome non si vedrebbe
  - ⚠️ Le chiavi degli oggetti JSON si **canonicalizzano** (ordinate, ricorsivamente) prima di
    confrontare, e non è lassismo: `SceneSerializer` scrive scorrendo `World.ComponentStorages`,
    cioè nell'ordine in cui gli storage sono **nati** — che nel World costruito a mano è
    l'ordine degli `AddComponent` e nel World reistanziato è quello delle chiavi nel file. Non
    coincidono e non devono. Gli **array** invece non si riordinano: lì l'ordine è contenuto
  - *(Effetto reale, non difetto del test: il primo salvataggio di una scena scritta a mano può
    riordinare i componenti dentro un'entità. Cambia il diff, non il contenuto — è la stessa
    nota già scritta per i `_comment`.)*
- [x] **Sei guasti** in `[Theory]` che il confronto **deve** vedere: un float spostato di un
  millesimo, un enum, un bool, un riferimento riappeso, un componente sparito, **due entità
  scambiate di posto** (l'ultimo è quello che una comparazione per nome lascerebbe passare)
- [x] Il fixture ha **un caso per ogni asimmetria** del formato — `Parent`, `ModelPath`, il nome
  come campo, `[RuntimeState]` — più **un'entità senza nome e senza componenti**, che è il caso
  degenere e quello che si dimentica
- [x] Coperti anche: il giro **attraverso il file** su disco (Save + Load: lì i `JsonElement`
  vengono riscritti e riparsati davvero), i `_comment` che sopravvivono al salvataggio **e** il
  loro rovescio dichiarato (senza `source` si perdono — è il motivo per cui quel parametro
  esiste), e il genitore senza nome che **fa fallire il salvataggio** invece di scrivere un file
  che si ricarica monco

### Cosa NON copre, per non farlo credere

L'ECS (`CreateEntity`, `Query`, il gotcha struct/copia) resta scoperto, e resta scoperto l'undo
— che è l'altro pezzo che regge tre cose e che nessuno ricontrolla. Restano voci di Fase 0.

---

## Fase 4.87 — `ISystem.OnDestroy`: il pannello aveva reso raggiungibile un buco 🟢

Era un debito **teorico** finché nessuno toglieva system. Il pannello Systems (Fase 4.7) li fa
togliere **col mouse**, e `SystemRegistry.Remove` sfilava il system dalle fasi senza dirgli
niente.

Il danno era peggio di come lo descriveva il tooltip. Diceva *«lascia i corpi nel mondo Bepu,
semplicemente nessuno li sincronizza più»*: in realtà la mappa entità→corpo è **privata
dell'istanza**, quindi con il system fuori dal registry quei corpi non erano più
**raggiungibili da nessuno** — continuavano a collidere contro un mondo che non li vede, e
nemmeno rimettere il system li avrebbe ripuliti.

- [x] **`ISystem.OnDestroy(World)`** — default interface member vuoto, come
  `MatchedComponents`: un system scritto fuori dall'engine continua a compilare e la
  maggioranza dei system non deve dichiarare niente
- [x] **`SystemRegistry.Remove`** lo chiama, e `Remove<T>` con lui
  - ⚠️ **Dopo** lo sfilamento dalle fasi, speculare ad `Add` che chiama `OnCreate` **dopo** lo
    smistamento. Non è simmetria estetica: un system a metà smontaggio non deve poter ricevere
    un `OnUpdate`
  - ⚠️ Su un system **non registrato** non chiama niente: distruggere due volte è peggio che
    non distruggere, perché chi scrive `OnDestroy` assume di essere in pari con un `OnCreate`
- [x] **`PhysicsSystem.OnDestroy`** — toglie i suoi corpi da Bepu e il `PhysicsBodyComponent`
  dalle entità vive (il link stantio farebbe leggere "il corpo c'è già" a un system rimesso in
  seguito: l'entità avrebbe un RigidBody e non cadrebbe)
  - ⚠️⚠️ **Non** dispone `IPhysicsWorld`, ed è la riga più facile da sbagliare: è
    `IDisposable`, quindi disporlo *sembra* la cosa ordinata da fare. È una **Resource del
    gioco**, ricevuta dal costruttore. Toglierlo dal pannello per curiosità distruggerebbe il
    mondo fisico di tutti, e "Ripristina" restituirebbe un system agganciato a un oggetto morto.
    **Si libera ciò che si è creato, non ciò che si è ricevuto** — c'è un test apposta

### Cade anche la seconda metà del debito

*«"Ripristina" richiama `OnCreate` su un'istanza già creata; oggi non morde perché tutti gli
`OnCreate` sono vuoti, cioè regge per caso»*. Adesso `Rimuovi` chiama `OnDestroy`: i due si
fanno il paio e l'istanza riparte da uno stato pulito. Non regge più per caso.

### Verificato sabotando, non sperando

Otto test sul ciclo di vita (`tests/gEngine.Tests/Ecs/`), con un `FakePhysicsWorld` che conta i
corpi vivi — con Bepu vero servirebbe una simulazione intera per una domanda di ciclo di vita.
Poi si è rotta l'implementazione per vedere se mordevano: tolta la chiamata a `OnDestroy` dal
registry → **7 rossi**; aggiunto il `_physics.Dispose()` sbagliato → **1 rosso**, quello scritto
apposta.

### Di sponda: due `—` nei messaggi d'eccezione

La scansione dei sorgenti per i caratteri `> 0xFF` fuori dai commenti (il modo prescritto di
trovarli — *rileggendoli non si vedono*) ne ha trovati due, entrambi lineette lunghe U+2014 in
messaggi di `InvalidOperationException`. Non sono innocui: quei messaggi finiscono sotto ImGui
(`MainMenuBar._status` quando il Salva fallisce, `FileSystemPanel`, `PlayMode.LastError`) e il
font di default copre solo Latin-1, quindi uscivano come `?`. Corretti in `-`, con un
`Assert.DoesNotContain(... > 0xFF)` sul messaggio nel test del salvataggio.

---

## Fase 4.88 — Il disco si può modificare, perché sotto c'è il cestino 🟢🟡

Punto 2 del piano. Il pannello File system sapeva solo **leggere**: creare, rinominare ed
eliminare erano bloccati, e non per pigrizia — *«l'undo dell'editor copre il World, non il
disco»*. Un comando in memoria non resuscita un file. Prima del bottone serviva la rete.

### La rete: `IFileTrash`, e chi non ce l'ha non elimina

- [x] **`IFileTrash`** (`src/gEngine.Editor/Files/`) — porta verso il cestino del sistema,
  stesso schema di `IRenderer`/`IAssetBackend`/`IPhysicsWorld`
- [x] **`RecycleBinTrash`** su Windows
- [x] ⚠️⚠️ **Chi non ha un cestino NON elimina**: la ricaduta su una piattaforma senza cestino
  è `Available = false`, **non** `File.Delete`. Cancellare davvero sarebbe l'unica operazione
  irreversibile dell'editor, offerta proprio dove la rete manca. Il comando resta **spento col
  motivo**, e c'è un test che fallisce se un giorno diventasse "tanto è lo stesso"

⚠️ **Sì, l'implementazione usa `Microsoft.VisualBasic.FileIO`**, e non è un errore di
copiatura: è l'unico modo documentato di raggiungere il Cestino da .NET senza scrivere a mano
`SHFileOperation` di shell32 — struct marshalata, stringa a doppio terminatore nullo e un
allineamento che su x64 si sbaglia **in silenzio**. `Microsoft.VisualBasic.Core` è nel
framework condiviso: nessun pacchetto in più. Il namespace è brutto, il P/Invoke che non c'è è
meglio di quello che c'è.

⚠️ **Verificato che il file finisca DAVVERO nel Cestino**, non solo che sparisca: ritrovato fra
gli elementi del Cestino, con la sua cartella d'origine. Dal lato del chiamante le due cose si
assomigliano; per l'utente sono opposte.

### Le regole stanno fuori dal pannello

`AssetFiles` tiene creare / rinominare / eliminare. Sta lì e non nel pannello per lo stesso
motivo per cui `EntityOperations` non sta nella Hierarchy: sono **politiche**, e sono la parte
che un test può interrogare senza aprire una finestra.

- [x] ⚠️⚠️ **Niente esce dalla radice.** Il pannello dice "assets" e deve mantenerlo. Si
  confronta il percorso **risolto** (`Path.GetFullPath` normalizza i `..`), mai la stringa
  scritta
  - ⚠️ Il separatore finale nel confronto **non è cosmesi**: senza, `assets2` risulterebbe
    "dentro" `assets` per semplice prefisso di stringa. C'è un test apposta
- [x] ⚠️ **Rinominare cambiando solo le maiuscole è permesso**: su Windows il file system è
  insensibile al caso, quindi un controllo "esiste già" ingenuo vieterebbe proprio
  `Texture.png` → `texture.png`, che è fra i motivi più comuni per rinominare
- [x] Gli errori sono **valori di ritorno** (`FileResult`), non eccezioni: queste operazioni
  partono da un clic dentro un frame di disegno, dove un'eccezione che sale è una finestra che
  sparisce — e i modi di fallire sono tutti casi normali che l'utente può correggere
- [x] Le mutazioni si applicano a **fine frame**: modificare il disco mentre si scorre
  `EnumerateFiles` sulla stessa cartella è il modo sicuro per far cadere il disegno. Stesso
  schema del pannello Systems
- [x] ⚠️ **Rinominare un asset rompe i riferimenti nelle scene** (il `ModelPath` è quel
  percorso e nessuno lo riscrive). Dichiarato nel tooltip del rinomina, non nascosto: sistemarlo
  vorrebbe dire conoscere **tutte** le scene, e l'editor ne tiene aperta una

### ⚠️⚠️ Il bug che si vedeva solo guardando: `OpenPopup` e l'ID stack

Scritto tutto e compilato senza warning, **dal menu contestuale "Elimina" non faceva niente**.
Nessun errore, nessun log, nessun crash: il comando semplicemente non produceva la modale.

La causa: `OpenPopup` e `BeginPopupModal` si accordano su un **id calcolato nell'ID stack
corrente**. Chiamando `OpenPopup("Elimina")` da dentro il menu contestuale — che è a sua volta
un popup, cioè un livello più in basso — l'id non è lo stesso che `BeginPopupModal("Elimina")`
calcola a livello di finestra. Dalla **barra degli strumenti** funzionava (lì si è già a livello
di finestra), dal menu contestuale no; rileggendo il codice i due rami sono identici.

Corretto rimandando l'apertura al livello di finestra (`_opening`). ⚠️ È il genere di cosa che
nessun test di quelli scritti qui avrebbe preso: la logica era giusta, era l'aggancio a ImGui a
non esserlo. **La UI si verifica guardandola** — questo è il caso che lo dimostra.

### Come è stato verificato

Rig di `PostMessage` sulla finestra viva (vedi l'handoff), giro completo end-to-end: creata una
cartella dall'interfaccia → **comparsa senza riavviare** → clic destro → Elimina → conferma →
sparita dal disco → **ritrovata nel Cestino con l'origine giusta**.

⚠️ **Trappola del rig, costata tre giri a vuoto**: `FindWindow(null, "gEngine Sandbox")`
restituisce **0**, perché il titolo della finestra è `Game`. I `PostMessage` andavano a un
handle nullo e non succedeva niente — che è indistinguibile da "il codice non funziona". Usare
`Process.MainWindowHandle`.

- [x] 20 test su `AssetFiles` (confine della radice, nomi degeneri, collisioni, maiuscole,
  cestino assente), più il controllo che i messaggi d'errore stiano in **Latin-1** — finiscono
  sotto ImGui, che col font di default mostra `?` per tutto il resto

---

## Fase 4.89 — "Crea oggetto", e la sfera che non c'era 🟢

Ultimo pezzo del punto 2. Il catalogo degli oggetti di scena — **Cubo, Sfera, Luce, Camera,
Vuoto** — deciso dal proprietario, e in **entrambi** i pannelli: la Hierarchy (dove il comando
sta di casa) e il File system come scorciatoia.

### ⚠️ La sfera non esisteva, e offrirla sarebbe stato il bug peggiore

`MeshKind` aveva `Cube`, `Plane`, `Grid`, `Model`. Una voce "Sfera" avrebbe creato un'entità che
si vede come un cubo o non si vede: cioè **il bug che assomiglia a un no-op**, quello che la
Fase 4.7 dichiara come il più caro da cercare. Detto al proprietario invece di sostituire in
silenzio "Sfera" con "Piano".

- [x] **`MeshKind.Sphere`** + il caso in `RayLibRenderer`, sullo schema esatto del cubo:
  `GenMeshSphere(0.5f, 16, 16)`, una mesh **unitaria condivisa** da tutta la scena, la stessa
  trasposizione della matrice e lo stesso ramo wireframe
  - Raggio 0.5, cioè **inscritta nel cubo unitario**: a `Scale = 1` le due primitive sono
    confrontabili a colpo d'occhio
  - Passa dalla **matrice di mondo completa**, quindi rotazione e scala non uniforme funzionano
    (a differenza di `Plane`, che usa solo la traslazione)
  - Scaricata in `Shutdown` accanto a `_unitCubeMesh`
  - ⚠️ Il **picking** la tratta come un cubo (`MeshRenderSystem` e `Ray` usano un AABB a cubo
    unitario): si clicca anche un po' fuori, agli angoli del cubo circoscritto. È lo stesso grado
    di approssimazione già accettato per `Model`, non un caso nuovo
  - Di sponda: rende **visibile** `ColliderShape.Sphere`, che finora si simulava al buio

### `SceneObjects`: un catalogo solo, due pannelli

- [x] **Non è codice nuovo, è un assemblaggio**: `EntityOperations.Create` più i **default
  dichiarati** nel `SceneComponentRegistry`. Quei valori esistono dalla Fase 4.7 e sono scelti
  perché aggiungere il componente **si veda**: ricostruirli qui darebbe due verità su cosa sia
  un cubo, e quella sbagliata sarebbe la nuova
- [x] ⚠️ La sfera parte dal default del MeshRenderer e cambia **un campo**, **prima** di
  aggiungerlo allo storage — così non c'è nessuna domanda su cosa significhi mutare un
  componente già dentro, che su `MeshRendererComponent` (unica class fra i componenti) sarebbe
  una domanda vera. C'è un test che fallisce se creare una sfera trasformasse in sfera i cubi
  già in scena
- [x] ⚠️ Lo `switch` è su un **enum**, non sull'etichetta del menu: il giorno che "Cubo" diventa
  "Cubo (primitiva)", il cubo smetterebbe di avere una mesh in silenzio
- [x] Il nome dell'entità è quello della voce ("Cubo", "Sfera") e viene **reso libero**: due
  omonime collassano nella mappa nome→Entity al reload, e con un menu che crea cubi a ripetizione
  il caso arriva al secondo clic
- [x] **Passa dall'undo**: creare è l'azione che si fa per sbaglio con un clic
- [x] ⚠️ Senza `SceneComponentRegistry` (lo dichiara il gioco, può mancare) resta creabile solo
  **Vuoto**; il resto è **spento col motivo**, non nascosto
- [x] Nel File system il tooltip dice esplicitamente *«nasce nella SCENA, non in questa
  cartella»*: è l'unico comando di quel pannello che non riguarda il disco

### Come è stato verificato

16 test su `SceneObjects` (i componenti giusti, l'aliasing della class, i nomi liberi, l'undo,
il caso senza registry, le etichette in Latin-1). Il menu e la creazione end-to-end col rig:
clic destro nella Hierarchy → Crea oggetto → Sfera → l'entità compare nell'albero, selezionata,
con `Kind = Sphere` e `Visible` nell'Inspector.

⚠️ **Onestà su un punto**: che a schermo la sfera sia *disegnata come una sfera* l'ha confermato
**il proprietario guardando**, non il rig. Il rig non è riuscito né a spostarla col gizmo (il
trascinamento sintetico è il limite noto) né a distinguerla dal personaggio che sta anch'esso
nell'origine.

---

## Fase 4.91 — Il logger esiste davvero, ed è il prerequisito della console 🟢

*Voce aperta dalla **Fase 0**: «`GameLoop` istanzia `_logger` ma non lo passa mai a `IGame`/ai
system». Andava chiusa prima della console in-editor.*

### ⚠️ Non era "non è raggiungibile": era codice morto

La ROADMAP raccontava un problema di **esposizione** — il logger c'è, manca il canale per
arrivarci. Guardando, il canale non era l'unica cosa che mancava: `_logger` era un campo
`readonly` di `GameLoop` **letto da nessuno**, `LogCategories` non era citato in nessun file, e
`LogMessage` veniva costruito dentro `Write` e formattato subito — cioè esisteva un tipo per
passare un messaggio a qualcuno, e non c'era nessun qualcuno.

**In tre anni di commit l'engine non ha mai loggato una riga.** Conta perché cambia la forma del
lavoro: non "collega una cosa che funziona", ma "una cosa mai esercitata sta per avere due
consumatori contemporaneamente". Un `Add<ILogger>` e via sarebbe stato un prerequisito chiuso
sulla carta e da riaprire al primo pannello.

### La decisione: due porte, una per verso

`ILogger` faceva **entrambi** i lavori — decidere cosa passa la soglia *e* scrivere su console.
Finché il destinatario era uno, le due cose combaciavano. Con la console in-editor i destinatari
diventano due e devono ricevere **entrambi lo stesso messaggio**, senza che chi logga sappia
quanti sono.

- **`ILogger`** resta la porta di chi **produce**.
- **`ILogSink`** è la porta nuova, di chi **consuma**.
- **`Logger`** applica la soglia **una volta** e fa fan-out sui sink.
- `ConsoleLogger` → **`ConsoleLogSink`**, che scrive sullo stdout.

È lo stesso ports & adapters di renderer, asset e fisica: qui l'uscita è `ILogSink`.

⚠️ **Il rename non è cosmesi.** Stavano per esistere due cose chiamate "console" — lo stdout e
il pannello — di cui una sola era `ConsoleLogger`. Rinominare **dopo** avrebbe voluto dire
rinominare mentre si legge codice che usa il nome sbagliato per la cosa sbagliata.

### Cosa è stato deciso di NON fare

- **Niente storia dentro il `Logger`.** La soglia è **una regola sola** e sta al centro; la
  storia è un **bisogno di chi guarda** e vive nel sink che ce l'ha. Un buffer nel logger
  avrebbe imposto una dimensione a tutti per il comodo di uno. ⚠️ Il prezzo è dichiarato: un
  sink registrato tardi non vede l'avvio, e la console dovrà decidere cosa farne (`ROADMAP.md`).
- **Niente thread-safety.** Il gioco è a thread singolo. Metterla "per sicurezza" avrebbe
  aggiunto un lock su un percorso caldo per un caso che non esiste. Il giorno che esiste, si
  cambia `Logger` e **non i chiamanti** — che è tutto il punto di avere una porta.
- **Senza sink non lancia.** Un logger è trasversale: far cadere il gioco perché nessuno ascolta
  è sproporzionato. È il verso sicuro dello sbaglio, ed è scritto dove serve perché "non vedo i
  miei log" non diventi un mistero.

### ⚠️ L'ordine di registrazione è invertito rispetto a tutto il resto

Il logger si registra **prima di `InitWindow`**, mentre un commento poco sopra impone l'opposto
a renderer e `AssetManager`. Sembra un'incoerenza ed è il motivo opposto: quei due **non
possono** esistere a finestra chiusa (tengono risorse GPU), il logger non ha quel vincolo — e
registrarlo insieme a loro renderebbe muto **proprio l'avvio**, cioè il tratto di vita del
programma in cui è più probabile che qualcosa vada storto e in cui, non essendoci ancora una
finestra, il log è l'unica cosa che parla. Il commento nel codice lo dice, perché letto di
sfuggita sembra una svista da "sistemare".

### Verificato: sabotando, e guardando

**I test discriminano** (8 nuovi, 77 totali verdi). Provato che falliscono davvero:

| Sabotaggio | Esito |
|---|---|
| Fan-out che serve **solo il primo** sink | 1 test rosso |
| Soglia **ignorata** (`if (false)`) | 2 test rossi |

⚠️ La coppia di asserzioni sulla soglia è voluta: "l'`Error` arriva" da solo passerebbe **anche
con la soglia disattivata**. Serve anche "il `Debug` non arriva", o non si sta verificando
niente — è la lezione della Fase 4.86 applicata a un caso nuovo.

**E il gioco logga davvero**, che i test non lo dicono: avviato il Sandbox con lo stdout
rediretto, chiuso con `CloseMainWindow()` (chiusura pulita, così il buffer si svuota e
`Shutdown` fa in tempo a scrivere). Le tre righe ci sono, nell'ordine e con i tempi giusti, in
mezzo al log nativo di raylib:

```
[15:12:06] [Info] [Window] Finestra 1920x1080 - Game
[15:12:09] [Info] [Engine] Gioco inizializzato
[15:12:09] [Info] [Engine] Chiusura
```

⚠️ Nota del rig, diversa da quella nota per gli screenshot: qui **non** si ammazza il processo.
`CloseMainWindow()` manda `WM_CLOSE`, `WindowShouldClose()` diventa vero e il loop esce dalla
porta principale — che è anche l'unico modo di vedere il log di chiusura.

⚠️ Con lo stdout rediretto su file, i colori di `ConsoleLogSink` finiscono nel file come
sequenze VT (`ESC[7m`). In un terminale vero non si vedono; leggendo un log catturato, sì.

### Cosa restava scoperto

**Loggava solo il `GameLoop`, tre righe** — chiuso subito dopo dalla Fase 4.92.

---

## Fase 4.92 — Il logger serve a qualcosa: i tre silenzi 🟢

*Un logger che nessuno chiama è metà lavoro: la Fase 4.91 ha costruito il tubo, questa ci fa
passare qualcosa.*

### Il criterio: non "log ovunque", ma i punti dove si fallisce in silenzio

La tentazione era spargere `Info` per l'engine finché le categorie non fossero tutte usate.
Sarebbe stato il modo di riempire una console di rumore e nascondere le tre righe che contano.
Il criterio scelto è l'opposto, ed è quello che questo repo insegue da sempre: **dove oggi
qualcosa va storto senza che nessuno lo dica?** Ne sono usciti tre punti, tutti già
*documentati come invisibili* e nessuno mai segnalato a runtime.

| Dove | Cosa succedeva | Ora |
|---|---|---|
| `AssetManager` | raylib su file mancante **non lancia**: handle vuoto, scena senza modello, silenzio invece dell'audio | `Warning` col path **relativo** (quello del file di scena) e con dove ha cercato |
| `RayLibRenderer` | `LoadShader` che fallisce ricade sullo shader di default: la scena esce **piatta** e sembra un problema di luci | `Error`, e il messaggio dice esplicitamente che **non** è un problema di luci |
| `PhysicsSystem` | riconciliazione dei corpi orfani e `OnDestroy` non lasciavano traccia | `Debug` sui corpi orfani, `Info` sui corpi liberati togliendo il system |

⚠️ **L'asset mancante è il più caro dei tre**, e non è teorico: i binari sono fuori da git,
quindi **ogni clone pulito parte già così**. Fino a ieri l'unico segnale era una riga del log
*nativo* di raylib in mezzo a duecento.

### Due decisioni dentro il dettaglio

- ⚠️ **Il controllo sta nell'`AssetManager`, non nel backend.** Il backend riceve un path
  assoluto; solo l'`AssetManager` sa da quale path **relativo** l'ha ricavato — ed è l'unica
  forma con cui chi legge il log può andare ad aggiustare il file di scena. Un messaggio col
  solo assoluto direbbe dove si è cercato, non cosa correggere.
- ⚠️ **Il log della fisica sta sotto la guardia `_orphaned.Count > 0`**, non fuori. Quel metodo
  gira a **60 Hz**: una riga incondizionata non sarebbe "log verboso", sarebbe una console
  inutilizzabile e un logger che costa quanto la simulazione.
- **`Warning` e non un'eccezione** per l'asset: la ricaduta silenziosa resta la scelta giusta a
  runtime (un gioco che non parte per una texture è peggio di un gioco senza quella texture).
  Quel che mancava non era la severità — era che qualcuno lo **dicesse**.

### Verificato: sabotando, e togliendo un file davvero

**Il sabotaggio** (`if (File.Exists(...))` → `if (true)`, cioè controllo disattivato): **4 test
rossi su 5**. Il quinto è `AssetPresente_NonAvvisa` e resta verde **giustamente** — è il test
opposto, quello che impedisce a un `AssetManager` che avvisa *sempre* di passare per buono.
Senza quella coppia, "l'asset mancante avvisa" lo supererebbe anche un urlatore seriale.

**E la prova vera**: rinominato l'mp3 del Sandbox, avviato il gioco, rimesso a posto.

```
[15:24:32] [Warning] [Assets] Asset non trovato: 'audio/Before_the_Light_Fades.mp3'
                     (cercato in '...\samples\Sandbox\assets\audio\Before_the_Light_Fades.mp3').
                     Si continua con una risorsa vuota.
```

⚠️ Serviva farlo per davvero: su questa macchina **gli asset ci sono tutti**, quindi il primo
giro a vuoto non aveva stampato nessun avviso — cioè il percorso d'allarme non era mai stato
esercitato fuori dai test. Un "funziona" basato sul non aver visto niente è esattamente il
genere di conclusione che questo progetto non accetta.

⚠️ **Trovato guardando l'output**: i path di scena usano `/` e `Path.Combine` non li converte,
quindi l'assoluto usciva coi separatori misti (`...\assets\audio/file.mp3`). Funziona, ma è la
riga che un umano legge per andare a cercare il file. Normalizzato.

### Cosa resta scoperto, per non farlo credere

- **`Audio` ed `Ecs` restano categorie senza clienti.** È voluto: aspettano l'audio manager e un
  ECS che abbia qualcosa da raccontare. Inventare messaggi per riempirle sarebbe stato il
  rumore contro cui è stato scelto il criterio.
- **Nessuno avvisa *prima***: chi clona il repo scopre gli asset mancanti una riga alla volta,
  mentre la scena si carica. Un controllo all'avvio che elenca cosa manca sarebbe più onesto —
  è in `ROADMAP.md`.

---

## Fase 4.93 — La console: il log entra nell'editor 🟢

*Punto 1 del piano del proprietario, chiuso. La richiesta era «unire il logger con una console
dentro l'editor, e **non mostrare solo gli errori** — tutto il flusso».*

### La decisione: il pannello NON è un sink

Sembrava ovvio che la console fosse un `ILogSink` registrato sul `Logger`. Non lo è, per una
ragione di tempi: **il pannello nasce dentro `IGame.Init`**, cioè a finestra già aperta e già
loggata. Un sink che nasce col pannello mostrerebbe tutto *tranne* l'avvio — cioè il tratto in
cui è più probabile che qualcosa sia andato storto.

Quindi: **`LogHistory`**, un sink con un buffer circolare che sta **nell'engine**, attaccato dal
`GameLoop` *prima* di `InitWindow` insieme al logger, e dichiarato come Resource. Il pannello lo
**legge**. Conseguenze, tutte volute:

- la console mostra anche ciò che è successo **prima che ci fosse una console**;
- il pannello non ha ciclo di vita da gestire (niente registra/sregistra all'apertura);
- chi non usa l'editor ha comunque una storia leggibile, che è utile a chiunque.

⚠️ **Il buffer non sta dentro `Logger`**, ed è la stessa linea tirata alla Fase 4.91: la soglia
è una regola **sola** e sta al centro; la storia è un bisogno di **chi guarda**. Metterla nel
logger avrebbe imposto una dimensione a tutti per comodo di uno.

### `TotalErrors` è monotòno, e non è un dettaglio

La console si apre da sé su un errore **nuovo** (regola presa in prestito dal `ScriptsPanel`,
che è l'unico altro pannello a cui è concesso aprirsi da solo). Per accorgersene serve un
contatore che **non scende**: contare gli errori *presenti nel buffer* sembra equivalente e non
lo è — quando il buffer gira, l'errore vecchio esce, il conteggio cala, e "sono aumentati"
torna vero **una seconda volta per lo stesso errore**. Il pannello si riaprirebbe da solo per
un guasto già visto.

Per lo stesso motivo `Clear()` svuota il buffer ma **non** azzera il contatore: azzerandolo, un
"Pulisci" farebbe riscattare l'apertura automatica al primo errore successivo.

### ⚠️ Il difetto che solo lo screenshot ha trovato

I test erano verdi, il pannello si apriva, le righe c'erano. Guardandolo:

```
[Warning] [Assets] Asset non trovato: 'audio/...' (cercato in 'C:\Users\ma
```

**Tagliato.** `ImGui.TextColored` non manda a capo, e la coda della riga spariva fuori dal
pannello — cioè spariva **il path**, che è l'unica ragione per cui quel messaggio esiste. Un log
che tronca a destra nasconde proprio la parte dove stanno i dettagli: path, numeri, nomi.

Risolto con `PushStyleColor` + `TextWrapped` (`TextColored` non ha una variante che vada a capo).
A capo e **non** una barra orizzontale: i messaggi lunghi non sono l'eccezione, sono la norma, e
si finirebbe a scorrere avanti e indietro per leggerne uno alla volta.

⚠️ È il quarto difetto in questo repo che **nessun test avrebbe preso** e che si è visto solo
aprendo l'immagine. La regola "la UI si verifica guardandola" continua a ripagarsi.

### Verificato col rig, non per costruzione

1. **A video**: avviato il Sandbox con l'mp3 rinominato, screenshot al frame 120. Si vedono le
   tre righe, i colori (giallo l'avviso, il resto neutro), i conteggi sulle spunte, e nessun
   `?` — cioè le stringhe sono davvero tutte Latin-1.
2. **I filtri filtrano**: click sintetico (`PostMessage`) sulla spunta `Info`, secondo
   screenshot. Le due righe Info spariscono, resta l'avviso.

⚠️ **Trappola nuova del rig, da sapere**: il contatore dei frame parte quando parte il `Draw`,
ma il caricamento degli asset ruba **~3 secondi** prima. Uno scatto programmato al frame 600 non
è "a 10 secondi", è a 13 — e il primo tentativo ha chiuso il gioco prima che arrivasse, dando
uno screenshot che semplicemente non c'era. Non era il rig rotto: era la sveglia messa presto.

### Le scelte piccole, scritte perché non si ridiscutano

- **Nasce accesa**, al contrario di Systems/Components/Scripts. Non è un inventario che si
  consulta quando serve: è il flusso di ciò che il gioco sta facendo, e un log che bisogna
  ricordarsi di aprire si guarda solo dopo aver già perso tempo a cercare altrove.
- **Le categorie del filtro si ricavano dal buffer**, non dalle costanti di `LogCategories`.
  Offrire una categoria che nessuno usa darebbe un filtro che svuota la console senza motivo —
  e un gioco può inventarsi le proprie.
- **I conteggi stanno sulle spunte** (`Avvisi (1)`), non in una riga a parte: "quanti ce ne
  sono" e "li sto vedendo" sono la stessa domanda.
- **`Segui` si incolla in fondo solo se ci si era già**: trascinare via l'utente che ha
  scrollato indietro per leggere renderebbe il log illeggibile proprio mentre lo legge.
- **L'Info non è colorato.** Se si colora anche il livello normale, tutto è colorato e il
  colore non distingue più niente.

### Cosa resta aperto

⚠️ **`Logger.RemoveSink` è rimasta senza clienti**: era pensata per un pannello-sink che si
sregistra chiudendosi, e il pannello non è un sink. È in `ROADMAP.md` — o le si trova un uso o
va tolta, perché è lo stesso codice morto che la Fase 4.91 ha trovato nel logger.

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
  - [ ] *(rimandato)* Raycast — resta un buco della fisica (query di gioco: line of sight,
    proiettili, appoggio a terra), ma **non serve più al picking**: quello vuole ciò che si
    vede, non ciò che collide. Vedi Fase 4
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
| Gizmi 3D | ~~ImGuizmo.NET~~ → **scritti a mano** | nessun binding compatibile con ImGui.NET 1.91 + rlImgui-cs: vedi Fase 4 |
| Fisica 3D | **BepuPhysics v2** | sostituisce Aether (2D) |
| Test | **xUnit** | progetto `tests/` |

---

## Riepilogo milestone

1. ~~**Fase 2** → scena di primitive navigabile in 3D.~~ ✅
2. ~~**Fase 3** → scena caricata da file (data-driven).~~ ✅
3. ~~**Fase 4** → editor: hierarchy + inspector + gizmi + save/load + play/stop~~ **chiusa**
4. **Fase 5** → modelli, luci e fisica 3D reali. *(quasi tutta fatta, mancano le rifiniture [~])*
