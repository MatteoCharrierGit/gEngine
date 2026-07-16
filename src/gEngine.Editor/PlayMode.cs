using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Scenes;

namespace gEngine.Editor;

/// <summary>In quale dei tre stati è l'editor. Vedi <see cref="PlayMode"/>.</summary>
public enum PlayState
{
    /// <summary>Si sta autorando: i system di gioco <b>non girano</b>, la scena sta ferma.</summary>
    Editing,

    /// <summary>Il gioco gira. Esiste uno snapshot a cui Stop può tornare.</summary>
    Playing,

    /// <summary>Il gioco è congelato a metà. Lo snapshot c'è ancora: si può riprendere o fermare.</summary>
    Paused,
}

/// <summary>
/// Play / Pausa / Stop: far girare il gioco dentro l'editor <b>senza perdere la scena</b>.
///
/// È l'ultimo pezzo della Fase 4, e la ragione per cui è arrivato buon ultimo è che dipende
/// da tutto il resto. Play/Stop è fatto di due metà:
/// <list type="number">
///   <item><b>il gating</b> — "non far girare i system quando non si sta giocando". Serviva un
///   posto solo dove i system vivono: è il <c>SystemRegistry</c>, nato apposta (Fase 4.5).
///   Il gating vero non è qui: è nel gioco, che è l'unico a chiamare le fasi. Qui c'è solo la
///   verità su cui decide, <see cref="ShouldSimulate"/>;</item>
///   <item><b>lo snapshot</b> — "rimetti tutto com'era". Non è una funzione nuova: è la
///   <b>serializzazione, in memoria invece che su file</b>. Giocare e poi fermarsi è un
///   Salva-senza-file seguito da un Apri-senza-file. È il motivo per cui il salvataggio
///   inverso (Fase 3) valeva la pena anche prima che qualcuno volesse premere Salva.</item>
/// </list>
///
/// <b>Lo snapshot si prende PRIMA di partire, e se fallisce non si parte.</b> Non è un
/// dettaglio di implementazione, è la scelta che rende la cosa sicura: una scena non
/// serializzabile (un componente senza writer, un genitore senza nome) romperebbe lo Stop,
/// cioè si scoprirebbe di non poter tornare indietro <i>dopo</i> aver giocato — quando il
/// danno è già fatto. Fallendo al Play si perde un clic; fallendo allo Stop si perde il
/// lavoro. Il pannello Components dice le stesse cose ancora prima, a riposo.
///
/// ⚠️ Lo snapshot passa <b>per il formato del file</b>, quindi ciò che il formato non
/// rappresenta non torna indietro. Non è un caso limite da ricordare: è esattamente ciò che
/// il file non salverebbe, ed è già documentato in <c>SceneSerializer</c>. In pratica:
/// <list type="bullet">
///   <item>i componenti <c>[RuntimeState]</c> (il corpo Bepu) non sono salvati e non sono
///   ripristinati — li <b>ricrea il system che li possiede</b> al primo update, ed è la
///   cosa giusta: un corpo fisico dello snapshot punterebbe a un corpo che non c'è più;</item>
///   <item>gli <b>id delle entità cambiano</b>: lo Stop distrugge e ricrea. Da qui il giro
///   per ritrovare la selezione (<see cref="_selectedName"/>) — un <c>Entity</c> tenuto da
///   parte da chiunque, dopo lo Stop, è invalido e non "sbagliato" (gli id non si riusano).</item>
/// </list>
/// </summary>
public sealed class PlayMode
{
    /// <summary>
    /// La scena com'era all'istante del Play. <c>null</c> in Editing: non è "lo snapshot
    /// vuoto", è che in Editing non c'è niente da cui tornare — è già il punto di partenza.
    /// </summary>
    private Scene? _snapshot;

    /// <summary>
    /// Il nome dell'entità selezionata al Play, per ritrovarla dopo lo Stop.
    ///
    /// Si tiene il <b>nome</b> e non l'<c>Entity</c> perché lo Stop ricrea tutto e gli id
    /// cambiano — il nome è l'unica identità che sopravvive al giro, ed è già così che i
    /// riferimenti fra entità attraversano il file (vedi il binder di <c>Parent</c>).
    ///
    /// ⚠️ Un'entità senza nome non si ritrova: la selezione si perde e basta. È lo stesso
    /// limite del formato, non uno in più.
    /// </summary>
    private string? _selectedName;

    public PlayState State { get; private set; } = PlayState.Editing;

    /// <summary>
    /// Se i system di gioco devono girare in questo tick. Da leggere <b>prima</b> di
    /// chiamare le fasi — vedi <c>EditorHost.ShouldSimulate</c>, che ci aggiunge il caso
    /// dell'editor chiuso.
    ///
    /// ⚠️ Vale per Input/Simulation/Late e <b>non</b> per Render: in Editing la scena si deve
    /// vedere, ferma. Chi disegna non è gated da nessuna parte, ed è voluto.
    /// </summary>
    public bool ShouldSimulate => State == PlayState.Playing;

