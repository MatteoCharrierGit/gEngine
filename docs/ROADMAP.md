# gEngine — Cosa resta da fare

> **Questo file è solo l'aperto.** Niente racconti di ciò che è già stato fatto: quelli stanno
> in [`DECISIONI.md`](DECISIONI.md), e ogni voce qui sotto ci rimanda quando il contesto serve.
>
> - Perché una cosa è stata fatta così → [`DECISIONI.md`](DECISIONI.md)
> - Cosa non va dimenticato mentre si lavora → [`DA_RICORDARE.md`](DA_RICORDARE.md)
> - Come si usa l'engine → [`USAGE.md`](USAGE.md)
>
> **Regola:** una voce si chiude qui **e** si racconta in `DECISIONI.md`. Chiuderla solo qui
> perde il perché; raccontarla solo lì lascia questo file a mentire.

---

## Stato

Aggiornato al **19 luglio 2026**, branch `feat/editor-mvp`.

| | |
|---|---|
| Build | `dotnet build gEngine.slnx --nologo -v q` → **0 errori, 0 warning** *(verificato)* |
| Test | `dotnet test tests/gEngine.Tests` → **92 verdi** *(verificato)* |
| Run | `dotnet run --project samples/Sandbox` — l'editor si apre di default, **F1** lo chiude |

**Fasi**: 0–4 chiuse (editor MVP completo: hierarchy, inspector, gizmi, save/load, play/stop,
undo/redo, script compilati a runtime, file system che scrive, console). **Fase 5** quasi tutta
fatta — mancano le rifiniture elencate sotto. Fase 6 non iniziata.

### ⬅️ Si riprende da qui

