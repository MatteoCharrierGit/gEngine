using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using gEngine.Assets;
using gEngine.Ecs;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Ecs.Interfaces;
using gEngine.Editor.Undo;
using gEngine.Rendering;
using ImGuiNET;
using Color = gEngine.Rendering.Color;

namespace gEngine.Editor.Panels;

/// <summary>
/// Mostra e modifica i componenti dell'entità selezionata nell'<see cref="EditorContext"/>.
///
/// È <b>reflection-driven</b> per necessità, non per gusto: l'Inspector deve saper mostrare
/// anche un componente definito fuori dall'engine (es. <c>PlayerComponent</c> di Sandbox),
/// e un pannello con i campi scritti a mano andrebbe modificato dentro l'engine ogni volta
/// che un gioco inventa un componente. È lo stesso vincolo che ha dato la forma al registry
/// delle scene: l'engine non può conoscere l'elenco dei tipi di componente.
///
/// La reflection resta automatica (un componente nuovo compare senza registrare nulla), ma
/// <b>cosa</b> mostrare è ora una scelta esplicita di chi scrive il componente: si vedono
/// solo i membri marcati <see cref="EditorConfigurationAttribute"/>. Prima si vedeva tutto
/// il pubblico, cioè il default era "esponi" e nessuno lo aveva deciso.
///
/// Il prezzo (invariato) è che i tipi supportati sono un elenco chiuso — quelli fuori elenco
/// si vedono in sola lettura invece di sparire.
/// </summary>
public class InspectorPanel() : PanelBase("Inspector", Vector2.Zero, new Vector2(340, 600))
{
    // Primo avvio: a destra, di fronte alla Hierarchy. Calcolata qui e non passata al
    // costruttore perché quando i pannelli nascono la taglia del viewport non c'è ancora.
    protected override Vector2 DefaultPosition => new(ImGui.GetMainViewport().Size.X - 360, 40);

    protected override void DrawContent(World world, EditorContext context, IRenderer renderer)
    {
        if (context.Selected is not { } entity || !world.Exists(entity))
        {
            ImGui.TextDisabled("Nessuna entità selezionata.");
            return;
        }

        TrackEditing(world, context, entity);

        ImGui.Text($"Entity {entity.Id}");
        ImGui.Separator();

        // La rimozione è rimandata a dopo il ciclo: togliere un componente mentre si
        // scorrono gli storage è innocuo (gli storage sono per tipo, non per entità),
        // ma applicarla fuori dal ciclo lo rende evidente invece che da dimostrare.
        IComponentStorage? removeFrom = null;

        foreach (var storage in world.ComponentStorages)
        {
            if (!storage.Has(entity.Id))
                continue;

            if (DrawComponent(entity, context, storage))
                removeFrom = storage;
        }

        if (removeFrom is { } removed)
        {
            context.Undo.Run(world, entity, $"rimuovi {DisplayName(removed.ComponentType)}",
                () => removed.Remove(entity.Id));
        }

        ImGui.Spacing();
        DrawAddComponent(world, context, entity);

        // Dopo aggiunta e rimozione, non prima: cambiare i componenti cambia quali system
        // agiscono sull'entità, e l'elenco deve dire com'è adesso — non com'era un istante fa.
        DrawTraceability(world, context, entity);
    }

    /// <summary>
    /// Lo stato dell'entità nell'ultimo istante in cui <b>nessuno stava editando</b>: è il
    /// "prima" di un'eventuale modifica. Null solo prima del primo frame utile.
    /// </summary>
    private EntitySnapshot? _resting;

    private Entity _restingEntity;

    /// <summary>Se dall'ultimo istante fermo qualcuno ha toccato qualcosa.</summary>
    private bool _edited;

