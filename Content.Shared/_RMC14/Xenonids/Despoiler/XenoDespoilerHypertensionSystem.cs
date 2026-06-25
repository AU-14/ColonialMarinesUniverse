using Robust.Shared.Timing;
using Content.Shared._RMC14.Armor;

namespace Content.Shared._RMC14.Xenonids.Despoiler;

public sealed partial class XenoDespoilerHypertensionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMArmorSystem _armor = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<
            XenoDespoilerHypertensionComponent,
            CMGetArmorEvent>(OnGetArmor);
    }

    private void OnGetArmor(
        Entity<XenoDespoilerHypertensionComponent> ent,
        ref CMGetArmorEvent args)
    {
        args.XenoArmor += (ent.Comp.Stacks / 2) * 5;
    }

    private void RefreshArmor(EntityUid uid)
    {
        if (TryComp<CMArmorComponent>(uid, out var armor))
            _armor.UpdateArmorValue((uid, armor));
    }

    public void AddSlashPoints(EntityUid uid, XenoDespoilerHypertensionComponent comp)
    {
        AddPoints(uid, comp, comp.PointsPerSlash);
    }

    public void AddPoints(EntityUid uid, XenoDespoilerHypertensionComponent comp, float amount)
    {
        if (amount <= 0)
            return;

        comp.Points += amount;
        comp.LastActivityAt = _timing.CurTime;

        var oldStacks = comp.Stacks;

        while (comp.Points >= comp.PointsPerStack && comp.Stacks < comp.MaxStacks)
        {
            comp.Points -= comp.PointsPerStack;
            comp.Stacks++;
        }

        if (comp.Stacks >= comp.MaxStacks)
            comp.Points = 0;

        if (oldStacks != comp.MaxStacks)
            RefreshArmor(uid);

        Dirty(uid, comp);
    }

    public bool TrySpendStacks(EntityUid uid, XenoDespoilerHypertensionComponent comp, int count)
    {
        if (comp.Stacks < count)
            return false;

        comp.Stacks -= count;

        RefreshArmor(uid);

        Dirty(uid, comp);
        return true;
    }

    public void RemoveStacks(
        EntityUid uid,
        XenoDespoilerHypertensionComponent comp,
        int amount)
    {
        if (amount <= 0)
            return;

        var oldStacks = comp.Stacks;

        comp.Stacks = Math.Max(0, comp.Stacks - amount);

        if (oldStacks == comp.Stacks)
            return;

        RefreshArmor(uid);

        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerHypertensionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Stacks <= 0 && comp.Points <= 0)
                continue;

            if (now - comp.LastActivityAt < comp.DecayDelay)
                continue;

            comp.Points -= comp.DecayPerSecond * frameTime;

            var oldStacks = comp.Stacks;

            while (comp.Points < 0 && comp.Stacks > 0)
            {
                comp.Stacks--;
                comp.Points += comp.PointsPerStack;
            }

            if (oldStacks != comp.Stacks)
                RefreshArmor(uid);

            Dirty(uid, comp);
        }
    }
}