**[Interfaccia per l'`InputHandler`](#11-interfaccia-per-linputhandler-e-per-i-system)**, che
adesso è il punto 1 del piano. Dietro non resta niente in sospeso: console chiusa (Fasi 4.91 /
4.92 / 4.93), `MeshRenderer` a struct col gotcha sotto test (4.94), e la potatura di
`RemoveSink` fatta.

---

## 1. Il piano deciso dal proprietario

In ordine. Due punti originali sono **chiusi** e per questo non compaiono più: il FileSystem
completo (Fasi 4.85 / 4.88 / 4.89) e la **console in-editor** (Fasi 4.91 / 4.92 / 4.93).

### 1.1 Interfaccia per l'`InputHandler` e per i system

`InputHandler` è una classe concreta e i system se la prendono nel costruttore: è **l'ultima
dipendenza del gioco che non passa da una porta**, in un engine dove renderer, asset, fisica e
cestino ci passano tutti.

- ⚠️ **Tocca `ScriptDiscovery`**, che risolve i parametri del costruttore **per tipo esatto**
  dalle Resource. Il giorno che l'input si registra sotto `IInputHandler`, uno script che chiede
  `InputHandler` **non lo trova più** — ed è giusto (chi consuma dipende dalla porta), ma va
  fatto **in un colpo solo**, registrazione e consumatori insieme.

### 1.2 Audio manager, dal punto di vista dell'UI

Oggi l'audio è **invisibile all'editor**: chi suona, cosa, a che volume, non si vede da nessuna
parte. E `SandboxGame` tiene un `_introSound` e chiama `UpdateMusic` a mano dentro `Draw` —
codice di gioco che fa il lavoro di un system.

⚠️ **Metà delle decisioni sono già prese e ti aspettano** — non ripartire da zero:

- `AssetKind` distingue già **Sound** (effetto tutto in memoria) e **Music** (stream), perché li
  distingue l'`AssetManager` (`LoadSound` vs `LoadMusicStream`).
- ⚠️ `AssetDragDrop.Classify` mappa **tutti** gli audio a `Music` e lo **dichiara nel codice**:
  lo stesso `.mp3` è l'uno o l'altro a seconda di *come lo usi*, e l'estensione non lo sa.
  Quella riga dice esplicitamente «il giorno che uno slot Sound esisterà, qui la scelta va
  rifatta». **Quel giorno è questo**: probabilmente il genere lo decide lo **slot** che riceve
  il drop, non il file che parte.
- `DrawAssetSlot` gestisce solo `AssetKind.Model` e lo dice (`<slot Music: non ancora
  gestito>`): il ramo c'è, gli manca il caso.
- Manca il componente: un **`AudioSource`** (dato d'autore: quale clip, volume, loop) più il
  system che lo suona. Con `[GameComponent]` + `[EditorAsset(AssetKind.Music)]` nasce già
  autorabile, salvabile e trascinabile — ed è la prova che gli strati sotto reggono.

### 1.3 Font dell'editor

⚠️ **Il limite più grosso del tema**: ProggyClean, il bitmap font di default, è ciò che fa
sembrare l'editor un prototipo più di ogni scelta di colore. Ma **non è un ritocco**.

- **Inter non è installato** su questa macchina: va scaricato con il suo file di licenza, e poi
  vanno **ritarate le metriche di `EditorTheme`** (padding, spacing e raggi sono tarati sui 13px
  di ProggyClean) e riguardato tutto a video.
- **Scorciatoia trovata**: **Cascadia Code è già installato ed è SIL OFL**, quindi
  redistribuibile senza scaricare niente. ⚠️ Ma è **monospaziato**: non è "Inter più in fretta",
  è un **editor diverso** — dice *strumento tecnico* e allinea le colonne di numeri
  dell'Inspector. È una scelta, non una comodità.
- Il razionale del tema (perché una sola scala di grigi, un solo accento, ecc.) sta in
  `DECISIONI.md` Fase 4.7bis.

---

## 2. Core / ECS

### Cache dei transform con dirty flag

Oggi `GetWorldMatrix` risale la catena **ricorsivamente a ogni chiamata**, senza cache e
**senza guard sui cicli**. Sufficiente per scene costruite a mano; da rifare quando le gerarchie
diventano profonde o numerose.
- ⚠️ Il guard sui cicli è meno urgente di quanto sembri: `EntityOperations.CanReparent` rifiuta
  i cicli **prima** che si creino. Ma il World non si difende da sé, e chi costruisse una
  gerarchia da codice può ancora appenderlo.

### `World.Clear()` non lascia andare gli storage

⚠️ **Blocca l'hot-reload degli script** — vedi [Scripting](#7-scripting). `Clear` svuota il
contenuto ma non toglie gli storage, e `World.Storages` è indicizzata per `Type`: tiene vivi i
`Type` del vecchio assembly e ne impedisce lo scaricamento. L'`AssemblyLoadContext` è già
collezionabile, ma non collezionerebbe niente.

**Da decidere, non da scoprire a metà lavoro**: dare al World un modo di lasciar andare gli
storage, oppure accettare che ogni ricaricamento perda un assembly (Unity ci ha convissuto per
anni).

---

## 3. Test

Il progetto esiste (`tests/gEngine.Tests`, xUnit, 92 verdi) e copre serializzazione, ciclo di
vita dei system, file degli asset, log e lo storage dei componenti.

- **ECS** — `CreateEntity`, `AddComponent`/`GetComponent`, `Query<..>` restano scoperti.
- ~~Il gotcha struct/copia~~ **coperto** (Fase 4.94, `ComponentStorageTests`): il giro
  `GetBoxed` → muta → `SetBoxed`, anche per reflection come lo fa l'Inspector, più il
  write-back intero del Transform. Era la voce col rapporto costo/danno peggiore della lista.
- **Undo/redo** — è l'altro pezzo che regge tre cose e che nessuno ricontrolla.
- Le verifiche numeriche vecchie (200k rotazioni, `SetWorldPose` su 50k pose, errore di
  proiezione) sono state fatte con **app scratch buttate via**: non c'è niente che le
  rieseguirebbe. Vale la pena recuperarne almeno una in `tests/`.

⚠️ **Prima di scrivere il prossimo test, leggi `DECISIONI.md` Fase 4.86**: il round-trip da
solo **non verifica niente** (misurato, non temuto). Vedi anche
[`DA_RICORDARE.md`](DA_RICORDARE.md#verificare-non-sperare).

---

## 4. Rendering

- **Ordinamento back-to-front dei trasparenti.** ⚠️ **Sbloccato**: era rimandato perché
  `OnRender` non riceveva la camera, e adesso la riceve (Fase 5, frustum culling). Il passo è:
  dentro il layer `Transparent`, ordinare per distanza **decrescente** da `camera.Position`.
  La nota è già nel codice, in fondo a `MeshRenderSystem`.
- **Bounds reali della mesh per il frustum culling.** Oggi `BoundingSphere` assume l'ingombro
  del **cubo unitario** — vero per `MeshKind.Cube`, sbagliato per un modello caricato. Un
  modello più grande del cubo unitario **sparisce ai bordi dello schermo**.
- **Metallic / roughness per-material.** Oggi sono **globali** (un solo set per tutta la
  scena). Il colore e la texture albedo funzionano già per-oggetto.
- **Ombre.** Non iniziate.
- **Animazioni scheletriche.** Rimandate esplicitamente al caricamento modelli, che ora c'è.

---

## 5. Fisica

- **Raycast.** ⚠️ Resta un buco vero della fisica — query di gioco: line of sight, proiettili,
  appoggio a terra. **Non serve al picking dell'editor**: quello vuole ciò che si *vede*, non
  ciò che *collide*, e usa `EntityPicker`.
- **Collider capsule e mesh.** Oggi box e sphere. ⚠️ Con mesh/compound la rimozione della shape
  vuole `RemoveAndDispose`, non `Remove` (che basta solo per le convesse).
- **In Editing il `PhysicsSystem` non gira**, quindi i corpi Bepu orfani lasciati da uno Stop
  restano nella simulazione finché non si preme Play di nuovo. Sono corpi fermi che nessuno
  guarda: non morde, ma è vero.

---

## 6. Asset e file

- **Anteprima del clone degradato.** Da Fase 4.92 un asset mancante **si dichiara** con un
  `Warning`. Resta da decidere se basta: chi clona il repo vede la scena senza modelli e
  qualche riga di log, ma nessuno gliel'ha detto *prima*. Un controllo all'avvio che elenca
  cosa manca sarebbe più onesto di N righe sparse.
- **Albedo JPEG dentro i glTF.** Il caso OBJ è risolto (`StbImageSharp` + `RepairFailedAlbedo`,
  Fase 4.85), ma un `.gltf`/`.glb` con albedo `.jpg` **viene ancora bianco**: lì il path della
  texture sta nel json (o nel chunk binario del `.glb`) e ripescarlo è **un parser**, non una
  riparazione.
- **Anteprime dei modelli nel File system.** Non ci sono, e non è un rinvio pigro: generarle
  vuol dire **caricare il modello** (SummonersRift è enorme). Serve caricamento pigro con budget
  + cache su disco.
- **Slot per asset diversi da `Model`.** `DrawAssetSlot` gestisce solo `AssetKind.Model` e **lo
  dice** a video. Gli altri generi hanno handle di tipo diverso e **nessun componente li
  dichiara ancora**: il ramo giusto non si può scrivere senza inventare a cosa servirebbe. Il
  primo caso reale sarà l'audio — vedi [1.2](#12-audio-manager-dal-punto-di-vista-dellui).
- **I binari degli asset sono fuori da git** (decisione del proprietario, niente Git LFS). ⚠️ Un
  clone pulito **parte degradato** e va detto a chi lo fa — vedi
  [`DA_RICORDARE.md`](DA_RICORDARE.md#gli-asset-non-sono-nel-repo).

---

## 7. Scripting

Gli script `.cs` sotto `assets/scripts/` **si compilano già a runtime** (Roslyn) e
`ScriptDiscovery` li registra da sé. Manca una cosa sola, ed è grossa.

### Ricaricamento a caldo (hot-reload)

Oggi gli script si compilano **all'avvio**: cambiarne uno vuol dire riavviare il gioco.

⚠️ **Il punto duro non è ricompilare** — quello è già scritto, basta richiamare
`ScriptCompiler.Compile`. È **cosa succede alla scena quando un tipo sparisce o cambia forma**
mentre il World ne tiene istanze.

- La strada è quella del Play/Stop: **snapshot → ricompila → reistanzia**. Funziona perché lo
  snapshot **è JSON**: parla di chiavi (`"Velocity": {...}`), non di tipi, quindi sopravvive al
  cambio di forma. `PlayMode` è già metà del lavoro.
- ⚠️ **Ostacolo da risolvere prima**: [`World.Clear()` non lascia andare gli
  storage](#worldclear-non-lascia-andare-gli-storage).
- ⚠️ **Ricaricare mentre si è in Play è un caso a sé.** *Vietarlo è una risposta legittima.*

⚠️ I limiti dello strato attuale (compilazione tutta insieme, freccia del tempo, implicit
usings, ordine dei system) sono **già noti e documentati**: vedi
[`DA_RICORDARE.md`](DA_RICORDARE.md#scripting) prima di toccare qualcosa.

---

## 8. Editor — comodità e buchi di verifica

Nessuna di queste è un buco funzionale: sono cose che l'editor non offre, non cose che promette
e non fa.

- **Save As.** ⚠️ Serve davvero: **`New Scene` lascia il documento senza percorso**, e senza un
  "Salva con nome" quella scena non si può salvare. Oggi `SceneDocument.Save` lancia con un
  messaggio esplicito invece di scrivere a caso — corretto, ma è un vicolo cieco per l'utente.
- **Ricerca nella Hierarchy** e **multi-selezione**.
- **"Aggiungi system"** non esiste: un system ha dipendenze e non c'è un default da costruire.
  Il bottone è spento **col motivo nel tooltip**. Servirebbe che il gioco dichiarasse le factory
  dei suoi system — stessa forma della factory dei componenti (`DECISIONI.md` Fase 4.7).
- **Ripristina un system lo rimette in fondo alla sua fase**, non dov'era: il registry smista in
  ordine di registrazione e non sa da dove veniva. Dentro una fase l'ordine **è** comportamento,
  quindi è un limite reale.
- **`NameComponent` non è aggiungibile dall'editor**: nel file il nome è il campo `name`
  dell'entità, non un componente, e metterlo nel registry darebbe **due punti di verità**.
  Conseguenza: un'entità a cui è stato tolto il nome non può riaverlo dall'Inspector.

### Buchi di verifica (non bug: cose mai guardate a video)

- **I popup Open / New / Save della barra dei menu** sono gli unici ancora verificati **per
  costruzione**. Il rig per cliccarli adesso esiste (vedi
  [`DA_RICORDARE.md`](DA_RICORDARE.md#il-rig-per-guardare-la-ui)) — non ci sono ancora passati.
- **Il trascinamento del gizmo** è verificato solo nella sua matematica, non esercitato col
  mouse. ⚠️ Il rig ha un limite proprio lì: la pressione vuole un frame tutto suo.

---

## 9. Convenzioni da mettere per iscritto

Piccole, ma sono le uniche cose che oggi vivono **solo nella testa di chi ha scritto il codice**.

- **Sistema di coordinate right-handed, Y-up** (come Raylib): implicito ovunque nell'uso
  attuale, **scritto da nessuna parte**. Va in `USAGE.md`.
- **Unità**: 1 unità = 1 metro? Non è deciso, e la scala di riferimento delle scene di demo è
  di fatto arbitraria. Da decidere prima che la fisica ci si appoggi seriamente.

---

## 10. Più avanti (Fase 6)

Non iniziate, nessuna decisione presa.

- **Prefab** (template di entità istanziabili)
- **Animazione**: skeletal per i modelli 3D, poi state machine / blending
- **Particelle**
- **Profiling / stats overlay** (FPS, draw call, ms per sistema)
- **Perf ECS**: valutare storage ad **archetipi / array densi** — ⚠️ solo *se serve*. Oggi lo
  storage è un `Dictionary<int, T>` per tipo, che è la ragione per cui il dibattito
  struct-vs-class sui componenti conta meno di quanto sembri.
- **Audio 3D** (sfx, canali, spatial audio) — dopo [1.2](#12-audio-manager-dal-punto-di-vista-dellui)

*(Undo/Redo e Asset browser erano in questa lista e sono **fatti**: Fasi 4.8, 4.85, 4.88.)*
