using gEngine.Assets;

namespace gEngine.Tests.Scenes;

/// <summary>
/// Backend asset finto: distribuisce handle progressivi e non tocca niente di nativo.
///
/// Serve perché il round-trip di <c>MeshRenderer</c> <b>passa dall'AssetManager</b> —
/// path → handle in lettura, handle → path in scrittura — quindi non si può testare la
/// serializzazione senza un AssetManager vivo. Con il backend raylib servirebbe una
/// finestra aperta e i file veri su disco: due dipendenze che non hanno niente a che fare
/// con ciò che si sta verificando.
///
/// ⚠️ Non verifica che il file esista, ed è voluto: qui interessa che l'<b>andata e
/// ritorno del path</b> sia esatta, non che il modello sia caricabile. Il caso "file
/// mancante" non è coperto da nessuna parte — vedi la nota nell'handoff: raylib non lancia,
/// logga e restituisce un handle vuoto.
/// </summary>
internal sealed class FakeAssetBackend : IAssetBackend
{
    private int _nextId;

    /// <summary>I path assoluti passati a <see cref="LoadModel"/>, in ordine di chiamata.</summary>
    public List<string> LoadedModelPaths { get; } = [];

    public ModelHandle LoadModel(string absolutePath)
    {
        LoadedModelPaths.Add(absolutePath);
        return new ModelHandle(++_nextId);
    }

    public TextureHandle LoadTexture(string absolutePath) => new(++_nextId);
    public TextureHandle LoadTextureThumbnail(string absolutePath, int maxSize) => new(++_nextId);
    public SoundHandle LoadSound(string absolutePath) => new(++_nextId);
    public MusicHandle LoadMusic(string absolutePath) => new(++_nextId);

    public void UnloadTexture(TextureHandle handle) { }
    public nint GetTextureId(TextureHandle handle) => handle.Id;
    public void PlaySound(SoundHandle handle) { }
    public void PlayMusic(MusicHandle handle) { }
    public void UpdateMusic(MusicHandle handle) { }
    public void StopMusic(MusicHandle handle) { }
    public void UnloadAll() { }
}
