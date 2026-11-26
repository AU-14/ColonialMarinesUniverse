using Content.Shared.CMU14.Xenomorphs.Pathogen.Sporecaster;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Popper;
using Content.Shared.CMU14.Xenomorphs.Pathogen;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Destructible;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Interaction;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.Examine;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Roles.Components;
  
namespace Content.Server.CMU14.Xenomorphs.Pathogen.Sporecaster;
  
public sealed partial class CMUPathogenSporecasterSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMycotoxinSystem _mycotoxin = default!;
    [Dependency] private CMUNeoHeadgearSystem _headgear = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private CMUPathogenSporeProtectionSystem _protection = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;   // NEW
  
    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
  
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<CMUPathogenPopperComponent, GhostRoleRequestAttemptEvent>((Entity<CMUPathogenPopperComponent> _, ref GhostRoleRequestAttemptEvent ev) => CheckPopperCooldown(ref ev));
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, GhostRoleRequestAttemptEvent>((Entity<CMUPathogenSporecasterComponent> _, ref GhostRoleRequestAttemptEvent ev) => CheckPopperCooldown(ref ev));
        SubscribeLocalEvent<CMUPathogenPopperComponent, GhostRoleSpawnerUsedEvent>(OnPopperSpawnerUsed);
    }
  
    private void CheckPopperCooldown(ref GhostRoleRequestAttemptEvent ev)
    {
        if (ev.Player.AttachedEntity is not { } user || !TryComp<GhostComponent>(user, out var ghost))
            return;

        var remaining = TimeSpan.FromMinutes(5) - (_timing.RealTime - ghost.TimeOfDeath);
        if (remaining <= TimeSpan.Zero)
            return;

        ev.Cancelled = true;
        if (ev.ExplicitRequest)
            _popup.PopupEntity(Loc.GetString("cmu-popper-ghost-cooldown",
                ("seconds", (int)Math.Ceiling(remaining.TotalSeconds))), user, user, PopupType.MediumCaution);
    }

    private void OnMapInit(Entity<CMUPathogenSporecasterComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextGrowAt = _timing.CurTime + ent.Comp.GrowInterval;
        Dirty(ent);
    }
    
    private void OnDestruction(Entity<CMUPathogenSporecasterComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.StoredClouds <= 0)
            return;
            
        _popup.PopupEntity(
            Loc.GetString("cmu-sporecaster-destruction-release"),
            ent.Owner,
            PopupType.LargeCaution);
            
        var coords = Transform(ent.Owner).Coordinates;
        for (var i = 0; i < ent.Comp.StoredClouds; i++)
        {
            if (!_random.Prob(ent.Comp.DestructionReleaseChance))
                continue;
                
            var parasite = SpawnAtPosition(ent.Comp.ParasiteProto, coords);
            // Assign hive
            if (_hive.GetHive(ent.Owner) is { } hive)
                _hive.SetHive(parasite, hive);
        }
    }
  
    // live tracking when a ghost takes the role  
    private void OnPopperSpawnerUsed(Entity<CMUPathogenPopperComponent> popper, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp(args.Spawner, out CMUPathogenSporecasterComponent? caster))
            return;
  
        _hive.SetSameHive(args.Spawner, popper.Owner);
        caster.LivePoppers.Add(popper.Owner);
    }
  
    // Allow xenos from same hive to manually trigger a cloud  
    private void OnInteract(Entity<CMUPathogenSporecasterComponent> ent, ref InteractHandEvent args)
    {
        if (!HasComp<XenoComponent>(args.User))
            return;
  
        if (!_hive.FromSameHive(ent.Owner, args.User))
            return;
            
        if (ent.Comp.StoredClouds <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-sporecaster-empty"),
                ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }
  
        ReleaseCloud(ent);
        args.Handled = true;
    }
    
    private void ReleaseCloud(Entity<CMUPathogenSporecasterComponent> ent)
    {
        ent.Comp.StoredClouds = Math.Max(0, ent.Comp.StoredClouds - 1);
  
        // Reset grow timer if was full
        if (ent.Comp.StoredClouds == ent.Comp.MaxClouds - 1)
            ent.Comp.NextGrowAt = _timing.CurTime + ent.Comp.GrowInterval;
  
        Dirty(ent);
        var cloud = SpawnAtPosition(ent.Comp.SporeCloudProto, Transform(ent.Owner).Coordinates);
        _mycotoxin.SetSporecaster(cloud, ent.Owner);
        _hive.SetSameHive(ent.Owner, cloud);
    }
  
    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPathogenSporecasterComponent>();
  
        while (query.MoveNext(out var uid, out var caster))
        {
            // Grow stored clouds
            if (time >= caster.NextGrowAt && caster.StoredClouds < caster.MaxClouds)
            {
                caster.StoredClouds = Math.Min(caster.MaxClouds, caster.StoredClouds + 1);
                caster.NextGrowAt = time + caster.GrowInterval;
                Dirty(uid, caster);
            }
            
            if (TryComp(uid, out GhostRoleMobSpawnerComponent? spawner))
            {
                for (var i = caster.LivePoppers.Count - 1; i >= 0; i--)
                {
                    var popper = caster.LivePoppers[i];
                    if (TerminatingOrDeleted(popper) || _mobState.IsDead(popper))
                        caster.LivePoppers.RemoveSwap(i);
                }
  
                _ghostRole.SetCurrent((uid, spawner), caster.LivePoppers.Count);
            }
            
            // Auto-release at targets
            if (caster.StoredClouds <= 0)
                continue;
                
            if (time < caster.NextAutoReleaseAt)
                continue;
                
            _nearby.Clear();
            _lookup.GetEntitiesInRange<MobStateComponent>(
                Transform(uid).Coordinates,
                caster.DetectionRange,
                _nearby);
  
            foreach (var (target, _) in _nearby)
            {
                if (HasComp<XenoComponent>(target))
                    continue;
                if (HasComp<SynthComponent>(target))
                    continue;
                if (!HasComp<InfectableComponent>(target))
                    continue;
                if (HasComp<VictimInfectedComponent>(target))
                    continue;
                if (HasComp<CMUPathogenWalkerComponent>(target))
                    continue;
                if (_mobState.IsDead(target))
                    continue;
                if (_protection.IsProtected(target))
                {
                    // Adjacency is not free: slow mask erosion on the same tick as releases.
                    caster.NextAutoReleaseAt = time + caster.AutoReleaseInterval;
                    Dirty(uid, caster);
                    _headgear.TryRipMask(uid, target, caster.MaskStripChance);
                    break;
                }

                caster.NextAutoReleaseAt = time + caster.AutoReleaseInterval;
                Dirty(uid, caster);
                ReleaseCloud((uid, caster));
                break;
            }
        }
    }
  
    private void OnExamine(Entity<CMUPathogenSporecasterComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Examiner) && !_hive.FromSameHive(ent.Owner, args.Examiner))
            return;
  
        var remaining = ent.Comp.NextGrowAt - _timing.CurTime;
        var seconds = (int) Math.Max(0, remaining.TotalSeconds);
  
        args.PushMarkup(Loc.GetString("cmu-sporecaster-examine",
            ("current", ent.Comp.StoredClouds),
            ("max", ent.Comp.MaxClouds),
            ("seconds", seconds)));
    }
}
