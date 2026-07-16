using gEngine.Assets;

namespace gEngine.Ecs.Component;

/// <summary>
/// Marca un campo (o una proprietà) di un componente come <b>riferimento a un asset</b> di un
/// certo genere: l'editor lo mostra come una casella in cui si trascina un file dal pannello
/// File system, invece che come un numero.
///
/// È il terzo attributo della stessa famiglia, e la ragione è sempre quella: l'editor
/// manipola dati di cui non conosce i tipi, quindi è il tipo a dovergli dire cosa può fare.
/// <see cref="RuntimeStateAttribute"/> esclude un componente intero, <see cref="EditorConfigurationAttribute"/>
/// espone un membro come valore modificabile, questo espone un membro come <b>slot</b>.
///
/// PERCHÉ non basta <c>[EditorConfiguration]</c>: il campo tiene un <see cref="ModelHandle"/>
/// (o simile), cioè un <b>id opaco valido solo per questa esecuzione</b>. Esposto come valore
/// darebbe un DragInt su un indice di cache — modificarlo punterebbe a un modello a caso, e al
/// prossimo avvio l'id non significherebbe più niente (è lo stesso motivo per cui nel file di
/// scena finisce il <i>path</i> e non l'handle: vedi il writer di MeshRenderer in
/// <c>SceneComponentRegistry</c>). Il dato d'autore è il file; l'handle è come lo si tiene in
/// mano. Questo attributo dice all'editor di far scegliere il <b>file</b> e di occuparsi lui
/// della conversione, che è l'unico verso in cui la cosa ha senso.
///
/// I due attributi sono <b>alternativi</b>, non cumulabili: un membro è un valore o è uno
/// slot. Marcandolo con entrambi vince questo — ma è una dichiarazione confusa, non una
/// combinazione.
///
/// ⚠️ Lo slot <b>non conosce</b> gli altri campi del componente: assegnare un modello a un
/// <c>MeshRenderer</c> con <c>Kind = Cube</c> riempie il campo e non cambia cosa si vede,
/// perché a decidere è <c>Kind</c>. L'editor lo dice nel tooltip dello slot invece di
/// aggiustare Kind da sé: un'UI generica che indovina i campi correlati indovina anche
/// quando non deve.
/// </summary>
/// <param name="kind">Il genere di asset che questo slot accetta. È anche la validazione:
/// un file di un altro genere non si può proprio lasciare cadere qui.</param>
/// <param name="label">Come per <see cref="EditorConfigurationAttribute"/>: etichetta a mano,
/// o <c>null</c> per il nome del membro.</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class EditorAssetAttribute(AssetKind kind, string? label = null) : Attribute
{
    public AssetKind Kind { get; } = kind;

    public string? Label { get; } = label;
}
