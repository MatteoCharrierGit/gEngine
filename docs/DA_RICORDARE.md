# gEngine — Da ricordare

> **A cosa serve questo file.** Le cose che, se le dimentichi, **le riscopri pagandole**. Non è
> documentazione dell'engine (quella è [`USAGE.md`](USAGE.md)) e non è la lista dei lavori
> (quella è [`ROADMAP.md`](ROADMAP.md)): è l'elenco delle trappole già scattate almeno una
> volta, dei limiti accettati con cognizione, e delle decisioni che **non vanno ri-discusse**
> ogni volta che qualcuno rilegge il codice.
>
> Ogni voce qui dentro è costata tempo vero a qualcuno. Il racconto completo di ciascuna sta in
> [`DECISIONI.md`](DECISIONI.md), alla fase indicata.

---

## Come si lavora qui

Non sono preferenze: sono lo standard del progetto.

- **Codice e commenti in italiano.** I commenti spiegano il **PERCHÉ** e segnalano i gotcha
  (⚠️), **mai** il "cosa".
- **La documentazione è parte del lavoro.** `DECISIONI.md` è la memoria del perché, `ROADMAP.md`
  è l'aperto. Tenerli aggiornati — e **non lasciarli mentire** — conta quanto il codice.
- **Onestà**: si riporta quello che non funziona, non una patch plausibile e non verificata.

### Verificare, non sperare

Questo progetto verifica la matematica **numericamente**: 200k rotazioni, errore di proiezione
misurato a 0.24px, `SetWorldPose` su 50k pose.

⚠️ **Una verifica che passa anche con l'implementazione sbagliata non sta verificando niente.**
Prova anche l'ordine errato e **mostra che sbaglia**. È una regola con dei numeri dietro: per
`SetWorldPose` l'ordine giusto sbaglia di `2.5e-5`, quello opposto di `675` — sette ordini di
grandezza, quindi la verifica discrimina davvero.

⚠️⚠️ **Il round-trip da solo NON verifica niente** — misurato, non temuto. Sabotato il writer
del `MeshRenderer` perché non scrivesse più `ModelPath` (cioè facendo **perdere il modello a
ogni salvataggio**), i test restavano **tutti verdi**: il giro è cieco a una perdita
*simmetrica* — non scritto → non ricaricato → non riscritto → le due scene coincidono. Serve la
seconda gamba, che verifica cosa il file **contiene**. `DECISIONI.md` Fase 4.86.

### La UI si verifica guardandola

`Raylib.TakeScreenshot` + aprire l'immagine. È così che sono stati trovati un `(?) (?)` in un
ramo mai renderizzato, un assert che piantava il gioco al primo frame, e uno slot rimasto senza
etichetta. ⚠️ Il codice temporaneo degli screenshot **va tolto dopo**.

### ⚠️ Non fidarti dei commenti, non documentare a memoria

