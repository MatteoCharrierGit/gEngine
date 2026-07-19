using System.Numerics;
using System.Text.Json;
using gEngine.Ecs.Component;
using gEngine.Physics;
using gEngine.Rendering;
using gEngine.Scenes.Json;
using Raylib_cs;

// Raylib_cs serve per CameraProjection e ha un Color suo: senza l'alias, il Color dei
// default qui sotto sarebbe ambiguo fra i due namespace.
using Color = gEngine.Rendering.Color;

namespace gEngine.Scenes;

/// <summary>Un tipo di componente registrato, visto da chi non ne conosce i tipi: l'editor.</summary>
/// <param name="Key">Come si chiama nel file di scena.</param>
/// <param name="CanCreateDefault">
/// Se esiste una factory del valore di default, cioè se l'editor può aggiungerne uno a
/// un'entità. Vedi <see cref="SceneComponentRegistry.TryCreateDefault"/> per il perché non è
/// sempre vero.
/// </param>
public readonly record struct RegisteredComponent(string Key, Type Type, bool CanCreateDefault);

public class SceneComponentRegistry
{
    private readonly Dictionary<string, IComponentBinder> _componentBinders = new();

    // Verso opposto: dal tipo del componente alla chiave JSON + come scriverlo + come
    // crearne uno nuovo. Indicizzato per Type e non per chiave perché chi salva parte da uno
    // storage (che conosce il suo Type) e deve scoprire come si chiama nel file.
    private readonly Dictionary<Type, ComponentEntry> _componentEntries = new();

    private sealed record ComponentEntry(
        string Key,
        Func<object, SceneWriteContext, JsonElement> Write,
        Func<object>? CreateDefault);

    /// <summary>
    /// I tipi di componente che questo gioco dichiara, in ordine di chiave.
    ///
    /// Serve all'editor, che deve offrire "aggiungi componente" senza conoscere nessun tipo
    /// a compile time. Sta qui e non in un registry a parte perché la domanda è la stessa
    /// che questa classe risponde già per le scene — <i>di quali componenti è fatto questo
    /// gioco e come si chiamano</i> — e un secondo elenco sarebbe un secondo posto in cui
    /// dimenticarsi di registrare il componente nuovo.
    ///
    /// ⚠️ Non è "tutti i tipi di componente esistenti": è quelli <b>registrati</b>. Un
    /// componente vivo nel World ma mai registrato non compare (e infatti non è nemmeno
    /// salvabile — <c>SceneSerializer</c> lancia). <c>NameComponent</c> è il caso opposto e
    /// noto: è nel formato come campo <c>name</c> dell'entità, non come componente, quindi
    /// non è registrato qui e l'editor non sa aggiungerlo.
    /// </summary>
    public IEnumerable<RegisteredComponent> RegisteredComponents =>
        _componentEntries
            .Select(entry => new RegisteredComponent(entry.Value.Key, entry.Key, entry.Value.CreateDefault is not null))
            .OrderBy(component => component.Key, StringComparer.Ordinal);

    /// <summary>Binder semplice: costruisce il componente dai soli dati JSON.</summary>
    public void Register<T>(string key, Func<JsonElement, T> parse,
        Func<T, SceneWriteContext, JsonElement>? write = null, Func<T>? createDefault = null)
    {
        _componentBinders[key] = new ComponentBinder<T>(parse);
        RegisterEntry(key, write, createDefault);
    }

    /// <summary>
    /// Binder con contesto: per componenti che referenziano altre entità (Parent) o
    /// risorse per path (ModelPath). Riceve il <see cref="SceneBindContext"/>.
    /// </summary>
    public void Register<T>(string key, Func<JsonElement, SceneBindContext, T> parse,
        Func<T, SceneWriteContext, JsonElement>? write = null, Func<T>? createDefault = null)
    {
        _componentBinders[key] = new ContextComponentBinder<T>(parse);
        RegisterEntry(key, write, createDefault);
    }

    /// <summary>
    /// Il writer è <b>opzionale</b> alla registrazione: per la stragrande maggioranza dei
    /// componenti "serializza i campi con le opzioni condivise" è già l'inverso corretto
    /// del parse di default, quindi chiederlo ogni volta sarebbe cerimonia. Va passato
    /// solo quando la lettura fa qualcosa di asimmetrico che la scrittura deve disfare
    /// (Parent risolve un nome in Entity; MeshRenderer trasforma un path in handle).
    ///
    /// La factory del default è opzionale per un motivo <b>opposto</b>: vedi
    /// <see cref="TryCreateDefault"/>. Qui non c'è un fallback perché non ne esiste uno
    /// giusto.
    /// </summary>
    private void RegisterEntry<T>(string key, Func<T, SceneWriteContext, JsonElement>? write,
        Func<T>? createDefault)
    {
        _componentEntries[typeof(T)] = new ComponentEntry(
            key,
            write is null
                ? (component, _) => JsonSerializer.SerializeToElement((T)component, SceneJson.Options)
                : (component, context) => write((T)component, context),
            createDefault is null ? null : () => createDefault()!);
    }

