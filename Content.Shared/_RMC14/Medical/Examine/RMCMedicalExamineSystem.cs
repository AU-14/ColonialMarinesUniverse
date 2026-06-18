using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Stun;
using Content.Shared.Body.Components;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Medical.Examine;

public sealed partial class RMCMedicalExamineSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCMedicalExamineComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<RMCMedicalExamineComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(RMCMedicalExamineSystem), -1))
        {
            if (ent.Comp.Simple && _mobState.IsDead(ent.Owner))
            {
                args.PushMarkup(Loc.GetString(ent.Comp.DeadText, ("victim", ent.Owner)));
                return;
            }

            if (HasComp<RMCBlockMedicalExamineComponent>(args.Examiner))
                return;

            args.PushMessage(GetExamineText(ent));
        }
    }

    public FormattedMessage GetExamineText(Entity<RMCMedicalExamineComponent> ent)
    {
        var msg = new FormattedMessage();

        if (TryComp<BloodstreamComponent>(ent, out var bloodstream) &&
            bloodstream.BleedAmount > 0 &&
            !HasCmuBleedingWoundDetails(ent.Owner))
        {
            var partsText = GetBleedingPartsText(ent);
            if (partsText != null)
                msg.AddMarkupOrThrow(Loc.GetString(ent.Comp.BleedFromText, ("victim", ent.Owner), ("parts", partsText)));
            else
                msg.AddMarkupOrThrow(Loc.GetString(ent.Comp.BleedText, ("victim", ent.Owner)));
            msg.PushNewline();
        }

        LocId? stateText = null;

        if (_mobState.IsDead(ent))
            stateText = _unrevivable.IsUnrevivable(ent) ? ent.Comp.UnrevivableText : ent.Comp.DeadText;
        else if (_mobState.IsCritical(ent) || _sizeStun.IsKnockedOut(ent))
            stateText = ent.Comp.CritText;

        if (stateText != null)
            msg.AddMarkupOrThrow(Loc.GetString(stateText, ("victim", ent.Owner)));

        return msg;
    }

    private bool HasCmuBleedingWoundDetails(EntityUid body)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) ||
            !_cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled) ||
            !TryComp<HumanMedicalComponent>(body, out var medical))
        {
            return false;
        }

        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Active && bleed.Kind != BleedKind.Internal)
                return true;
        }

        return false;
    }

    private string? GetBleedingPartsText(EntityUid body)
    {
        return null;
    }
}