- In questo repo sono stati trovati **tre commenti che mentivano**. Il terzo (`GetBoxed` "dà una
  copia") non mentiva del tutto, **il che è peggio**: era giusto sul caso che si stava
  guardando e sbagliato sull'altro. **Verifica il codice.**
- È già successo di scrivere in `USAGE.md` **una firma che non esiste**. Leggi il sorgente.

---

## ECS

### ⚠️⚠️ Il gotcha struct/copia — ha morso cinque volte

`TryGetComponent` / `GetBoxed` su un componente **`struct`** danno una **copia**. Mutarla non
tocca lo storage: serve il **write-back** con `AddComponent` / `SetBoxed` (che fanno da upsert).

Senza write-back **niente segnala l'errore**: il codice sembra funzionare e non salva. Ha morso
`MovementSystem` (write-back parziale che azzerava `Scale`/`Rotation`), l'Inspector, il gizmo, e
altri due.

✅ **Da Fase 4.94 è sotto test** (`ComponentStorageTests`), incluso il giro per reflection che
fa l'Inspector. Sabotando `SetBoxed` diventano rossi 7 test.

### ⚠️ …e ha una seconda metà, che è costata un bug vero

Quella frase vale per gli **struct**. Se un componente fosse una **`class`**, `GetBoxed`
restituirebbe **il riferimento**:

- chi lo passa a una seconda entità **lega le due**;
- chi lo tiene da parte come "il valore di prima" si ritrova un **alias** che la modifica
  successiva sovrascrive.

⚠️ **Oggi nessun componente è una class** — `MeshRendererComponent` era l'ultimo ed è diventato
struct (Fase 4.94). Quindi questa metà **non morde adesso**. Resta scritta perché l'engine non
vieta a nessuno di dichiararne una domani, e perché spiega perché il codice intorno è fatto
così: `ComponentCopy.Shallow` è **ridondante e si tiene**, per non rendere `Duplicate` e gli
snapshot dell'undo corretti *per coincidenza*. Il ragionamento intero è nel commento di
`ComponentCopy`; il bug storico in `DECISIONI.md` Fase 4.8.

### La regola, in una riga

**Un componente è uno `struct`.** Se ti serve farne uno `class`, quello che ti serve davvero è
quasi sempre una Resource — e se proprio dev'essere un componente, prima leggi le due metà qui
sopra e i tre chiamanti di `ComponentCopy`.

### Altre regole del World

- **`Query<T>` è tipizzata e senza boxing**: i system passano di lì, non da `GetBoxed` (che è
  la faccia per l'editor, che non conosce i tipi a compile time).
- **Un componente è dati puri, senza logica.** La world matrix si ricava con un extension
  method (`GetLocalMatrix`), non con un metodo sul componente.
- **Niente lista figli**: la gerarchia esiste solo come `ParentComponent` che punta in su. Un
  solo punto di verità.
- **`default(T)` è un default rotto, non neutro**: `Transform` con `Scale = 0` è invisibile,
  `Light` con `Intensity = 0` non illumina. Per questo il default di un componente è
  **dichiarato** (`CreateDefault()` trovato per convenzione) e non costruito con
  `Activator.CreateInstance`. `DECISIONI.md` Fase 4.7.
- **`World = Local * ParentWorld`** — locale del figlio a **sinistra**, per la convenzione
  row-vector di `System.Numerics`. È l'**opposto** del `Parent * Local` di OpenGL.

---

## ImGui

Tutte queste falliscono **in silenzio**: nessun errore, nessun log.

### ⚠️⚠️ `OpenPopup` e `BeginPopupModal` devono stare allo stesso livello di ID stack

Altrimenti **la modale non si apre e basta**. Il caso vivo: `OpenPopup("X")` chiamata da dentro
un menu contestuale (che è a sua volta un popup) con `BeginPopupModal("X")` a livello di
finestra — **dalla barra degli strumenti funzionava, dalla stessa voce nel menu no**, e i due
rami rileggendoli sono identici. Si rimanda l'apertura al livello di finestra con un campo.
`DECISIONI.md` Fase 4.88.

### ⚠️⚠️ `BeginPopupContextItem()` dopo un `Text` fa `IM_ASSERT`

Un `Text` non ha id, e senza argomenti il popup usa l'id dell'ultimo item. Su Windows l'assert è
una **dialog modale nativa**: il gioco non crasha e non logga, si **pianta al primo frame** e
sembra un hang del game loop. Usa un `Selectable`. **Costata mezza sessione.**

### ⚠️ Le stringhe passate a ImGui possono usare solo Latin-1 (`0x20–0xFF`)

È quel che copre il font di default; tutto il resto esce `?`.

⚠️ **Non è "niente emoji"** — con quella formulazione è passata la **lineetta lunga `—`**
(U+2014), che si scrive senza pensarci. Il `·` (U+00B7) invece va bene. Nei **commenti** il ⚠️
resta; nelle **stringhe**: `"Attenzione:"`, `"-"`, `"(!)"`. Si trovano **scandendo i sorgenti**
per i caratteri `> 0xFF` nelle righe non di commento, **non rileggendoli**.

### Altre

- ⚠️ ImGui identifica le finestre **per titolo**: due `Begin` con lo stesso nome sono lo stesso
  pannello riempito due volte, **e ImGui non lo segnala**.
- ⚠️ Il layout è in **`imgui.ini`** nella working directory (gitignorato) e **vince** sui default
  `FirstUseEver`: per provare il primo avvio, **cancellalo**.
- ⚠️ Un bottone a larghezza piena (`-1`) seguito da `SameLine` spinge l'etichetta **fuori dal
  pannello**. Per allinearsi agli altri campi: `ImGui.CalcItemWidth()`.
- ⚠️ **`TextColored` non manda a capo**: una riga lunga viene **troncata a destra**, in
  silenzio. Nella Console spariva il path dell'asset mancante — cioè l'unica ragione per cui
  quel messaggio esisteva. Per testo colorato *e* a capo serve `PushStyleColor(ImGuiCol.Text)`
  + `TextWrapped` + `PopStyleColor`. Vale per qualunque testo di lunghezza non nota.
### Due bug che non esistono — non "ri-aggiustarli"

- **L'input da tastiera nell'Inspector.** Un click su un widget `Drag*` **trascina**; per
  digitare serve **Ctrl+Click** — scorciatoia ImGui non scopribile, verificata decompilando
  `rlImGui.dll` e pilotando la finestra viva. C'è un tooltip di scopribilità apposta.
- **La gerarchia nel `TransformGizmo`.** `ToParentSpace` divide **già** per il mondo del
  genitore. Un commento nel codice diceva il contrario **ed era falso** — è uno dei tre.

---

## Il rig per guardare la UI

I popup **si possono cliccare**. Come si fa:

1. `Start-Process` dell'exe — **non** `dotnet run` sotto `timeout`: uccidendolo si perde il log
   nativo, che è bufferizzato.
2. Da PowerShell, `PostMessage` alla finestra: `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`/`UP`,
   `WM_RBUTTONDOWN`/`UP`. Non tocca il mouse dell'utente.
3. Nel gioco, codice **temporaneo** che fa `Raylib.TakeScreenshot` a frame prestabiliti, così
   gli scatti cadono fra un clic e l'altro. **Toglilo dopo.**
4. Il file finisce nella working directory dell'exe (`bin/Debug/net10.0`).

⚠️ **Il titolo della finestra del Sandbox è `Game`**, non "gEngine Sandbox":
`FindWindow(null, "gEngine Sandbox")` restituisce **0** e i `PostMessage` vanno a un handle
nullo — cioè non succede niente, **indistinguibile da "il codice non funziona"**. Sono costati
tre giri a vuoto. Usa **`Process.MainWindowHandle`**.

### Limiti del rig

- ⚠️ **Il doppio clic sintetico non si riproduce**: rlImGui campiona il mouse **a livello, una
  volta per frame**.
- ⚠️ Lo stesso campionamento morde il **trascinamento**: se la pressione e i primi spostamenti
  cadono nello stesso frame, ImGui registra la pressione sulla posizione **finale** — si afferra
  la riga sbagliata e sembra che il drag&drop non funzioni. **La pressione vuole un frame tutto
  suo** (~900ms di margine).
- Un trascinamento che parte da un punto vuoto **sposta la finestra ImGui** invece dell'item.
- ✅ **I tasti sintetici passano**, Ctrl+Z compreso — serve il `lParam` giusto **anche sul
  KEYDOWN** (scancode nell'HIWORD), non solo sul KEYUP. *(Un documento precedente diceva che F1
  non era pilotabile: era falso.)*
- ⚠️ **Uno scatto programmato "al frame N" non è "al secondo N/60"**: il contatore parte col
  primo `Draw`, ma il caricamento degli asset ruba **~3 secondi** prima. Un frame 600 cade a
  ~13s, non a 10 — e se il rig chiude il gioco a 11s lo screenshot semplicemente non esiste,
  il che assomiglia molto a un rig rotto.

---

## Rendering e math

- ⚠️ **`Raylib.DrawMesh` vuole matrici column-major**, `System.Numerics.Matrix4x4` è row-major →
  la world matrix va **trasposta** prima della chiamata nativa. Verificato:
  `raylib == transpose(numerics)`.
- ⚠️ Il namespace è **`gEngine.MathUtils`**, non `gEngine.Math`: quest'ultimo farebbe shadowing
  di `System.Math` in tutto il progetto.
- Dentro un viewport dell'editor si disegna su un **render target grande quanto il pannello**:
  serve `GetRenderWidth/Height`, **non** `GetScreenWidth/Height`, o l'aspect della finestra fa
  cullare le entità sbagliate (visibili ai lati, scartate lo stesso).
- **Quel che si disegna dev'essere quel che si clicca**: `MeshRenderSystem` e `EntityPicker`
  condividono `GetRenderMatrix` **apposta**. Se divergono, si clicca di striscio e nessuno se ne
  accorge.
- Il wireframe della mesh mostra anche le **diagonali** delle facce (il cubo è triangolato) —
  diverso dai 12 spigoli del vecchio `DrawCubeWires`.

---

## Scripting

⚠️ **La freccia del tempo.** Uno script può nominare i tipi del gioco; il gioco **non** può
nominare quelli di uno script (quando è stato compilato non esistevano). È il motivo per cui i
**system** sono diventati script e i **componenti** no: l'HUD di `SandboxGame` interroga
`VelocityComponent` per nome. Se si vogliono anche i componenti negli script, il pezzo da
spostare è **l'HUD** — diventerebbe un `[GameSystem] IRenderSystem`, ed è la cosa giusta.

- ⚠️ **Gli script si compilano tutti insieme**: un errore in un file solo li porta giù tutti. È
  il compilatore C#, non una scelta dell'engine — ma va **detto a chi guarda** (lo dice il
  tooltip).
- ⚠️ Gli **implicit usings** vanno dati a mano al compilatore (`ScriptCompiler.ImplicitUsings`),
  e le **reference sono gli assembly caricati**: una libreria che il gioco referenzia senza mai
  toccarla **non c'è**. Entrambe già pagate una volta.
- `ScriptDiscovery.RegisterSystems` va chiamata **dove** gli script devono stare nell'ordine
  delle fasi: l'`Order` li ordina *fra loro*, non rispetto ai system registrati a mano. È una
  riga visibile in `SandboxGame.Init`, ed è voluto.
- Un system scoperto vuole **un solo costruttore pubblico**, e i suoi parametri devono essere
  **Resource dichiarate**. Altrimenti: eccezione col nome di ciò che manca.
- I componenti dell'engine con binder **asimmetrici** (`MeshRenderer` path↔handle, `Parent`
  nome↔Entity) **non** passano dall'attributo e non devono: `RegisterEngineDefaults` resta.

