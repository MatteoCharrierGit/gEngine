using gEngine.Assets;

namespace gEngine.Editor;

/// <summary>
/// Le anteprime delle immagini per il pannello File system: caricate <b>pigramente</b>, poche
/// per frame, e buttate quando si cambia cartella.
///
/// Ognuna delle tre cose difende da un modo diverso di rovinare l'editor, e nessuna è
/// ipotetica — la cartella <c>SummonersRift/Textures</c> di questo repo ha <b>405</b> file
/// <c>.dds</c>:
/// <list type="bullet">
///   <item><b>pigre</b>: si chiede l'anteprima solo di ciò che si sta guardando. Aprire una
///   cartella non deve caricarne il contenuto — è la stessa regola per cui il payload del
///   drag&amp;drop porta un path e non un handle;</item>
///   <item><b>poche per frame</b>: caricare 405 immagini nello stesso frame è un blocco di
///   secondi in cui la finestra non risponde. Distribuite, la griglia si riempie a vista
///   d'occhio e il pannello resta vivo;</item>
///   <item><b>buttate uscendo</b>: sono texture GPU. Tenerle tutte vorrebbe dire che sfogliare
///   il progetto costa memoria video che non torna più indietro.</item>
/// </list>
///
/// ⚠️ Anche i <b>fallimenti</b> stanno in cache. Senza, un file che non è un'immagine (o è
/// corrotto) verrebbe riletto dal disco a ogni frame, per sempre: il costo peggiore proprio nel
/// caso che non dà niente in cambio.
/// </summary>
public sealed class ThumbnailCache(AssetManager assets)
{
    /// <summary>
    /// Il lato lungo in pixel. Più grande della griglia più grande, così ingrandire non
    /// significa ricaricare tutto — e comunque una miniatura non è la texture del gioco.
    /// </summary>
    private const int Resolution = 128;

    /// <summary>
    /// Quante se ne caricano per frame. Due, non una: una sola riempie una griglia di
    /// cinquanta riquadri in quasi due secondi, e si vede scendere. Non venti: una .dds 4K
    /// costa parecchio a decomprimere, e venti insieme sono di nuovo uno scatto.
    /// </summary>
    private const int PerFrame = 2;

    private readonly Dictionary<string, TextureHandle> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private int _loadedThisFrame;

    /// <summary>Da chiamare una volta per frame, prima di disegnare: riapre il budget.</summary>
    public void BeginFrame() => _loadedThisFrame = 0;

    /// <summary>
    /// L'id GPU dell'anteprima, oppure 0 se non c'è (ancora, o mai).
    ///
    /// ⚠️ Lo 0 significa <b>due cose diverse</b> e il chiamante non deve distinguerle: "non
    /// pronta" e "non è un'immagine" si disegnano allo stesso modo — col riquadro del genere.
    /// Un'anteprima che appare un attimo dopo è normale; un riquadro che diventa immagine è
    /// esattamente ciò che deve succedere.
    /// </summary>
    public nint GetOrRequest(string absolutePath)
    {
        if (_byPath.TryGetValue(absolutePath, out var cached))
            return assets.GetTextureId(cached);

        if (_loadedThisFrame >= PerFrame)
            return 0;

        _loadedThisFrame++;

        // TextureHandle.None se il file non è leggibile come immagine: va in cache com'è, ed è
        // il motivo per cui questo dizionario tiene anche i fallimenti.
        var handle = assets.LoadThumbnail(absolutePath, Resolution);
        _byPath[absolutePath] = handle;

        return assets.GetTextureId(handle);
    }

    /// <summary>
    /// Libera tutto. La chiama chi cambia cartella: da lì in poi quelle anteprime non le
    /// guarderà più nessuno.
    /// </summary>
    public void Clear()
    {
        foreach (var handle in _byPath.Values)
            assets.UnloadTexture(handle);

        _byPath.Clear();
    }
}
