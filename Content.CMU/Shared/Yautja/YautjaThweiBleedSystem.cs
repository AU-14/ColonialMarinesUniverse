using Content.Shared._RMC14.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Thwei works from presence in the bloodstream, not from metabolism: code/modules/organs/limbs.dm:691 runs
/// "if(owner.reagents.get_reagent_amount("thwei") >= 0.05) remove_all_bleeding(FALSE, TRUE)" on the limb's own
/// health tick, and the reagent carries no wound-related property in the reference. CMU previously modelled the
/// wound half as a reagent metabolism effect, which only fires once thwei has been metabolised into the metabolites
/// solution, so a hunter with thwei in their blood did not get bleeding cleared at all.
/// </summary>
public sealed partial class YautjaThweiBleedSystem : EntitySystem
{
    /// <summary>CM-SS13's threshold: 0.05 units in the bloodstream.</summary>
    private static readonly FixedPoint2 ThweiThreshold = FixedPoint2.New(0.05f);

    /// <summary>The reference clears this on the limb health tick, about once a second.</summary>
    private static readonly TimeSpan ClearInterval = TimeSpan.FromSeconds(1);

    [Dependency] private SharedRMCBloodstreamSystem _bloodstream = default!;
    [Dependency] private Content.Shared.Chemistry.EntitySystems.SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextClear;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodstreamComponent, BleedModifierEvent>(OnBleed);
    }

    /// <summary>
    /// Total thwei the mob is carrying. The bloodstream metabolism type transfers reagents out of the blood solution
    /// into the metabolites solution as they are metabolised, so checking only the blood solution misses thwei that
    /// is already being processed - which is why the presence check looked like it never fired.
    /// </summary>
    private FixedPoint2 ThweiInBody(EntityUid uid)
    {
        var total = FixedPoint2.Zero;

        if (TryComp(uid, out BloodstreamComponent? bloodstream) &&
            _solution.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out var blood))
        {
            total += blood.GetTotalPrototypeQuantity("thwei");
        }

        if (TryComp(uid, out bloodstream) &&
            _solution.TryGetSolution(uid, bloodstream.BloodTemporarySolutionName, out _, out var temporary))
        {
            total += temporary.GetTotalPrototypeQuantity("thwei");
        }

        if (TryComp(uid, out bloodstream) &&
            _solution.TryGetSolution(uid, bloodstream.MetabolitesSolutionName, out _, out var metabolites))
        {
            total += metabolites.GetTotalPrototypeQuantity("thwei");
        }

        return total;
    }

    private void OnBleed(Entity<BloodstreamComponent> ent, ref BleedModifierEvent args)
    {
        if (ThweiInBody(ent.Owner) >= ThweiThreshold)
            args.BleedReductionAmount += 0.75f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextClear)
            return;

        _nextClear = _timing.CurTime + ClearInterval;


        var query = EntityQueryEnumerator<BloodstreamComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (ThweiInBody(uid) < ThweiThreshold)
                continue;


            foreach (var part in _medicalIndex.GetBodyParts(uid))
            {
                _wounds.StopSurfaceBleedingOnPart(part.Owner);
                _wounds.ClearInternalBleed(part.Owner);
            }
        }
    }
}
