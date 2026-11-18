using Content.Server.Ghost.Roles;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Yautja.BadBlood;

// FLOW-115: the kennel. CMU14 ARCH-603: it is a free hound spawner, the pattern of RMC's lesser-drone pylon
// (XenoPylonSystem.UpdateGhostRoles): ghosts take hounds from its GhostRoleMobSpawner, the stock regrows on a timer
// and the living clan raises its cap. Nobody buys a hound any more; SP buys the kennel's rate and stock at the
// armory (STATE-602). Who may take one is BadBloodHoundSystem.CanTakeHound. The landing sleepers are unchanged.
// CMU14 ARCH-706: kennel hounds bind through the pack layer (BadBloodBeastSystem), to the Beast-Master first.
// Dependency fields taken by this file: _kennelBeasts. It reuses _squadGhostRoles (Squads.cs) and _spitMobState (Spit.cs).
public sealed partial class BadBloodCampSystem
{
    [Dependency] private BadBloodBeastSystem _kennelBeasts = default!;

    partial void InitializeKennel()
    {
        SubscribeLocalEvent<BadBloodKennelSpawnerComponent, MapInitEvent>(OnKennelMapInit);
        SubscribeLocalEvent<BadBloodHoundComponent, GhostRoleSpawnerUsedEvent>(OnKennelHoundSpawned);
    }

    // pitched with the Lodge: MinStock hounds on offer at once. Outside a Bad Blood round the kennel offers nothing
    private void OnKennelMapInit(Entity<BadBloodKennelSpawnerComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.NextAt = now + ent.Comp.Interval;
        ent.Comp.StockedAt = now;
        ent.Comp.Max = GetKennelMax(ent.Comp);

        // a rebuilt or moved Lodge: the hounds an earlier kennel let out still live and still count against its stock
        var hounds = EntityQueryEnumerator<BadBloodHoundComponent>();
        while (hounds.MoveNext(out var hound, out var houndComp))
        {
            if (houndComp.FromKennel && !TerminatingOrDeleted(hound) && !_spitMobState.IsDead(hound) &&
                !ent.Comp.LiveHounds.Contains(hound))
            {
                ent.Comp.LiveHounds.Add(hound);
            }
        }

        ent.Comp.Current = _rule.IsRuleActive()
            ? Math.Max(ent.Comp.LiveHounds.Count, Math.Max(0, ent.Comp.MinStock))
            : 0;

        if (TryComp(ent, out GhostRoleMobSpawnerComponent? spawner))
        {
            _squadGhostRoles.SetAvailable((ent.Owner, spawner), ent.Comp.Current);
            _squadGhostRoles.SetCurrent((ent.Owner, spawner), ent.Comp.LiveHounds.Count);
        }
    }

    partial void TickKennel(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var kennels = EntityQueryEnumerator<BadBloodKennelSpawnerComponent, GhostRoleMobSpawnerComponent>();
        while (kennels.MoveNext(out var uid, out var kennel, out var spawner))
        {
            UpdateKennel((uid, kennel, spawner), now);
        }
    }

    /// <summary>
    /// The kennel's stock, once a second while a Lodge stands: dead hounds give their takeover up, the cap follows
    /// the living clan and one takeover is stocked every <see cref="BadBloodKennelSpawnerComponent.Interval"/> up to it.
    /// </summary>
    public void UpdateKennel(Entity<BadBloodKennelSpawnerComponent, GhostRoleMobSpawnerComponent> ent, TimeSpan now)
    {
        var (uid, kennel, spawner) = ent;
        for (var i = kennel.LiveHounds.Count - 1; i >= 0; i--)
        {
            var hound = kennel.LiveHounds[i];
            if (!TerminatingOrDeleted(hound) && !_spitMobState.IsDead(hound))
                continue;

            kennel.LiveHounds.RemoveSwap(i);
            kennel.Current = Math.Max(0, kennel.Current - 1);
        }

        kennel.Max = GetKennelMax(kennel);
        if (now >= kennel.NextAt)
        {
            kennel.NextAt = now + kennel.Interval;
            if (kennel.Current < kennel.Max)
            {
                kennel.Current++;
                kennel.StockedAt = now;
            }
        }

        // available first, then taken: the other order briefly re-registers the role while a dead hound is pruned
        _squadGhostRoles.SetAvailable((uid, spawner), kennel.Current);
        _squadGhostRoles.SetCurrent((uid, spawner), kennel.LiveHounds.Count);
    }

