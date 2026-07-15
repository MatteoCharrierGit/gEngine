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

## Roadmap

Il piano di sviluppo (obiettivo: mini engine **3D** con editor visuale) è in
[`docs/ROADMAP.md`](docs/ROADMAP.md).

## Licenza

[MIT](LICENSE)
