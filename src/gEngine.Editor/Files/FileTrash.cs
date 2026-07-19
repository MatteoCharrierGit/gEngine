namespace gEngine.Editor.Files;

/// <summary>Da dove si prende il cestino giusto per la piattaforma su cui si sta girando.</summary>
public static class FileTrash
{
    /// <summary>
    /// Il cestino di questo sistema, o uno che <b>rifiuta</b> se qui non ce n'è uno.
    ///
    /// ⚠️ Il ramo di ricaduta non cancella. Sembra ovvio scritto così, ed è proprio la riga che
    /// in un pomeriggio di fretta diventa <c>File.Delete</c> "tanto è lo stesso": non lo è.
    /// Senza cestino, "elimina" sarebbe l'unica operazione irreversibile dell'editor — e
    /// l'undo, che copre tutto il resto, sul disco non arriva. Vedi <see cref="IFileTrash"/>.
    /// </summary>
    public static IFileTrash ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new RecycleBinTrash() : new UnavailableTrash();

    /// <summary>
    /// Il cestino che non c'è: dice di no, e dice perché. Non è un doppio da test — è il
    /// comportamento vero su una piattaforma senza cestino, ed è per questo che sta accanto
    /// all'implementazione vera e non fra i test.
    /// </summary>
    private sealed class UnavailableTrash : IFileTrash
    {
        public bool Available => false;

        public string UnavailableReason =>
            "Su questo sistema l'editor non sa raggiungere un cestino, quindi non elimina: " +
            "cancellare davvero sarebbe irreversibile e l'annulla copre il World, non il disco. " +
            "Elimina il file dal gestore di file del sistema.";

        public FileResult Send(string absolutePath) => FileResult.Fail(UnavailableReason);
    }
}
