namespace gEngine.Ecs.Component;

/// <summary>
/// Marca un campo (o una proprietà) di un componente come <b>configurazione d'autore</b>:
/// un valore che ha senso vedere e modificare dall'editor.
///
/// L'Inspector è reflection-driven e finora mostrava <b>tutto</b> ciò che era pubblico. Il
/// default era quindi "esponi", e ogni campo nuovo — un accumulatore, una cache, un
/// contatore di stato — finiva nell'UI senza che nessuno lo avesse deciso, con un DragFloat
/// sopra. Qui il default si inverte: si vede solo ciò che è stato dichiarato configurabile.
/// La decisione torna a chi scrive il componente, che è l'unico a sapere quali dei suoi
/// campi sono dati d'autore.
///
/// È il gemello a livello di <b>membro</b> di <see cref="RuntimeStateAttribute"/>, che
/// esclude un componente <b>intero</b>: stessa idea (l'editor manipola dati che non
/// conosce, e ha bisogno che il tipo gli dica cosa può toccare), granularità diversa.
///
/// Vive nell'engine e non in <c>gEngine.Editor</c> apposta: un componente di un gioco deve
/// potersi descrivere senza referenziare il progetto dell'editor — altrimenti spedire un
/// gioco senza editor significherebbe togliere gli attributi dai propri componenti. È lo
/// stesso motivo per cui <c>[RuntimeState]</c> sta qui: l'attributo è <b>letto</b>
/// dall'editor, ma è <b>scritto</b> da chi definisce i dati.
/// </summary>
/// <param name="label">
/// Etichetta mostrata al posto del nome del membro (es. <c>[EditorConfiguration("Velocità max")]</c>).
/// Opzionale: senza, vale il nome del membro, che per un componente di dati puri è quasi
/// sempre già la parola giusta.
/// </param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class EditorConfigurationAttribute(string? label = null) : Attribute
{
    /// <summary>Etichetta scelta a mano, oppure <c>null</c> = usa il nome del membro.</summary>
    public string? Label { get; } = label;
}
