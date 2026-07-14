using gEngine.Core;
using Sandbox;

var gameLoop = new GameLoop(1920, 1080, "Game", new SandboxGame());
gameLoop.Run();