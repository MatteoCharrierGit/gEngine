namespace gEngine.Assets;

// Handle opachi verso le risorse caricate. Sono volutamente "stupidi": solo un id
// intero verso la tabella interna del backend (vedi IAssetBackend). Il consumatore
// non sa — e non deve sapere — cosa c'è dietro (Texture2D di raylib, un handle
// OpenGL, ecc.): questo è ciò che rende l'engine indipendente dalla libreria.
//
// Convenzione: Id == 0 significa "nessuna risorsa" (handle non valido). I metodi di
// playback/uso su un handle non valido sono no-op sicuri lato backend.

public readonly record struct TextureHandle(int Id)
{
    public static readonly TextureHandle None = new(0);
    public bool IsValid => Id != 0;
}

public readonly record struct SoundHandle(int Id)
{
    public static readonly SoundHandle None = new(0);
    public bool IsValid => Id != 0;
}

public readonly record struct MusicHandle(int Id)
{
    public static readonly MusicHandle None = new(0);
    public bool IsValid => Id != 0;
}

public readonly record struct ModelHandle(int Id)
{
    public static readonly ModelHandle None = new(0);
    public bool IsValid => Id != 0;
}