    /// <summary>
    /// Trasforma le scritture continue dell'Inspector in <b>un</b> comando annullabile per
    /// gesto.
    ///
    /// Il problema che risolve: un <c>DragFloat</c> riscrive il componente a <b>ogni frame</b>
    /// finché lo trascini. Registrare lì darebbe sessanta comandi al secondo, ognuno che disfa
    /// un millimetro — cioè un annulla inutilizzabile, che è peggio di non averlo. Il confine
    /// giusto è il <b>gesto</b>: da quando afferro un campo a quando lo lascio.
    ///
    /// Il gesto si riconosce da <c>IsAnyItemActive</c> letto <b>prima</b> di disegnare, cioè
    /// riferito al frame precedente: se nessuno era attivo, lo stato di adesso è uno stato
    /// "fermo" e vale come "prima". Non è per pannello e non per widget: un <c>Vector3</c> sono
    /// tre campi e una posa è una cosa sola, quindi trascinare X e poi Y dà due annulla — che è
    /// quel che ci si aspetta — ma un singolo trascinamento ne dà uno.
    ///
    /// ⚠️ Lo stato fermo si rifotografa <b>ogni frame</b>, ed è voluto anche se sembra
    /// sprecato: fra un'edizione e l'altra la stessa entità può essere cambiata da qualcun
    /// altro (un gizmo, la Hierarchy, un annulla), e un "prima" tenuto da parte diventerebbe
    /// vecchio senza dirlo — l'annulla successivo riporterebbe indietro <b>anche</b> ciò che
    /// non era stato toccato qui. Costa una manciata di copie superficiali per frame, contro
    /// un frame di ImGui intero: non è il posto in cui questo editor spende.
    /// </summary>
    private void TrackEditing(World world, EditorContext context, Entity entity)
    {
        var editing = ImGui.IsAnyItemActive();

        // Gesto finito: dal "prima" a com'è adesso, un comando solo.
        if (_edited && !editing)
        {
            _edited = false;

            if (_resting is { } before && _restingEntity == entity)
            {
                var command = EntityStateCommand.Between(world, entity, $"modifica {Name(world, entity)}", before);

                if (command.ChangedSomething)
                    context.Undo.Push(command);
            }
        }

        // Fuori da un gesto, "adesso" è il prossimo "prima".
        if (!_edited)
        {
            _resting = EntitySnapshot.Capture(world, entity);
            _restingEntity = entity;
        }
    }

    /// <summary>
    /// Segnala che il gesto in corso ha <b>toccato</b> qualcosa. Lo chiamano i punti che
    /// scrivono di continuo (i campi, lo slot degli asset); aggiunta e rimozione di un
    /// componente no — quelle sono azioni istantanee e si registrano da sé.
    /// </summary>
    private void MarkEdited() => _edited = true;

    private static string Name(World world, Entity entity)
    {
        return world.TryGetComponent<NameComponent>(entity, out var name) &&
               !string.IsNullOrWhiteSpace(name.Value)
            ? name.Value
            : $"Entity {entity.Id}";
    }

    private const string AddComponentPopup = "aggiungi-componente";

