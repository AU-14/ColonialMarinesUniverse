using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.StatusEffects;

public sealed class PainShockSystem : SharedPainShockSystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    protected override void ApplyShockEntryEffect(EntityUid body)
    {
        _stun.TryKnockdown(body, TimeSpan.FromSeconds(1), refresh: false);
    }

    protected override void ApplyPeriodicShockKnockdown(EntityUid body)
    {
        _stun.TryKnockdown(body, TimeSpan.FromSeconds(1), refresh: false);
    }
}
