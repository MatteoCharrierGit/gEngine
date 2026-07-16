using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace gEngine.Editor.Scripting;

/// <summary>Un errore (o un avviso) del compilatore, già pronto da mostrare.</summary>
/// <param name="File">Path relativo alla cartella degli script: il path assoluto della
/// macchina non aggiunge niente in un pannello largo 300px.</param>
public sealed record ScriptDiagnostic(string Message, string File, int Line, bool IsError);

/// <summary>
/// L'esito di una compilazione: l'assembly se è andata, e comunque cosa ha da dire il
/// compilatore.
/// </summary>
public sealed class ScriptCompilation
{
    /// <summary><c>null</c> se la compilazione è fallita. Chi lo legge deve reggerlo.</summary>
    public Assembly? Assembly { get; init; }

    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>I file trovati, anche quando non compilano: "quali script esistono" resta una
    /// domanda con risposta pure se il compilatore ha detto di no.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>La cartella da cui si è compilato, o vuota se non esiste.</summary>
    public string Folder { get; init; } = string.Empty;

    public bool Ok => Assembly is not null;

    public int ErrorCount => Diagnostics.Count(diagnostic => diagnostic.IsError);
}

/// <summary>
/// Compila i <c>.cs</c> che stanno sotto una cartella (in pratica <c>assets/scripts/</c>) in un
/// assembly in memoria, che <c>ScriptDiscovery</c> poi scandisce come qualunque altro.
///
/// <b>Perché a runtime.</b> È il pezzo che rende vera la frase "chi usa l'editor non tocca il
/// progetto": uno script non è più un file del <c>.csproj</c> che qualcuno deve aver aggiunto
/// e ricompilato — è un file dentro gli asset, come un modello o una scena. È il modello Unity.
///
/// <b>Perché non è un mostro.</b> Perché lo strato sotto era già pronto:
/// <c>ScriptDiscovery</c> prende un <see cref="Assembly"/> e non gli importa da dove venga.
/// Qui si produce quell'assembly, e basta. Fosse stato costruito nell'ordine inverso, questo
/// file avrebbe dovuto contenere anche la registrazione.
///
/// ⚠️ <b>Un errore di compilazione non è un crash</b>: è il caso normale di chi scrive codice,
/// e succede mentre il gioco gira. Quindi qui non si lancia mai — si torna un
/// <see cref="ScriptCompilation"/> con dentro cosa non va, e l'editor lo mostra. Un
/// <c>throw</c> davanti a un punto e virgola mancante vorrebbe dire riavviare il gioco a ogni
/// errore di battitura.
/// </summary>
public static class ScriptCompiler
{
    /// <summary>
    /// Compila tutti i <c>.cs</c> sotto <paramref name="folder"/>, ricorsivamente ("da
    /// qualsiasi cartella dentro assets", che è la richiesta).
    ///
    /// Non lancia: vedi il commento della classe. Una cartella che non c'è è un caso normale
    /// (un gioco può non avere script) e torna una compilazione vuota e <c>Ok</c> a false, non
    /// un errore.
    /// </summary>
    public static ScriptCompilation Compile(string folder)
    {
        if (!Directory.Exists(folder))
            return new ScriptCompilation { Folder = folder };

        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).ToList();

        if (files.Count == 0)
            return new ScriptCompilation { Folder = folder };

        var trees = new List<SyntaxTree>(files.Count);
        var readErrors = new List<ScriptDiagnostic>();

        foreach (var file in files)
        {
            try
            {
                // path: serve a Roslyn per dire in QUALE file sta l'errore. Senza, i
                // diagnostici arrivano senza indirizzo.
                trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un file illeggibile non deve portarsi dietro gli altri: è un errore come un
                // altro, e va mostrato come tale.
                readErrors.Add(new ScriptDiagnostic(ex.Message, Relative(folder, file), 0, IsError: true));
            }
        }

        trees.Add(ImplicitUsings());

        var compilation = CSharpCompilation.Create(
            // Un nome nuovo a ogni compilazione: due assembly con lo stesso nome nello stesso
            // processo sono una fonte di confusione gratuita il giorno che si ricaricherà.
            $"gEngine.Scripts.{Guid.NewGuid():N}",
            trees,
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        var diagnostics = readErrors
            .Concat(result.Diagnostics
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                .Select(diagnostic => Describe(folder, diagnostic)))
            .ToList();

        if (!result.Success)
            return new ScriptCompilation { Diagnostics = diagnostics, Files = files, Folder = folder };

        stream.Seek(0, SeekOrigin.Begin);

        // ⚠️ Collezionabile anche se oggi non si scarica niente: un AssemblyLoadContext non
        // collezionabile si carica e resta lì per sempre, e la scelta NON si può cambiare dopo.
        // Costa nulla ora e sarebbe da rifare tutto poi — vedi HANDOFF.md per il ricaricamento.
        var context = new AssemblyLoadContext($"gEngine.Scripts", isCollectible: true);

        return new ScriptCompilation
        {
            Assembly = context.LoadFromStream(stream),
            Diagnostics = diagnostics,
            Files = files,
            Folder = folder
        };
    }

