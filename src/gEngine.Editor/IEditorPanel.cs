using gEngine.Ecs.Base;
using gEngine.Rendering;

namespace gEngine.Editor;

/// <summary>
/// Un pannello dell'editor. Viene disegnato una volta per frame, dentro il blocco
/// ImGui aperto da <see cref="EditorHost"/>: l'implementazione apre la propria
/// finestra e basta, non deve gestire begin/end del frame ImGui.
///
/// Identità e visibilità stanno <b>qui</b> e non solo in <see cref="PanelBase"/> perché
/// l'unico che le usa è l'host, e le usa in modo polimorfico: il menu Panels elenca i
/// pannelli senza sapere cosa siano. L'implementazione onesta è però una sola — vedi
/// <see cref="PanelBase"/>, che è ciò che i pannelli estendono davvero.
/// </summary>
public interface IEditorPanel
{
    /// <summary>
    /// Il nome della finestra ImGui <b>e</b> la voce nel menu Panels: una stringa sola.
    ///
    /// ⚠️ Non è pignoleria. ImGui identifica le finestre <b>per titolo</b>: due <c>Begin</c>
    /// con lo stesso nome non sono due pannelli, sono lo stesso pannello riempito due volte,
    /// e ImGui non lo segnala in alcun modo (è già successo: un pannello "Scena" e il
    /// viewport "Scena" si fondevano, coi bottoni dentro la vista 3D). Se il menu tenesse
    /// una propria copia del titolo, un rename ne cambierebbe una sola e la voce smetterebbe
    /// in silenzio di comandare la finestra giusta.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Se false il pannello non si disegna affatto. È lo <b>stesso</b> flag della X della
    /// finestra e della spunta nel menu Panels: <c>ImGui.Begin(title, ref visible)</c> lo
    /// scrive, il menu lo legge e lo scrive. Due stati paralleli si sarebbero desincronizzati
    /// al primo pannello chiuso con la X.
    /// </summary>
    bool Visible { get; set; }

    /// <param name="renderer">
    /// Il renderer del frame. Quasi nessun pannello ne ha bisogno — ImGui disegna da sé —
    /// ma il viewport sì, perché mostra una texture che il renderer possiede. Arriva come
    /// parametro e non dal costruttore perché il gioco riceve l'<c>IRenderer</c> solo in
    /// <c>Draw</c>, mentre i pannelli nascono in <c>Init</c>.
    /// </param>
    void Draw(World world, EditorContext context, IRenderer renderer);
}
