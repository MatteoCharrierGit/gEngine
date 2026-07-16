using gEngine.Ecs.Base;
using gEngine.Ecs.Component;

namespace gEngine.Editor;

/// <summary>
/// Operazioni di struttura sulle entità che servono all'editor: duplicare, eliminare un
/// sottoalbero. Stanno qui e non nel <see cref="World"/> perché sono <b>politiche</b>, non
/// meccanismi: "duplicare significa copiare i componenti tranne quelli di runtime" e
/// "eliminare significa portarsi via i figli" sono decisioni dell'editor. Il World offre i
/// mattoni (<c>CreateEntity</c>, <c>DestroyEntity</c>, gli storage) e non ha opinioni.
/// </summary>
public static class EntityOperations
{
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
    /// </summary>
    private static string MakeUniqueName(World world, string name)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, existing) in world.Query<NameComponent>())
        {
            if (!string.IsNullOrWhiteSpace(existing.Value))
                taken.Add(existing.Value);
        }

        var root = StripCopySuffix(name);

        for (var i = 1; ; i++)
        {
            var candidate = $"{root} ({i})";
            if (taken.Add(candidate))
                return candidate;
        }
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
