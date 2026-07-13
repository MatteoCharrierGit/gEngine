# gEngine

Un piccolo game engine 2D scritto in C#, nato come progetto per imparare il
linguaggio e ragionare su architettura ECS, game loop e input. Costruito sopra
[Raylib](https://www.raylib.com/) (rendering/finestra/audio) e
[Aether.Physics2D](https://github.com/nkast/Aether.Physics2D) (fisica).

> Progetto didattico, work in progress. Le API cambiano spesso.

## Struttura del repository

```
gEngine/
├─ src/
│  └─ gEngine/            # la libreria engine
└─ samples/
   └─ gEngine.Sample/     # esempio minimale d'uso dell'engine
```

- **`src/gEngine`** — la libreria vera e propria. Contiene:
  - `Core/` — game loop e interfaccia `IGame`
  - `Ecs/` — Entity, World, storage dei componenti, query, sistemi
  - `Input/` — mappatura tasti → azioni di gioco
  - `Rendering/` — camera 2D
  - `Assets/` — caricamento texture/audio
- **`samples/gEngine.Sample`** — un esempio essenziale: apre una finestra e
  muove un'entità con WASD. Nessun asset esterno, gira appena clonato.

## Requisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Come eseguire l'esempio

```bash
dotnet run --project samples/gEngine.Sample
```

## Usare l'engine

Un gioco implementa `IGame` e viene passato al `GameLoop`:

```csharp
using gEngine.Core;

var loop = new GameLoop(1280, 720, "Il mio gioco", new MyGame());
loop.Run();
```

`IGame` espone quattro metodi: `Init`, `Update` (a passo fisso), `Draw` e
`Shutdown`. Vedi [`samples/gEngine.Sample/SampleGame.cs`](samples/gEngine.Sample/SampleGame.cs)
per un esempio completo e commentato.

## Licenza

[MIT](LICENSE)
