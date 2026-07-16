using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor;

/// <summary>
/// La cornice di un pannello: titolo, visibilità, posizione del primo avvio e la coppia
/// <c>Begin</c>/<c>End</c>. Le sottoclassi scrivono solo il contenuto.
///
/// Perché una classe base e non lasciare tutto ai pannelli (che erano classi indipendenti):
/// il preambolo era già identico e <b>copiato</b> in ognuno — due <c>SetNextWindow*</c>, il
/// <c>Begin</c>, il ramo "collassato → End e torna" con tanto di commento sul perché
/// dell'<c>End</c>. Aggiungere la visibilità a mano avrebbe voluto dire quattro copie di un
/// pezzo che ha una sola risposta giusta, e la parte delicata (il <c>ref visible</c> che
/// deve puntare allo stesso flag del menu) è proprio lì dentro.
///
/// ⚠️ La sottoclasse non deve chiamare <c>ImGui.End()</c>: lo fa questa classe. Il ramo
/// "non disegnato" invece esce <b>prima</b> del <c>Begin</c>, quindi lì non c'è nessun
/// <c>End</c> da bilanciare.
/// </summary>
/// <param name="defaultPosition">
/// Posizione del primo avvio. Senza, ImGui dà a ogni finestra la stessa posizione e i
/// pannelli nascono impilati uno sull'altro.
///
/// ⚠️ È <c>FirstUseEver</c>: dal secondo avvio comanda il layout salvato in
/// <c>imgui.ini</c> (docking incluso), che è il motivo per cui una modifica a questi
/// numeri sembra non avere effetto finché non si cancella quel file.
/// </param>
public abstract class PanelBase(string title, Vector2 defaultPosition, Vector2 defaultSize)
    : IEditorPanel
{
    public string Title => title;

    public bool Visible { get; set; } = true;

    /// <summary>Flag della finestra. Vedi <c>ViewportPanel</c> per un caso in cui non sono estetica.</summary>
    protected virtual ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.None;

    /// <summary>Padding della finestra, o null per quello del tema.</summary>
    protected virtual Vector2? WindowPadding => null;

    /// <summary>
    /// Virtuale e non solo il parametro del costruttore: l'Inspector si vuole ancorare al
    /// bordo destro, e la taglia del viewport non si conosce quando i pannelli nascono.
    /// Costa una lettura per frame, che <c>FirstUseEver</c> butta via subito dopo la prima.
    /// </summary>
    protected virtual Vector2 DefaultPosition => defaultPosition;

    /// <summary>
    /// ⚠️ <c>virtual</c> per un solo caso, e vale la pena saperlo: il <c>ScriptsPanel</c> deve
    /// girare <b>anche da chiuso</b>, perché si accende da sé quando uno script non compila.
    /// Tutto qui dentro sta dietro il guard su <see cref="Visible"/>, quindi un pannello
    /// chiuso non ha nessun altro modo di riaprirsi.
    ///
    /// ⚠️ E dev'essere <c>virtual</c>, non nascosto con un <c>new</c>: l'host disegna i
    /// pannelli attraverso <see cref="IEditorPanel"/>, e un metodo nascosto verrebbe saltato
    /// senza un errore — si vedrebbe solo un pannello che non si apre mai.
    /// </summary>
    public virtual void Draw(World world, EditorContext context, IRenderer renderer)
    {
        if (!Visible)
        {
            OnNotDrawn();
            return;
        }

        ImGui.SetNextWindowPos(DefaultPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(defaultSize, ImGuiCond.FirstUseEver);

        var padding = WindowPadding;
        if (padding is { } value)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, value);

        // ⚠️ Il ref DEVE essere questo flag e non una copia locale che poi si scarta: è così
        // che la X della finestra e la spunta nel menu Panels restano la stessa cosa. La
        // proprietà non si può passare per ref, da cui il giro tramite variabile.
        var visible = Visible;
        var expanded = ImGui.Begin(title, ref visible, WindowFlags);
        Visible = visible;

        if (padding is not null)
            ImGui.PopStyleVar();

        // Begin è false quando la finestra è collassata (o appena chiusa), ma End va chiamato
        // comunque: la coppia deve restare bilanciata o ImGui va in assert.
        if (expanded)
            DrawContent(world, context, renderer);
        else
            OnNotDrawn();

        ImGui.End();
    }

    protected abstract void DrawContent(World world, EditorContext context, IRenderer renderer);

    /// <summary>
    /// Chiamato nei frame in cui il contenuto non viene disegnato: pannello chiuso dal menu,
    /// o finestra collassata. Serve a chi tiene stato che deve smettere di girare quando
    /// nessuno lo guarda — il viewport, che altrimenti continuerebbe a riempire di GPU una
    /// texture invisibile.
    /// </summary>
    protected virtual void OnNotDrawn()
    {
    }
}
