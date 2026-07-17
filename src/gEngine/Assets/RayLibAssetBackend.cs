using Raylib_cs;

namespace gEngine.Assets;

/// <summary>
/// Adapter raylib di <see cref="IAssetBackend"/>: l'<b>unico</b> file lato-asset che
/// importa <c>Raylib_cs</c>. Tiene le vere risorse native (<c>Texture2D</c>,
/// <c>Sound</c>, <c>Music</c>) in tabelle id→risorsa e le espone all'esterno solo
/// tramite handle opachi. Cambiare libreria = riscrivere solo questa classe.
///
/// Precondizione: per gli asset audio, <c>Raylib.InitAudioDevice()</c> dev'essere già
/// stato chiamato (lo fa <c>GameLoop</c> prima di <c>IGame.Init</c>).
/// </summary>
public class RayLibAssetBackend : IAssetBackend
{
    // 0 è riservato agli handle "None": si parte da 1.
    private int _nextId = 1;

    private readonly Dictionary<int, Texture2D> _textures = new();
    private readonly Dictionary<int, Sound> _sounds = new();
    private readonly Dictionary<int, Music> _musics = new();
    private readonly Dictionary<int, Model> _models = new();

    public TextureHandle LoadTexture(string absolutePath)
    {
        var texture = Raylib.LoadTexture(absolutePath);
        var id = _nextId++;
        _textures[id] = texture;
        return new TextureHandle(id);
    }

    public SoundHandle LoadSound(string absolutePath)
    {
        var sound = Raylib.LoadSound(absolutePath);
        var id = _nextId++;
        _sounds[id] = sound;
        return new SoundHandle(id);
    }

    public MusicHandle LoadMusic(string absolutePath)
    {
        var music = Raylib.LoadMusicStream(absolutePath);
        var id = _nextId++;
        _musics[id] = music;
        return new MusicHandle(id);
    }

    public ModelHandle LoadModel(string absolutePath)
    {
        var model = Raylib.LoadModel(absolutePath);
        GenerateAlbedoMipmaps(model);

        var id = _nextId++;
        _models[id] = model;
        return new ModelHandle(id);
    }

    /// <summary>
    /// Genera le mipmap delle mappe albedo del modello e ci mette sopra il filtro
    /// trilineare.
    ///
    /// Serve perché il loader glTF di raylib le texture le carica e basta: un solo livello
    /// di mipmap, e in quel caso rlgl imposta il filtro a <c>NEAREST</c>. Su un atlas di
    /// personaggio si vede eccome — blocchettoso da vicino e con lo sfarfallio tipico
    /// dell'aliasing appena la mesh si muove.
    ///
    /// Il posto giusto è qui e non nel renderer: è l'unico punto in cui il modello passa
    /// dalle nostre mani con le texture già caricate sulla GPU, e va fatto una volta per
    /// modello — non una per frame.
    /// </summary>
    private static unsafe void GenerateAlbedoMipmaps(Model model)
    {
        // ⚠️ La texture di default di raylib (1x1 bianca) è CONDIVISA: raylib la assegna ai
        // material glTF privi di baseColorTexture, ma la usa anche per disegnare le shape
        // 2D. Generarci sopra le mipmap e cambiarle il filtro toccherebbe anche quelle, da
        // sotto — quindi la si salta.
        var defaultTextureId = Rlgl.GetTextureIdDefault();

        for (var i = 0; i < model.MaterialCount; i++)
        {
            var map = &model.Materials[i].Maps[(int)MaterialMapIndex.Albedo];
            if (map->Texture.Id == 0 || map->Texture.Id == defaultTextureId)
                continue;

            // In quest'ordine: il trilineare interpola TRA livelli di mipmap, e senza
            // livelli raylib lo rifiuta con un warning lasciando il filtro com'era.
            Raylib.GenTextureMipmaps(&map->Texture);
            Raylib.SetTextureFilter(map->Texture, TextureFilter.Trilinear);
        }
    }

    /// <summary>
    /// Risolve un <see cref="ModelHandle"/> nella <c>Model</c> nativa di raylib. È il
    /// ponte tra i due adapter raylib: lo usa <c>RayLibRenderer</c> per disegnare. Il
    /// tipo di ritorno è raylib, quindi questo metodo NON fa parte del port
    /// <see cref="IAssetBackend"/> (che deve restare lib-independent) — è specifico
    /// dell'adapter e visibile solo lato raylib.
    /// </summary>
    public bool TryGetModel(ModelHandle handle, out Model model)
    {
        return _models.TryGetValue(handle.Id, out model);
    }

    public void PlaySound(SoundHandle handle)
    {
        if (_sounds.TryGetValue(handle.Id, out var sound))
            Raylib.PlaySound(sound);
    }

    public void PlayMusic(MusicHandle handle)
    {
        if (_musics.TryGetValue(handle.Id, out var music))
            Raylib.PlayMusicStream(music);
    }

    public void UpdateMusic(MusicHandle handle)
    {
        if (_musics.TryGetValue(handle.Id, out var music))
            Raylib.UpdateMusicStream(music);
    }

    public void StopMusic(MusicHandle handle)
    {
        if (_musics.TryGetValue(handle.Id, out var music))
            Raylib.StopMusicStream(music);
    }

    public void UnloadAll()
    {
        foreach (var texture in _textures.Values)
            Raylib.UnloadTexture(texture);

        foreach (var sound in _sounds.Values)
            Raylib.UnloadSound(sound);

        foreach (var music in _musics.Values)
            Raylib.UnloadMusicStream(music);

        foreach (var model in _models.Values)
            Raylib.UnloadModel(model);

        _textures.Clear();
        _sounds.Clear();
        _musics.Clear();
        _models.Clear();
    }
}
