namespace gEngine.Log;

/// <summary>
/// Le categorie con cui si tagga un messaggio. Costanti e non un enum: una categoria è
/// un'etichetta che finisce in una stringa formattata, e il giorno che un gioco vuole la sua
/// (<c>"Inventario"</c>) deve poterla passare senza estendere un tipo dell'engine.
///
/// ⚠️ Non sono tutte usate, ed è voluto: <see cref="Audio"/> e <see cref="Ecs"/> aspettano le
/// funzionalità che le riempiranno (l'audio manager, e un ECS che abbia qualcosa da raccontare).
/// Meglio una costante in attesa che una categoria inventata al momento del primo uso, che
/// sarebbe scritta diversa in due punti.
/// </summary>
public static class LogCategories
{
    public const string Engine = "Engine";
    public const string Renderer = "Renderer";
    public const string Assets = "Assets";
    public const string Audio = "Audio";
    public const string Physics = "Physics";
    public const string Ecs = "Ecs";
    public const string Window = "Window";
}