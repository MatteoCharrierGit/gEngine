using System.Runtime.InteropServices;
using gEngine.Ecs.Base;
using ImGuiNET;

namespace gEngine.Editor;

/// <summary>
/// Il trascinamento di un'<b>entità</b> dentro l'albero della Hierarchy, che è il canale con
/// cui si riparenta.
///
/// Stessa meccanica di <see cref="AssetDragDrop"/> con un payload diverso — un
/// <see cref="Entity"/> invece di un path — e per lo stesso motivo: il nome del payload <b>è</b>
/// il tipo, quindi un modello trascinato dal File system non illumina una riga dell'albero e
/// un'entità non illumina lo slot di un asset. Nessuno dei due ha bisogno di un ramo "se il
/// tipo è sbagliato": il tipo sbagliato non arriva.
///
/// ⚠️ Il payload porta l'<b>id</b> e non il nome: qui non si attraversa un salvataggio (il
/// trascinamento inizia e finisce nella stessa sessione, spesso nello stesso frame), quindi
/// vale l'identità vera e non quella che sopravvive al file. Un'entità distrutta fra il
/// rilascio e l'applicazione del comando è comunque gestita a valle, con <c>World.Exists</c>.
/// </summary>
public static class EntityDragDrop
{
    /// <summary>
    /// ⚠️ ImGui tiene questo nome in un <c>char[32+1]</c> e <b>asserisce</b> se è più lungo
    /// (vedi la stessa nota in <see cref="AssetDragDrop"/>).
    /// </summary>
    private const string PayloadId = "gEngine.Entity";

    /// <summary>
    /// Rende l'ultimo item disegnato una sorgente di trascinamento per questa entità.
    /// <paramref name="label"/> è solo l'anteprima che segue il puntatore: senza, si trascina
    /// un rettangolo vuoto e in un albero profondo non si sa più cosa si ha in mano.
    /// </summary>
    public static void Source(Entity entity, string label)
    {
        if (!ImGui.BeginDragDropSource())
            return;

        // SetDragDropPayload fa una memcpy nel proprio buffer prima di tornare, quindi questi
        // quattro byte servono solo per la durata della chiamata.
        var buffer = Marshal.AllocCoTaskMem(sizeof(int));

        try
        {
            Marshal.WriteInt32(buffer, entity.Id);
            ImGui.SetDragDropPayload(PayloadId, buffer, sizeof(int));
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }

        ImGui.TextUnformatted(label);

        ImGui.EndDragDropSource();
    }

    /// <summary>
    /// L'entità che si sta trascinando <b>in questo momento</b>, senza accettare niente e
    /// senza essere un bersaglio.
    ///
    /// Serve a decidere <b>se</b> rendere una riga un bersaglio: un riparentamento illegale
    /// (su sé stessi, dentro un proprio discendente) non deve nemmeno illuminarsi. È la stessa
    /// idea del payload tipizzato portata un passo più in là — lì è ImGui a non accoppiare due
    /// tipi diversi, qui siamo noi a non offrire un bersaglio che rifiuteremmo.
    /// </summary>
    public static unsafe bool TryPeek(out Entity entity)
    {
        entity = default;

        var payload = ImGui.GetDragDropPayload();

        if (payload.NativePtr is null || !payload.IsDataType(PayloadId) ||
            payload.Data == IntPtr.Zero || payload.DataSize < sizeof(int))
            return false;

        entity = new Entity(Marshal.ReadInt32(payload.Data));
        return true;
    }

    /// <summary>
    /// Rende l'ultimo item disegnato un bersaglio per un'entità trascinata.
    /// </summary>
    /// <returns>true (una volta sola, nel frame del rilascio) se è stata lasciata cadere
    /// un'entità.</returns>
    public static bool Target(out Entity entity)
    {
        entity = default;

        if (!ImGui.BeginDragDropTarget())
            return false;

        var accepted = TryRead(out entity);

        ImGui.EndDragDropTarget();
        return accepted;
    }

    /// <summary>
    /// ⚠️ <c>unsafe</c> per lo stesso motivo di <see cref="AssetDragDrop"/>:
    /// <c>AcceptDragDropPayload</c> torna un wrapper attorno a un puntatore che è <b>null</b>
    /// quando non è stato rilasciato niente, e leggerne una proprietà prima di controllarlo
    /// dereferenzia il null.
    /// </summary>
    private static unsafe bool TryRead(out Entity entity)
    {
        entity = default;

        var payload = ImGui.AcceptDragDropPayload(PayloadId);

        if (payload.NativePtr is null || payload.Data == IntPtr.Zero || payload.DataSize < sizeof(int))
            return false;

        entity = new Entity(Marshal.ReadInt32(payload.Data));
        return true;
    }
}
