using Content.Shared.CMU14.Chemistry;
using Content.Shared.CMU14.Hypospray;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Hypospray;

/// <summary>
/// The hypospray stings. Adds a little raw pain to the patient on each injection, kept under a configurable tier
/// so repeated doses cannot push someone into shock.
/// </summary>
public sealed partial class CMUHyposprayPainSystem : EntitySystem
{
    [Dependency] private SharedPainShockSystem _pain = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PainShockComponent, CMUBeforeHyposprayInjectEvent>(OnBeforeInject);
    }

    private void OnBeforeInject(Entity<PainShockComponent> ent, ref CMUBeforeHyposprayInjectEvent args)
    {
        var (_, injector) = args;

        if (!TryComp(injector, out CMUHyposprayComponent? jet)
            || jet.Pain <= FixedPoint2.Zero)
            return;

        var amount = jet.Pain;

        // Stay under the first threshold above the allowed tier. Shock has no tier above it, so it is uncapped.
        if (jet.MaxPainTier < PainTier.Shock)
        {
            var limit = PainTierThresholds.GetUpwardThreshold(jet.MaxPainTier + 1, _pain.ShockThreshold) - FixedPoint2.FromCents(1);
            amount = FixedPoint2.Min(amount, FixedPoint2.Max(FixedPoint2.Zero, limit - ent.Comp.Pain));
        }

        if (amount <= FixedPoint2.Zero)
            return;

        _pain.AddPainPulse(ent, amount);
    }
}
