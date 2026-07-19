namespace gEngine.Editor.Files;

/// <summary>
/// Le mutazioni su disco che il pannello File system può fare: creare una cartella,
/// rinominare, eliminare.
///
/// Stanno qui e non dentro il pannello per lo stesso motivo per cui
/// <c>EntityOperations</c> non sta nella Hierarchy: sono <b>politiche</b>, e sono la parte
/// verificabile. Il pannello disegna e chiede conferma; cosa sia un nome legale e cosa sia
/// "dentro la cartella asset" si decide qui, dove un test può interrogarlo senza aprire una
/// finestra.
///
/// ⚠️⚠️ <b>La regola che tiene su tutto: niente esce dalla radice.</b> Il pannello dice
/// "assets" e deve mantenere la promessa. Un nome come <c>..\..\Windows</c> non è un caso di
/// scuola — è quello che succede se si costruisce un percorso concatenando stringhe e si passa
/// il risultato a <c>Directory.Delete</c>. Ogni operazione qui dentro ricontrolla il percorso
/// <b>risolto</b>, non quello scritto: <c>Path.GetFullPath</c> normalizza i <c>..</c>, e il
/// confronto avviene dopo.
/// </summary>
public static class AssetFiles
{
    /// <summary>
    /// Crea una cartella dentro <paramref name="parent"/>.
    /// </summary>
    /// <param name="createdPath">Il percorso creato, per chi vuole entrarci subito.</param>
    public static FileResult CreateFolder(string root, string parent, string name, out string createdPath)
    {
        createdPath = string.Empty;

        if (ValidateName(name) is { Ok: false } invalid)
            return invalid;

        if (!IsInside(root, parent))
            return FileResult.Fail("La cartella di destinazione e' fuori dalla cartella asset.");

        var target = Path.Combine(parent, name.Trim());

        if (!IsInside(root, target))
            return FileResult.Fail("Quel nome porterebbe fuori dalla cartella asset.");

        if (Directory.Exists(target) || File.Exists(target))
            return FileResult.Fail($"Esiste gia': {name.Trim()}");

        try
        {
            Directory.CreateDirectory(target);
            createdPath = target;
            return FileResult.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Rinomina un file o una cartella, lasciandolo dov'è.
    ///
    /// ⚠️ Rinominare un asset <b>rompe i riferimenti nelle scene</b>: un <c>ModelPath</c> nel
    /// file di scena è il percorso relativo, e nessuno lo aggiorna. È un limite dichiarato nel
    /// tooltip, non un bug nascosto — sistemarlo vorrebbe dire riscrivere le scene che citano
    /// il file, cioè conoscere tutte le scene, che l'editor oggi non fa (ne tiene aperta una).
    /// </summary>
    public static FileResult Rename(string root, string path, string newName)
    {
        if (ValidateName(newName) is { Ok: false } invalid)
            return invalid;

        if (!IsInside(root, path))
            return FileResult.Fail("Quel percorso e' fuori dalla cartella asset.");

        if (IsRoot(root, path))
            return FileResult.Fail("La cartella asset non si rinomina da qui.");

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
            return FileResult.Fail("Percorso senza cartella padre.");

        var target = Path.Combine(directory, newName.Trim());

        if (!IsInside(root, target))
            return FileResult.Fail("Quel nome porterebbe fuori dalla cartella asset.");

        // ⚠️ Confronto ordinale e non "sono lo stesso file": su Windows il file system e'
        // insensibile alle maiuscole, quindi rinominare "Texture.png" in "texture.png" e'
        // un'operazione legittima verso un percorso che "esiste gia'". Bloccarla con un
        // controllo di esistenza insensibile al caso vieterebbe proprio il cambio di sole
        // maiuscole, che e' fra i motivi piu' comuni per rinominare.
        var isCaseOnlyChange = string.Equals(Path.GetFullPath(path), Path.GetFullPath(target),
            StringComparison.OrdinalIgnoreCase);

        if (!isCaseOnlyChange && (Directory.Exists(target) || File.Exists(target)))
            return FileResult.Fail($"Esiste gia': {newName.Trim()}");

        try
        {
            if (Directory.Exists(path))
                Directory.Move(path, target);
            else if (File.Exists(path))
                File.Move(path, target, overwrite: false);
            else
                return FileResult.Fail($"Non esiste piu': {Path.GetFileName(path)}");

            return FileResult.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Manda al cestino un file o una cartella.
    ///
    /// ⚠️ Passa <b>sempre</b> dal <see cref="IFileTrash"/> e non tocca mai <c>File.Delete</c>:
    /// vedi <see cref="FileTrash.ForCurrentPlatform"/> per il perché non esiste una ricaduta
    /// che cancella davvero.
    /// </summary>
    public static FileResult Delete(string root, string path, IFileTrash trash)
    {
        if (!trash.Available)
            return FileResult.Fail(trash.UnavailableReason);

        if (!IsInside(root, path))
            return FileResult.Fail("Quel percorso e' fuori dalla cartella asset.");

        // ⚠️ La radice si escluderebbe da sola con un IsInside stretto, ma qui il controllo e'
        // esplicito perche' l'errore sarebbe catastrofico e silenzioso: mandare al cestino
        // l'intera cartella asset del progetto.
        if (IsRoot(root, path))
            return FileResult.Fail("La cartella asset non si elimina da qui.");

        return trash.Send(path);
    }

    /// <summary>
    /// Se un nome è utilizzabile come nome di file o cartella.
    ///
    /// ⚠️ I separatori sono vietati <b>esplicitamente</b> anche se sarebbero già fra gli
    /// <c>InvalidFileNameChars</c>: qui il punto non è che il nome sia strano, è che
    /// <c>Path.Combine(parent, "..\\x")</c> risolve fuori. Il controllo sulla radice a valle è
    /// la rete; questa è la porta.
    /// </summary>
    private static FileResult ValidateName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
            return FileResult.Fail("Il nome non puo' essere vuoto.");

        if (trimmed is "." or "..")
            return FileResult.Fail("Nome non valido.");

        if (trimmed.Contains(Path.DirectorySeparatorChar) ||
            trimmed.Contains(Path.AltDirectorySeparatorChar) ||
            trimmed.Contains(':'))
            return FileResult.Fail("Il nome non puo' contenere separatori di percorso.");

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return FileResult.Fail("Il nome contiene caratteri non ammessi dal file system.");

        return FileResult.Success;
    }

    /// <summary>
    /// Se <paramref name="path"/> sta dentro <paramref name="root"/> (o <b>è</b> la radice).
    ///
    /// ⚠️ Il separatore finale non è cosmesi: senza, <c>assets2</c> risulterebbe "dentro"
    /// <c>assets</c> per semplice prefisso di stringa. È il classico modo in cui un controllo
    /// del genere sembra funzionare e non funziona.
    /// </summary>
    public static bool IsInside(string root, string path)
    {
        string fullRoot, fullPath;

        try
        {
            fullRoot = Path.GetFullPath(root);
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Un percorso che non si riesce nemmeno a normalizzare non e' "fuori": e' peggio.
            // Trattarlo come fuori e' la risposta sicura.
            return false;
        }

        if (string.Equals(fullRoot, fullPath, PathComparison))
            return true;

        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(prefix, PathComparison);
    }

    private static bool IsRoot(string root, string path)
    {
        try
        {
            return string.Equals(Path.GetFullPath(root), Path.GetFullPath(path), PathComparison);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// ⚠️ Insensibile alle maiuscole <b>solo su Windows</b>. Su Linux <c>Assets/</c> e
    /// <c>assets/</c> sono due cartelle diverse, e confrontarle senza distinguere direbbe
    /// "dentro la radice" a un percorso che sta altrove.
    /// </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