    /// <summary>
    /// "Aggiungi componente": l'elenco dei tipi che il gioco ha registrato, meno quelli che
    /// l'entità ha già.
    ///
    /// È un <b>bottone</b> e non un menu contestuale come la rimozione, e non è
    /// un'incoerenza: un menu contestuale si trova solo se sai che c'è, e questa è l'azione
    /// principale del pannello — quella per cui non esiste un altro modo. La rimozione un
    /// altro modo ce l'ha (togliere il componente è raro e chi lo cerca ha già l'header
    /// sotto il puntatore), aggiungere no.
    ///
    /// ⚠️ L'elenco viene dal <c>SceneComponentRegistry</c>, cioè dai componenti che il gioco
    /// dichiara per le sue scene. Non è "tutti i tipi che esistono" e non può esserlo:
    /// l'engine non conosce i componenti dei giochi, e cercarli scandendo gli assembly
    /// caricati offrirebbe di aggiungere anche i tipi di una libreria di terze parti.
    /// </summary>
    private static void DrawAddComponent(World world, EditorContext context, Entity entity)
    {
        // Largo quanto il pannello: è l'azione principale, e un bottone a misura di testo
        // in mezzo a una colonna di header si legge come un dettaglio di un componente.
        if (ImGui.Button("Aggiungi componente", new Vector2(-1f, 0f)))
            ImGui.OpenPopup(AddComponentPopup);

        if (!ImGui.BeginPopup(AddComponentPopup))
            return;

        // Il registry è del gioco: può non esserci. Stessa regola della traceability — "non
        // lo so" non è "nessun componente", e un elenco vuoto qui direbbe una cosa falsa.
        if (context.Components is not { } registry)
        {
            ImGui.TextDisabled("Registry dei componenti non disponibile.");
            HelpMarker(
                "Il gioco non ha dichiarato un SceneComponentRegistry fra le sue Resource.\n" +
                "L'editor non ha modo di sapere quali tipi di componente esistono: non è\n" +
                "\"nessuno\".");

            ImGui.EndPopup();
            return;
        }

        var offered = 0;

        foreach (var component in registry.RegisteredComponents)
        {
            // Già addosso: aggiungerlo di nuovo lo sovrascriverebbe col default, cioè
            // butterebbe via i valori che l'utente ha appena messo nei campi qui sopra.
            if (world.HasComponent(entity, component.Type))
                continue;

            offered++;

            if (!component.CanCreateDefault)
            {
                // Spento e non assente: "questo componente esiste ma nessuno ha detto come
                // nasce" è l'informazione che serve a chi si chiede perché non lo trova.
                ImGui.TextDisabled(component.Key);
                HelpMarker(
                    $"'{component.Key}' è registrato per le scene ma non dichiara un valore\n" +
                    "di default, quindi l'editor non sa come crearne uno.\n\n" +
                    "Per i riferimenti (Parent) è voluto: un genitore di default non esiste,\n" +
                    "ci si riparenta dalla Hierarchy. Per gli altri si dichiara il default\n" +
                    "alla registrazione - vedi SceneComponentRegistry.TryCreateDefault.");

                continue;
            }

            if (!ImGui.Selectable(component.Key))
                continue;

            if (registry.TryCreateDefault(component.Type, out var created))
            {
                context.Undo.Run(world, entity, $"aggiungi {component.Key}",
                    () => world.AddComponent(entity, created));
            }

            ImGui.CloseCurrentPopup();

            // RegisteredComponents è ricalcolata a ogni accesso e il World è appena cambiato:
            // non si continua a iterare una sequenza il cui presupposto non vale più.
            break;
        }

        if (offered == 0)
            ImGui.TextDisabled("Nessun altro componente registrato da aggiungere.");

        ImGui.EndPopup();
    }

