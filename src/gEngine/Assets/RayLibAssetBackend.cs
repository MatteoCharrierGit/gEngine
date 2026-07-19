using System.Runtime.InteropServices;
using Raylib_cs;
using StbImageSharp;

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

    public unsafe TextureHandle LoadTexture(string absolutePath)
    {
        var image = LoadImageFile(absolutePath);

        if (image.Width <= 0 || image.Height <= 0)
            return TextureHandle.None;

        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);

        var id = _nextId++;
        _textures[id] = texture;
        return new TextureHandle(id);
    }

    /// <summary>
    /// Un'immagine dal disco, con <b>ricaduta gestita</b> sui formati che la native di raylib
    /// non sa decodificare.
    ///
    /// ⚠️ Nasce da un caso vero e vale la pena ricordarlo: <b>questa build di raylib non
    /// decodifica i JPEG</b>. Un .jpg valido (baseline, YCbCr, 8 bit) dà nel log
    /// <c>FILEIO: File loaded successfully</c> seguito da <c>IMAGE: Failed to load image
    /// data</c> — i byte si leggono, il decoder no: il nativo è compilato senza
    /// <c>SUPPORT_FILEFORMAT_JPG</c>. Non era un problema delle anteprime: una texture jpg non
    /// si caricava <b>affatto</b>, e un modello che la referenzia veniva disegnato bianco
    /// (albedo = la texture di default 1x1).
    ///
    /// Il decoder di ricambio è <c>StbImageSharp</c>, che è la porta in C# dello <b>stesso</b>
    /// <c>stb_image</c> che raylib usa dentro: non si sta aggiungendo un secondo modo di
    /// leggere le immagini, si sta riaccendendo un pezzo che nel nativo è spento.
    ///
    /// Si prova <b>prima</b> raylib e si ricade solo se fallisce, invece di dirottare le
    /// estensioni note: così i formati che il nativo gestisce (comprese le .dds compresse, che
    /// stb non tocca) continuano a passare dalla strada veloce, e il giorno che la native
    /// avrà il JPEG questa ricaduta smetterà da sé di essere usata.
    /// </summary>
    private static unsafe Image LoadImageFile(string absolutePath)
    {
        var image = Raylib.LoadImage(absolutePath);

        if (image.Width > 0 && image.Height > 0)
            return image;

        // Niente UnloadImage sull'immagine fallita: raylib non ha allocato niente da liberare.
        return DecodeManaged(absolutePath);
    }

    /// <summary>
    /// Decodifica in C# e consegna i pixel a raylib in una <c>Image</c> costruita a mano.
    ///
    /// ⚠️ La memoria si chiede a <c>Raylib.MemAlloc</c> e <b>non</b> a <c>Marshal</c>: quella
    /// <c>Image</c> finisce in mano a raylib, e <c>UnloadImage</c> la libera col <i>suo</i>
    /// allocatore. Mescolare i due è il genere di errore che non si vede subito e poi corrompe
    /// l'heap in un punto che non c'entra niente.
    /// </summary>
    private static unsafe Image DecodeManaged(string absolutePath)
    {
        ImageResult decoded;

        try
        {
            using var stream = File.OpenRead(absolutePath);
            decoded = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }
        catch (Exception)
        {
            // Qui sotto ci sono i file dell'utente: qualunque cosa può non essere un'immagine,
            // e chi chiama sa già leggere un'Image vuota (Width 0). Un'eccezione da qui
            // salirebbe fino al frame di disegno del pannello che stava solo sfogliando.
            return default;
        }

        if (decoded.Data is not { Length: > 0 })
            return default;

        var buffer = Raylib.MemAlloc((uint)decoded.Data.Length);
        Marshal.Copy(decoded.Data, 0, (nint)buffer, decoded.Data.Length);

        return new Image
        {
            Data = buffer,
            Width = decoded.Width,
            Height = decoded.Height,
            Mipmaps = 1,
            Format = PixelFormat.UncompressedR8G8B8A8 // ColorComponents.RedGreenBlueAlpha
        };
    }

    /// <summary>
    /// Immagine sul processore, ridotta, e solo dopo sulla GPU.
    ///
    /// ⚠️ L'ordine è tutto il punto: <c>LoadTexture</c> + disegno piccolo caricherebbe comunque
    /// l'immagine intera in memoria video. Sfogliando una cartella di 400 texture 4K sarebbero
    /// gigabyte per disegnare dei francobolli.
    /// </summary>
    public unsafe TextureHandle LoadTextureThumbnail(string absolutePath, int maxSize)
    {
        // Stessa ricaduta di LoadTexture: un'anteprima di un .jpg deve funzionare come il .jpg.
        var image = LoadImageFile(absolutePath);

        // ⚠️ Né raylib né il decoder di ricambio lanciano su un file illeggibile: tornano
        // un'Image con dati nulli, e caricarla darebbe una texture invalida che poi ImGui
        // disegnerebbe come sporcizia. Qui sotto ci sono i file dell'utente: qualunque cosa
        // può non essere un'immagine.
        if (image.Width <= 0 || image.Height <= 0)
            return TextureHandle.None;

        var longest = Math.Max(image.Width, image.Height);
        if (longest > maxSize)
        {
            var scale = (float)maxSize / longest;

            // Almeno 1 pixel per lato: un'immagine molto allungata arrotonderebbe a zero, e
            // una texture 0-larga è invalida quanto un file corrotto.
            Raylib.ImageResize(&image,
                Math.Max(1, (int)(image.Width * scale)),
                Math.Max(1, (int)(image.Height * scale)));
        }

        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image); // la copia sulla GPU c'è già: questa è memoria di sistema

        var id = _nextId++;
        _textures[id] = texture;
        return new TextureHandle(id);
    }

    public void UnloadTexture(TextureHandle handle)
    {
        if (!_textures.Remove(handle.Id, out var texture))
            return;

        Raylib.UnloadTexture(texture);
    }

    public nint GetTextureId(TextureHandle handle)
    {
        return _textures.TryGetValue(handle.Id, out var texture) ? (nint)texture.Id : 0;
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

        RepairFailedAlbedo(model, absolutePath);
        GenerateAlbedoMipmaps(model);

        var id = _nextId++;
        _models[id] = model;
        return new ModelHandle(id);
    }

    /// <summary>
    /// Ricarica le albedo che raylib <b>non è riuscito</b> a caricare, decodificandole noi.
    ///
    /// ⚠️ Serve perché la ricaduta di <see cref="LoadImageFile"/> <b>non basta</b>: le texture
    /// di un modello non passano da noi: le apre il loader di raylib mentre legge il
    /// <c>.mtl</c>. Quindi un modello con albedo in <c>.jpg</c> restava bianco anche con il
    /// decoder di ricambio in casa — il caso reale da cui è nato tutto questo.
    ///
    /// Come si riconosce una texture fallita: raylib assegna al material la propria texture di
    /// <b>default</b> (1x1 bianca) quando non riesce a caricarne una. È lo stesso confronto che
    /// <see cref="GenerateAlbedoMipmaps"/> fa già per un altro motivo.
    ///
    /// ⚠️ <b>Solo OBJ</b>, e per una ragione onesta: il path della texture va ripescato dal
    /// <c>.mtl</c>, che è un formato di testo di due righe. Per glTF servirebbe leggere il json
    /// (o il chunk binario di un .glb) e seguire l'indirezione material→texture→image: è un
    /// parser, non una riparazione. Se un glTF avrà albedo jpg resterà bianco, e questo commento
    /// è il posto in cui è scritto perché.
    ///
    /// ⚠️ Si patcha solo se il numero di material combacia con quello dei <c>newmtl</c>: si sta
    /// assumendo che raylib crei i material <b>nell'ordine del file</b>, e se i conti non
    /// tornano l'assunzione non regge — meglio un modello bianco che uno con le texture
    /// scambiate, che sembrerebbe un errore d'autore.
    /// </summary>
    private static unsafe void RepairFailedAlbedo(Model model, string modelPath)
    {
        if (!string.Equals(Path.GetExtension(modelPath), ".obj", StringComparison.OrdinalIgnoreCase))
            return;

        var defaultTextureId = Rlgl.GetTextureIdDefault();
        var broken = new List<int>();

        for (var i = 0; i < model.MaterialCount; i++)
        {
            if (model.Materials[i].Maps[(int)MaterialMapIndex.Albedo].Texture.Id == defaultTextureId)
                broken.Add(i);
        }

        if (broken.Count == 0)
            return;

        var maps = ReadDiffuseMaps(modelPath);

        if (maps.Count != model.MaterialCount)
            return;

        var directory = Path.GetDirectoryName(modelPath) ?? string.Empty;

        foreach (var index in broken)
        {
            if (maps[index] is not { Length: > 0 } relative)
                continue;

            var full = Path.GetFullPath(Path.Combine(directory, relative));

            if (!File.Exists(full))
                continue;

            var image = LoadImageFile(full);

            if (image.Width <= 0 || image.Height <= 0)
                continue;

            var texture = Raylib.LoadTextureFromImage(image);
            Raylib.UnloadImage(image);

            model.Materials[index].Maps[(int)MaterialMapIndex.Albedo].Texture = texture;
        }
    }

    /// <summary>
    /// I <c>map_Kd</c> del <c>.mtl</c> accanto al modello, <b>uno per material</b> e
    /// nell'ordine dei <c>newmtl</c>. Null dove un material non dichiara una diffuse.
    ///
    /// Parsing minimo e volutamente stupido: qui non si vuole leggere un .mtl, si vuole sapere
    /// quale file una texture avrebbe dovuto essere. ⚠️ L'ultimo token della riga, perché le
    /// opzioni stanno in mezzo (<c>map_Kd -bm 0.05 texture.jpg</c>).
    /// </summary>
    private static List<string?> ReadDiffuseMaps(string objPath)
    {
        var maps = new List<string?>();
        var mtlPath = Path.ChangeExtension(objPath, ".mtl");

        if (!File.Exists(mtlPath))
            return maps;

        try
        {
            foreach (var raw in File.ReadLines(mtlPath))
            {
                var line = raw.Trim();

                if (line.StartsWith("newmtl", StringComparison.OrdinalIgnoreCase))
                    maps.Add(null);
                else if (line.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase) && maps.Count > 0)
                    maps[^1] = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Replace('\\', '/');
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Il .mtl è un di più: senza, il modello resta com'era. Non è un motivo per non
            // caricarlo.
            return [];
        }

        return maps;
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
