using Content.Shared.Chat.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Medical.Asphyxiation;

/// <summary>
///     Makes the entity perform <see cref="Emote"/> once its current Asphyxiation (oxygen) damage
///     reaches <see cref="Threshold"/>.
/// </summary>
[RegisterComponent]
public sealed partial class GaspOnAsphyxiationComponent : Component
{
    [DataField]
    public FixedPoint2 Threshold = FixedPoint2.New(20);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    [DataField]
    public ProtoId<EmotePrototype> Emote = "Gasp";

    [ViewVariables]
    public TimeSpan NextGasp;
}