    /// <summary>
    /// "Processata da": i system che agiscono sull'entità selezionata, e — a parte — quelli
    /// che la leggono per agire altrove.
    ///
    /// È la metà diagnostica dell'Inspector: i componenti dicono <i>cos'è</i> l'entità, questo
    /// dice <i>chi la tocca</i>. Insieme rispondono alla domanda che si fa davvero davanti a
    /// un editor — "perché questa cosa non si muove?" — che con i soli componenti si risponde
    /// solo andando a rileggere il codice dei system.
    ///
    /// La fase è mostrata accanto al nome perché è metà della risposta: un system che c'è ma
    /// gira in Render non muoverà mai niente, e senza la fase l'elenco lo nasconde.
    ///
    /// ⚠️ Ricalcolato ogni frame, come tutto il resto del pannello. È un confronto fra due
    /// manciate di tipi per una sola entità: nell'ordine di grandezza dell'albero della
    /// Hierarchy, che si ricostruisce ogni frame senza che nessuno se ne accorga.
    /// </summary>
    private static void DrawTraceability(World world, EditorContext context, Entity entity)
    {
        ImGui.SeparatorText("Processata da");

        // Il registry è del gioco: può non esserci. "Non lo so" non è "nessun system" — una
        // lista vuota qui direbbe una cosa falsa, e per giunta rassicurante.
        if (context.Systems is not { } registry)
        {
            ImGui.TextDisabled("Registry dei system non disponibile.");
            HelpMarker(
                "Il gioco non ha dichiarato un SystemRegistry fra le sue Resource.\n" +
                "L'editor non ha modo di sapere quali system girano: non è \"nessuno\".");
            return;
        }

        var acting = registry.SystemsActingOn(entity).ToList();

        if (acting.Count == 0)
            ImGui.TextDisabled("Nessun system dichiara di agire su questa entità.");

        foreach (var registered in acting)
            DrawSystemRow(registered);

        // "Legge": chi guarda questa entità per agire su un'ALTRA. Sezione separata e non
        // fusa sopra, perché la differenza è tutto il punto — un system che legge il player
        // non lo muoverà mai, e vederlo sotto "agisce su" manderebbe a cercare il bug nel
        // posto sbagliato. Niente riga quando è vuoto: qui il silenzio è il caso normale
        // (quasi nessun system legge entità che non tocca), e un "nessuno" ogni volta
        // sarebbe rumore su tutte le entità della scena.
        var observing = registry.SystemsObserving(entity).ToList();

        if (observing.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Legge (senza agirci)");
            HelpMarker(
                "Questi system leggono questa entità per decidere cosa fare a un'altra:\n" +
                "non la modificano.\n\n" +
                "Esempio: CameraFollowSystem legge il player e muove la camera. Se la\n" +
                "camera non segue, il system è questo - ma il bug non è nel player.");

            foreach (var registered in observing)
                DrawSystemRow(registered, observed: true);
        }

        // ⚠️ Chi non dichiara MatchedComponents NON è "non agisce": è "non si sa". Sono qui,
        // in una sezione loro, invece che assenti (mentirebbe per omissione) o mescolati
        // sopra (mentirebbe per eccesso). La distinzione esiste apposta — vedi SystemMatch.
        var unknown = registry.Systems
            .Where(registered => SystemRegistry.MatchOn(registered.System, world, entity) == SystemMatch.Unknown)
            .ToList();

        if (unknown.Count > 0)
        {
            ImGui.Spacing();

            // ⚠️ "Non si sa" e non "Non si sa (?)": il (?) lo aggiunge HelpMarker qui sotto,
            // e scriverlo anche nell'etichetta lo raddoppiava ("Non si sa (?) (?)"). Il ramo
            // non si era mai disegnato — tutti i system di Sandbox dichiarano
            // MatchedComponents — quindi il refuso è vissuto finché non si è registrato un
            // system fittizio senza dichiarazioni apposta per fotografarlo.
            ImGui.TextDisabled("Non si sa");
            HelpMarker(
                "Questi system non dichiarano MatchedComponents: non si può sapere su chi\n" +
                "agiscono senza farli girare. Potrebbero toccare questa entità o no.");

            foreach (var registered in unknown)
                DrawSystemRow(registered, unknownMark: true);
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Elenco dichiarativo");
        HelpMarker(
            "Ricavato da ciò che i system DICHIARANO, non da ciò che fanno: è\n" +
            "un'approssimazione utile, non una prova. Vale per entrambe le sezioni.\n\n" +
            "MatchedComponents e ObservedComponents sono un solo insieme in AND ciascuno,\n" +
            "quindi un system con più query dichiara solo l'insieme principale per verbo.\n" +
            "Chi scrive altro (PhysicsSystem scrive anche PhysicsBodyComponent,\n" +
            "DetectCollision2DSystem tocca gli eventi di collisione) non compare qui per\n" +
            "quelle entità.\n\n" +
            "\"Legge\" non è più affidabile di \"Agisce su\", è meno: è opzionale e nessuno\n" +
            "verifica che sia scritta. Un system che legge questa entità senza dichiararlo\n" +
            "è indistinguibile da uno che non la legge - la sezione vuota non è una prova\n" +
            "che nessuno la guardi.");
    }

    /// <param name="unknownMark">Un system che non dichiara nulla: marcato "?" e non "•".</param>
    /// <param name="observed">Legge e non agisce: marcato "o" e non "•". Niente emoji: il
    /// font di default di ImGui non ha i glifi e disegnerebbe un tofu.</param>
    private static void DrawSystemRow(RegisteredSystem registered, bool unknownMark = false,
        bool observed = false)
    {
        var name = registered.System.GetType().Name;

        // Il marcatore ripete l'intestazione della sezione apposta: le sezioni sono vicine e
        // scorrono via, la riga no.
        if (unknownMark)
            ImGui.Text($"?  {name}");
        else if (observed)
            ImGui.Text($"o  {name}");
        else
            ImGui.BulletText(name);

        // Il ToString di un [Flags] dà già "Input, Simulation" per chi sta in due fasi: la
        // formattazione a mano sarebbe stata solo un modo più lungo di ottenere lo stesso.
        ImGui.SameLine();
        ImGui.TextDisabled($"[{registered.Phases}]");
    }

    /// <summary>
    /// Il "(?)" con la spiegazione sotto il puntatore. Sta su una riga già disegnata
    /// (<c>SameLine</c>): è una nota a piè di riga, non una voce dell'elenco.
    /// </summary>
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

    /// <returns>true se l'utente ha chiesto di rimuovere il componente.</returns>
    private bool DrawComponent(Entity entity, EditorContext context, IComponentStorage storage)
    {
        var type = storage.ComponentType;

        var boxed = storage.GetBoxed(entity.Id);
        if (boxed is null)
            return false;

        ImGui.PushID(type.FullName ?? type.Name);

        var open = ImGui.CollapsingHeader(DisplayName(type), ImGuiTreeNodeFlags.DefaultOpen);

        // Il destro sull'header, al posto del bottone "X" che stava a fine riga. Quel bottone
        // era piazzato con un SameLine su una coordinata calcolata a mano, quindi ballava col
        // padding del tema e si sovrapponeva ai nomi lunghi; e una "X" senza etichetta accanto
        // a un header è già un indovinello. Subito dopo l'item: BeginPopupContextItem senza id
        // usa l'id dell'ultimo disegnato, cioè l'header.
        var removeRequested = false;

        if (ImGui.BeginPopupContextItem())
        {
            if (ImGui.MenuItem("Rimuovi componente"))
                removeRequested = true;

            ImGui.EndPopup();
        }

        if (open)
        {
            var changed = false;
            var members = ExposedMembers(type);

            if (members.Length == 0)
                ImGui.TextDisabled("Nessuna proprietà esposta.");

            // |= e non ||=: tutti i campi vanno disegnati comunque, non solo fino al primo
            // che cambia.
            foreach (var member in members)
                changed |= DrawMember(member, context, boxed);

            // Il write-back. Se il componente è uno struct, `boxed` è una COPIA: le
            // SetValue di sopra hanno mutato la scatola, non lo storage. Senza questa
            // riga l'Inspector sembrerebbe funzionare e non salverebbe niente — è lo
            // stesso gotcha struct/copia dei system. Se invece è una class (es.
            // MeshRendererComponent) la mutazione è già in loco e questa è una riscrittura
            // dello stesso riferimento: innocua, e ci evita di distinguere i due casi.
            if (changed)
            {
                storage.SetBoxed(entity.Id, boxed);

                // Il gesto in corso ha toccato qualcosa: al suo termine TrackEditing ne farà
                // UN comando. Qui non si registra niente apposta - siamo dentro il frame di
                // un trascinamento, e questa riga gira sessanta volte al secondo.
                MarkEdited();
            }
        }

        ImGui.PopID();
        return removeRequested;
    }

    /// <returns>true se il campo è stato modificato.</returns>
    private static bool DrawMember(EditorMember member, EditorContext context, object target)
    {
        var type = member.ValueType;
        var label = member.Label;
        var value = member.Get(target);

        // Prima della sola lettura: uno slot non è modificabile a mano per definizione (è un
        // handle), ma lo è per trascinamento. Fosse dopo, si vedrebbe il numero grezzo.
        if (member.Asset is { } kind)
            return DrawAssetSlot(member, context, target, kind);

        // Sola lettura senza bisogno di un flag nell'attributo: una proprietà senza setter
        // lo dice già da sé, ed è la stessa strada dei tipi fuori elenco qui sotto.
        if (!member.CanWrite)
            return DrawReadOnly(label, value);

        if (type == typeof(float))
        {
            var v = (float)value!;
            var edited = ImGui.DragFloat(label, ref v, 0.05f);
            DragHint();
            if (!edited) return false;
            member.Set(target, v);
            return true;
        }

        if (type == typeof(int))
        {
            var v = (int)value!;
            var edited = ImGui.DragInt(label, ref v);
            DragHint();
            if (!edited) return false;
            member.Set(target, v);
            return true;
        }

        if (type == typeof(bool))
        {
            var v = (bool)value!;
            if (!ImGui.Checkbox(label, ref v)) return false;
            member.Set(target, v);
            return true;
        }

        if (type == typeof(Vector3))
        {
            var v = (Vector3)value!;
            var edited = ImGui.DragFloat3(label, ref v, 0.05f);
            DragHint();
            if (!edited) return false;
            member.Set(target, v);
            return true;
        }

        if (type == typeof(Quaternion))
        {
            // Mostrato in gradi: vedi EulerAngles per convenzione e limiti.
            var euler = EulerAngles.ToDegrees((Quaternion)value!);
            var edited = ImGui.DragFloat3($"{label} (°)", ref euler, 1f);
            DragHint();
            if (!edited) return false;
            member.Set(target, EulerAngles.ToQuaternion(euler));
            return true;
        }

        if (type == typeof(Color))
        {
            var c = (Color)value!;
            var v = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            if (!ImGui.ColorEdit4(label, ref v)) return false;

            // I campi di Color sono readonly: non si mutano uno a uno. Si ricostruisce il
            // valore e si riassegna il campo INTERO del componente — che è comunque la
            // regola generale qui, per struct annidati.
            member.Set(target, new Color(ToByte(v.X), ToByte(v.Y), ToByte(v.Z), ToByte(v.W)));
            return true;
        }

        if (type == typeof(string))
        {
            var v = (string?)value ?? string.Empty;
            if (!ImGui.InputText(label, ref v, 128)) return false;
            member.Set(target, v);
            return true;
        }

        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var current = Array.IndexOf(names, value!.ToString());
            if (!ImGui.Combo(label, ref current, names, names.Length)) return false;
            member.Set(target, Enum.Parse(type, names[current]));
            return true;
        }

        // Fuori elenco (Entity, ModelHandle, ...): mostrato ma non editabile. Sono
        // riferimenti, non numeri — un DragInt sull'id di un'entità è un piede nel
        // fucile, non una feature. Serviranno widget dedicati (un picker).
        return DrawReadOnly(label, value);
    }

