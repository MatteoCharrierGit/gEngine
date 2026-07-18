# Handoff — ripresa lavori editor/ECS

> File di passaggio di consegne fra sessioni. **Non è documentazione del progetto**: quando i
> task qui sotto sono chiusi, questo file va cancellato. La documentazione vera è
> `ROADMAP.md` (decisioni e gotcha) e `USAGE.md` (come si usa l'engine).

## Dove siamo

Lavoro su branch `feat/editor-mvp`. ⚠️ Fino alla sessione dell'undo il lavoro stava **tutto nel
working tree senza commit**; ora è committato (cinque commit, ognuno verificato che compili da
solo in un worktree separato).
Build: `dotnet build gEngine.slnx --nologo -v q --no-incremental` → **0 errori, 0 warning**.
Run: `dotnet run --project samples/Sandbox` (l'editor si apre di default, F1 lo chiude).

⚠️ **I binari degli asset sono fuori da git** — decisione presa dal proprietario, niente Git
LFS. `.gitignore` esclude `assets/models/` e `assets/audio/`; i 29 MB che erano già tracciati
(`gwen.glb`, `little_witch_academia/`, l'mp3) sono stati tolti dall'indice con `git rm --cached`
e **restano su disco**. Restano tracciati apposta `assets/scenes/` (i dati di scena) e
`assets/scripts/`, che **non sono asset ma sorgenti**: dalla Fase 4.9 i system del Sandbox sono
usciti dal `.csproj` e vivono lì.
- ⚠️ `git rm --cached` **non rimpicciolisce il repo**: la storia passata contiene ancora quei
  blob. Per recuperare lo spazio servirebbe riscrivere la storia — non fatto, non deciso.
- ⚠️ **Un clone pulito parte degradato, e va detto a chi lo fa**: `demo.json` cita
  `models/little_witch_academia/scene.gltf` e `SandboxGame` carica
  `audio/Before_the_Light_Fades.mp3`. raylib su file mancante **non lancia** — logga un WARNING
  e restituisce un handle vuoto: si vede una scena senza modello e si sente silenzio, non un
  crash. Comodo, ma è anche il modo in cui un asset perso passa inosservato.
- I `.mtl` **riparati** dei modelli pesanti (path assoluti di un'altra macchina → path relativi
  veri) vivono quindi solo su disco: gli originali sono accanto come `.mtl.orig`.

### Fatto nelle sessioni precedenti

- **Split Component/Resource** (`ROADMAP.md` Fase 4.5). La regola: *i dati di scena vivono nel
  World come Component; l'infrastruttura è una Resource registrata*. La richiesta originale era
  "tutto è un Component / niente esiste fuori dal World": è stata **respinta e riscritta**,
  perché `SceneSerializer` scorre gli storage e avrebbe scritto il renderer dentro `demo.json`.
- `Resources` (`Core/Resources.cs`), `IGame.Init(Resources)`, `SystemRegistry` (`Ecs/`).
- `CameraComponent` + `World.GetCamera`/`GetPrimaryCamera`: la camera **di gioco** è un'entità
  del World; la camera **di scena dell'editor** resta fuori — asimmetria voluta.
- `[EditorConfiguration]`, traceability nell'Inspector, `World.SetWorldPose`.
- **Scheletro UI** (Fase 4.6): Top Menu Bar, `IEditorPanel`/`PanelBase`, layout a 5 pannelli.
- **Fase 4.7 (ultima sessione)**: context menu al posto dei bottoni, pannelli globali
  **Systems** e **Components**, **drag&drop tipizzato** degli asset, e la decisione su **da
  dove nasce un componente** (factory del default dichiarata nel `SceneComponentRegistry`).
  Tutto il razionale sta in `ROADMAP.md` Fase 4.7 — **leggilo prima di toccare queste cose**.
- **Fase 4.7bis**: **Play / Pausa / Stop**, con lo snapshot preso *prima* di partire (se la
  scena non è serializzabile non si parte: fallendo al Play si perde un clic, fallendo allo
  Stop si perde il lavoro). Con questo **la Fase 4 è chiusa**.

### Fatto in QUESTA sessione (i cinque commit)

1. **`ContentRoot`** — l'editor leggeva la *copia* degli asset dentro `bin/`. Quindi un file
   nuovo non si vedeva (e non bastava riavviare: serviva ricompilare) e **il Salva scriveva in
   `bin/`** invece che nel file versionato. Ora la cartella di progetto **è** la cartella asset.
2. **Supporto JPEG** — questa build di raylib non li decodifica: ricaduta su `StbImageSharp` +
   `RepairFailedAlbedo` per le texture *dentro* i modelli (che non passano da noi). È la causa
   vera del "gli obj restano bianchi". ⚠️ Solo OBJ: glTF resta scoperto.
3. **Riparentamento** (buco A) e **undo/redo** (buco B), in un commit solo perché toccano gli
   stessi file.
4. **Pannello File system a griglia** con anteprime.
5. Documentazione: `ROADMAP.md` Fase 4.8 e 4.85.

### Verità utili (imparate a caro prezzo)

- **Il "bug" dell'input da tastiera nell'Inspector NON esiste.** Un click su un widget `Drag*`
  trascina; per digitare serve **Ctrl+Click** (scorciatoia ImGui non scopribile). Verificato
  decompilando `rlImGui.dll` e pilotando la finestra viva. C'è un tooltip di scopribilità.
  Non "ri-aggiustarlo".
- **`TransformGizmo` NON ha il bug della gerarchia**: `ToParentSpace` divide già per il mondo
  del genitore. (Un commento nel codice diceva il contrario ed era falso.)
- ⚠️ **Non fidarti dei commenti**: in questo repo ne sono stati trovati **tre** che mentivano —
  e il terzo (`GetBoxed` "dà una copia", vero solo a metà) non mentiva del tutto, il che è
  peggio: era **giusto sul caso che si guardava** e sbagliato sull'altro. Verifica il codice.
- ⚠️ **Non documentare API a memoria**: è già successo di scrivere una firma inesistente in
  `USAGE.md`. Leggi il sorgente.
- ⚠️ ImGui identifica le finestre **per titolo**: due `Begin` con lo stesso nome sono lo stesso
  pannello riempito due volte, e ImGui non lo segnala.
- ⚠️ Il layout ImGui è in `imgui.ini` nella working directory (gitignorato) e **vince** sui
  default `FirstUseEver`: per provare il primo avvio, cancellalo.
- ⚠️ Gotcha struct/copia: `TryGetComponent`/`GetBoxed` danno una **copia**. Serve il write-back
  (`AddComponent`/`SetBoxed`). Ha già morso **cinque** volte.
- ⚠️⚠️ **E ha una seconda metà, che è costata un bug vero** (Fase 4.8): quella frase vale per gli
  **struct**. Se il componente è una **class** (`MeshRendererComponent`) `GetBoxed` dà **il
  riferimento** — chi lo passa a un'altra entità le lega, chi lo tiene da parte come "prima" si
  ritrova un alias che la modifica successiva sovrascrive. Per una copia indipendente:
  `ComponentCopy.Shallow`.
- ⚠️⚠️ **`BeginPopupContextItem()` dopo un `Text` fa `IM_ASSERT`** (un `Text` non ha id, e
  senza argomenti il popup usa l'id dell'ultimo item). Su Windows l'assert è una **dialog
  modale nativa**: il gioco non crasha e non logga, si **pianta al primo frame** e sembra un
  hang del game loop. Usa un `Selectable`. Costata mezza sessione — Fase 4.7.
- ⚠️ **Le stringhe passate a ImGui possono usare solo Latin-1** (`0x20–0xFF`): è quel che copre
  il font di default, tutto il resto esce `?`. Attenzione: **non** è "niente emoji" — con
  quella formulazione è passata la **lineetta lunga `—`** (U+2014), che si scrive senza
  pensarci ed era già in un tooltip della Fase 4. Il `·` (U+00B7) invece va bene. Nei
  **commenti** il `⚠️` resta; nelle stringhe: "Attenzione:", "-", "(!)". Si trovano
  scandendo i sorgenti per i caratteri `> 0xFF` nelle righe non di commento, non rileggendoli.
- ⚠️ Un bottone a larghezza piena (`-1`) seguito da `SameLine` spinge l'etichetta **fuori dal
  pannello**. Per allinearsi agli altri campi: `ImGui.CalcItemWidth()`.

### Come si guarda la UI (il rig di verifica, ricostruibile)

Il buco "i popup non si possono cliccare" **è chiuso**. Come si fa:

1. `Start-Process` dell'exe (**non** `dotnet run` sotto `timeout`: uccidendolo si perde il log
   nativo, che è bufferizzato).
2. Da PowerShell, `PostMessage` alla finestra: `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`/`UP`,
   `WM_RBUTTONDOWN`/`UP`. Non tocca il mouse dell'utente.
3. Nel gioco, codice **temporaneo** che fa `Raylib.TakeScreenshot` a frame prestabiliti, così
   gli scatti cadono fra un clic e l'altro. **Toglilo dopo** (è lo standard del progetto).
4. Il file finisce nella working directory dell'exe (`bin/Debug/net10.0`).

⚠️ Limiti del rig: il **doppio clic** sintetico non si riesce a riprodurre (rlImGui campiona
il mouse **a livello, una volta per frame**), e un trascinamento che parte da un punto vuoto
**sposta la finestra ImGui** invece dell'item.

⚠️ Due cose imparate dopo, **che smentiscono quanto scritto altrove**:
- **I tasti sintetici passano**, Ctrl+Z compreso: serve il `lParam` giusto **anche sul KEYDOWN**
  (scancode nell'HIWORD), non solo sul KEYUP. La Fase 4.7bis dava F1 per non pilotabile.
- Lo stesso campionamento a livello morde il **trascinamento**: se la pressione e i primi
  spostamenti cadono nello stesso frame, ImGui registra la pressione sulla posizione **finale** —
  si afferra la riga sbagliata e sembra che il drag&drop non funzioni. La pressione vuole un
  frame tutto suo (~900ms di margine nel rig).

Per provare il drag&drop **degli asset** si è fatto partire temporaneamente il `FileSystemPanel`
dentro la cartella del modello. Quello **fra entità** (riparentamento) non ha bisogno del
trucco: sorgente e bersaglio sono due righe della stessa Hierarchy.

## Da chiudere PRIMA del piano (buchi, non comodità)

Quattro cose trovate guardando il codice a fine sessione. Non sono desiderata: sono punti in
cui l'editor **promette** qualcosa che non fa, o in cui una cosa nuova ha reso raggiungibile un
debito vecchio. In ordine.

### A. Il riparentamento non esiste — e cinque posti dicono che c'è ✅ CHIUSO
`ParentComponent` non è esposto nell'Inspector **apposta**, col commento *«il posto dove si
riparenta è l'albero della Hierarchy»*. `SceneComponentRegistry` non gli dà un default perché
*«ci si riparenta dalla Hierarchy»*. E **due tooltip mostrati all'utente** ripetono la frase.

La Hierarchy legge `ParentComponent` solo per **costruire l'albero**. Nessun drag&drop, nessuna
voce di menu: riparentare un'entità esistente **è impossibile dall'editor** — si può solo creare
un figlio nuovo. Quindi l'editor manda l'utente a fare una cosa che non si può fare, ed è la
regola del progetto ("non lasciare che i commenti mentano") violata in cinque punti, due dei
quali scritti nella Fase 4.7.

Va chiuso il buco, non corrette le cinque frasi. Il macchinario c'è già: `BeginDragDropSource`/
`BeginDragDropTarget` sulle righe dell'albero — vedi `AssetDragDrop`, stessa meccanica con un
payload diverso (un `Entity` invece di un path).
- ⚠️ **Deve rifiutare i cicli**: trascinare un genitore dentro un suo discendente. Non è
  teorico — `EntityOperations.DestroyRecursive` ha già un visited set proprio perché *«un editor
  permette di costruire dati che il codice di scena non costruirebbe mai»*. È lo stesso
  pericolo, dall'altro lato. Il World non si difende: qui un ciclo è un blocco totale.
- ⚠️ Trascinare **fuori** (a radice) deve togliere il `ParentComponent`, non metterlo a
  `Entity(0)`.
- ⚠️ Il Transform è **locale**: riparentando, l'entità salta di posa se non si ricalcola.
  `World.SetWorldPose` esiste apposta ed è l'inverso di `GetWorldMatrix` — ma ⚠️ non onora
  l'orientamento con genitore a **scala non uniforme** (vedi il debito noto). Decidere se
  "mantieni la posa di mondo" è la semantica giusta, o se si riparenta e basta.

**Fatto** (`EntityDragDrop` + `EntityOperations.Reparent`/`CanReparent`, Hierarchy). Tutte e tre
le ⚠️ qui sopra sono state chiuse: i cicli sono rifiutati **prima** che il bersaglio si illumini,
"fuori" **toglie** il componente, e il proprietario ha deciso per **mantieni la posa di mondo**.
Il razionale, i numeri e la trappola del rig stanno in `ROADMAP.md` Fase 4 — **leggili prima di
toccare queste cose**. ⚠️ Il debito ereditato (genitore a scala non uniforme → orientamento
sbagliato in silenzio) ora è **molto più raggiungibile**: prima i genitori vivi avevano scala
uniforme, adesso ci si trascina a mano.

### B. Undo — e va fatto PRIMA del punto 2 del piano ✅ CHIUSO
Non esiste da nessuna parte. Oggi ogni azione distruttiva è definitiva: Elimina, "Rimuovi da
tutte", Svuota lo slot, ogni trascinamento del gizmo. Si sopravvive perché il disco non è
toccato — `File > Open Scene` è l'undo del poveraccio.

⚠️ **Il punto 2 (FileSystem completo) aggiunge "elimina file dal disco", e lì quella rete non
c'è più.** Costruire quel bottone senza undo vuol dire tornarci sopra dopo.

Il materiale c'è già: **lo snapshot del `PlayMode` è la serializzazione in memoria**. Un undo a
grana grossa (snapshot prima di ogni comando, stack di N) è quasi gratis con quello che esiste;
uno a grana fine (command stack) è più lavoro ma è la cosa giusta a lungo termine. ⚠️ È una
decisione da prendere **una volta**, prima di scriverne metà.

**Fatto: command stack a grana fine** (`src/gEngine.Editor/Undo/`), deciso dal proprietario.
⚠️ Lo snapshot a grana grossa **non era "quasi gratis"**: `PlayMode.Stop` è `World.Clear` +
`Instantiate`, quindi annullare la digitazione di un numero avrebbe ricostruito la scena — id
nuovi, `[RuntimeState]` persi, selezione persa, e una serializzazione che **può fallire**.
Razionale, comandi e verifiche in `ROADMAP.md` Fase 4.8 — **leggila prima di toccare queste
cose**. ⚠️ Il disco resta scoperto **per scelta**: quando il FileSystem saprà cancellare servirà
il Cestino, non lo stack (un comando in memoria non resuscita un file).

### C. Un progetto di test, con UN test: il round-trip di serializzazione ✅ CHIUSO
La Fase 0 lo prevedeva e non c'è mai stato. Non serve testare tutto: serve **World → Scene →
World, e confronta**.

Perché proprio quello: quel codice adesso regge **tre** cose insieme — il Salva, il Play/Stop
(lo snapshot *è* il serializer) e, domani, l'hot-reload (snapshot → ricompila → reistanzia).
Tre pilastri su un pezzo che nessuno controlla e che finora è stato verificato **guardando dei
cubi cadere**. È mezz'ora e copre tutto e tre.

**Fatto** (`tests/gEngine.Tests`, xUnit, 13 test verdi). ⚠️ **Ma la stima "mezz'ora e copre
tutto e tre" era sbagliata su una cosa, e va saputa prima di scrivere il prossimo test:**
il round-trip **da solo non verifica niente**. Sabotato il writer del `MeshRenderer` perché non
scrivesse più `ModelPath` — cioè facendo perdere il modello a ogni salvataggio — **i test
restavano tutti verdi**: il giro è cieco a una perdita *simmetrica* (non scritto → non
ricaricato → non riscritto → le due scene coincidono). Serve la seconda gamba, che verifica
cosa il file **contiene**. Razionale, i tre sabotaggi provati e le trappole del confronto (per
indice e non per nome; chiavi JSON canonicalizzate) stanno in `ROADMAP.md` **Fase 4.86** —
**leggila prima di aggiungere test qui**.

### D. `ISystem` non ha un `OnDestroy` 🟡
Era un debito teorico finché nessuno toglieva system. Il pannello Systems (Fase 4.7) li fa
togliere **col mouse**: togliere il `PhysicsSystem` lascia i corpi nel mondo Bepu senza che
nessuno li liberi. C'è un tooltip che lo dice, ma è una pezza — il pannello ha reso
raggiungibile un buco che prima non lo era.
- ⚠️ Stessa famiglia: "Ripristina" richiama `OnCreate` su un'istanza già creata. Oggi non morde
  perché **tutti** gli `OnCreate` sono vuoti, cioè regge per caso.

*(Restano fuori perché sono comodità e non buchi: Save As, ricerca nella Hierarchy,
multi-selezione.)*

---

## Il piano deciso dal proprietario (in ordine)

**1. Console in-editor.** Unire il logger del progetto (`Log/`, oggi `ConsoleLogger`) con una
console dentro l'editor, e **non mostrare solo gli errori** — tutto il flusso. ⚠️ Aggancio già
mezzo pronto: il `ScriptsPanel` è di fatto una console per un solo produttore, e il suo "si apre
da sé quando c'è un errore" è la regola da riusare, non da reinventare. ⚠️ Nodo aperto dalla
Fase 0 e ancora lì: *«`GameLoop` istanzia `_logger` ma non lo passa mai a `IGame`/ai system»* —
prima di avere una console serve che il logger sia raggiungibile, e le `Resources` sono il
posto (`resources.Add<ILogger>(...)`).

**2. FileSystem completo.** *(l'undo che lo bloccava ora c'è — ma copre il World, non il disco:
per "elimina file" serve il Cestino di Windows, deciso col proprietario.)*
⚠️ **Metà è fatta** (Fase 4.85): il pannello è a griglia con anteprime, e `ContentRoot` ha
chiuso il bug per cui l'editor guardava la copia degli asset in `bin/` — quindi ora un file
copiato da Explorer si vede subito **e il Salva scrive nel file versionato**. Restano le
mutazioni su disco (creare/rinominare/eliminare) e il cestino. Creare/rinominare/eliminare davvero + **creare oggetti dei tipi
basilari direttamente da lì**. ⚠️ La parte "crea oggetti" incrocia il `SceneComponentRegistry`:
un "cubo" è un'entità con Transform + MeshRenderer coi default della Fase 4.7 — cioè
`EntityOperations.Create` + due `TryCreateDefault`, non codice nuovo. ⚠️ La parte "elimina" è
quella che non ha rete. ⚠️ **L'undo ora c'è ma NON copre il disco** (il punto B è chiuso): un
comando in memoria non resuscita un file. Prima di scrivere quel bottone serve il **Cestino di
Windows**, deciso col proprietario come rete separata.

**3. Interfaccia per l'InputHandler e per i system.** L'`InputHandler` è una classe concreta e i
system se la prendono nel costruttore: è l'ultima dipendenza del gioco che non passa da una
porta. ⚠️ Tocca `ScriptDiscovery`, che risolve i parametri **per tipo esatto** dalle Resource:
il giorno che l'input si registra sotto `IInputHandler`, uno script che chiede `InputHandler`
non lo trova più — ed è giusto (chi consuma dipende dalla porta), ma va fatto in un colpo solo.

**4. Audio manager, dal punto di vista dell'UI.** Oggi l'audio è invisibile all'editor: chi
suona, cosa, a che volume, non si vede da nessuna parte — e `SandboxGame` tiene un
`_introSound` e chiama `UpdateMusic` a mano dentro `Draw`, che è codice del gioco che fa il
lavoro di un system. ⚠️ Prima di disegnare il pannello, sappi che metà delle decisioni sono
già state prese e ti aspettano:
- `AssetKind` distingue già **Sound** (effetto tutto in memoria) e **Music** (stream), perché
  li distingue l'`AssetManager` (`LoadSound` vs `LoadMusicStream`).
- ⚠️ `AssetDragDrop.Classify` mappa **tutti** gli audio a `Music` e lo **dichiara**: lo stesso
  .mp3 è l'uno o l'altro a seconda di *come lo usi*, e l'estensione non lo sa. Quella riga
  dice esplicitamente «il giorno che uno slot Sound esisterà, qui la scelta va rifatta».
  Quel giorno è questo: probabilmente il genere lo decide lo **slot** che riceve il drop, non
  il file che parte.
- `DrawAssetSlot` gestisce solo `AssetKind.Model` e lo dice (`<slot Music: non ancora
  gestito>`): il ramo c'è già, gli manca il caso.
- Manca il componente: un `AudioSource` (dato d'autore: quale clip, volume, loop) più il
  system che lo suona. Con `[GameComponent]` + `[EditorAsset(AssetKind.Music)]` nasce già
  autorabile, salvabile e trascinabile — è la prova che gli strati sotto reggono.

**5. Font dell'editor.** ⚠️ Inter **non è installato** su questa macchina: va scaricato, con
il suo file di licenza, e poi vanno ritarate le metriche di `EditorTheme` (padding/spacing/raggi
sono tarati sui 13px di ProggyClean) e riguardato tutto a video. Non è un ritocco — vedi
`ROADMAP.md` Fase 4.7bis, sezione tema, per il perché vale la pena.
- Scorciatoia trovata guardando: **Cascadia Code è già installato ed è SIL OFL**, quindi
  redistribuibile senza scaricare niente. ⚠️ Ma è **monospaziato**: non è "Inter più in
  fretta", è un editor diverso — dice *strumento tecnico* e allinea le colonne di numeri
  dell'Inspector. È una scelta, non una comodità.

**6. Poi si torna al piano originale della ROADMAP** (Fase 5 in avanti).

## Cosa resta, oltre al piano

### Ricaricamento a caldo degli script
Vedi la sezione Scripting più sotto: c'è già l'ostacolo individuato (`World.Clear()` non
lascia andare gli storage).

### Il buco del FileSystem
Vedi il punto 2 qui sopra: oggi il disco è in sola lettura.

### Scripting: la strada è decisa, il primo strato è fatto

**Deciso dal proprietario: si va sugli script come `.cs` sotto `assets/`, compilati a
runtime** (modello Unity). Ma la compilazione a runtime è lo strato **sopra**: sotto serve
comunque la scoperta per attributo, ed è quella che è stata fatta (`ROADMAP.md` Fase 4.9).

**Fatto**: `[GameComponent]` / `[GameSystem(Order)]` + `ScriptDiscovery`, che scandisce un
`Assembly` e registra da sé. Sandbox non cita più i propri script: `MovementSystem`,
`PlayerInputSystem`, `CameraFollowSystem`, `PlayerComponent` e `VelocityComponent` si
dichiarano da soli. `Player`/`Velocity` sono anche **usciti dall'engine** e vivono in
`samples/Sandbox/Components/` — erano componenti del gioco parcheggiati nel core.

**Fatto anche la compilazione a runtime** (stessa fase): `ScriptCompiler` (Roslyn, in
`gEngine.Editor`) compila i `.cs` sotto `assets/scripts/` in un assembly in memoria, e
`ScriptDiscovery` lo scandisce come qualunque altro. `MovementSystem`, `PlayerInputSystem` e
`CameraFollowSystem` **sono usciti dal `.csproj`** e vivono lì: il gioco compila senza di loro.
Gli errori li mostra il `ScriptsPanel`, che si apre da solo.

### Resta: il ricaricamento a caldo
Oggi gli script si compilano **all'avvio**: cambiarne uno vuol dire riavviare il gioco.

⚠️ **Il punto duro non è ricompilare** — quello è già scritto, basta richiamare
`ScriptCompiler.Compile`. È **cosa succede alla scena quando un tipo sparisce o cambia forma**
mentre il World ne tiene istanze. La strada è la stessa del Play/Stop: **snapshot → ricompila →
reistanzia**, e funziona perché **lo snapshot è JSON** — parla di chiavi (`"Velocity": {...}`),
non di tipi — quindi sopravvive al cambio. `PlayMode` è già metà del lavoro.

⚠️ **Ostacolo già individuato, da risolvere prima:** `World.Clear()` **non toglie gli storage**,
solo il loro contenuto. `World.Storages` è indicizzata per `Type`, quindi tiene vivi i `Type`
del vecchio assembly e ne impedisce lo scaricamento — l'`AssemblyLoadContext` è già
collezionabile, ma non collezionerebbe niente. Le strade: dare al World un modo di lasciar
andare gli storage, o accettare che ogni ricaricamento perda un assembly (Unity ci ha convissuto
per anni). **Da decidere, non da scoprire a metà lavoro.**

⚠️ Ricaricare mentre si è in **Play** è un caso a sé. Vietarlo è una risposta legittima.

⚠️ **Limiti dello strato di oggi, da non riscoprire:**
- **Gli script si compilano tutti insieme**: un errore in un file solo li porta giù tutti. È il
  compilatore C#, non una scelta dell'engine — ma va detto a chi guarda (lo dice il tooltip).
- ⚠️ **La freccia del tempo**: uno script può nominare i tipi del gioco, il gioco **non** può
  nominare quelli di uno script (quando è stato compilato non esistevano). È il motivo per cui i
  system sono diventati script e i componenti no: l'HUD di `SandboxGame` interroga
  `VelocityComponent` per nome. Se si vogliono anche i componenti negli script, il pezzo da
  spostare è l'HUD — diventerebbe un `[GameSystem] IRenderSystem`, ed è la cosa giusta.
- ⚠️ Gli **implicit usings** vanno dati a mano al compilatore (`ScriptCompiler.ImplicitUsings`)
  e le **reference sono gli assembly caricati**: una libreria che il gioco referenzia senza mai
  toccarla non c'è. Entrambe le trappole sono già state pagate una volta.
- `ScriptDiscovery.RegisterSystems` va chiamata **dove** gli script devono stare nell'ordine
  delle fasi: l'`Order` li ordina *fra loro*, non rispetto ai system che il gioco registra a
  mano. È una riga visibile in `SandboxGame.Init`, ed è voluto.
- I componenti dell'engine con binder **asimmetrici** (`MeshRenderer` path↔handle, `Parent`
  nome↔Entity) **non** passano dall'attributo e non devono: `RegisterEngineDefaults` resta.
- Un system scoperto vuole **un solo costruttore pubblico** e i suoi parametri devono essere
  **Resource dichiarate**. Altrimenti: eccezione con dentro il nome di ciò che manca.

### "Double click → apre lo script nell'IDE"
**Cassato dal proprietario**: basta aprire l'IDE sulla cartella degli asset. Non serve la
lookup tipo → file sorgente (che avrebbe voluto dire leggere il PDB).

### Task 6 — gestione file reale nel FileSystemPanel
Il pannello ora trascina, **mostra a griglia con anteprime** e vede i file nuovi senza riavviare
(vedi `ContentRoot`), ma il disco resta in **sola lettura**: niente creare / rinominare /
eliminare. ⚠️ Non è più "manca l'undo": l'undo c'è e copre il World, non il disco. Quel che
manca sotto "elimina" è il **cestino**.

### Slot per asset diversi da `Model`
`DrawAssetSlot` gestisce solo `AssetKind.Model` e **lo dice** (`<slot Texture: non ancora
gestito>`). Gli altri generi hanno handle di tipo diverso e **nessun componente li dichiara
ancora**: il ramo giusto non si può scrivere senza inventare a cosa servirebbe.

## Debito noto (dichiarato, non nascosto)

- **`SetWorldPose` non onora l'orientamento con genitore a scala NON uniforme** (~90° di errore
  mediano) e **non lo segnala**. Con shear il quaternione locale che darebbe quell'orientamento
  non esiste. ⚠️ **Non è più vero che "oggi non morde"**: da quando si riparenta trascinando
  (punto A), qualunque entità può finire sotto un genitore schiacciato — misurato su quel
  percorso: `1-|dot|` 0.47 mediano (~117°), posizione esatta.
- **La traceability è metadata scritta a mano**: può mentire. `ObservedComponents` è opzionale,
  quindi "sezione vuota" non prova che nessuno legga l'entità. Cura vera = derivare i match
  dalle query reali (refactor grosso, non fatto).
- Più camere `Primary`: vince la prima incontrata.
- **`New Scene` lascia il documento senza percorso**, e non c'è un "Save As": `SceneDocument.Save`
  lancia con un messaggio esplicito invece di scrivere a caso.
- **Popup Open / New / Save della barra dei menu**: gli unici ancora verificati per
  costruzione. Ora il rig per cliccarli c'è (vedi sopra) — non ci sono ancora passati.
- **In Editing il `PhysicsSystem` non gira**, quindi i corpi Bepu orfani lasciati da uno Stop
  restano nella simulazione finché non si preme Play di nuovo. Sono corpi fermi che nessuno
  guarda: non morde, ma è vero.
- **Uno Stop perde la selezione di un'entità senza nome**: si ritrova per nome, perché gli id
  cambiano. È lo stesso limite del formato di scena, non uno in più.
- **`NameComponent` non è aggiungibile dall'editor**: non è nel registry (nel file il nome è il
  campo `name` dell'entità, non un componente — due punti di verità divergerebbero). Un'entità
  a cui è stato tolto il nome non può riaverlo dall'Inspector. Limite stretto ma reale.
- **"Aggiungi system" non esiste**: un system ha dipendenze, non c'è un default da costruire.
  Il bottone è spento **col motivo nel tooltip**. Servirebbe che il gioco dichiarasse le
  factory dei suoi system.
- **Ripristina un system lo rimette in fondo alla sua fase**, non dov'era, e richiama
  `OnCreate` sulla stessa istanza (oggi tutti vuoti, quindi non si vede).
- ~~Niente undo, da nessuna parte.~~ **Fatto** (Fase 4.8). ⚠️ Resta scoperto il **disco**: il
  FileSystemPanel non ha ancora un cestino, e lo stack non può coprirlo.
- **Lo slot degli asset non conosce `Kind`**: un modello su un `MeshRenderer` con `Kind = Cube`
  resta assegnato e invisibile. Detto nel tooltip; vedi Fase 4.7 per perché non lo si aggiusta.
- ~~raylib non decodifica i JPEG~~ **risolto** con la ricaduta su `StbImageSharp` + la
  riparazione delle albedo dei modelli (Fase 4.85). ⚠️ Resta scoperto **glTF**: lì il path della
  texture sta nel json (o nel chunk binario del `.glb`) e ripescarlo è un parser, non una
  riparazione — un `.gltf`/`.glb` con albedo jpg viene ancora bianco.
- **Anteprime dei modelli**: non ci sono, e non è un rinvio pigro — generarle vuol dire
  caricare il modello (SummonersRift è enorme). Serve caricamento pigro con budget + cache su
  disco.
- ~~Nessun progetto di test (Fase 0).~~ **C'è** (`tests/gEngine.Tests`, Fase 4.86), ma copre
  **solo la serializzazione**. Restano scoperti: l'**ECS** (`CreateEntity`, `Query`, e
  soprattutto il gotcha struct/copia, che ha già morso cinque volte) e l'**undo/redo**, che è
  l'altro pezzo che regge tre cose e che nessuno ricontrolla. Le verifiche numeriche vecchie
  restano fatte con app scratch buttate: non c'è niente che le rieseguirebbe.

## Come si lavora qui (standard del progetto, non opzionali)

- Codice e commenti in **italiano**. I commenti spiegano il **PERCHÉ** e documentano i gotcha
  (⚠️), mai il "cosa". `ROADMAP.md` è la memoria delle decisioni: **tenerlo aggiornato, e non
  lasciarlo mentire** è parte del lavoro.
- **Verificare, non sperare**: questo progetto verifica la matematica numericamente (200k
  rotazioni, errore di proiezione a 0.24px, `SetWorldPose` su 50k pose). Una verifica che passa
  anche con l'implementazione sbagliata non sta verificando niente: prova anche l'ordine errato
  e mostra che sbaglia.
- **La UI si verifica guardandola**: `Raylib.TakeScreenshot` + aprire l'immagine. È così che è
  stato trovato un `(?) (?)` in un ramo mai renderizzato, e in Fase 4.7 un assert che piantava
  il gioco al primo frame e uno slot rimasto senza etichetta. Rimuovere il codice temporaneo dopo.
- **Onestà**: riportare quello che non funziona, non una patch plausibile e non verificata.