---

## Logging

- ⚠️ **"Console" significa due cose diverse.** `ConsoleLogSink` è lo **stdout del processo**
  (`System.Console`); la console **in-editor** è un pannello, quindi un sink diverso registrato
  sullo stesso `Logger`. Il tipo si chiamava `ConsoleLogger` ed è stato rinominato **prima** che
  le due cose coesistessero, non dopo.
- ⚠️ **Il logger non tiene storia, per scelta.** La soglia è una regola sola e sta nel `Logger`;
  la storia è un bisogno di **chi guarda**, e vive nel sink che ne ha bisogno. Conseguenza da
  ricordare: un sink registrato tardi **non vede l'avvio**.
- ⚠️ **Senza sink non lancia e non avvisa**, sempre per scelta: far cadere il gioco perché
  nessuno ascolta i log sarebbe sproporzionato. Ma è anche il modo in cui "non vedo i miei log"
  diventa un mistero — **il primo posto da guardare è chi ha chiamato `AddSink`**, non il
  chiamante di `Info`.
- ⚠️ **Non è thread-safe, ed è voluto**: il gioco è a thread singolo e i sink si registrano al
  setup. Il giorno che qualcosa logga da un thread di lavoro, si cambia `Logger` — **non i
  chiamanti**.
- ⚠️ **Il logger si registra PRIMA di `InitWindow`**, al contrario di tutte le altre Resource.
  Non è una svista: gli altri servizi non *possono* esistere a finestra chiusa (risorse GPU), il
  logger sì — e registrarlo con loro renderebbe muto proprio l'avvio, cioè il momento in cui
  qualcosa va storto più facilmente e in cui il log è l'unica cosa che parla.