    /// <summary>
    /// Uno <b>slot</b> per un asset: una casella che dice quale file c'è dentro e in cui si
    /// trascina un file dal pannello File system.
    ///
    /// Il valore vero del campo è un handle (un id opaco), ma quello che si mostra è il
    /// <b>path</b>: è il dato d'autore, l'unico che significhi qualcosa fra un avvio e
    /// l'altro, ed è ciò che finisce nel file di scena. L'editor fa il giro path→handle al
    /// drop e handle→path per mostrarlo, così i due lati non si incontrano mai nell'UI.
    ///
    /// ⚠️ Oggi sa fare i <see cref="AssetKind.Model"/> e basta. Non è un caso mancante da
    /// riempire con un default: gli altri generi hanno handle di tipo diverso
    /// (<c>TextureHandle</c>, <c>SoundHandle</c>) e nessun componente li dichiara ancora,
    /// quindi il ramo giusto non si può scrivere senza inventare a cosa servirebbe. Lo slot
    /// lo dice invece di far finta di funzionare.
    /// </summary>
    /// <returns>true se il campo è stato riempito da un drop.</returns>
    private static bool DrawAssetSlot(EditorMember member, EditorContext context, object target,
        AssetKind kind)
    {
        if (kind != AssetKind.Model)
        {
            DrawReadOnly(member.Label, $"<slot {kind}: non ancora gestito>");
            return false;
        }

        // L'AssetManager è la sola cosa che sa tradurre fra path e handle. Senza, lo slot non
        // può né dire cosa c'è dentro né accettare un drop: si degrada e lo dice.
        if (context.Assets is not { } assets)
        {
            DrawReadOnly(member.Label, "<AssetManager non disponibile>");
            return false;
        }

        var handle = member.Get(target) is ModelHandle current ? current : ModelHandle.None;

        var path = handle.IsValid && assets.TryGetModelPath(handle, out var relative)
            ? relative
            // Valido ma senza path: la cache dei modelli è indicizzata per path, quindi
            // succede solo a un handle costruito a mano. Dirlo è meglio di mostrare "nessuno"
            // su un campo pieno.
            : handle.IsValid
                ? $"<handle {handle.Id}, path sconosciuto>"
                : "(nessun modello)";

        // Button e non Text: serve un item vero, con un id e un'area — è quella che diventa
        // il bersaglio del drop. Che non faccia niente al clic è voluto: il picker "sfoglia"
        // non c'è, si trascina.
        //
        // ⚠️ CalcItemWidth e non -1 (tutta la riga): è la stessa larghezza che si prendono i
        // DragFloat e i Combo qui sopra, cioè quella che lascia il posto all'etichetta a
        // destra. Con -1 il bottone arrivava al bordo e il SameLine spingeva "Model" fuori
        // dal pannello: lo slot restava senza nome, che su un campo il cui contenuto è un
        // path lungo lo rendeva indistinguibile da una riga di testo qualunque.
        //
        // Il testo visibile è il path, l'id è l'etichetta (##): senza, cambiare modello
        // cambierebbe l'id del bottone, e con lui l'identità del suo menu contestuale.
        ImGui.Button($"{path}##{member.Label}", new Vector2(ImGui.CalcItemWidth(), 0f));

        // Letto subito: IsItemHovered parla dell'<b>ultimo</b> item, e fra qui e il tooltip
        // ci finiscono in mezzo l'etichetta e il menu.
        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip);

