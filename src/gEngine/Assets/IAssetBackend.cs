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

    /// <summary>
    /// Carica un'immagine gia' <b>rimpicciolita</b>: il lato lungo non supera
    /// <paramref name="maxSize"/> pixel.
    ///
    /// Esiste per le anteprime dell'editor, e la riduzione avviene <b>prima</b> di salire sulla
    /// GPU perche' e' l'unico modo in cui e' sostenibile: sfogliare una cartella di texture 4K
    /// caricandole intere significherebbe riempire la memoria video per disegnare dei
    /// francobolli. Una miniatura non e' una texture del gioco che per caso si guarda piccola.
    /// </summary>
    /// <returns><see cref="TextureHandle.None"/> se il file non e' un'immagine leggibile: chi
    /// chiede un'anteprima sta sfogliando il disco dell'utente, dove qualunque file puo' non
    /// essere quel che dichiara.</returns>
    TextureHandle LoadTextureThumbnail(string absolutePath, int maxSize);

    /// <summary>
    /// Libera una singola texture. Serve alle anteprime, che hanno un ciclo di vita loro
    /// (si sfoglia una cartella, si va via) al contrario degli asset del gioco, che vivono
    /// quanto la scena e se ne va tutto insieme con <see cref="UnloadAll"/>.
    /// </summary>
    void UnloadTexture(TextureHandle handle);

    /// <summary>
    /// L'id GPU grezzo della texture, per chi deve disegnarla fuori dal renderer.
    ///
    /// ⚠️ E' una <b>crepa voluta</b> nel port, la stessa gia' aperta da
    /// <c>IRenderer.GetRenderTargetTextureId</c> e per lo stesso motivo: un'immagine di ImGui
    /// <b>e'</b> un handle GPU, e un tipo intermedio nasconderebbe soltanto che i due lati
    /// devono parlare della stessa texture. Un gioco non la usa mai.
    /// </summary>
    nint GetTextureId(TextureHandle handle);

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
