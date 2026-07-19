using System.Runtime.Versioning;
using Microsoft.VisualBasic.FileIO;

namespace gEngine.Editor.Files;

/// <summary>
/// Il cestino vero, su Windows.
///
/// ⚠️ <b>Sì, è <c>Microsoft.VisualBasic</c>, e non è un errore di copiatura.</b> È l'unico modo
/// documentato di raggiungere il Cestino da .NET senza scrivere P/Invoke a mano: l'alternativa
/// è <c>SHFileOperation</c> di shell32, che vuole una struct marshalata a mano, una stringa a
/// doppio terminatore nullo e un allineamento che su x64 è facile sbagliare <b>in silenzio</b>.
/// <c>Microsoft.VisualBasic.Core</c> è nel framework condiviso (nessun pacchetto da aggiungere)
/// e quella chiamata è esattamente quel P/Invoke, già scritto e già testato da qualcun altro.
/// Il namespace è brutto; il codice che non c'è è meglio del codice che c'è.
///
/// <b>Verificato che finisca davvero nel Cestino</b>, non solo che il file sparisca: il file di
/// prova è stato ritrovato fra gli elementi del Cestino, con la sua cartella d'origine. Le due
/// cose si assomigliano dal lato del chiamante e sono opposte per l'utente.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RecycleBinTrash : IFileTrash
{
    public bool Available => true;

    public string UnavailableReason => string.Empty;

    public FileResult Send(string absolutePath)
    {
        try
        {
            // UIOption.OnlyErrorDialogs: niente finestra di conferma di Windows. La conferma la
            // chiede gia' l'editor, con il nome di quel che sta per sparire, e una seconda
            // dialog di sistema sopra la nostra e' solo un secondo clic per la stessa domanda.
            if (Directory.Exists(absolutePath))
                FileSystem.DeleteDirectory(absolutePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else if (File.Exists(absolutePath))
                FileSystem.DeleteFile(absolutePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else
                // Non e' un errore da nascondere: il pannello rilegge il disco a ogni frame, ma
                // fra il clic e questa riga passa un frame e il file puo' essere sparito da
                // fuori. Dirlo e' meglio di un successo silenzioso su un'operazione che non e'
                // avvenuta.
                return FileResult.Fail($"Non esiste piu': {Path.GetFileName(absolutePath)}");

            return FileResult.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return FileResult.Fail(ex.Message);
        }
    }
}
