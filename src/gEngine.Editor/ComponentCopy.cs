using System.Reflection;

namespace gEngine.Editor;

/// <summary>
/// Una copia <b>indipendente</b> di un componente di cui non si conosce il tipo.
///
/// Esiste perché <c>GetBoxed</c> non basta, e la differenza è invisibile finché non morde:
/// per uno struct il boxing è già una copia, per una <b>class</b> è il riferimento. Chi tratta
/// i due casi allo stesso modo scrive codice che funziona su metà dei componenti — è successo
/// davvero (<see cref="EntityOperations.Duplicate"/>: dipingere la copia dipingeva
/// l'originale) ed è il motivo per cui questo non è un dettaglio dell'undo ma un pezzo a sé.
///
/// L'undo ne ha bisogno per la stessa ragione, più stringente: un "prima" che è lo stesso
/// oggetto del "dopo" non è una storia, è un alias — la modifica successiva riscriverebbe il
/// passato, e annullare riporterebbe allo stato in cui si è già.
/// </summary>
public static class ComponentCopy
{
    /// <summary>
    /// ⚠️ <c>MemberwiseClone</c> è <c>protected</c> su <c>object</c>, quindi si raggiunge solo
    /// per reflection. È comunque la scelta giusta rispetto a copiare i campi a mano: fa
    /// esattamente la copia superficiale che serve, <b>campi privati ed ereditati compresi</b>,
    /// che una scansione con <c>GetFields</c> sbaglierebbe in silenzio sul primo componente che
    /// ha una gerarchia.
    /// </summary>
    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Copia <b>superficiale</b>: i campi sono copiati uno a uno, quindi due componenti non
    /// condividono più la propria scatola. Se un campo è a sua volta un riferimento mutabile
    /// (oggi nessun componente ne ha: sono numeri, enum, handle e stringhe — e le stringhe
    /// sono immutabili) quello resta condiviso. È la stessa profondità che il salvataggio
    /// garantisce, ed è voluta: una copia profonda generica duplicherebbe anche ciò che
    /// <b>deve</b> restare condiviso, come un handle verso una risorsa caricata.
    /// </summary>
    public static object Shallow(object component) => CloneMethod.Invoke(component, null)!;
}
