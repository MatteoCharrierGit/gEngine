# gEngine

Un piccolo game engine 3D scritto in C#, nato come progetto per imparare il
linguaggio e ragionare su architettura ECS, game loop, input e rendering.
Costruito sopra [Raylib](https://www.raylib.com/) (rendering/finestra/audio).

> Progetto didattico, work in progress. Le API cambiano spesso.

## Struttura del repository

```
gEngine/
├─ src/
│  └─ gEngine/            # la libreria engine
└─ samples/
   └─ Sandbox/            # scena 3D d'esempio (caricata da file JSON)
```

- **`src/gEngine`** — la libreria vera e propria. Contiene:
  - `Core/` — game loop e interfaccia `IGame`
  - `Ecs/` — Entity, World, storage dei componenti, query, sistemi
  - `Input/` — mappatura tasti → azioni di gioco
  - `Rendering/` — camera 3D, mesh e renderer astratto (`IRenderer`)
  - `Scenes/` — caricamento scene da JSON (registry di componenti data-driven)
  - `Assets/` — caricamento texture/audio
- **`samples/Sandbox`** — una scena 3D navigabile: una "città" di cubi caricata
  da `assets/scenes/city.json`, con player controllabile a WASD e camera 3D.

## Requisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Come eseguire l'esempio

```bash
dotnet run --project samples/Sandbox
```

> I **modelli e l'audio non sono versionati** (`assets/models/`, `assets/audio/`): sono
> centinaia di MB non redistribuibili. Un clone pulito parte, ma la scena demo non ha il suo
> modello e non c'è musica — raylib su file mancante logga un WARNING e prosegue, non lancia.
> Le scene e gli script sotto `assets/` invece **ci sono**: gli script sono sorgenti compilati
> a runtime, non asset.

## Test

```bash
dotnet test tests/gEngine.Tests
```

Coprono il round-trip di serializzazione (`World → Scene → World`), che è il pezzo su cui
poggiano il Salva dell'editor, il Play/Stop e — domani — il ricaricamento a caldo degli script.

## Usare l'engine

Un gioco implementa `IGame` e viene passato al `GameLoop`:

```csharp
using gEngine.Core;

var loop = new GameLoop(1280, 720, "Il mio gioco", new MyGame());
loop.Run();
```

`IGame` espone quattro metodi: `Init`, `Update` (a passo fisso),
`Draw(IRenderer)` e `Shutdown`. Vedi
[`samples/Sandbox/SandboxGame.cs`](samples/Sandbox/SandboxGame.cs)
per un esempio completo.

## Documentazione

| File | Cosa contiene |
|---|---|
| [`docs/USAGE.md`](docs/USAGE.md) | Come si costruisce un gioco con gEngine **oggi** |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Cosa resta da fare, per argomento |
| [`docs/DA_RICORDARE.md`](docs/DA_RICORDARE.md) | Trappole già pagate, limiti accettati, standard di lavoro |
| [`docs/DECISIONI.md`](docs/DECISIONI.md) | Il perché di ogni scelta già presa, fase per fase |

Obiettivo del progetto: mini engine **3D** con editor visuale.

## Licenza

[MIT](LICENSE)
