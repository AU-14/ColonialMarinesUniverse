using Content.Server.Atmos.Piping.EntitySystems;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Piping.Components;

[RegisterComponent]
public sealed partial class AtmosPipeColorComponent : Component
{
    [DataField]
    public Color Color { get; set; } = Color.White;

    public EntityUid OwnerEntity;

    [ViewVariables(VVAccess.ReadWrite), UsedImplicitly]
    public Color ColorVV
    {
        get => Color;
        set => IoCManager.Resolve<IEntityManager>().System<AtmosPipeColorSystem>().SetColor(OwnerEntity, this, value);
    }
}

[ByRefEvent]
public record struct AtmosPipeColorChangedEvent(Color color)
{
    public Color Color = color;
}
