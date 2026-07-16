namespace gEngine.Scripting;

/// <summary>
/// Marca una classe come <b>system del gioco da registrare da sé</b>: chi la scrive non deve
/// andare a citarla in nessun altro file.
///
/// È il punto di tutta la faccenda "script": oggi un system nuovo esiste solo se qualcuno si
/// ricorda di scrivere <c>_systems.Add(new MioSystem())</c> dentro il gioco — cioè scrivere
/// uno script significa <b>toccare un file che non è lo script</b>. Con l'attributo il file si
/// dichiara da solo e <see cref="ScriptDiscovery"/> lo trova.
///
/// Si chiama <c>GameSystem</c> e non <c>System</c> per un motivo stupido e insormontabile:
/// <c>[System]</c> collide col namespace <c>System</c> e non compila.
///
/// ⚠️ Non fa girare niente da sé: un system è tale perché implementa una <b>interfaccia di
/// fase</b> (<c>IInputSystem</c>, <c>ISimulationSystem</c>, ...). Questo attributo dice solo
/// "registrami"; a smistarti resta il <c>SystemRegistry</c>, che lancia se non ne implementi
/// nessuna.
///
/// ⚠️ Il costruttore può chiedere le sue dipendenze: vengono risolte dalle
/// <see cref="Core.Resources"/> per tipo. È il caso d'uso per cui le Resource esistono — un
/// contenitore in cui è <b>dichiarato</b> di cosa vive il gioco. Ciò che non è dichiarato lì
/// non è iniettabile, e la scoperta lo dice invece di costruire un system mezzo rotto.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GameSystemAttribute : Attribute
{
    /// <summary>
    /// Ordine <b>dentro la fase</b>, fra gli script scoperti: più basso gira prima. Default 0.
    ///
    /// ⚠️ Esiste perché la riflessione <b>non ha un ordine</b>: <c>GetTypes()</c> restituisce
    /// i tipi nell'ordine in cui capitano nei metadati, che cambia ricompilando. E dentro una
    /// fase l'ordine <i>è</i> comportamento (il caso vivo dell'engine: <c>LightingSystem</c>
    /// deve girare prima di <c>MeshRenderSystem</c>, o le uniform delle luci arrivano dopo le
    /// mesh che dovevano illuminare). Senza questo numero, aggiungere uno script potrebbe
    /// riordinarne altri due — e non lo direbbe nessuno.
    ///
    /// A parità di Order l'ordine è il <b>nome del tipo</b>: arbitrario, ma almeno stabile fra
    /// una compilazione e l'altra. Se due script si contendono un ordine, il numero va messo.
    /// </summary>
    public int Order { get; init; }
}
