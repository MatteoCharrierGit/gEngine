namespace gEngine.Ecs.Component;

/// <summary>
/// Marca un componente come <b>stato di runtime</b>: non è un dato d'autore, ma un link
/// verso qualcosa che vive fuori dall'ECS (un corpo fisico, una risorsa GPU) creato da un
/// system durante l'esecuzione.
///
/// Serve a chi manipola i componenti <b>senza conoscerne i tipi</b> — l'editor — per non
/// trattarli come dati normali. In particolare <b>non vanno duplicati</b>: copiare un
/// link farebbe puntare due entità alla stessa risorsa esterna, che poi verrebbe liberata
/// due volte o sincronizzata su entrambe.
///
/// Finora questa regola viveva solo nei commenti (<see cref="PhysicsBodyComponent"/>:
/// "NON va messo nei file scena"). L'attributo la rende verificabile dal codice, così un
/// prossimo componente-link non deve ri-scoprire il problema.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
public sealed class RuntimeStateAttribute : Attribute;
