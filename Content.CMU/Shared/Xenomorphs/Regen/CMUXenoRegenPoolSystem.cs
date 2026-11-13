using System.Numerics;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.RecoveryNode;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.CCVar;
using Content.Shared.Alert;
using Content.Shared.Alert.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Xenomorphs.Regen;

public sealed partial class CMUXenoRegenPoolSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, CMUXenoPassiveRegenAttemptEvent>(OnPassiveRegen);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, CMUXenoHealerHealEvent>(OnHealerHeal);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, NewXenoEvolvedEvent>(OnEvolved);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, XenoDevolvedEvent>(OnDevolved);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, GetGenericAlertCounterAmountEvent>(OnAlertCounter);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUXenoRegenPoolComponent, RejuvenateEvent>(OnRejuvenate);
        Subs.CVar(_config, CCVars.CMUXenoRegenPoolEnabled, OnEnabledChanged, true);
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUXenoRegenPoolComponent>();
        while (query.MoveNext(out var uid, out var pool))
            Reset((uid, pool));
    }

    private void OnMapInit(Entity<CMUXenoRegenPoolComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsServer)
            Reset(ent);
    }

    private void Reset(Entity<CMUXenoRegenPoolComponent> ent)
    {
        ent.Comp.Charges = Math.Max(0, ent.Comp.MaxCharges);
        ent.Comp.NextDropAt = TimeSpan.Zero;
        ent.Comp.RechargeAt = null;
        ent.Comp.BelowThreshold = IsBelowThreshold(ent);
        Dirty(ent);
        UpdateAlert(ent);
    }

    private bool IsBelowThreshold(Entity<CMUXenoRegenPoolComponent> ent)
    {
        return _thresholds.TryGetIncapThreshold(ent, out var threshold) &&
               threshold > 0 &&
               _damage.GetTotalDamage(ent.Owner) > threshold.Value * (1 - ent.Comp.HealthThreshold);
    }

    private void OnDamageChanged(Entity<CMUXenoRegenPoolComponent> ent, ref DamageChangedEvent args)
    {
        if (_net.IsClient || !_enabled)
            return;

        var below = IsBelowThreshold(ent);
        if (below == ent.Comp.BelowThreshold)
            return;

        if (below && _timing.CurTime >= ent.Comp.NextDropAt)
        {
            var oldCharges = ent.Comp.Charges;
            ent.Comp.Charges = Math.Max(0, ent.Comp.Charges - 1);
            ent.Comp.NextDropAt = _timing.CurTime + ent.Comp.GracePeriod;
            if (oldCharges > 0 && ent.Comp.Charges == 0)
                _popup.PopupEntity(Loc.GetString("cmu-xeno-regen-depleted"), ent, ent, PopupType.LargeCaution);
            UpdateAlert(ent);
        }

        ent.Comp.BelowThreshold = below;
        Dirty(ent);
    }

    private void OnPassiveRegen(Entity<CMUXenoRegenPoolComponent> ent, ref CMUXenoPassiveRegenAttemptEvent args)
    {
        if (_enabled && ent.Comp.Charges <= 0)
            args.Multiplier *= args.OnFriendlyWeeds ? ent.Comp.DepletedWeedsHealingMultiplier : 0f;
    }

    private void OnHealerHeal(Entity<CMUXenoRegenPoolComponent> ent, ref CMUXenoHealerHealEvent args)
    {
        if (_net.IsClient || !_enabled)
            return;

        ent.Comp.Charges = Math.Max(0, ent.Comp.MaxCharges);
        ent.Comp.RechargeAt = null;
        Dirty(ent);
        UpdateAlert(ent);
    }

    private void OnEvolved(Entity<CMUXenoRegenPoolComponent> ent, ref NewXenoEvolvedEvent args)
        => Transfer(args.OldXeno.Owner, ent);

    private void OnDevolved(Entity<CMUXenoRegenPoolComponent> ent, ref XenoDevolvedEvent args)
        => Transfer(args.OldXeno, ent);

    private void Transfer(EntityUid old, Entity<CMUXenoRegenPoolComponent> ent)
    {
        if (_net.IsClient || !TryComp(old, out CMUXenoRegenPoolComponent? previous))
            return;

        var spent = Math.Max(0, previous.MaxCharges - previous.Charges);
        ent.Comp.Charges = Math.Max(0, ent.Comp.MaxCharges - spent);
        ent.Comp.NextDropAt = previous.NextDropAt;
        ent.Comp.BelowThreshold = IsBelowThreshold(ent);
        ent.Comp.RechargeAt = null;
        Dirty(ent);
        UpdateAlert(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || !_enabled)
            return;

        var query = EntityQueryEnumerator<CMUXenoRegenPoolComponent>();
        while (query.MoveNext(out var uid, out var pool))
        {
            if (pool.Charges >= pool.MaxCharges || _mobState.IsDead(uid) ||
                !HasComp<XenoRestingComponent>(uid) || !HasRechargeSource(uid, pool.RechargeRange))
            {
                if (pool.RechargeAt != null)
                {
                    pool.RechargeAt = null;
                    Dirty(uid, pool);
                }
                continue;
            }

            if (pool.RechargeAt == null)
            {
                pool.RechargeAt = _timing.CurTime + pool.RechargeTime;
                Dirty(uid, pool);
            }
            else if (_timing.CurTime >= pool.RechargeAt)
            {
                pool.Charges++;
                pool.RechargeAt = pool.Charges < pool.MaxCharges ? _timing.CurTime + pool.RechargeTime : null;
                Dirty(uid, pool);
                UpdateAlert((uid, pool));
            }
        }
    }

    private bool HasRechargeSource(EntityUid xeno, float range)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(xeno, range, _nearby);
        var position = _transform.GetWorldPosition(xeno);
        foreach (var source in _nearby)
        {
            if (source == xeno || Vector2.DistanceSquared(position, _transform.GetWorldPosition(source)) > range * range ||
                !_hive.FromSameHive(xeno, source))
                continue;

            if (HasComp<HiveClusterComponent>(source) || HasComp<HiveCoreComponent>(source) ||
                HasComp<RecoveryNodeComponent>(source) ||
                (HasComp<XenoEvolutionGranterComponent>(source) && !_mobState.IsDead(source)))
            {
                return true;
            }
        }

        return false;
    }

    private void OnRejuvenate(Entity<CMUXenoRegenPoolComponent> ent, ref RejuvenateEvent args)
    {
        if (_net.IsServer)
            Reset(ent);
    }

    private void OnRemove(Entity<CMUXenoRegenPoolComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsServer)
            _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void UpdateAlert(Entity<CMUXenoRegenPoolComponent> ent)
    {
        if (!_enabled)
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
            return;
        }

        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert,
            dynamicMessage: Loc.GetString("cmu-xeno-regen-alert-count", ("charges", ent.Comp.Charges), ("max", ent.Comp.MaxCharges)));
    }

    private void OnAlertCounter(Entity<CMUXenoRegenPoolComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (!args.Handled && args.Alert.ID == ent.Comp.Alert.Id)
            args.Amount = ent.Comp.Charges;
    }

    private void OnExamined(Entity<CMUXenoRegenPoolComponent> ent, ref ExaminedEvent args)
    {
        if (!_enabled || !HasComp<XenoComponent>(args.Examiner))
            return;

        using (args.PushGroup(nameof(CMUXenoRegenPoolComponent)))
        {
            args.PushText(Loc.GetString("cmu-xeno-regen-examine", ("charges", ent.Comp.Charges), ("max", ent.Comp.MaxCharges)));
        }
    }
}
