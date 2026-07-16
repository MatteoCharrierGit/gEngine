namespace gEngine.Assets;

/// <summary>
/// Il genere di un asset: quale delle <c>Load*</c> dell'<see cref="AssetManager"/> lo carica
/// e, di conseguenza, quale handle ne esce.
///
/// Non è una tassonomia dei file: è l'elenco delle cose che l'engine sa caricare, ed è per
/// questo che ha esattamente quattro voci — una per famiglia di handle. Serve a chi deve
/// parlare di asset <b>senza averne uno in mano</b>: un campo di un componente che dichiara
/// "qui ci va un modello" (<see cref="Ecs.Component.EditorAssetAttribute"/>) lo fa molto
/// prima che un modello esista.
///
/// ⚠️ Sound e Music sono separati perché lo sono nell'AssetManager (<c>LoadSound</c> vs
/// <c>LoadMusicStream</c>): stesso file .mp3, due handle diversi e due usi diversi (un
/// effetto tutto in memoria contro uno stream). Dedurre l'uno dall'altro dall'estensione
/// non si può — vedi <c>AssetDragDrop.Classify</c>, che infatti non ci prova.
/// </summary>
public enum AssetKind
{
    Model,
    Texture,
    Sound,
    Music,
}