    /// <summary>
    /// Perché non si può premere Play, o <c>null</c> se si può.
    ///
    /// Restituisce il <b>motivo</b> e non un bool: l'UI deve poter mostrare un comando spento
    /// e dire perché. Un Play spento e muto manda a cercare la causa nel posto sbagliato — è
    /// la stessa regola dei componenti senza default.
    /// </summary>
    public string? BlockedReason(EditorContext context)
    {
        if (State != PlayState.Editing)
            return null;

        if (context.Components is null)
            return "Il gioco non ha dichiarato un SceneComponentRegistry fra le sue Resource.\n" +
                   "Senza, l'editor non sa serializzare la scena - cioè non sa prendere lo\n" +
                   "snapshot, e senza snapshot Stop non potrebbe riportare niente indietro.\n\n" +
                   "Play è spento apposta: farlo girare senza rete sarebbe peggio.";

        if (context.Assets is null)
            return "AssetManager non disponibile fra le Resource: serve a scrivere i path\n" +
                   "degli asset nello snapshot (un handle non sopravvive al giro).";

        return null;
    }

    /// <summary>
    /// Perché l'ultimo <see cref="Start"/> non è partito, o <c>null</c>. Chi preme Play lo
    /// mostra; chi entra in Play per altre vie (F1) lo lascia lì per la barra dei menu.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Prende lo snapshot e fa partire il gioco. Da Paused riprende soltanto, senza toccare
    /// lo snapshot già preso.
    ///
    /// ⚠️ <b>Non lancia, torna false</b> e mette il motivo in <see cref="LastError"/>. Non è
    /// per pigrizia: questo metodo lo chiamano due posti (il bottone Play e F1), e un'eccezione
    /// obbligherebbe il secondo a fare da parafulmine per un errore che non sa mostrare. Il
    /// contratto che conta resta l'altro: <b>se torna false non è partito niente</b>, lo stato
    /// è ancora Editing e la scena non si è mossa.
    /// </summary>
    /// <returns>false se non è partito: la scena non è serializzabile, o manca ciò che serve.</returns>
    public bool Start(World world, EditorContext context)
    {
        if (State == PlayState.Paused)
        {
            State = PlayState.Playing;
            return true;
        }

        if (State == PlayState.Playing)
            return true;

        if (BlockedReason(context) is { } reason)
        {
            LastError = reason;
            return false;
        }

        var registry = context.Components!;
        var assets = context.Assets!;

        // ⚠️ L'ordine è il punto: se ToScene lancia, State resta Editing e non è successo
        // niente. Mettere `State = Playing` una riga più su vorrebbe dire un gioco che gira
        // senza snapshot — esattamente lo scenario da cui questa classe esiste per difendere.
        //
        // source: null — i _comment del file non c'entrano con uno snapshot in memoria (nel
        // World non ci sono mai stati). Restano in document.Source, che Play/Stop non tocca:
        // un salvataggio dopo un giro di Play li ritrova.
        try
        {
            _snapshot = SceneSerializer.ToScene(world, registry, assets, "snapshot");
        }
        catch (Exception ex)
        {
            // Il caso vero: un componente senza writer, un genitore senza nome. Il pannello
            // Components lo dice già a riposo — qui è l'ultima rete prima di far muovere una
            // scena che poi non si potrebbe rimettere a posto.
            LastError = ex.Message;
            return false;
        }

        _selectedName = context.Selected is { } selected && world.Exists(selected) &&
                        world.TryGetComponent<NameComponent>(selected, out var name)
            ? name.Value
            : null;

        LastError = null;
        State = PlayState.Playing;
        return true;
    }

    /// <summary>Congela senza buttare lo snapshot: da qui si può riprendere o fermare.</summary>
    public void Pause()
    {
        if (State == PlayState.Playing)
            State = PlayState.Paused;
    }

    /// <summary>
    /// Ferma e <b>rimette la scena com'era</b> al Play.
    ///
    /// Non lancia: lo Stop deve funzionare sempre. Ciò che poteva andare storto (la scena non
    /// serializzabile) è già stato scoperto al Play — è tutto il senso di prendere lo
    /// snapshot prima.
    /// </summary>
    public void Stop(World world, EditorContext context)
    {
        State = PlayState.Editing;

        if (_snapshot is not { } snapshot)
            return;

        _snapshot = null;

        // Non dovrebbe capitare: senza questi due non si sarebbe entrati in Play. Ma il gioco
        // resta libero di smontare le proprie Resource mentre l'editor guarda, e uno Stop che
        // esplode lascerebbe una scena mezza distrutta.
        if (context.Components is not { } registry || context.Assets is not { } assets)
            return;

        // Prima di Clear: la selezione punterebbe a un'entità distrutta. Vedi MainMenuBar,
        // che fa lo stesso prima di caricare — è la stessa operazione.
        context.ClearSelection();

        world.Clear();
        SceneInstantiator.Instantiate(snapshot, world, registry, assets);

        // I corpi fisici delle entità appena distrutte non si liberano qui: se ne accorge il
        // PhysicsSystem, che li trova senza entità e li toglie dalla simulazione. ⚠️ Ma in
        // Editing quel system non gira, quindi la pulizia arriva al prossimo Play. Nel
        // frattempo sono corpi fermi che nessuno guarda.
        Reselect(world, context);
    }

    private void Reselect(World world, EditorContext context)
    {
        if (_selectedName is not { } name)
            return;

        _selectedName = null;

        // Lineare, ma succede una volta per Stop: è la stessa scansione che fa
        // SceneInstantiator per costruire la sua mappa dei nomi, e non vale un indice.
        foreach (var (entity, current) in world.Query<NameComponent>())
        {
            if (current.Value != name)
                continue;

            context.Select(entity);
            return;
        }
    }
}
