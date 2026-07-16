using gEngine.Input;
using gEngine.Rendering;

namespace gEngine.Core;

public interface IGame
{
    /// <summary>
    /// Riceve le <see cref="Resources"/> del <c>GameLoop</c>, già popolate con ciò che
    /// il loop possiede (<c>InputHandler</c>, <c>AssetManager</c>, <c>IRenderer</c>).
    ///
    /// PERCHÉ un contenitore invece dei servizi uno per uno: la firma era
    /// <c>Init(InputHandler, AssetManager)</c> e cresceva di un parametro a ogni servizio
    /// nuovo — ogni implementatore di IGame si rompeva per una dipendenza che magari non
    /// usava. Peggio: l'<c>IRenderer</c> non ci stava proprio, perché il loop lo crea dopo
    /// <c>InitWindow</c> e lo passava solo a <c>Draw</c>; il gioco era costretto a
    /// registrarlo pigramente al primo frame. La regola "l'infrastruttura è una Resource
    /// registrata" (vedi <see cref="Resources"/>) nasceva così con un'eccezione
    /// strutturale, e un'eccezione in una regola giovane è una regola morta.
    ///
    /// Il gioco può aggiungere qui le SUE risorse (es. l'<c>IPhysicsWorld</c>, che il loop
    /// non conosce): il contenitore è condiviso, non impone chi crea cosa.
    ///
    /// Garanzia di ciclo di vita: <c>Init</c> gira a finestra già aperta, quindi qui il
    /// contesto grafico è vivo ed è lecito toccare risorse GPU.
    /// </summary>
    void Init(Resources resources);

    // Update e Draw continuano a ricevere i due servizi che usano SEMPRE, invece di
    // pescarli dalle Resources: il parametro esplicito dice "senza questo non esisto" a
    // colpo d'occhio, un Get<T>() no. Le due cose non sono in contraddizione — la Resource
    // resta il punto di verità (è la stessa istanza), il parametro è comodità.
    void Update(float fixedDeltaTime, InputHandler inputHandler);
    void Draw(IRenderer renderer);
    void Shutdown();
}
