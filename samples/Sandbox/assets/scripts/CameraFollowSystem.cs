using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces.System;
using gEngine.Input;
using gEngine.MathUtils;
using gEngine.Scripting;
using Sandbox.Components;

namespace Sandbox.Systems;

/// <summary>
/// Centra la camera del gioco sul player (barra spaziatrice).
///
/// Non tiene più una <c>Camera3D</c>: la camera è un'entità del World, quindi questo system
/// fa quello che fa ogni altro system — scrive un <see cref="TransformComponent"/>. È il
/// guadagno del refactoring: prima muoveva un oggetto che viveva fuori dal World, che
/// nessun gizmo poteva toccare e che nessuna scena poteva salvare.
/// </summary>
[GameSystem(Order = 30)]
public class CameraFollowSystem(InputHandler inputHandler) : IInputSystem
{
    private readonly Vector3 _offsetPosition = new(0, 7, 6);

    // Due insiemi in AND, uno per verbo: agisce sulle camere, legge il player e basta. Il
    // player NON va in MatchedComponents (non lo tocchiamo) ma nemmeno taciuto: senza
    // ObservedComponents, davanti al player l'Inspector non mostrerebbe questo system —
    // cioè nasconderebbe la risposta a "perché la camera non mi segue?".
    public IReadOnlyList<Type> MatchedComponents { get; } =
        [typeof(CameraComponent), typeof(TransformComponent)];

    public IReadOnlyList<Type> ObservedComponents { get; } =
        [typeof(PlayerComponent), typeof(TransformComponent)];

    public void OnCreate(World world)
    {
    }

    public void OnUpdate(World world, float dt)
    {
        if (!inputHandler.IsActionDown(GameAction.CameraCenter))
            return;

        // FirstOrDefault e non First: né il player né la camera sono invarianti da quando
        // l'editor sa cancellare entità.
        var target = world.Query<PlayerComponent, TransformComponent>()
            .Select(query => (Vector3?)world.GetWorldMatrix(query.Entity).Translation)
            .FirstOrDefault();

        if (target is not { } playerPosition)
            return;

        foreach (var (entity, transform, camera) in world.Query<TransformComponent, CameraComponent>())
        {
            if (!camera.Primary)
                continue;

            // Il player lo si legge in MONDO (oggi è root, ma non è un invariante da
            // presupporre) e la posa che ne esce è in mondo: l'offset è "sette metri sopra
            // e sei dietro il player", non "sette unità del genitore della camera".
            var position = playerPosition + _offsetPosition;

            // La camera guardava il player tramite Target; ora l'unica posa che esiste è il
            // Transform, quindi "guarda lì" va espresso come rotazione. LookRotation è
            // l'inverso di GetForward, cioè la stessa convenzione Forward=+Z.
            var rotation = TransformExtensions.LookRotation(playerPosition - position, Vector3.UnitY);

            // SetWorldPose divide per il mondo del genitore e fa il write-back della struct:
            // è quello che tiene in piedi il conto se un giorno la camera viene agganciata a
            // qualcosa. Finché è root si riduce esattamente alla scrittura letterale di prima.
            // Il ritorno si può ignorare: false = genitore degenere (scala 0), e saltare un
            // frame di inseguimento è meglio che spedire la camera nell'origine.
            world.SetWorldPose(entity, position, rotation);
        }
    }
}
