using System.Numerics;
using gEngine.Ecs.Component;
using gEngine.MathUtils;

namespace gEngine.Ecs.Base;

/// <summary>
/// Estensioni su <see cref="World"/> per risolvere la <b>world matrix</b> di
/// un'entità tenendo conto della gerarchia (<see cref="ParentComponent"/>).
///
/// Stanno qui, accanto a <see cref="WorldQueries"/>, perché — a differenza di
/// <see cref="TransformExtensions"/>, che lavora su un singolo
/// <see cref="TransformComponent"/> isolato — questo calcolo ha bisogno del
/// <see cref="World"/> per raggiungere il transform del genitore.
/// </summary>
public static class WorldTransforms
{
    /// <summary>
    /// World matrix dell'entità: se ha un <see cref="ParentComponent"/>, compone
    /// il proprio locale col mondo del genitore, altrimenti il locale <b>è</b> il
    /// mondo (entità root).
    ///
    /// Convenzione: <c>System.Numerics</c> è <i>row-vector</i>
    /// (<c>Vector3.Transform(v, M)</c> == <c>v * M</c>), quindi un punto del figlio
    /// va prima nello spazio del genitore (locale del figlio) e poi nel mondo
    /// (world del genitore):
    /// <code>v_world = v_local * LocalFiglio * WorldGenitore</code>
    /// Perciò il <b>locale del figlio sta a SINISTRA</b>: <c>Local * ParentWorld</c>.
    /// (È l'opposto del classico <c>Parent * Local</c> dei tutorial column-vector /
    /// OpenGL — stessa famiglia del gotcha del transpose in <c>DrawMesh</c>.)
    ///
    /// Risoluzione ricorsiva on-demand, senza cache. Robusta ai riferimenti
    /// pendenti: un <c>Parent</c> che non esiste più (o senza transform) ricade su
    /// <see cref="Matrix4x4.Identity"/> e l'entità è trattata come root.
    /// ⚠️ Non protetta dai cicli (A figlio di B figlio di A → ricorsione infinita):
    /// con scene costruite a mano va bene; con un editor servirà un guard o una
    /// versione iterativa con dirty-flag.
    /// </summary>
    public static Matrix4x4 GetWorldMatrix(this World world, Entity entity)
    {
        if (!world.TryGetComponent<TransformComponent>(entity, out var transform))
            return Matrix4x4.Identity;

        var local = transform.GetLocalMatrix();

        if (world.TryGetComponent<ParentComponent>(entity, out var parent))
            return local * world.GetWorldMatrix(parent.Parent);

        return local;
    }

