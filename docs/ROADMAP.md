# gEngine — Roadmap (3D)

## Fase 0 — Fondamenta & igiene 🟢

Piccole cose che userai ovunque, meglio averle prima.

- [ ] **Logging** a livelli (Debug/Info/Warn/Error)
  - [ ] Interfaccia `ILogger` + implementazione console
  - [ ] Timestamp e categoria/tag per messaggio
  - [ ] Punto d'accesso comodo dall'engine
- [ ] **Unit test** sull'ECS (primo contatto col testing in C#)
  - [ ] Progetto di test (`tests/gEngine.Tests`, xUnit)
  - [ ] Test su `CreateEntity`, `AddComponent`/`GetComponent`, `Query<..>`
  - [ ] Test sul gotcha struct/copia (mutazione + write-back)
- [ ] **Adozione math**: standardizzare su `System.Numerics`
  - [ ] Decidere convenzioni: sistema **right-handed, Y-up** (come Raylib)
  - [ ] Note su unità (1 unità = 1 metro?) e scala di riferimento

**Milestone:** log leggibile a runtime + test verdi.

---

## Fase 1 — Transform 3D & math 🟢🟡

Il cuore matematico, già in ottica 3D.

- [ ] **`TransformComponent`** che sostituisce `PositionComponent`
  - [ ] `Position : Vector3`
  - [ ] `Rotation : Quaternion` (in 2D useresti solo la Z)
  - [ ] `Scale : Vector3`
- [ ] **World matrix** dal transform
  - [ ] Comporre `Matrix4x4 = Scale * Rotation * Translation`
  - [ ] Helper per direzioni (forward/right/up) dal quaternion
- [ ] **Migrazione** del codice esistente da `Position` a `Transform`
  - [ ] Aggiornare sample e sistemi
  - [ ] Ricordare il **write-back** dopo la mutazione (struct = copia)
- [ ] *(rimandabile)* **Gerarchia di transform** (parent/child)
  - [ ] `Parent`/figli, transform locale vs mondo
  - [ ] Ricalcolo world matrix (dirty flag)

**Milestone:** entità con posizione/rotazione/scala 3D reali.

---

## Fase 2 — Fondamenta rendering 3D 🟡🔴

Da "il gioco disegna a mano" a "un sistema disegna la scena".

- [ ] **Wrapper `Camera3D`**
  - [ ] Position / Target / Up / FovY / Projection (perspective)
  - [ ] Conversione verso `Raylib_cs.Camera3D`
  - [ ] `BeginMode3D`/`EndMode3D` nel passo di rendering
- [ ] **Camera di debug free-fly / orbit** (fondamentale per iterare in 3D)
  - [ ] Movimento WASD + mouse look, oppure orbit attorno a un target
- [ ] **Componenti disegnabili**
  - [ ] `MeshComponent`/`ModelComponent` con primitive (`GenMeshCube`, `GenMeshSphere`)
  - [ ] Colore/material base
- [ ] **`RenderSystem`**
  - [ ] Itera entità con `Transform` + renderable
  - [ ] Applica la world matrix e chiama `DrawModel`/`DrawMesh`
  - [ ] Depth test e back-face culling (default Raylib)
- [ ] **Render order / layer** (opachi vs trasparenti)

**Milestone:** una scena di cubi/sfere navigabile con camera 3D. 🎉

---

## Fase 3 — Scene management & serializzazione 🔴 *(fase pivot)*

Rendere la scena un **file**. Precondizione dell'editor.

- [ ] **Registry dei componenti** (via reflection)
  - [ ] Enumerare i tipi di componente disponibili
  - [ ] Enumerare i campi di ciascuno (per l'inspector futuro)
  - [ ] Attributi tipo `[SerializeField]` / `[HideInInspector]`
- [ ] **Serializzazione** (JSON con `System.Text.Json`)
  - [ ] Converter per `Vector3` / `Quaternion`
  - [ ] Serializzare un'entità (id + lista componenti + valori)
  - [ ] Serializzare/deserializzare una **scena** intera
- [ ] **`SceneManager`**
  - [ ] `LoadScene(path)` / `UnloadScene` / switch
  - [ ] Concetto di scena "attiva"
- [ ] **Refactor** del gioco
  - [ ] Il sample **carica una scena da file** invece di costruirla in codice
  - [ ] Una scena `.json` d'esempio versionata

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