- ⚠️ `Resources.Add<ILogger>(logger)` e **non** `Add(logger)`: la chiave è `typeof(T)`, quindi
  il secondo lo registrerebbe sotto `Logger` e ogni `Get<ILogger>()` fallirebbe — compreso
  quello di `ScriptDiscovery` quando riempie il costruttore di un system. C'è un test apposta.
- Con lo stdout **rediretto su file** i colori di `ConsoleLogSink` finiscono nel file come
  sequenze VT (`ESC[7m`). In un terminale vero non si vede; leggendo un log catturato, sì.

### Dove si logga, e dove no

Il criterio non è "log ovunque": è **dove qualcosa fallisce in silenzio**. Oggi sono tre punti
(Fase 4.92) e vale la pena sapere quali, perché sono anche i tre modi in cui questo engine ti
fa cercare nel posto sbagliato:

- **Asset mancante** (`Warning`) — raylib non lancia, dà un handle vuoto. Sembra "la scena è
  fatta male", è "il file non c'è".
- **Shader non compilato** (`Error`) — raylib ricade sul default. Sembra **un problema di
  luci**, è lo shader che non c'è. Il messaggio lo dice apposta.
- **Corpi fisici orfani** (`Debug`) — nessun sintomo visibile, corpi che collidono da fantasmi.

