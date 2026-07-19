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
    /// <param name="name">
    /// Come chiamarla, prima di renderlo libero. Serve a chi crea qualcosa di <b>riconoscibile</b>
    /// — un "Cubo", una "Luce" — perché "Nuova entità (3)" nella Hierarchy costringe a cliccarla
    /// per sapere cos'è. Resta opzionale: chi crea un'entità nuda non ha un nome migliore da
    /// dare.
    /// </param>
    public static Entity Create(World world, Entity? parent = null, string? name = null)
    {
        var entity = world.CreateEntity();

        world.AddComponent(entity, new NameComponent
        {
            Value = FreeName(world, string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim())
        });

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
    /// La copia è <b>superficiale e per valore</b>, e passa da <see cref="ComponentCopy"/>: la
    /// faccia non generica dell'Inspector (<c>GetBoxed</c>/<c>SetBoxed</c>) da sola <b>non
    /// basta</b>, perché su una class restituisce il riferimento e le due entità resterebbero
    /// legate. Vedi lì per il bug che questo ha smesso di produrre.
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

            // ⚠️ ComponentCopy e non il boxed nudo: per uno struct il boxing è già una copia,
            // per una CLASS è il riferimento — e le due entità finivano a condividere lo stesso
            // componente. Non era teorico: dipingere di rosso il MeshRenderer della copia
            // dipingeva anche l'originale (verificato, poi corretto).
            //
            // ⚠️ Da quando quel MeshRenderer è uno struct, qui NESSUN componente è una class e
            // questa riga copia una copia. Resta perché toglierla renderebbe questo ciclo
            // corretto per coincidenza — vedi ComponentCopy, dove sta il ragionamento intero.
            var component = storage.GetBoxed(source.Id);
            if (component is not null)
                storage.SetBoxed(clone.Id, ComponentCopy.Shallow(component));
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
    /// Se spostare <paramref name="child"/> sotto <paramref name="newParent"/> (o a radice, con
    /// null) è un'operazione <b>legale</b>.
    ///
    /// Esiste separata da <see cref="Reparent"/>, e non come suo valore di ritorno, perché
    /// serve <b>prima</b>: la Hierarchy la interroga mentre il trascinamento è in volo per
    /// decidere se rendere una riga un bersaglio. Un riparentamento impossibile non deve
    /// illuminarsi e poi rifiutare — deve non essere offerto.
    ///
    /// Le regole, in ordine di cosa proteggono:
    /// <list type="bullet">
    /// <item>Entità vive, e non sé stessa: un'entità figlia di sé è un ciclo di lunghezza uno.</item>
    /// <item><b>Niente cicli</b>: un genitore non può finire dentro un proprio discendente.
    /// ⚠️ Non è teorico e non è cosmetico — <c>GetWorldMatrix</c> risale i genitori
    /// ricorsivamente <b>senza guard</b>, quindi un ciclo non è un dato strano, è un blocco
    /// totale al primo frame che prova a disegnare quell'entità. È lo stesso pericolo per cui
    /// <see cref="DestroyRecursive"/> ha un visited set, visto dall'altro lato: lì ci si
    /// difende da un ciclo esistente, qui si impedisce di crearlo.</item>
    /// <item>Niente no-op: chi è già figlio di quel genitore (o già radice) non si sposta, e
    /// una riga che si illumina per non fare niente mente.</item>
    /// </list>
    /// </summary>
    public static bool CanReparent(World world, Entity child, Entity? newParent)
    {
        if (!world.Exists(child))
            return false;

        var currentParent = world.TryGetComponent<ParentComponent>(child, out var parent) &&
                            world.Exists(parent.Parent)
            ? parent.Parent
            : (Entity?)null;

        if (newParent is not { } target)
            return currentParent is not null; // a radice: solo se un genitore ce l'ha

        if (!world.Exists(target) || target == child || currentParent == target)
            return false;

        return !IsDescendantOf(world, target, child);
    }

    /// <summary>
    /// Sposta <paramref name="child"/> sotto <paramref name="newParent"/>, o a <b>radice</b> se
    /// è null — e a radice significa <b>togliere</b> il <see cref="ParentComponent"/>, non
    /// metterlo a <c>Entity(0)</c>: un componente che dice "sono figlia di un'entità che non
    /// esiste" è peggio del non averlo, e la Hierarchy lo tratterebbe comunque come radice
    /// (vedi <c>BuildTree</c>) portandosi dietro un riferimento rotto fino al salvataggio.
    ///
    /// <b>La posa di mondo è mantenuta</b>: l'entità resta visivamente dov'è, e a cambiare è il
    /// suo <see cref="TransformComponent"/>, che è <b>locale</b> rispetto al genitore. È la
    /// semantica di Unity, ed è quella giusta qui perché il trascinamento è un gesto sull'albero
    /// — chi riordina la gerarchia non sta chiedendo di spostare l'oggetto nella scena, e
    /// vederlo saltare via sembrerebbe un bug del trascinamento.
    ///
    /// ⚠️ <b>Eredita il debito noto di <see cref="WorldTransforms.SetWorldPose"/></b>: con un
    /// genitore a scala <b>non uniforme</b> la posizione resta esatta ma l'orientamento sbaglia
    /// (~90° in mediana), <b>in silenzio</b>. Non è arrotondamento: con shear il quaternione
    /// locale che darebbe quell'orientamento non esiste. Qui il caso è più raggiungibile che
    /// altrove — le camere erano root, un'entità trascinata a mano finisce dove capita.
    ///
    /// Se la posa non è ricostruibile (nessun Transform, o una world matrix non decomponibile
    /// perché un anello della catena ha scala 0) il <b>riparentamento avviene comunque</b> e la
    /// posa non si tocca: la gerarchia è ciò che è stato chiesto, la posa è la cortesia.
    /// </summary>
    /// <returns>false, e nessuna modifica, se <see cref="CanReparent"/> dice di no. Il
    /// controllo è ripetuto qui e non dato per fatto: fra il rilascio del mouse e
    /// l'applicazione del comando passa un frame, e il mondo nel frattempo ha girato.</returns>
    public static bool Reparent(World world, Entity child, Entity? newParent)
    {
        if (!CanReparent(world, child, newParent))
            return false;

        // Letta PRIMA di toccare la gerarchia: dopo, questa stessa chiamata darebbe la posa
        // nel nuovo spazio, cioè quella che stiamo cercando di non far cambiare.
        var worldMatrix = world.GetWorldMatrix(child);

        // ⚠️ Il && corto avrebbe lasciato la rotazione non assegnata quando l'entità non ha un
        // Transform: qui il valore serve solo se canRestorePose, ma il compilatore ha ragione
        // a non fidarsi di un out dietro uno short-circuit.
        var rotation = Quaternion.Identity;
        var canRestorePose = world.HasComponent<TransformComponent>(child) &&
                             Matrix4x4.Decompose(worldMatrix, out _, out rotation, out _);

        if (newParent is { } target)
            world.AddComponent(child, new ParentComponent { Parent = target });
        else
            world.RemoveComponent<ParentComponent>(child);

        if (canRestorePose)
            world.SetWorldPose(child, worldMatrix.Translation, rotation);

        return true;
    }

    /// <summary>
    /// Se <paramref name="entity"/> discende da <paramref name="ancestor"/>. Si risale la
    /// catena dei genitori invece di scendere fra i figli: verso l'alto il cammino è unico
    /// (<see cref="ParentComponent"/> tiene un solo riferimento), quindi è una passeggiata e
    /// non una visita.
    ///
    /// ⚠️ Il visited set non è per i cicli che creiamo noi — <see cref="CanReparent"/> esiste
    /// per impedirli — ma per quelli che potrebbero <b>già</b> esserci: una scena scritta a mano
    /// può contenere qualunque cosa, e senza guard il controllo che deve proteggere dal blocco
    /// sarebbe esso stesso il blocco.
    /// </summary>
    private static bool IsDescendantOf(World world, Entity entity, Entity ancestor)
    {
        var visited = new HashSet<int>();
        var current = entity;

        while (visited.Add(current.Id) &&
               world.TryGetComponent<ParentComponent>(current, out var parent))
        {
            if (parent.Parent == ancestor)
                return true;

            if (!world.Exists(parent.Parent))
                return false;

            current = parent.Parent;
        }

        return false;
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
        foreach (var entity in Descendants(world, root))
            world.DestroyEntity(entity);
    }

    /// <summary>
    /// L'entità e tutti i suoi discendenti, radice inclusa e per prima.
    ///
    /// Separato da <see cref="DestroyRecursive"/> perché serve anche a chi deve sapere
    /// <b>cosa sta per sparire</b> prima che sparisca: l'undo fotografa il sottoalbero, e dopo
    /// la distruzione non c'è più niente da fotografare. Due chiamanti sulla stessa
    /// definizione di "sottoalbero" è esattamente il caso in cui non si riscrive il giro.
    /// </summary>
    public static List<Entity> Descendants(World world, Entity root)
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

        var collected = new List<Entity>();
        var visited = new HashSet<int> { root.Id };
        var queue = new Queue<Entity>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            collected.Add(current);

            if (!childrenByParent.TryGetValue(current.Id, out var children))
                continue;

            foreach (var child in children)
            {
                if (visited.Add(child.Id))
                    queue.Enqueue(child);
            }
        }

        return collected;
    }
}
