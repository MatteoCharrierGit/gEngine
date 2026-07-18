namespace gEngine.Assets;

/// <summary>
/// Gestore asset indipendente dalla libreria grafica/audio. Non conosce
/// <c>Raylib_cs</c>: risolve i path relativi rispetto alla cartella asset, tiene una
/// cache path→handle (stesso file caricato una volta sola) e delega ogni operazione
/// nativa a un <see cref="IAssetBackend"/>. Per usare un'altra libreria basta passare
/// un backend diverso al costruttore — questa classe non cambia.
/// </summary>
public class AssetManager
{
    private readonly string _rootDir;
    private readonly IAssetBackend _backend;

    private readonly Dictionary<string, TextureHandle> _textures = new();
    private readonly Dictionary<string, SoundHandle> _sounds = new();
    private readonly Dictionary<string, MusicHandle> _musics = new();
    private readonly Dictionary<string, ModelHandle> _models = new();

    public AssetManager(string rootDirectory, string assetsDir, IAssetBackend backend)
    {
        _rootDir = Path.Combine(rootDirectory, assetsDir);
        _backend = backend;
    }

    public TextureHandle LoadTexture2D(string relPath)
    {
        var fullPath = Path.Combine(_rootDir, relPath);
        if (_textures.TryGetValue(fullPath, out var cached))
            return cached;

        var handle = _backend.LoadTexture(fullPath);
        _textures[fullPath] = handle;
        return handle;
    }

    /// <summary>
    /// Un'anteprima: l'immagine ridotta a <paramref name="maxSize"/> pixel sul lato lungo.
    ///
    /// ⚠️ Tre differenze volute rispetto agli altri <c>Load*</c>, tutte per lo stesso motivo —
    /// questa non è una risorsa del gioco:
    /// <list type="bullet">
    ///   <item>prende un path <b>assoluto</b>: si sfoglia il disco, non la cartella asset;</item>
    ///   <item><b>non è in cache qui</b>: la cache è di chi sfoglia, che sa quando ha smesso di
    ///   guardare una cartella. Metterla nel dizionario degli asset vorrebbe dire che le
    ///   miniatura vivono quanto il gioco;</item>
    ///   <item>si libera una alla volta con <see cref="UnloadTexture"/>, invece di aspettare
    ///   <c>UnloadAll</c>.</item>
    /// </list>
    /// </summary>
    public TextureHandle LoadThumbnail(string absolutePath, int maxSize) =>
        _backend.LoadTextureThumbnail(absolutePath, maxSize);

    public void UnloadTexture(TextureHandle handle) => _backend.UnloadTexture(handle);

    /// <summary>L'id GPU, per disegnare la texture in ImGui. Vedi <see cref="IAssetBackend.GetTextureId"/>.</summary>
    public nint GetTextureId(TextureHandle handle) => _backend.GetTextureId(handle);

    public SoundHandle LoadSound(string relPath)
    {
        var fullPath = Path.Combine(_rootDir, relPath);
        if (_sounds.TryGetValue(fullPath, out var cached))
            return cached;

        var handle = _backend.LoadSound(fullPath);
        _sounds[fullPath] = handle;
        return handle;
    }

    public MusicHandle LoadMusicStream(string relPath)
    {
        var fullPath = Path.Combine(_rootDir, relPath);
        if (_musics.TryGetValue(fullPath, out var cached))
            return cached;

        var handle = _backend.LoadMusic(fullPath);
        _musics[fullPath] = handle;
        return handle;
    }

    public ModelHandle LoadModel(string relPath)
    {
        var fullPath = Path.Combine(_rootDir, relPath);
        if (_models.TryGetValue(fullPath, out var cached))
            return cached;

        var handle = _backend.LoadModel(fullPath);
        _models[fullPath] = handle;
        return handle;
    }

    /// <summary>
    /// Path relativo da cui è stato caricato un modello: il <b>verso opposto</b> della
    /// cache. Serve a salvare una scena — un <see cref="ModelHandle"/> è un id opaco e
    /// non significa niente al prossimo avvio, mentre <c>"models/x/scene.gltf"</c> sì.
    ///
    /// La cache è indicizzata per path, quindi qui si scandisce: è O(n) sui modelli
    /// caricati, ma succede solo al salvataggio, non per frame.
    /// </summary>
    public bool TryGetModelPath(ModelHandle handle, out string relativePath)
    {
        foreach (var (fullPath, cached) in _models)
        {
            if (cached.Id != handle.Id)
                continue;

            // Rimettiamo il path nella stessa forma in cui è stato chiesto (relativo alla
            // cartella asset), così il file di scena resta indipendente dalla macchina.
            relativePath = Path.GetRelativePath(_rootDir, fullPath).Replace('\\', '/');
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    // Comodità di playback: passthrough verso il backend, così il gioco parla con un
    // solo oggetto (l'AssetManager) e non deve tenere un riferimento separato al backend.
    public void PlaySound(SoundHandle handle) => _backend.PlaySound(handle);
    public void PlayMusic(MusicHandle handle) => _backend.PlayMusic(handle);
    public void UpdateMusic(MusicHandle handle) => _backend.UpdateMusic(handle);
    public void StopMusic(MusicHandle handle) => _backend.StopMusic(handle);

    public void UnloadAll()
    {
        _backend.UnloadAll();

        _textures.Clear();
        _sounds.Clear();
        _musics.Clear();
        _models.Clear();
    }
}
