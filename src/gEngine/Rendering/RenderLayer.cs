namespace gEngine.Rendering;

/// <summary>
/// Macro-fascia di ordinamento del disegno. Gli <see cref="Opaque"/> vanno
/// disegnati <b>prima</b> dei <see cref="Transparent"/>: gli opachi scrivono il
/// depth buffer e si occludono correttamente tra loro in qualsiasi ordine,
/// mentre i trasparenti devono fondersi <i>sopra</i> ciò che è già stato scritto.
///
/// L'ordine numerico dei membri è significativo (è la chiave di sort primaria):
/// tienilo Opaque = 0 &lt; Transparent = 1.
/// </summary>
public enum RenderLayer
{
    Opaque = 0,
    Transparent = 1,
}
