using gEngine.Ecs.Base;

namespace gEngine.Ecs.Interfaces.System;

public interface ISystem
{
    void OnCreate(World world);
    void OnUpdate(World world, float dt);

    /// <summary>
    /// Il system sta per smettere di girare: qui libera ciò che ha <b>creato lui</b> e che
    /// vive fuori dall'ECS.
    ///
    /// Era un debito teorico finché nessuno toglieva system. Il pannello Systems li fa
    /// togliere <b>col mouse</b>, e senza questo metodo togliere il <c>PhysicsSystem</c>
    /// lasciava i suoi corpi a simulare nel mondo Bepu senza che nessuno potesse più
    /// raggiungerli — la mappa entità→corpo è privata del system, quindi con l'istanza fuori
    /// dal registry quei corpi diventavano irrecuperabili, non solo non sincronizzati.
    ///
    /// ⚠️ <b>"Creato lui" è la riga che conta, e non è pignoleria.</b> Un system riceve le
    /// sue dipendenze dal costruttore (<c>PhysicsSystem</c> riceve un <c>IPhysicsWorld</c>,
    /// che è una Resource del gioco): quelle <b>non</b> vanno liberate qui. Toglierlo dal
    /// pannello per curiosità farebbe crollare il mondo fisico anche per chi lo rimette, e
    /// "Ripristina" restituirebbe un system agganciato a un oggetto morto. Si libera ciò che
    /// si è allocato, non ciò che si è ricevuto.
    ///
    /// ⚠️ Va scritto <b>reidempotente e simmetrico a <see cref="OnCreate"/></b>: la stessa
    /// istanza può essere tolta e rimessa più volte dal pannello, quindi <c>OnDestroy</c>
    /// deve lasciarla in uno stato da cui <c>OnCreate</c> riparta pulita.
    ///
    /// Default vuoto (default interface member), come <see cref="MatchedComponents"/>: la
    /// stragrande maggioranza dei system non possiede niente di esterno e non deve
    /// dichiarare nulla, e un system scritto fuori dall'engine continua a compilare.
    /// </summary>
    void OnDestroy(World world) { }

    /// <summary>
    /// I tipi di componente che un'entità deve avere <b>tutti</b> perché questo system la
    /// veda. È una <b>dichiarazione</b>, non un filtro: nessuno la usa per eseguire le
    /// query — dentro <c>OnUpdate</c> resta il normale <c>world.Query&lt;A, B&gt;()</c>,
    /// tipizzato e senza reflection.
    ///
    /// Serve a rispondere a "quali system agiscono su questa entità?" (traceability:
    /// <c>SystemRegistry.SystemsActingOn</c>) <b>senza far girare i system</b>, e a
    /// documentarli in un futuro pannello Systems dell'editor. È il motivo per cui è
    /// dichiarativo e non un <c>bool Matches(World, Entity)</c>: un predicato saprebbe
    /// rispondere solo entità per entità, e non potrebbe dire di cosa parla il system.
    ///
    /// Default = lista vuota, cioè "non dichiara nulla" = traceability <b>sconosciuta</b>,
    /// e va letto così, non come "matcha tutto" (vedi <c>SystemMatch.Unknown</c>). È un
    /// default interface member apposta: un system scritto fuori dall'engine continua a
    /// compilare senza modifiche, semplicemente non compare nella traceability.
    ///
    /// ⚠️ Limite noto: descrive <b>un solo</b> insieme in AND. Un system con più query
    /// disgiunte può dichiarare solo l'insieme principale (quello che decide su chi agisce);
    /// l'elenco che ne esce è un'approssimazione utile, non una prova. Ciò che il system
    /// guarda senza agirci va in <see cref="ObservedComponents"/>.
    /// </summary>
    IReadOnlyList<Type> MatchedComponents => Array.Empty<Type>();

    /// <summary>
    /// I tipi di componente che un'entità deve avere <b>tutti</b> perché questo system la
    /// <b>legga senza agirci</b>: un ingresso, non un bersaglio.
    ///
    /// Esiste perché <see cref="MatchedComponents"/> risponde solo a "su chi agisce", e un
    /// system che legge un'entità per decidere cosa fare a un'<i>altra</i> spariva dalla
    /// traceability della prima. Caso vivo: <c>CameraFollowSystem</c> agisce sulla camera ma
    /// legge il player — davanti al player, alla domanda "perché la camera non mi segue?",
    /// l'Inspector non mostrava il system che è esattamente la risposta.
    ///
    /// Si chiama <b>Observed</b> e non <c>Read</c> perché "letto" non distingue niente: un
    /// system legge anche i componenti che matcha (il <c>TransformComponent</c> che poi
    /// riscrive). La parola che serviva è "guarda ma non tocca", ed è questa.
    ///
    /// ⚠️ Vale lo stesso limite di <see cref="MatchedComponents"/>, anzi peggiore: è
    /// <b>metadata scritto a mano</b>, un solo insieme in AND, e nessuno verifica che
    /// corrisponda alle query vere. Un system che legge e non lo dichiara è indistinguibile
    /// da uno che non legge — quindi l'assenza qui non è una prova di niente. Default vuoto
    /// (default interface member): nessun system esistente si rompe, e la stragrande
    /// maggioranza dei system non legge davvero nulla di esterno e <b>non deve</b>
    /// dichiarare niente.
    /// </summary>
    IReadOnlyList<Type> ObservedComponents => Array.Empty<Type>();
}