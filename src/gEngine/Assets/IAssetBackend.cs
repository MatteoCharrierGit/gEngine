namespace gEngine.Assets;

/// <summary>
/// Port (astrazione) verso la libreria che sa davvero caricare/riprodurre gli asset.
/// L'engine dipende solo da questa interfaccia; l'unica implementazione concreta
/// (<see cref="RayLibAssetBackend"/>) è l'unico punto che tocca <c>Raylib_cs</c>.
/// È lo stesso schema "ports &amp; adapters" già usato da <c>IRenderer</c>/<c>RayLibRenderer</c>.
///
/// I metodi di caricamento ricevono un <b>path assoluto</b> (la risoluzione della
/// cartella asset è responsabilità di <see cref="AssetManager"/>) e restituiscono un
/// handle opaco. I metodi di playback su un handle non valido devono essere no-op.
///
/// Nota di design: qui load e playback stanno insieme per semplicità di wiring. Se in
/// futuro la parte audio cresce, si può scorporare un <c>IAudioBackend</c> separato —
/// lo stesso adapter raylib implementerebbe entrambe le interfacce.
/// </summary>
public interface IAssetBackend
{
    TextureHandle LoadTexture(string absolutePath);
    SoundHandle LoadSound(string absolutePath);
    MusicHandle LoadMusic(string absolutePath);
    ModelHandle LoadModel(string absolutePath);

    void PlaySound(SoundHandle handle);
    void PlayMusic(MusicHandle handle);
    void UpdateMusic(MusicHandle handle);
    void StopMusic(MusicHandle handle);

    /// <summary>Scarica tutte le risorse native caricate finora.</summary>
    void UnloadAll();
}
