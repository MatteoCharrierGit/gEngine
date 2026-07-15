namespace gEngine.Physics;

/// <summary>
/// Handle opaco verso un corpo nel mondo fisico (mappa a un BodyHandle/StaticHandle di
/// Bepu dentro l'adapter). Come per gli asset handle, l'engine non conosce il tipo
/// nativo: <c>Id == 0</c> = nessun corpo.
/// </summary>
public readonly record struct BodyId(int Id)
{
    public static readonly BodyId None = new(0);
    public bool IsValid => Id != 0;
}