⚠️ **Prima di aggiungere una riga di log in un percorso che gira per frame, guarda la
frequenza.** Il log della fisica sta sotto una guardia `Count > 0` perché quel metodo gira a
60 Hz: incondizionato non sarebbe "verboso", sarebbe una console inutilizzabile e un logger che
costa quanto la simulazione.

## Gli asset non sono nel repo

Decisione del proprietario: **niente Git LFS**. `.gitignore` esclude `assets/models/` e
`assets/audio/`; i 29 MB già tracciati sono stati tolti con `git rm --cached` e **restano su
disco**. Restano tracciati apposta `assets/scenes/` e `assets/scripts/`, che **non sono asset ma
sorgenti**.

- ⚠️ **`git rm --cached` non rimpicciolisce il repo**: la storia contiene ancora quei blob.
  Servirebbe riscrivere la storia — **non fatto, non deciso**.
- ⚠️⚠️ **Un clone pulito parte degradato, e va detto a chi lo fa.** `demo.json` cita
  `models/little_witch_academia/scene.gltf` e `SandboxGame` carica un `.mp3`. **raylib su file
  mancante non lancia**: logga un WARNING e restituisce un handle vuoto. Si vede una scena senza
  modello e si sente silenzio, **non un crash**. Comodo — ed è anche il modo in cui un asset
  perso passa inosservato.
- I `.mtl` **riparati** dei modelli pesanti (path assoluti di un'altra macchina → path relativi
  veri) vivono quindi **solo su disco**; gli originali sono accanto come `.mtl.orig`.

---

## Limiti accettati (dichiarati, non nascosti)

Non sono bug da aggiustare al volo: sono compromessi presi sapendo cosa si perdeva. Cambiarli è
una decisione, non una correzione.

