using gEngine.Ecs.Base;
using gEngine.Ecs.Component;

namespace gEngine.Editor.Undo;

/// <summary>
/// I componenti di <b>una</b> entità, copiati e messi da parte: il materiale con cui i comandi
/// dell'undo ricostruiscono uno stato passato.
///
/// È deliberatamente più piccolo dello snapshot del <c>PlayMode</c>, che è la scena intera in
/// formato file. La differenza non è di misura ma di conseguenze: quello ricostruisce il World
/// da zero, quindi cambia tutti gli id, perde i <c>[RuntimeState]</c> e la selezione delle
/// entità senza nome — accettabile per Play/Stop, che è un giro completo, insopportabile per
/// annullare la digitazione di un numero. Qui si tocca solo l'entità coinvolta, gli id restano
/// quelli, e nulla "sbatte".
///
/// ⚠️ Passa per gli <b>oggetti</b> e non per il JSON, quindi — al contrario dello snapshot del
/// PlayMode — non è limitato a ciò che il formato di scena sa rappresentare. In compenso non
/// sopravvive a un cambio di forma dei tipi (hot-reload degli script): è il verso opposto dello
/// stesso compromesso, ed è giusto così — uno stack di undo non deve attraversare una
/// ricompilazione, e infatti verrà svuotato quando succede.
/// </summary>
public sealed class EntitySnapshot
{
    public required Entity Entity { get; init; }

    /// <summary>Copie indipendenti, non riferimenti allo storage. Vedi <see cref="ComponentCopy"/>.</summary>
    public required IReadOnlyList<object> Components { get; init; }

    /// <summary>
    /// Fotografa i componenti dell'entità <b>adesso</b>.
    ///
    /// ⚠️ I componenti <see cref="RuntimeStateAttribute"/> restano fuori, esattamente come nel
    /// salvataggio e nella duplicazione: sono link a risorse che vivono fuori dal World (il
    /// corpo Bepu), e rimetterne indietro uno vorrebbe dire puntare a un corpo che nel
    /// frattempo è stato tolto dalla simulazione. Li ricrea il system che li possiede, che è
    /// l'unico che sa come. È la stessa regola, nel terzo posto in cui serve: il fatto che
    /// l'attributo esista è ciò che impedisce di ri-decidere ogni volta.
    /// </summary>
    public static EntitySnapshot Capture(World world, Entity entity)
    {
        var components = new List<object>();

        foreach (var storage in world.ComponentStorages)
        {
            if (!storage.Has(entity.Id))
                continue;

            if (storage.ComponentType.IsDefined(typeof(RuntimeStateAttribute), inherit: false))
                continue;

            if (storage.GetBoxed(entity.Id) is { } component)
                components.Add(ComponentCopy.Shallow(component));
        }

        return new EntitySnapshot { Entity = entity, Components = components };
    }

    /// <summary>
    /// Rimette l'entità in questo stato: la resuscita se non c'è più, le ridà i componenti
    /// fotografati e <b>toglie quelli che nel frattempo sono comparsi</b>.
    ///
    /// ⚠️ La rimozione è la metà che si dimentica: senza, annullare un "aggiungi componente"
    /// non toglierebbe niente — riscriverebbe i vecchi valori lasciando lì il componente nuovo,
    /// e l'undo sembrerebbe funzionare a metà (i numeri tornano indietro, il componente resta).
    ///
    /// I <c>[RuntimeState]</c> non si toccano in nessuna delle due direzioni: non sono nello
    /// snapshot, quindi non vanno rimessi, e non vanno tolti perché il loro system è l'unico
    /// che sa cosa comportano.
    /// </summary>
    public void Restore(World world)
    {
        if (!world.Exists(Entity))
            world.RestoreEntity(Entity.Id);

        var restored = new HashSet<Type>();

        foreach (var component in Components)
        {
            world.AddComponent(Entity, ComponentCopy.Shallow(component));
            restored.Add(component.GetType());
        }

        // Da materializzare: togliere un componente tocca gli storage, e ci stiamo iterando
        // sopra per decidere cosa togliere.
        var extra = world.ComponentStorages
            .Where(storage => storage.Has(Entity.Id))
            .Select(storage => storage.ComponentType)
            .Where(type => !restored.Contains(type) &&
                           !type.IsDefined(typeof(RuntimeStateAttribute), inherit: false))
            .ToList();

        foreach (var type in extra)
            world.RemoveComponent(Entity, type);
    }
}