        var changed = false;

        if (AssetDragDrop.Target(AssetKind.Model, out var dropped))
        {
            // ⚠️ Qui si carica davvero il modello, dentro il frame di disegno: è una upload
            // GPU nel mezzo del BeginFrame/EndFrame. Regge perché siamo fuori da ogni blocco
            // 3D (i viewport hanno già finito) e perché succede una volta sola per path —
            // l'AssetManager tiene la cache. Un file che raylib non digerisce non lancia: dà
            // un modello vuoto e lo scrive nel log.
            member.Set(target, assets.LoadModel(dropped));
            changed = true;
        }

        // Il destro per svuotare: senza, un modello assegnato per sbaglio non si toglie più
        // — non c'è nessun file "vuoto" da trascinarci sopra.
        if (ImGui.BeginPopupContextItem($"slot-{member.Label}"))
        {
            if (ImGui.MenuItem("Svuota", null, false, handle.IsValid))
            {
                member.Set(target, ModelHandle.None);
                changed = true;
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(member.Label);

        if (hovered)
            ImGui.SetTooltip(
                $"Trascina qui un asset di tipo {kind} dal pannello File system.\n" +
                "Tasto destro per svuotare.\n\n" +
                "Attenzione: Riempire questo campo non basta a vederlo: a decidere cosa si disegna è\n" +
                "Kind, qui sopra. Con Kind = Cube il modello resta assegnato e invisibile.");

        return changed;
    }

    /// <summary>
    /// Il suggerimento "Ctrl+Click per digitare" sui campi numerici.
    ///
    /// ⚠️ Gotcha, e non nostro: <b>un clic semplice su un Drag* non apre il campo di testo</b>
    /// — trascina e basta. Per <i>digitare</i> un valore ImGui vuole il <b>Ctrl+Click</b>, ed
    /// è tutto qui il "non si riesce a scrivere numeri nei fields": non un bug della catena
    /// di input, ma una scorciatoia che non si vede da nessuna parte.
    ///
    /// Verificato a mano guidando la finestra con eventi sintetici (PostMessage), perché il
    /// sospetto ovvio era un altro e andava scartato: i caratteri arrivano eccome in
    /// <c>io.InputQueueCharacters</c>, un clic su un <c>InputText</c> lo attiva e ci si
    /// scrive dentro (il campo Nome dell'Inspector: <c>player</c> → <c>player-EDIT</c>,
    /// write-back incluso). Col Ctrl+Click anche i Drag accettano la digitazione. La catena
    /// ImGui/rlImgui è sana, mancava solo il cartello.
    ///
    /// Non c'è un <c>ImGuiSliderFlags</c> che dica "clic = digita": l'unico flag in tema è
    /// <c>NoInput</c>, che toglie pure il Ctrl+Click. E sostituire i Drag con degli
    /// <c>InputFloat</c> pagherebbe il trascinamento — che su un transform è il modo normale
    /// di lavorare. Quindi si tiene il Drag e si dice come si fa.
    ///
    /// <c>ForTooltip</c> e non un <c>IsItemHovered()</c> nudo: rispetta il ritardo e la
    /// regola del puntatore fermo, così il cartello non sfarfalla addosso a chi trascina.
    /// </summary>
    private static void DragHint()
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip("Trascina per cambiare · Ctrl+Click per digitare");
    }