    /// <summary>
    /// Un valore di default per questo tipo di componente, se chi l'ha registrato ne ha
    /// dichiarato uno.
    ///
    /// <b>Perché una factory dichiarata e non <c>Activator.CreateInstance</c>.</b> I
    /// componenti dell'engine sono struct di dati nudi, quindi "crearne uno" senza dire come
    /// significa <c>default(T)</c>: tutti i campi a zero. Non è un default neutro, è un
    /// default <b>rotto</b>, e in modo diverso per ogni tipo — un Transform con
    /// <c>Scale = 0</c> è invisibile e la sua rotazione non è nemmeno un quaternione valido
    /// (0,0,0,0); una Camera con <c>FovY = Near = Far = 0</c> non inquadra niente; una Light
    /// con <c>Intensity = 0</c> non illumina; un MeshRenderer nasce <c>Visible = false</c>.
    /// L'utente vedrebbe "componente aggiunto" e nessun effetto, che è il bug più caro da
    /// cercare: quello che assomiglia a un no-op.
    ///
    /// Da qui la scelta: chi registra il componente <b>dichiara</b> il default, ed è l'unico
    /// che può — è lo stesso principio di <c>[EditorConfiguration]</c> (l'editor manipola
    /// dati che non conosce e ha bisogno che il tipo glielo dica). Chi non lo dichiara
    /// resta nell'elenco ma non è aggiungibile, e l'editor lo <b>mostra spento col
    /// motivo</b> invece di nasconderlo: sparire farebbe cercare la registrazione mancante
    /// nel posto sbagliato.
    ///
    /// ⚠️ Per qualche tipo il default non esiste proprio, e non è una dimenticanza:
    /// <c>ParentComponent</c> è un riferimento a un'altra entità — un default sarebbe
    /// <c>Entity(0)</c>, che non esiste. Ci si riparenta dalla Hierarchy, non aggiungendo un
    /// componente vuoto.
    /// </summary>
    public bool TryCreateDefault(Type componentType, out object component)
    {
        if (_componentEntries.TryGetValue(componentType, out var entry) &&
            entry.CreateDefault is { } create)
        {
            component = create();
            return true;
        }

        component = null!;
        return false;
    }

    public bool TryGet(string key, out IComponentBinder binder)
    {
        return _componentBinders.TryGetValue(key, out binder!);
    }

    /// <summary>Come si chiama e come si scrive questo tipo di componente nel file di scena.</summary>
    public bool TryGetWriter(Type componentType, out string key, out Func<object, SceneWriteContext, JsonElement> write)
    {
        if (_componentEntries.TryGetValue(componentType, out var writer))
        {
            key = writer.Key;
            write = writer.Write;
            return true;
        }

        key = string.Empty;
        write = null!;
        return false;
    }