- ⚠️ **`SetWorldPose` non onora l'orientamento con genitore a scala NON uniforme**, e **non lo
  segnala**. Con shear, il quaternione locale che darebbe quell'orientamento **non esiste**.
  ⚠️ **Non è più vero che "oggi non morde"**: da quando si riparenta trascinando, qualunque
  entità può finire sotto un genitore schiacciato — misurato su quel percorso, `1-|dot|` **0.47
  mediano (~117°)**, posizione esatta.
- **Il disco non è coperto dall'undo, e non lo sarà**: un comando in memoria non resuscita un
  file. La rete lì è **il cestino del sistema**, e la modale di conferma lo dice. Chi non ha un
  cestino **non elimina** — la ricaduta non è `File.Delete`, è il comando spento col motivo.
- **La traceability dell'Inspector è metadata scritta a mano**: **può mentire**.
  `ObservedComponents` è opzionale, quindi "sezione vuota" **non prova** che nessuno legga
  l'entità. La cura vera sarebbe derivare i match dalle query reali — refactor grosso, non fatto.
- **Più camere `Primary`: vince la prima incontrata.** Documentato, non imposto.
- **Uno Stop perde la selezione di un'entità senza nome**: si ritrova per nome, perché gli id
  cambiano. È lo stesso limite del formato di scena, non uno in più.
- **Lo slot degli asset non conosce `Kind`**: un modello assegnato a un `MeshRenderer` con
  `Kind = Cube` resta assegnato **e invisibile**. Detto nel tooltip; il perché non lo si
  aggiusta sta in `DECISIONI.md` Fase 4.7.

---

## Decisioni da non ri-litigare

Sono state prese una volta, con un motivo. Se le si vuole cambiare, si cambia il motivo — non si
riparte dalla domanda.

- **"Tutto è un Component / niente esiste fuori dal World"** è stata **respinta e riscritta**:
  `SceneSerializer` scorre gli storage e avrebbe scritto **il renderer dentro `demo.json`**. La
  regola è: *i dati di scena vivono nel World come Component; l'infrastruttura è una Resource
  registrata.* `DECISIONI.md` Fase 4.5.
- **La camera di gioco è un'entità del World; la camera di scena dell'editor resta fuori.**
  Asimmetria **voluta**.
- **Undo a grana fine (command stack), non snapshot.** Lo snapshot a grana grossa **non era
  "quasi gratis"**: `PlayMode.Stop` è `World.Clear` + `Instantiate`, quindi annullare la
  digitazione di un numero avrebbe ricostruito la scena — id nuovi, `[RuntimeState]` persi,
  selezione persa, e una serializzazione **che può fallire**. `DECISIONI.md` Fase 4.8.
- **Lo snapshot del Play si prende *prima* di partire**: fallendo al Play si perde un clic,
  fallendo allo Stop si perde il lavoro.
- **Il riparentamento mantiene la posa di mondo** (deciso dal proprietario), rifiuta i cicli
  **prima** che il bersaglio si illumini, e trascinare "fuori" **toglie** il `ParentComponent`
  invece di metterlo a `Entity(0)`.
- **Gizmi scritti a mano**, non ImGuizmo: nessun binding compatibile con ImGui.NET 1.91 +
  rlImgui-cs.
- **Un catalogo solo per "crea oggetto"** (`SceneObjects`), condiviso fra Hierarchy e File
  system: così il cubo **non può nascere diverso** a seconda di dove lo si chiede.
- **`OnDestroy` non fa `Dispose` del mondo fisico**: il mondo fisico è una **Resource del
  gioco**, non roba del system. `DECISIONI.md` Fase 4.87.
- **"Doppio clic → apre lo script nell'IDE" è cassato** dal proprietario: basta aprire l'IDE
  sulla cartella degli asset. Avrebbe voluto dire leggere il PDB per la lookup tipo → file.
