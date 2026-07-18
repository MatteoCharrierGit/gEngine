using System.Reflection;
using System.Text.Json;
using gEngine.Core;
using gEngine.Ecs;
using gEngine.Ecs.Interfaces.System;
using gEngine.Scenes;
using gEngine.Scenes.Json;

namespace gEngine.Scripting;

/// <summary>
/// Trova i tipi marcati <see cref="GameComponentAttribute"/> / <see cref="GameSystemAttribute"/>
/// in un assembly e li registra da sé.
///
/// <b>Perché esiste</b>: scrivere uno script non deve voler dire modificare un file che non è
/// lo script. Oggi un system nuovo va citato in <c>Game.Init</c> e un componente nuovo nel
/// registry delle scene: due punti lontani dal file che si sta scrivendo, e dimenticarne uno
/// non dà un errore di compilazione — dà un system che non gira mai, o una scena che non si
/// salva. La scoperta toglie i due punti.
///
/// <b>Cosa NON è</b>: non è "il motore carica gli script da una cartella". Qui si guarda un
/// assembly <b>già caricato</b>, cioè codice compilato insieme al gioco. La compilazione a
/// runtime dei <c>.cs</c> sotto <c>assets/</c> è lo strato <b>sopra</b> questo (vedi
/// <c>HANDOFF.md</c>): quando arriverà, produrrà un <see cref="Assembly"/> e lo darà in pasto
/// a questi stessi metodi. Costruire prima questo strato è ciò che rende l'altro un'aggiunta
/// invece che una riscrittura.
///
/// ⚠️ La riflessione è pagata <b>una volta all'avvio</b>, non per frame: si scandisce
/// l'assembly, si registra, e da lì in poi non ne resta niente nel ciclo di gioco.
/// </summary>
public static class ScriptDiscovery
{
    /// <summary>
    /// Registra tutti i componenti marcati <see cref="GameComponentAttribute"/>.
    ///
    /// Il binder è quello di default (deserializza i campi con le opzioni condivise), che è
    /// l'inverso corretto per la stragrande maggioranza dei componenti. Chi ha bisogno di un
    /// binder asimmetrico non usa l'attributo e si registra a mano — vedi
    /// <see cref="GameComponentAttribute"/>.
    /// </summary>
    /// <returns>Le chiavi registrate, per poterlo dire a chi guarda.</returns>
    /// <exception cref="InvalidOperationException">
    /// Se due tipi si contendono la stessa chiave. Fail-fast e non "vince l'ultimo": due
    /// componenti sotto lo stesso nome nel file di scena vorrebbero dire che caricare una
    /// scena ne istanzia uno a caso, e il bug si manifesterebbe lontanissimo da qui.
    /// </exception>
    public static IReadOnlyList<string> RegisterComponents(Assembly assembly, SceneComponentRegistry registry)
    {
        var registered = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var type in Types(assembly))
        {
            if (type.GetCustomAttribute<GameComponentAttribute>() is not { } attribute)
                continue;

            var key = attribute.Key ?? StripComponentSuffix(type.Name);

            if (registered.TryGetValue(key, out var other))
                throw new InvalidOperationException(
                    $"Due componenti si contendono la chiave '{key}' nel file di scena: " +
                    $"{other.FullName} e {type.FullName}. Dài a uno dei due una chiave " +
                    $"esplicita: [GameComponent(\"AltroNome\")].");

            registered[key] = type;

            // Il registry è generico e il tipo lo si conosce solo ora: l'unico modo di
            // chiamarlo senza scriverne il T è chiudere il generico a mano. Costa una
            // MakeGenericMethod per componente, una volta all'avvio.
            RegisterComponentMethod
                .MakeGenericMethod(type)
                .Invoke(null, [registry, key]);
        }