    /// <summary>
    /// Registra i componenti built-in dell'engine. I giochi estendono lo stesso
    /// registry con i propri componenti custom (es. Player, Velocity), riusando
    /// <see cref="SceneJson.Options"/> per mantenere identico il formato JSON.
    ///
    /// ⚠️ I <c>createDefault</c> qui sotto non sono "valori a caso perché ne serviva uno":
    /// sono i valori per cui aggiungere il componente dall'editor <b>si vede</b>. Un default
    /// che compila ma non fa niente (scala 0, intensità 0, fov 0) sarebbe indistinguibile da
    /// un bug dell'editor. Vedi <see cref="TryCreateDefault"/>.
    /// </summary>
    public void RegisterEngineDefaults()
    {
        Register("Transform", data => data.Deserialize<TransformComponent>(SceneJson.Options),
            createDefault: () => new TransformComponent
            {
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            });

        // Bianca e a intensità 1: la luce si aggiunge per illuminare. Directional perché non
        // dipende da dove sta l'entità — una point nell'origine, dentro il pavimento, non si
        // vedrebbe e sembrerebbe che il componente non funzioni.
        Register("Light", data => data.Deserialize<LightComponent>(SceneJson.Options),
            createDefault: () => new LightComponent
            {
                Kind = LightKind.Directional,
                Color = Color.White,
                Intensity = 1f
            });

        // Massa 1 e un box unitario: un corpo dinamico che cade. Statico sarebbe il default
        // più "sicuro" ma anche quello che non fa niente, e Mass = 0 su un dinamico è
        // degenere per il solver.
        Register("RigidBody", data => data.Deserialize<RigidBodyComponent>(SceneJson.Options),
            createDefault: () => new RigidBodyComponent
            {
                Shape = ColliderShape.Box,
                Size = Vector3.One,
                Mass = 1f,
                IsStatic = false
            });

        // Camera: solo i dati ottici. La posa è nel Transform dell'entità, quindi è già
        // scritta dal suo binder — qui non c'è niente di asimmetrico da disfare e il writer
        // di default basta.
        //
        // Primary = false: la camera nuova non ruba l'inquadratura a quella che sta già
        // guardando il giocatore. Chi la vuole primaria lo spunta nell'Inspector — e vede
        // subito il cambio, che è il modo giusto di scoprire che l'interruttore c'è.
        Register("Camera", data => data.Deserialize<CameraComponent>(SceneJson.Options),
            createDefault: () => new CameraComponent
            {
                FovY = 60f,
                Near = 0.01f,
                Far = 1000f,
                Projection = CameraProjection.Perspective,
                Primary = false
            });

        // MeshRenderer: oltre ai campi standard, un opzionale "ModelPath" carica il modello
        // via AssetManager (dal contesto) e ne assegna l'handle. Così i modelli sono
        // autorabili da scena senza esporre gli handle nativi.
        Register("MeshRenderer",
            (data, ctx) =>
            {
                var mesh = data.Deserialize<MeshRendererComponent>(SceneJson.Options)!;
                if (data.TryGetProperty("ModelPath", out var modelPath) && modelPath.ValueKind == JsonValueKind.String)
                    mesh.Model = ctx.Assets.LoadModel(modelPath.GetString()!);
                return mesh;
            },
            (mesh, ctx) =>
            {
                var node = JsonSerializer.SerializeToNode(mesh, SceneJson.Options)!.AsObject();

                // Il campo Model è un handle: un id opaco valido solo per questa
                // esecuzione. Scriverlo sarebbe peggio che inutile — al reload verrebbe
                // deserializzato come handle buono e punterebbe a un modello a caso.
                // Al suo posto va il path, che è l'unica cosa che sopravvive al riavvio.
                node.Remove(nameof(MeshRendererComponent.Model));

                if (mesh.Model.IsValid && ctx.Assets.TryGetModelPath(mesh.Model, out var path))
                    node["ModelPath"] = path;

                return JsonSerializer.SerializeToElement(node, SceneJson.Options);
            },
            // Un cubo bianco unitario e visibile: la forma che si vede senza aver caricato
            // niente. Model resta None — è un asset, e si trascina dal pannello File system.
            //
            // ⚠️ La factory costruisce un valore NUOVO a ogni chiamata, e va tenuta così: se
            // restituisse un'istanza condivisa, con un componente a riferimento tutte le entità
            // che l'aggiungono editerebbero lo stesso oggetto. Oggi il MeshRenderer è uno
            // struct e il pericolo non c'è (l'assegnamento copia), ma la regola vale per tutte
            // le factory di questo registry, comprese quelle che un gioco dichiarerà.
            createDefault: () => new MeshRendererComponent
            {
                Kind = MeshKind.Cube,
                Size = Vector3.One,
                Tint = Color.White,
                Wireframe = false,
                Visible = true,
                Layer = RenderLayer.Opaque,
                SortingOrder = 0
            });

        // Parent: il valore JSON è il "name" dell'entità genitore; lo risolviamo nella
        // Entity creata tramite il contesto (richiede l'istanziazione a due passate).
        //
        // ⚠️ Niente createDefault, e non è una dimenticanza: un genitore di default non
        // esiste. Entity(0) non è un'entità, e "il primo che trovi" sarebbe una gerarchia
        // decisa a caso. Ci si riparenta dalla Hierarchy — l'editor mostrerà questa voce
        // spenta, che è l'informazione giusta.
        Register("Parent",
            (data, ctx) =>
            {
                var parentName = data.GetString()
                                 ?? throw new InvalidOperationException("Il componente 'Parent' dev'essere il nome (stringa) dell'entità genitore.");

                if (!ctx.EntitiesByName.TryGetValue(parentName, out var parent))
                    throw new InvalidOperationException(
                        $"Parent: nessuna entità con name '{parentName}' nella scena. Assicurati che il genitore abbia un campo \"name\" corrispondente.");

                return new ParentComponent { Parent = parent };
            },
            // Scrittura: dall'Entity si torna al nome. Gli id non sopravvivono al reload
            // (si riassegnano da capo), i nomi sì.
            (parent, ctx) => JsonSerializer.SerializeToElement(ctx.RequireName(parent.Parent, "Parent"), SceneJson.Options));
    }
}