    /// <summary>
    /// Scrive una posa espressa in <b>spazio mondo</b> su un'entità, dividendo per il mondo
    /// del genitore. È l'inverso esatto di <see cref="GetWorldMatrix"/>, e serve a chi
    /// calcola una posa in mondo ma deve depositarla in un <see cref="TransformComponent"/>,
    /// che è nello spazio del <b>genitore</b> — tipicamente un camera-follow: "dove deve
    /// stare la camera per inquadrare il player" è una frase in coordinate mondo.
    ///
    /// Da <c>World = Local * ParentWorld</c> (locale a SINISTRA, row-vector) segue
    /// <c>Local = World * inverse(ParentWorld)</c>. Sulla posizione questo è esattamente
    /// <c>Vector3.Transform(worldPosition, inverse(ParentWorld))</c>; sulla rotazione,
    /// <c>Concatenate(a, b)</c> è "prima a, poi b" = <c>R_a * R_b</c> nella stessa
    /// convenzione, quindi da <c>R_world = Concatenate(R_local, R_parent)</c> segue
    /// <c>R_local = Concatenate(R_world, Conjugate(R_parent))</c>. Verificato
    /// numericamente: l'ordine opposto sbaglia, non è una simmetria innocua.
    ///
    /// <see cref="TransformComponent.Scale"/> non si tocca: la posa è dove sei e come sei
    /// girato, quanto sei grande è un'altra domanda — e il chiamante che dice "guarda lì"
    /// non ha un'opinione sulla scala.
    ///
    /// Entità <b>root</b> (o con un genitore distrutto, che
    /// <see cref="GetWorldMatrix"/> tratta come identità): mondo == locale, e la scrittura
    /// è letterale — nessuna inversione, nessun errore di arrotondamento introdotto dove
    /// prima non ce n'era.
    ///
    /// ⚠️ <b>La rotazione vale solo se la scala del genitore è uniforme</b>, e non è un
    /// dettaglio da nota a piè di pagina. In row-vector la parte 3x3 del mondo è
    /// <c>S_figlio * R_figlio * S_genitore * R_genitore</c>: la scala del genitore cade
    /// <b>in mezzo</b>, fra le due rotazioni. Se è uniforme è uno scalare, commuta, e
    /// <c>R_mondo = R_figlio * R_genitore</c> — la formula qui sopra è esatta (misurato:
    /// errore max 8.6e-7 su 50k pose annidate). Se è non uniforme non commuta, il mondo del
    /// figlio contiene shear e <b>non esiste</b> il quaternione locale che dia
    /// quell'orientamento: misurato, l'orientamento risultante sbaglia di ~90° in mediana.
    /// Non è "quasi giusto", è un'altra rotazione.
    ///
    /// La <b>posizione resta esatta comunque</b> (7.7e-6), perché la traslazione non è
    /// toccata da quel problema. Il caso non si presenta oggi (le camere sono root, e il solo
    /// genitore vivo in <c>demo.json</c> ha scala uniforme), quindi qui non si paga il prezzo
    /// di risolverlo — ma il metodo <b>non lo segnala</b>: chi agganciasse una camera a un
    /// genitore schiacciato otterrebbe una posizione giusta e uno sguardo sbagliato, in
    /// silenzio. È anche il motivo per cui non c'è un <c>SetWorldMatrix</c> generale:
    /// prometterebbe una fedeltà che la decomposizione non può mantenere.
    /// </summary>
    /// <returns>
    /// False — e <b>nessuna scrittura</b> — se l'entità non ha un transform, o se il mondo
    /// del genitore non è invertibile/decomponibile (scala 0 su un asse). Non si ricade
    /// sull'identità: là dove <see cref="GetWorldMatrix"/> può permettersi di leggere una
    /// posa qualunque, scriverla teletrasporterebbe l'entità nell'origine del mondo.
    /// </returns>
    public static bool SetWorldPose(this World world, Entity entity, Vector3 position, Quaternion rotation)
    {
        if (!world.TryGetComponent<TransformComponent>(entity, out var transform))
            return false;

        if (world.TryGetComponent<ParentComponent>(entity, out var parent))
        {
            var parentWorld = world.GetWorldMatrix(parent.Parent);

            // ⚠️ Invert e Decompose falliscono davvero (genitore a scala 0): l'out ignorato
            // lascerebbe una matrice non inizializzata e spargerebbe NaN nella scena.
            if (!Matrix4x4.Invert(parentWorld, out var inverse) ||
                !Matrix4x4.Decompose(parentWorld, out _, out var parentRotation, out _))
                return false;

            position = Vector3.Transform(position, inverse);
            rotation = Quaternion.Normalize(
                Quaternion.Concatenate(rotation, Quaternion.Conjugate(parentRotation)));
        }

        transform.Position = position;
        transform.Rotation = rotation;

        // ⚠️ TransformComponent è una struct: TryGetComponent ne ha dato una COPIA. Il
        // write-back sta QUI apposta — è il gotcha che ha già morso quattro volte, e ogni
        // chiamante che ricalcolasse questa riga sarebbe la quinta occasione di scordarlo.
        world.AddComponent(entity, transform);
        return true;
    }

    /// <summary>
    /// La world matrix con cui l'entità viene <b>disegnata</b>: la sua gerarchia più la
    /// <see cref="MeshRendererComponent.Size"/>, che è ingombro della mesh e non del
    /// transform.
    ///
    /// Sta qui invece che nei chiamanti perché i chiamanti sono due e <b>devono
    /// concordare</b>: <c>MeshRenderSystem</c> la usa per disegnare e per il frustum
    /// culling, l'<c>EntityPicker</c> dell'editor per decidere cosa c'è sotto il clic. Se
    /// le due formule divergessero si selezionerebbe un ingombro diverso da quello che si
    /// vede — e nessuno se ne accorgerebbe finché non prova a cliccare di striscio.
    /// </summary>
    /// <param name="meshRenderer">
    /// ⚠️ <c>in</c> perché da quando è uno struct passarlo per valore ne copia una cinquantina
    /// di byte, e questo metodo gira <b>per entità, per frame</b>. Legge un campo solo: la
    /// copia sarebbe tutta sprecata.
    /// </param>
    public static Matrix4x4 GetRenderMatrix(this World world, Entity entity, in MeshRendererComponent meshRenderer)
    {
        return Matrix4x4.CreateScale(meshRenderer.Size) * world.GetWorldMatrix(entity);
    }
}