    /// <summary>
    /// I <c>global using</c> che il .NET SDK genera quando <c>ImplicitUsings</c> è acceso, e
    /// che qui nessuno genererebbe.
    ///
    /// ⚠️ Trappola pagata: senza questi, uno script che usa <c>IReadOnlyList</c>, <c>Type</c> o
    /// <c>.Select()</c> <b>non compila</b>, con una raffica di CS0246 che sembrano un problema
    /// di riferimenti mancanti e non lo sono. Il file è identico a uno del progetto e lì
    /// compila: la differenza invisibile è che il csproj ci mette dentro un
    /// <c>GlobalUsings.g.cs</c> generato, e Roslyn su dei file grezzi no.
    ///
    /// L'elenco è quello dell'SDK per <c>Microsoft.NET.Sdk</c>: gli script devono sembrare
    /// file del progetto, perché è ciò che erano fino a ieri e ciò che chi li scrive si
    /// aspetta. Un elenco diverso qui sarebbe un dialetto di C# da imparare.
    /// </summary>
    private static SyntaxTree ImplicitUsings()
    {
        return CSharpSyntaxTree.ParseText(
            """
            global using global::System;
            global using global::System.Collections.Generic;
            global using global::System.IO;
            global using global::System.Linq;
            global using global::System.Net.Http;
            global using global::System.Threading;
            global using global::System.Threading.Tasks;
            """,
            path: "<implicit usings>");
    }

    /// <summary>
    /// Cosa gli script possono usare: <b>tutto ciò che il gioco ha già caricato</b>, cioè
    /// l'engine, il gioco stesso e il framework.
    ///
    /// È il motivo per cui uno script può scrivere <c>using Sandbox.Components;</c> e vedere i
    /// componenti del gioco: l'assembly del gioco è fra questi. ⚠️ Il verso opposto no — il
    /// codice del gioco <b>non può nominare un tipo di uno script</b>, perché quando il gioco
    /// è stato compilato lo script non esisteva. Non è un limite da aggirare, è la freccia del
    /// tempo: è anche il motivo per cui i system possono diventare script (nessuno li nomina,
    /// li trova la scoperta) e un componente che l'HUD interroga per nome no.
    ///
    /// ⚠️ Si guardano gli assembly <b>caricati</b>, non quelli referenziati: uno che il runtime
    /// non ha ancora avuto motivo di caricare non è qui, e uno script che lo usasse non
    /// compilerebbe. <b>È già successo</b>, ed è il motivo del <c>Load</c> qui sotto: gli
    /// implicit usings promettono <c>System.Net.Http</c>, che è l'unico dell'elenco a non stare
    /// in CoreLib e che nessuno carica mai — la promessa e le reference divergevano, e il
    /// sintomo era un CS0234 dentro un file che non esiste.
    ///
    /// La regola generale, dichiarata: un gioco che referenzia una libreria senza mai toccarla
    /// non la carica, quindi gli script non la vedono. Il rimedio, se capiterà, è che il gioco
    /// la nomini una volta — oppure che sia lui a dire alla compilazione cosa deve vedere.
    /// </summary>
    private static IEnumerable<MetadataReference> References()
    {
        // Prima di guardare cosa c'è: ciò che gli implicit usings promettono deve esserci.
        // Il try è d'obbligo — è un assembly che potrebbe non essere spedito con il gioco, e
        // la sua assenza non deve buttare giù la compilazione di tutto il resto.
        try
        {
            Assembly.Load("System.Net.Http");
        }
        catch (Exception ex) when (ex is FileNotFoundException or BadImageFormatException)
        {
            // Nessun rimedio e niente da dire: gli script che non usano HTTP compilano lo
            // stesso, e quelli che lo usano avranno il loro CS0234 con dentro il nome giusto.
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            // Dinamici e in-memory non hanno un file da cui leggere i metadati. Gli assembly
            // degli script compilati prima cadono qui: è giusto, non devono vedersi fra loro.
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location));
    }

    private static ScriptDiagnostic Describe(string folder, Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();

        return new ScriptDiagnostic(
            $"{diagnostic.Id}: {diagnostic.GetMessage()}",
            span.Path.Length > 0 ? Relative(folder, span.Path) : "<script>",
            // Roslyn conta le righe da 0, gli editor da 1. Mostrare "riga 0" manderebbe a
            // guardare la riga sbagliata.
            span.StartLinePosition.Line + 1,
            diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static string Relative(string folder, string file)
    {
        try
        {
            return Path.GetRelativePath(folder, file).Replace('\\', '/');
        }
        catch (ArgumentException)
        {
            return file;
        }
    }
}
