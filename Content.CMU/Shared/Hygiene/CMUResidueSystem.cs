using Content.Shared.CMU14.Item.Stain;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using System;

namespace Content.Shared.CMU14.Hygiene;

public sealed partial class CMUResidueSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUResidueComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUResidueComponent, CMUCleaningEligibilityEvent>(OnCleaningEligibility);
    }

    private void OnCleaningEligibility(Entity<CMUResidueComponent> ent, ref CMUCleaningEligibilityEvent args)
    {
        if (ent.Comp.Color == null)
            return;
        args.CanClean = true;
        if (args.DistanceThreshold <= 0f)
            args.DistanceThreshold = 1.5f;
    }

    private void OnExamined(Entity<CMUResidueComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Color == null)
            return;
        var amount = ent.Comp.Buildup >= 0.6f ? "heavy" : "light";
        args.PushMarkup(Loc.GetString($"cmu-residue-{(ent.Comp.Alien ? "alien" : "blood")}-{amount}"));
    }

    public void Stain(EntityUid target, Color color, bool alien = false)
    {
        if (_net.IsClient || !HasComp<HumanoidProfileComponent>(target))
            return;

        var residue = EnsureComp<CMUResidueComponent>(target);
        // Count exposure immediately; coalesce duplicate hooks in the same simulation frame.
        var increased = false;
        if (residue.Color == null || _timing.CurTime != residue.NextBuildup)
        {
            var buildup = Math.Min(1f, residue.Buildup + (residue.Color == null ? 0.3f : 0.12f));
            increased = residue.Buildup != buildup;
            residue.Buildup = buildup;
            residue.NextBuildup = _timing.CurTime;
        }
        // Ordinary blood must not hide existing alien contamination.
        if (residue.Color != null && residue.Alien && !alien)
        {
            if (increased)
                Dirty(target, residue);
            return;
        }
        if (!increased && residue.Color == color && residue.Alien == alien)
            return;
        residue.Color = color;
        residue.Alien = alien;
        Dirty(target, residue);
    }

    public void Clean(EntityUid target)
    {
        if (_net.IsClient || !TryComp<CMUResidueComponent>(target, out var residue) || residue.Color == null)
            return;
        residue.Buildup = 0f;
        residue.NextBuildup = TimeSpan.Zero;
        residue.Color = null;
        residue.Alien = false;
        Dirty(target, residue);
    }
}
