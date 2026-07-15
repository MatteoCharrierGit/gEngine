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
}
