using gEngine.Rendering;

namespace gEngine.Ecs.Component;

/// <summary>
/// Sorgente di luce. Dati puri: posizione e direzione NON stanno qui — si ricavano dal
/// <see cref="TransformComponent"/> dell'entità (posizione per le point, forward per le
/// direzionali). La presenza del componente = luce attiva; per spegnerla, rimuovi il
/// componente o metti <see cref="Intensity"/> a 0.
/// </summary>
public struct LightComponent
{
    [EditorConfiguration] public LightKind Kind;
    [EditorConfiguration] public Color Color;
    [EditorConfiguration("Intensità")] public float Intensity;
}
