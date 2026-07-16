using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Editor.Scripting;
using gEngine.Rendering;
using ImGuiNET;

namespace gEngine.Editor.Panels;

/// <summary>
/// Gli script compilati e — soprattutto — <b>cosa non ha compilato</b>.
///
/// È il pannello che rende usabili gli script a runtime. Senza, un punto e virgola dimenticato
/// dà un gioco che parte con un system in meno e nessuno che lo dice: il caso peggiore di
/// tutti, perché assomiglia a "il mio system non funziona" e manda a cercare il bug dentro il
/// system invece che nella riga che non compila.
///
/// Acceso <b>da solo</b> quando c'è un errore, al contrario dei suoi gemelli Systems e
/// Components: quelli si aprono quando li si cerca, questo deve farsi trovare. È l'unico
/// pannello che si permette di aprirsi da sé, e la regola per cui gli è concesso è che ha
/// qualcosa da dire che l'utente non sa ancora di dover chiedere.
/// </summary>
public class ScriptsPanel : PanelBase
{
    public ScriptsPanel() : base("Scripts", new Vector2(340, 460), new Vector2(560, 240))
    {
        // Vedi il commento della classe: si accende da sé in Draw, se serve.
        Visible = false;
    }

    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 WarningColor = new(0.95f, 0.8f, 0.35f, 1f);

    /// <summary>
    /// Per accendersi <b>una volta sola</b> per compilazione: senza, il pannello si
    /// riaprirebbe a ogni frame e non si potrebbe chiudere.
    /// </summary>
    private ScriptCompilation? _announced;

    /// <summary>
    /// ⚠️ Non è <c>DrawContent</c>: deve girare anche quando il pannello è chiuso, o non
    /// potrebbe mai riaprirsi da sé. <c>PanelBase.Draw</c> esce prima di disegnare se
    /// <c>Visible</c> è false — quindi il controllo va prima, qui.
    /// </summary>
    public override void Draw(World world, EditorContext context, IRenderer renderer)
    {
        if (context.Scripts is { } compilation && !ReferenceEquals(_announced, compilation))
        {
            _announced = compilation;

            // Solo gli errori aprono il pannello. Un avviso non merita di interrompere: chi
            // fa aprire una finestra per ogni "variabile non usata" si fa ignorare, e poi
            // l'errore vero passa inosservato insieme al resto.
            if (compilation.ErrorCount > 0)
                Visible = true;
        }

        base.Draw(world, context, renderer);
    }

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        if (context.Scripts is not { } compilation)
        {
            ImGui.TextDisabled("Compilazione degli script non disponibile.");
            HelpMarker(
                "Il gioco non ha dichiarato una ScriptCompilation fra le sue Resource: o non\n" +
                "usa gli script compilati a runtime, o non ha passato l'esito all'editor.\n" +
                "Non e' \"nessuno script\": e' \"non lo so\".");
            return;
        }

        ImGui.TextDisabled(compilation.Folder);
        ImGui.Separator();

        DrawSummary(compilation);
        ImGui.Separator();

        if (compilation.Diagnostics.Count == 0)
        {
            ImGui.TextDisabled("Nessun errore, nessun avviso.");
            DrawFiles(compilation);
            return;
        }

        // Gli errori prima: sono l'unica cosa che impedisce al gioco di girare, e in una lista
        // lunga un avviso in cima li nasconde.
        foreach (var diagnostic in compilation.Diagnostics.OrderByDescending(d => d.IsError))
        {
            ImGui.TextColored(diagnostic.IsError ? ErrorColor : WarningColor,
                $"{diagnostic.File}({diagnostic.Line}): {diagnostic.Message}");
        }

        DrawFiles(compilation);
    }

    private static void DrawSummary(ScriptCompilation compilation)
    {
        if (compilation.Files.Count == 0)
        {
            ImGui.TextDisabled("Nessuno script trovato.");
            HelpMarker(
                "La cartella non esiste o non contiene .cs. E' un caso normale: un gioco puo'\n" +
                "non avere script.");
            return;
        }

        if (compilation.Ok)
        {
            ImGui.Text($"{compilation.Files.Count} script compilati.");
            return;
        }

        // Il punto che vale il pannello: dire cosa NON gira adesso, non solo che c'e' un
        // errore. "Non compila" e "i tuoi system non esistono" sono la stessa cosa, ma solo la
        // seconda spiega quello che si sta vedendo a schermo.
        ImGui.TextColored(ErrorColor, $"Compilazione FALLITA ({compilation.ErrorCount} errori).");
        HelpMarker(
            "Nessuno degli script e' stato caricato: i system e i componenti che dichiarano\n" +
            "NON esistono in questa esecuzione. Non e' che \"non funzionano\" - non ci sono.\n\n" +
            "Un assembly si compila tutto insieme: un errore in un file solo li porta giu'\n" +
            "tutti. E' il compilatore C#, non una scelta dell'engine.");
    }

    private static void DrawFiles(ScriptCompilation compilation)
    {
        if (compilation.Files.Count == 0)
            return;

        ImGui.Spacing();

        if (!ImGui.CollapsingHeader($"File ({compilation.Files.Count})"))
            return;

        foreach (var file in compilation.Files)
            ImGui.TextDisabled(Path.GetRelativePath(compilation.Folder, file).Replace('\\', '/'));
    }

    /// <summary>Il "(?)" con la spiegazione sotto il puntatore. Vedi il gemello nell'Inspector.</summary>
    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 32f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