    // CMU14 STATE-702: each living Beast-Master keeps one more hound ready
    private int GetKennelMax(BadBloodKennelSpawnerComponent kennel)
    {
        var perHound = Math.Max(1, kennel.BadBloodsPerHound);
        return Math.Max(kennel.MinStock, _rule.CountLivingBadBloods() / perHound) +
               _kennelBeasts.CountLivingBeastMasters();
    }

    /// <summary>
    /// The standing Lodge's kennel spawner.
    /// </summary>
    public bool TryGetKennel(out Entity<BadBloodKennelSpawnerComponent> kennel)
    {
        kennel = default;
        if (!TryGetPart(BadBloodCampPartKind.Kennel, out var part) || !TryComp(part, out BadBloodKennelSpawnerComponent? comp))
            return false;

        kennel = (part, comp);
        return true;
    }

    /// <summary>
    /// STATE-602: an armory kennel ware bought for <paramref name="kennel"/>. The interval shrinks at once, so a
    /// stock already waiting comes no later than the new interval.
    /// </summary>
    public void ApplyKennelUpgrade(Entity<BadBloodKennelSpawnerComponent> kennel, EntProtoId ware, BadBloodKennelUpgradeComponent upgrade)
    {
        var comp = kennel.Comp;
        comp.Upgrades[ware] = comp.Upgrades.GetValueOrDefault(ware) + 1;

        if (upgrade.IntervalMultiplier > 0f)
        {
            comp.Interval *= upgrade.IntervalMultiplier;
            var next = _timing.CurTime + comp.Interval;
            if (next < comp.NextAt)
                comp.NextAt = next;
        }

        comp.MinStock += Math.Max(0, upgrade.MinStockBonus);
        comp.Max = GetKennelMax(comp);
    }

    // a ghost took a hound from the kennel: it is the kennel's to count. CMU14 ARCH-706: it answers to the clan's
    // Beast-Master, or the living Hunt Leader if none lives (BadBloodBeastSystem.GetMasterFor). With neither it has
    // no master yet (a preset-bond dog simply has no owner to sense) and binds when one appears.
    // STATE-702: a war-painted kennel's hounds bite harder
    private void OnKennelHoundSpawned(Entity<BadBloodHoundComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp(args.Spawner, out BadBloodKennelSpawnerComponent? kennel))
            return;

        kennel.LiveHounds.Add(ent.Owner);
        ent.Comp.FromKennel = true;

        if (TryComp(args.Spawner, out BadBloodWarPaintComponent? paint))
            EnsureComp<BadBloodWarPaintComponent>(ent).Multiplier = paint.Multiplier;

        if (_kennelBeasts.GetMasterFor(ent.Owner) is { } master)
            _kennelBeasts.Bind(ent.Owner, master);
    }

    /// <summary>
    /// CMU14 STATE-702: war-paints <paramref name="kennel"/>: its living hounds and every hound it lets out from now on
    /// bite <paramref name="multiplier"/> times as hard.
    /// </summary>
    public void ApplyKennelWarPaint(Entity<BadBloodKennelSpawnerComponent> kennel, float multiplier)
    {
        EnsureComp<BadBloodWarPaintComponent>(kennel).Multiplier = multiplier;
        foreach (var hound in kennel.Comp.LiveHounds)
        {
            if (!TerminatingOrDeleted(hound))
                EnsureComp<BadBloodWarPaintComponent>(hound).Multiplier = multiplier;
        }
    }
}
