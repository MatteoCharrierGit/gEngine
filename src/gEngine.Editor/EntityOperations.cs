using System.Numerics;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;

namespace gEngine.Editor;

/// <summary>
/// Operazioni di struttura sulle entità che servono all'editor: creare, duplicare, eliminare
/// un sottoalbero. Stanno qui e non nel <see cref="World"/> perché sono <b>politiche</b>, non
/// meccanismi: "creare significa dare un nome libero e un transform neutro", "duplicare
/// significa copiare i componenti tranne quelli di runtime" e "eliminare significa portarsi
/// via i figli" sono decisioni dell'editor. Il World offre i mattoni (<c>CreateEntity</c>,
/// <c>DestroyEntity</c>, gli storage) e non ha opinioni.
/// </summary>
public static class EntityOperations
{
    private const string DefaultName = "Nuova entità";

    /// <summary>
    /// Un'entità nuova, pronta a essere vista: un nome libero e un transform neutro.
    ///
    /// <c>World.CreateEntity</c> da solo dà un'entità <b>senza componenti</b>, che è la cosa
    /// giusta per il World e quella sbagliata da dare all'utente: senza Transform non ha una
    /// posa, e senza nome nella Hierarchy è "Entity 7".
    ///
    /// ⚠️ Il nome è <b>reso libero</b> e non lasciato a "Nuova entità" per tutte: due entità
    /// omonime collassano nella mappa nome→Entity di <c>SceneInstantiator</c> al reload —
    /// vince l'ultima, senza un errore. È lo stesso motivo per cui <see cref="Duplicate"/>
    /// rinomina la copia; qui mordeva al secondo clic su "Crea entità".
    /// </summary>
    /// <param name="parent">
    /// Il genitore, o null per una radice. Con un genitore il Transform va letto come
    /// <b>locale</b>: neutro significa "sovrapposta al genitore", che è il posto da cui ci si
    /// aspetta di partire quando si crea un figlio.
    /// </param>
    public static Entity Create(World world, Entity? parent = null)
    {
        var entity = world.CreateEntity();

        world.AddComponent(entity, new NameComponent { Value = FreeName(world, DefaultName) });

        world.AddComponent(entity, new TransformComponent
        {
            // Scale a zero renderebbe l'entità invisibile e sembrerebbe un bug della
            // creazione: un transform neutro è l'unico default onesto.
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        // Solo se il genitore è vivo: un ParentComponent verso un'entità morta farebbe
        // nascere il figlio come radice (vedi HierarchyPanel.BuildTree) con dentro un
        // riferimento rotto — peggio del non averlo.
        if (parent is { } value && world.Exists(value))
            world.AddComponent(entity, new ParentComponent { Parent = value });

        return entity;
    }

    /// <summary>
    /// Crea una copia dell'entità con gli stessi componenti.
    ///
    /// La copia è <b>superficiale e per valore</b>: i componenti sono struct (o piccole
    /// class di dati), quindi <c>GetBoxed</c>+<c>SetBoxed</c> bastano — la stessa faccia
    /// non generica che usa l'Inspector, riusata qui senza conoscere un solo tipo.
    ///
    /// I componenti marcati <see cref="RuntimeStateAttribute"/> sono <b>saltati</b>:
    /// sono link a risorse esterne, e copiarli farebbe puntare due entità alla stessa
    /// (vedi <see cref="PhysicsBodyComponent"/>). Il system proprietario ricreerà il
    /// proprio stato per la copia al prossimo update.
    ///
    /// Il figlio duplicato resta figlio dello stesso genitore: <c>ParentComponent</c> è un
    /// dato normale e viene copiato. I <b>discendenti no</b>: duplicare un genitore non
    /// duplica il suo sottoalbero.
    /// </summary>
    public static Entity Duplicate(World world, Entity source)
    {
        var clone = world.CreateEntity();

        foreach (var storage in world.ComponentStorages)
        {
            if (!storage.Has(source.Id))
                continue;

            if (storage.ComponentType.IsDefined(typeof(RuntimeStateAttribute), inherit: false))
                continue;

            var component = storage.GetBoxed(source.Id);
            if (component is not null)
                storage.SetBoxed(clone.Id, component);
        }

        // Il nome è stato copiato come qualunque altro dato, ma non è un dato qualunque:
        // nel file scena identifica l'entità ed è il bersaglio dei riferimenti Parent.
        // Due omonime, una volta salvate e ricaricate, collasserebbero nella mappa
        // name→Entity di SceneInstantiator — vince l'ultima, senza un errore. Quindi la
        // copia si prende un nome libero.
        if (world.TryGetComponent<NameComponent>(clone, out var name) &&
            !string.IsNullOrWhiteSpace(name.Value))
            world.AddComponent(clone, new NameComponent { Value = MakeUniqueName(world, name.Value) });

        return clone;
    }

    /// <summary>
    /// "cubo" → "cubo (1)", e se anche quello è preso "cubo (2)", ecc. Se il nome finisce
    /// già con un suffisso del genere si riparte dalla radice, così duplicare più volte
    /// non produce "cubo (1) (1) (1)".
    ///
    /// ⚠️ Il suffisso c'è <b>sempre</b>, anche se la radice fosse libera — al contrario di
    /// <see cref="FreeName"/>. È la differenza fra le due: una copia si chiama "cubo (1)"
    /// perché è una copia, e duplicare "cubo (1)" dopo aver rinominato l'originale non deve
    /// produrre un'entità di nome "cubo" che non è l'originale di nessuno.
    /// </summary>
    private static string MakeUniqueName(World world, string name)
    {
        var taken = TakenNames(world);
        var root = StripCopySuffix(name);

        for (var i = 1; ; i++)
        {
            var candidate = $"{root} ({i})";
            if (taken.Add(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// <paramref name="preferred"/> se nessuno ce l'ha, altrimenti "preferred (n)" col primo
    /// n libero. Vedi <see cref="MakeUniqueName"/> per il perché sono due metodi e non uno.
    /// </summary>
    private static string FreeName(World world, string preferred)
    {
        var taken = TakenNames(world);
        var name = preferred;

        for (var i = 1; !taken.Add(name); i++)
            name = $"{preferred} ({i})";

        return name;
    }

    /// <summary>
    /// I nomi già in uso. I vuoti restano fuori: un'entità senza nome non "occupa" la
    /// stringa vuota, e comunque non è raggiungibile per nome nel file scena.
    /// </summary>
    private static HashSet<string> TakenNames(World world)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (_, existing) in world.Query<NameComponent>())
        {
            if (!string.IsNullOrWhiteSpace(existing.Value))
                taken.Add(existing.Value);
        }

        return taken;
    }

    private static string StripCopySuffix(string name)
    {
        var open = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (open < 0 || !name.EndsWith(')'))
            return name;

        var inner = name[(open + 2)..^1];
        return inner.Length > 0 && inner.All(char.IsDigit) ? name[..open] : name;
    }

    /// <summary>
    /// Elimina l'entità <b>e tutti i suoi discendenti</b>. Un figlio senza genitore non
    /// avrebbe un significato migliore di "sparisci anche tu": il suo transform è locale
    /// rispetto a qualcosa che non esiste più, quindi salterebbe a una posa arbitraria.
    ///
    /// La raccolta dei discendenti è iterativa e con visited set: non perché oggi ci siano
    /// cicli (una gerarchia a genitore singolo non può averne di raggiungibili da una
    /// radice), ma perché un editor permette di costruire dati che il codice di scena non
    /// costruirebbe mai, e qui un ciclo significherebbe un blocco totale.
    /// </summary>
    public static void DestroyRecursive(World world, Entity root)
    {
        // Mappa padre→figli costruita UNA volta: cercare i figli riscandendo i
        // ParentComponent a ogni nodo renderebbe l'eliminazione quadratica sul numero
        // di entità, per poi buttare via il lavoro.
        var childrenByParent = new Dictionary<int, List<Entity>>();
        foreach (var (child, parent) in world.Query<ParentComponent>())
        {
            if (!childrenByParent.TryGetValue(parent.Parent.Id, out var children))
            {
                children = [];
                childrenByParent[parent.Parent.Id] = children;
            }

            children.Add(child);
        }

        var toDestroy = new List<Entity>();
        var visited = new HashSet<int> { root.Id };
        var queue = new Queue<Entity>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            toDestroy.Add(current);

            if (!childrenByParent.TryGetValue(current.Id, out var children))
                continue;

            foreach (var child in children)
            {
                if (visited.Add(child.Id))
                    queue.Enqueue(child);
            }
        }

        foreach (var entity in toDestroy)
            world.DestroyEntity(entity);
    }
}