    /// <summary>
    /// TextWrapped e non TextDisabled: il ToString di un record struct è lungo e sborderebbe
    /// dal pannello invece di andare a capo.
    /// </summary>
    /// <returns>Sempre false: non si è modificato niente, per definizione.</returns>
    private static bool DrawReadOnly(string label, object? value)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped($"{label}: {value}");
        ImGui.PopStyleColor();
        return false;
    }

    /// <summary>
    /// I membri di <paramref name="type"/> che l'editor deve mostrare — marcati
    /// <see cref="EditorConfigurationAttribute"/> (un valore) oppure
    /// <see cref="EditorAssetAttribute"/> (uno slot per un asset) — nell'ordine in cui sono
    /// dichiarati.
    ///
    /// La cache non è per la reflection in sé ma perché il risultato è <b>immutabile per
    /// tipo</b> e questo gira per ogni componente di ogni entità selezionata, ogni frame:
    /// tenerlo sarebbe stato comunque ovvio.
    /// </summary>
    private static EditorMember[] ExposedMembers(Type type)
    {
        return MemberCache.GetOrAdd(type, static t => t
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(TryDescribe)
            .OfType<EditorMember>()
            .ToArray());
    }

    private static readonly ConcurrentDictionary<Type, EditorMember[]> MemberCache = new();

    /// <returns>null se il membro non è esposto all'editor (il caso normale).</returns>
    private static EditorMember? TryDescribe(MemberInfo member)
    {
        // Lo slot vince sul valore quando ci sono entrambi: un handle mostrato come numero
        // non serve a nessuno (vedi EditorAssetAttribute), quindi fra le due dichiarazioni
        // quella che porta informazione è sempre questa.
        var asset = member.GetCustomAttribute<EditorAssetAttribute>();
        var configuration = member.GetCustomAttribute<EditorConfigurationAttribute>();

        if (asset is null && configuration is null)
            return null;

        var label = asset?.Label ?? configuration?.Label ?? member.Name;
        var kind = asset?.Kind;

        switch (member)
        {
            // Un campo readonly non si edita: SetValue lo permetterebbe pure, ma un valore
            // che il codice dichiara immutabile non diventa mutabile perché passa dall'UI.
            case FieldInfo field:
                return new EditorMember(label, field.FieldType, !field.IsInitOnly,
                    field.GetValue, field.SetValue, kind);

            // Le proprietà passano dal loro accessor, quindi un setter con logica dentro
            // (validazione, clamp, invalidazione) viene rispettato — è il motivo per cui
            // supportarle vale la riga in più: un componente che si difende da un valore
            // assurdo non può farlo con un campo nudo.
            case PropertyInfo property when property.CanRead:
                return new EditorMember(label, property.PropertyType,
                    property.CanWrite && property.SetMethod is { IsPublic: true },
                    target => property.GetValue(target),
                    (target, value) => property.SetValue(target, value), kind);

            // Marcata ma inservibile (proprietà senza getter, metodo, ...): l'attributo è
            // una dichiarazione d'intento e va onorata o segnalata, non ignorata in
            // silenzio — altrimenti si passa la giornata a chiedersi perché il campo non
            // compare.
            default:
                return new EditorMember(label, typeof(void), false,
                    static _ => "<non leggibile>", static (_, _) => { }, kind);
        }
    }

    /// <summary>
    /// Un campo o una proprietà, visti uguali dall'UI. L'astrazione esiste perché
    /// <see cref="EditorConfigurationAttribute"/> vale su entrambi: senza, decorare una
    /// proprietà compilerebbe e non mostrerebbe niente — una trappola silenziosa, che è
    /// esattamente ciò che l'attributo doveva togliere di mezzo.
    /// </summary>
    /// <param name="Asset">
    /// Il genere di asset, se il membro è uno <b>slot</b> (<see cref="EditorAssetAttribute"/>)
    /// e non un valore. Null è il caso normale.
    /// </param>
    private readonly record struct EditorMember(
        string Label,
        Type ValueType,
        bool CanWrite,
        Func<object, object?> Get,
        Action<object, object?> Set,
        AssetKind? Asset);

    private static byte ToByte(float normalized)
    {
        return (byte)Math.Clamp(MathF.Round(normalized * 255f), 0f, 255f);
    }

    /// <summary>"MeshRendererComponent" → "MeshRenderer": il suffisso è rumore, qui sono tutti componenti.</summary>
    private static string DisplayName(Type type)
    {
        const string suffix = "Component";
        return type.Name.EndsWith(suffix, StringComparison.Ordinal) && type.Name.Length > suffix.Length
            ? type.Name[..^suffix.Length]
            : type.Name;
    }
}
