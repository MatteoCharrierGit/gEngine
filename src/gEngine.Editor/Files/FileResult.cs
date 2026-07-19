namespace gEngine.Editor.Files;

/// <summary>
/// L'esito di un'operazione su disco: riuscita, oppure il <b>motivo</b> per cui no.
///
/// Un <c>bool</c> non basterebbe e un'eccezione sarebbe peggio. Queste operazioni le innesca
/// un clic dentro un frame di disegno, dove un'eccezione che sale è una finestra che sparisce;
/// e i modi di fallire sono tutti casi normali che l'utente può capire e correggere — il nome
/// è già preso, il file è aperto in un altro programma, la cartella non c'è più. Quindi il
/// motivo è un valore di ritorno, e chi chiama lo mostra.
///
/// ⚠️ Il messaggio finisce sotto ImGui, quindi va scritto in <b>Latin-1</b>: niente lineette
/// lunghe, niente virgolette tipografiche. Il font di default copre solo quello e il resto
/// esce come "?".
/// </summary>
public readonly record struct FileResult(bool Ok, string Error)
{
    public static FileResult Success { get; } = new(true, string.Empty);

    public static FileResult Fail(string error) => new(false, error);
}
