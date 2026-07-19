namespace gEngine.Editor.Files;

/// <summary>
/// Port verso il cestino del sistema operativo: dove finisce un file che l'editor elimina.
///
/// È una porta e non una chiamata diretta per lo stesso motivo di <c>IRenderer</c>,
/// <c>IAssetBackend</c> e <c>IPhysicsWorld</c> — il cestino è un servizio della piattaforma —
/// ma qui c'è una ragione in più, ed è la ragione per cui questa interfaccia esiste prima del
/// bottone che la usa: <b>l'undo dell'editor copre il World, non il disco</b>. Un comando in
/// memoria non resuscita un file. Il cestino è l'unica rete sotto "elimina", quindi va
/// dichiarato come dipendenza esplicita invece di essere una riga dentro il pannello.
///
/// ⚠️ <b>Chi non ha un cestino non elimina.</b> L'implementazione per una piattaforma senza
/// cestino non è "allora cancella e basta": è <see cref="Available"/> a false. Cancellare
/// davvero sarebbe l'unica operazione irreversibile di tutto l'editor, offerta proprio dove la
/// rete manca. Meglio un bottone spento col motivo scritto.
/// </summary>
public interface IFileTrash
{
    /// <summary>
    /// Se su questa piattaforma il cestino c'è. Chi disegna il bottone "Elimina" lo interroga
    /// <b>prima</b>: un'azione distruttiva non deve essere offerta e poi rifiutata.
    /// </summary>
    bool Available { get; }

    /// <summary>Perché non è disponibile, da mostrare all'utente. Vuoto se lo è.</summary>
    string UnavailableReason { get; }

    /// <summary>
    /// Manda al cestino un file o una cartella (con tutto il suo contenuto).
    ///
    /// Non lancia: chi elimina sta guardando un disco che può negare l'accesso, avere il file
    /// aperto in un altro programma o essere sparito da sotto fra il clic e la chiamata. Un
    /// errore qui è un caso normale da mostrare, non un'eccezione da propagare dentro un frame
    /// di disegno.
    /// </summary>
    /// <returns>L'esito, con il messaggio da mostrare se è andata male.</returns>
    FileResult Send(string absolutePath);
}