        return registered.Keys.ToList();
    }

    private static readonly MethodInfo RegisterComponentMethod =
        typeof(ScriptDiscovery).GetMethod(nameof(RegisterComponent), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// La registrazione vera, con il <c>T</c> finalmente in mano. Chiamata solo per
    /// riflessione da <see cref="RegisterComponents"/>.
    /// </summary>
    private static void RegisterComponent<T>(SceneComponentRegistry registry, string key)
    {
        registry.Register<T>(
            key,
            data => data.Deserialize<T>(SceneJson.Options)!,
            write: null,
            createDefault: FindCreateDefault<T>());
    }

    /// <summary>
    /// La factory del default dichiarata dal tipo (<c>public static T CreateDefault()</c>), o
    /// null se non c'è.
    ///
    /// ⚠️ Solo dichiarata: niente <c>Activator.CreateInstance</c> come ripiego. Per uno struct
    /// di dati nudi darebbe tutti i campi a zero — un default rotto travestito da neutro, che
    /// nell'editor si vedrebbe come "componente aggiunto, nessun effetto". La decisione, col
    /// perché, sta in <c>SceneComponentRegistry.TryCreateDefault</c>: chi non dichiara resta
    /// nell'elenco ma spento, col motivo.
    ///
    /// La firma è cercata per convenzione e non con un attributo perché è già una convenzione
    /// che si autodocumenta: un metodo che si chiama <c>CreateDefault</c> e torna il proprio
    /// tipo non ha bisogno di essere annunciato.
    /// </summary>
    private static Func<T>? FindCreateDefault<T>()
    {
        var method = typeof(T).GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static,
            binder: null, types: Type.EmptyTypes, modifiers: null);

        if (method is null || method.ReturnType != typeof(T))
            return null;

        return method.CreateDelegate<Func<T>>();
    }

    /// <summary>
    /// Registra tutti i system marcati <see cref="GameSystemAttribute"/>, in ordine di
    /// <see cref="GameSystemAttribute.Order"/> e poi di nome, costruendoli con le dipendenze
    /// prese dalle <paramref name="resources"/>.
    ///
    /// ⚠️ <b>Il posto da cui si chiama questo metodo conta.</b> I system finiscono nel registry
    /// nell'ordine in cui li si aggiunge, e dentro una fase l'ordine è comportamento: chiamare
    /// questa dopo <c>Add(new MeshRenderSystem())</c> mette gli script del gioco <b>dopo</b> il
    /// disegno. L'attributo ordina gli script <i>fra loro</i>, non rispetto ai system che il
    /// gioco registra a mano — quella resta una riga che il gioco scrive dove vuole, ed è
    /// l'unico punto in cui si decide.
    /// </summary>
    /// <returns>I system registrati, nell'ordine in cui sono stati aggiunti.</returns>
    /// <exception cref="InvalidOperationException">
    /// Se un system non è costruibile: nessun costruttore pubblico, o un parametro che le
    /// Resource non contengono. Fail-fast, con dentro il nome di ciò che manca — l'alternativa
    /// (saltarlo) sarebbe uno script che non gira e nessuno che lo dice, cioè esattamente il
    /// problema che questo file esiste per togliere.
    /// </exception>
    public static IReadOnlyList<ISystem> RegisterSystems(Assembly assembly, SystemRegistry systems, Resources resources)
    {
        var found = Types(assembly)
            .Where(type => type.GetCustomAttribute<GameSystemAttribute>() is not null)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .OrderBy(type => type.GetCustomAttribute<GameSystemAttribute>()!.Order)
            .ThenBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        var created = new List<ISystem>(found.Count);

        foreach (var type in found)
        {
            if (!typeof(ISystem).IsAssignableFrom(type))
                throw new InvalidOperationException(
                    $"{type.FullName} è marcato [GameSystem] ma non implementa ISystem: " +
                    "non c'è niente da registrare.");

            var system = (ISystem)Construct(type, resources);

            // Add smista sulle fasi e lancia se il system non ne implementa nessuna: quel
            // controllo è suo e non va duplicato qui.
            systems.Add(system);
            created.Add(system);
        }

        return created;
    }

    /// <summary>
    /// Costruisce il system risolvendo i parametri del costruttore dalle Resource, per tipo.
    ///
    /// Un solo costruttore, e voluto: con due, "quale" sarebbe una regola implicita da
    /// indovinare (il più lungo? il più corto?) e da ricordare. Uno script che ne vuole due
    /// sta chiedendo una cosa che questo strato non sa decidere.
    /// </summary>
    private static object Construct(Type type, Resources resources)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length != 1)
            throw new InvalidOperationException(
                $"{type.FullName} è marcato [GameSystem] ma ha {constructors.Length} costruttori " +
                "pubblici: ne serve esattamente uno, o non si può sapere quale usare.");

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var arguments = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            if (!resources.TryGet(parameters[i].ParameterType, out var resource))
                throw new InvalidOperationException(
                    $"{type.FullName} chiede un {parameters[i].ParameterType.Name} nel costruttore, " +
                    "ma non è dichiarato fra le Resource. Registralo con resources.Add<" +
                    $"{parameters[i].ParameterType.Name}>(...) prima di scoprire gli script - " +
                    "le Resource sono l'elenco di ciò di cui il gioco vive, e ciò che non è lì " +
                    "dentro non è iniettabile.");

            arguments[i] = resource;
        }

        return constructor.Invoke(arguments);
    }

    /// <summary>
    /// ⚠️ <c>ReflectionTypeLoadException</c> va gestita e non lasciata passare: succede quando
    /// un tipo dell'assembly referenzia qualcosa che non si carica. Con un assembly compilato
    /// insieme al gioco non capita quasi mai — ma quando gli script arriveranno da una
    /// compilazione a runtime sarà il caso <b>normale</b>, e qui è dove si vedrà. I tipi che si
    /// caricano si tengono: uno script rotto non deve portarsi dietro tutti gli altri.
    /// </summary>
    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>"HealthComponent" → "Health". La stessa convenzione degli header dell'Inspector.</summary>
    private static string StripComponentSuffix(string name)
    {
        const string suffix = "Component";
        return name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
            ? name[..^suffix.Length]
            : name;
    }
}
