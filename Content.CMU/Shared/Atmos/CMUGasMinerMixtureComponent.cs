using Content.Shared.Atmos;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Replaces a gas miner's single-gas output with a weighted mixture. The miner's
/// configured spawn amount remains the total number of moles produced per second.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGasMinerMixtureComponent : Component
{
    [DataField(required: true)]
    public Dictionary<Gas, float> Gases = new();
}
