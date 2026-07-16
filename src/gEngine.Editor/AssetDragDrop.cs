using System.Runtime.InteropServices;
using System.Text;
using gEngine.Assets;
using ImGuiNET;

namespace gEngine.Editor;

/// <summary>
/// Il trascinamento di un asset dal pannello File system a uno slot dell'Inspector, e la sua
/// <b>validazione di tipo</b>.
///
/// Il punto è che la validazione non è un controllo che facciamo noi dopo il drop: è il
/// <b>payload stesso</b>. ImGui accoppia sorgente e bersaglio confrontando la stringa che
/// identifica il payload, quindi un <c>.mp3</c> — che parte come payload "Music" — sopra lo
/// slot Model di un MeshRenderer non si illumina nemmeno, e il drop non può avvenire. Non
/// c'è un ramo "se il tipo è sbagliato mostra un errore" perché non c'è un momento in cui il
/// tipo sbagliato arriva: è la differenza fra validare e rendere irrappresentabile.
///
/// ⚠️ ImGui tiene il nome del payload in un <c>char[32+1]</c> e <b>asserisce</b> se è più
/// lungo. Da qui il prefisso corto: "gEngine.Asset.Texture" è la voce peggiore, 21 caratteri.
/// </summary>
public static class AssetDragDrop
{
    /// <summary>
    /// Il genere di un file dedotto dall'<b>estensione</b>, o null se non è un asset che
    /// sappiamo caricare.
    ///
    /// ⚠️ È un'euristica per il browser, non una verità: l'estensione dice cosa un file
    /// dichiara di essere, non cosa è. Un .gltf corrotto parte lo stesso come Model e sarà
    /// raylib a lamentarsi al caricamento. Va bene così — l'alternativa sarebbe aprire ogni
    /// file della cartella a ogni frame per decidere se si può trascinare.
    ///
    /// ⚠️ Gli audio si fermano a <see cref="AssetKind.Music"/> e non è una scelta fra i due:
    /// lo stesso .mp3 è un Sound o una Music a seconda di <b>come lo si usa</b> (effetto in
    /// memoria contro stream), e l'estensione non lo sa. Music perché è ciò a cui serve un
    /// file lungo su disco; il giorno che uno slot Sound esisterà, questa riga sarà il posto
    /// in cui la scelta va rifatta — non un dettaglio da scoprire altrove.
    ///
    /// L'elenco delle estensioni è quello che carica raylib (il backend di oggi). Vive qui e
    /// non nell'<c>AssetManager</c>, che è indipendente dalla libreria: sapere che un .qoa si
    /// carica è una cosa che sa il backend, non l'astrazione.
    /// </summary>
    public static AssetKind? Classify(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".gltf" or ".glb" or ".obj" or ".iqm" or ".vox" or ".m3d" => AssetKind.Model,
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" or ".gif" or ".hdr" or ".dds" => AssetKind.Texture,
            ".wav" or ".mp3" or ".ogg" or ".flac" or ".qoa" or ".xm" or ".mod" => AssetKind.Music,
            _ => null
        };
    }

    /// <summary>
    /// Rende l'ultimo item disegnato una sorgente di trascinamento per questo asset.
    ///
    /// <paramref name="relativePath"/> è il path <b>relativo alla cartella asset</b>, che è
    /// la forma in cui l'AssetManager li vuole e in cui il file di scena li scrive: l'unica
    /// che sopravvive al cambio di macchina. Il payload non porta l'handle perché a questo
    /// punto il modello non è ancora caricato — e non deve esserlo, o sfogliare una cartella
    /// significherebbe caricarne il contenuto.
    /// </summary>
    public static void Source(AssetKind kind, string relativePath)
    {
        if (!ImGui.BeginDragDropSource())
            return;

        // Il payload è UTF-8 nudo, senza terminatore: la taglia ce la riporta ImGui e la
        // usiamo per rileggerlo esatto. Marshal e non un puntatore fisso perché non serve —
        // SetDragDropPayload fa una memcpy nel proprio buffer prima di tornare, quindi la
        // memoria qui sotto è già inutile alla riga dopo.
        var bytes = Encoding.UTF8.GetByteCount(relativePath);
        var buffer = Marshal.StringToCoTaskMemUTF8(relativePath);

        try
        {
            ImGui.SetDragDropPayload(PayloadId(kind), buffer, (uint)bytes);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }

        // L'anteprima che segue il puntatore. Senza, si trascina un rettangolo vuoto e non si
        // sa cosa si ha in mano quando le cartelle sono profonde.
        ImGui.TextUnformatted(Path.GetFileName(relativePath));

        ImGui.EndDragDropSource();
    }

    /// <summary>
    /// Rende l'ultimo item disegnato un bersaglio che accetta <b>solo</b> asset di questo
    /// genere.
    /// </summary>
    /// <returns>true (una volta sola, nel frame del rilascio) se è stato lasciato cadere
    /// qualcosa; <paramref name="relativePath"/> è il path trascinato.</returns>
    public static bool Target(AssetKind kind, out string relativePath)
    {
        relativePath = string.Empty;

        if (!ImGui.BeginDragDropTarget())
            return false;

        var accepted = TryRead(kind, out relativePath);

        ImGui.EndDragDropTarget();
        return accepted;
    }

    /// <summary>
    /// ⚠️ <c>unsafe</c>, e non c'è modo di evitarlo: <c>AcceptDragDropPayload</c> torna un
    /// wrapper attorno a un <c>ImGuiPayload*</c> che è <b>null</b> quando non è stato
    /// rilasciato niente, e leggere qualunque proprietà di quel wrapper prima di aver
    /// controllato il puntatore dereferenzia il null. Il confronto con null di un puntatore
    /// C è l'unico controllo possibile, e in C# vuole questo blocco.
    /// </summary>
    private static unsafe bool TryRead(AssetKind kind, out string relativePath)
    {
        relativePath = string.Empty;

        var payload = ImGui.AcceptDragDropPayload(PayloadId(kind));

        if (payload.NativePtr is null || payload.Data == IntPtr.Zero || payload.DataSize <= 0)
            return false;

        var bytes = new byte[payload.DataSize];
        Marshal.Copy(payload.Data, bytes, 0, payload.DataSize);

        relativePath = Encoding.UTF8.GetString(bytes);
        return true;
    }

    /// <summary>
    /// Il nome del payload, che <b>è</b> il tipo: due generi diversi danno due stringhe
    /// diverse, e ImGui non le accoppia. Il prefisso evita di collidere con i payload di
    /// qualunque altra cosa disegni ImGui nella stessa finestra.
    /// </summary>
    private static string PayloadId(AssetKind kind) => $"gEngine.Asset.{kind}";
}
