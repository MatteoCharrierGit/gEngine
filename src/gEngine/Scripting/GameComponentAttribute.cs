namespace gEngine.Scripting;

/// <summary>
/// Marca un componente come <b>dato di scena del gioco da registrare da sé</b>: chi lo scrive
/// non deve andare a citarlo nel <c>SceneComponentRegistry</c> a mano.
///
/// Gemello di <see cref="GameSystemAttribute"/> e stessa ragione: un componente nuovo, oggi,
/// esiste per il file di scena e per l'editor solo se qualcuno si ricorda di scrivere
/// <c>registry.Register("Mio", ...)</c> altrove. Dimenticarsene non dà un errore di
/// compilazione: dà una scena che <b>non si salva</b> (il serializer lancia) e un componente
/// che non compare in "Aggiungi componente". Il pannello Components della Fase 4.7 esiste
/// apposta per intercettarlo — questo attributo toglie di mezzo il problema a monte.
///
/// ⚠️ <b>Non</b> sostituisce <c>SceneComponentRegistry.RegisterEngineDefaults</c> e non deve:
/// i componenti dell'engine hanno binder <b>asimmetrici</b> che un attributo non può
/// esprimere (<c>MeshRenderer</c> converte un path in handle e viceversa, <c>Parent</c>
/// risolve un nome in <c>Entity</c>). Questo attributo copre il caso normale — "serializza i
/// campi con le opzioni condivise" — che è quello di quasi tutti i componenti di un gioco.
/// Chi ha bisogno dell'asimmetria continua a registrarsi a mano, ed è giusto che costi.
///
/// <b>Il valore di default</b> (senza il quale l'editor non sa creare il componente: vedi
/// <c>SceneComponentRegistry.TryCreateDefault</c>) si dichiara con un metodo statico:
/// <code>
/// [GameComponent]
/// public struct HealthComponent
/// {
///     [EditorConfiguration] public float Max;
///     [EditorConfiguration] public float Current;
///
///     public static HealthComponent CreateDefault() => new() { Max = 100f, Current = 100f };
/// }
/// </code>
/// ⚠️ Un metodo statico e <b>non</b> <c>Activator.CreateInstance</c>: per uno struct di dati
/// nudi quello darebbe tutti i campi a zero, cioè un default rotto travestito da neutro (la
/// decisione è in <c>ROADMAP.md</c> Fase 4.7). Senza <c>CreateDefault</c> il componente si
/// salva e si carica lo stesso, ma nell'editor resta spento col motivo — che è la stessa
/// regola di prima, non una nuova.
/// </summary>
/// <param name="key">
/// Come si chiama nel file di scena. Se <c>null</c>, il nome del tipo <b>senza il suffisso
/// "Component"</b> — la stessa convenzione con cui l'Inspector intitola gli header, così la
/// parola nel .json e quella sullo schermo sono la stessa.
///
/// ⚠️ Va messo a mano quando la chiave deve restare stabile mentre il tipo si rinomina: la
/// chiave sta scritta dentro i file di scena già salvati, il nome del tipo no.
/// </param>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
public sealed class GameComponentAttribute(string? key = null) : Attribute
{
    public string? Key { get; } = key;
}
