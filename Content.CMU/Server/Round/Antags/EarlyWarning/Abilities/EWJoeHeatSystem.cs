using Content.Server.Administration.Logs;
using Content.Server.Decals;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Smoke;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared.Alert;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Decals;
using Content.Shared.Fluids.Components;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

using Content.Shared.Mobs.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Actions.Events;
namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Phase E5: the Heat resource, Evolution's stat changes (level 3), Overclock (formerly Smokescreen)
/// and Emergency Coolant. Heat is server state; the Joe sees it through an alert. The heat component is
/// granted by the level-3 table, so stand-down removes it and the shutdown here puts Evolution back.
/// Overclock's speed lives in the shared <see cref="EWJoeOverclockSystem"/>.
/// </summary>
public sealed partial class EWJoeHeatSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PuddleSystem _puddles = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private RMCStatusEffectSystem _statusEffects = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";

    /// <summary>The RMC blood puddle entity; its decal goes with it when it is deleted.</summary>
    public const string BloodPuddlePrototype = "BloodDecalPuddle";

    private readonly HashSet<Entity<PuddleComponent>> _puddleSet = new();
    private readonly HashSet<Entity<EvenSmokeComponent>> _smokeSet = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeHeatComponent, ComponentStartup>(OnHeatStartup);
        SubscribeLocalEvent<EWJoeHeatComponent, ComponentShutdown>(OnHeatShutdown);
        SubscribeLocalEvent<EWJoeLevelChangedEvent>(OnLevelChanged);

        SubscribeLocalEvent<EWJoeComponent, EWJoeOverclockActionEvent>(OnOverclock);
        SubscribeLocalEvent<EWJoeComponent, EWJoeCoolantActionEvent>(OnCoolant);
        SubscribeLocalEvent<EWJoeHeatGatedActionComponent, ActionAttemptEvent>(OnGatedAttempt);
        // after the yautja system, in this system: the arsenal already orders its own leap handler before it,
        // and one system must give every subscription to an event the same ordering
        SubscribeLocalEvent<EWJoeHeatComponent, YautjaLeapDoAfterEvent>(OnZLeapLaunched, after: [typeof(YautjaAbilitySystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EWJoeHeatComponent, EWJoeComponent>();
        while (query.MoveNext(out var uid, out var heat, out _))
        {
            if (now < heat.NextTick)
                continue;

            heat.NextTick = now + TimeSpan.FromSeconds(1);
            Tick((uid, heat), now);
        }
    }

    #region Heat

    private void OnHeatStartup(Entity<EWJoeHeatComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        UpdateAlert(ent);
    }

    private void OnHeatShutdown(Entity<EWJoeHeatComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        RemComp<EWJoeOverclockComponent>(ent);
        RevertEvolution(ent);
    }

    private void Tick(Entity<EWJoeHeatComponent> ent, TimeSpan now)
    {
        var heat = ent.Comp;

        // A-18: decay always runs; Overclock adds on top of it
        heat.Heat = Math.Max(0f, heat.Heat - heat.PassiveDecay);

        // ABIL-01: a downed Joe cannot throttle back, so its overclock stops with it
        if (HasComp<EWJoeOverclockComponent>(ent) && !_mobState.IsAlive(ent))
            RemComp<EWJoeOverclockComponent>(ent);

        if (TryComp(ent, out EWJoeOverclockComponent? overclock) && overclock.LifeStage <= ComponentLifeStage.Running)
        {
            AddHeat(ent, heat.OverclockHeat);
            // the overheat removed the overclock; do not puff from a component that is on its way out
            if (!heat.Overheated && now >= overclock.NextPuff && HasComp<EWJoeOverclockComponent>(ent))
            {
                overclock.NextPuff = now + overclock.PuffInterval;
                SpawnSmokeSeed(ent, overclock);
            }
        }

        if (heat.Overheated && heat.Heat < heat.OverheatClear)
        {
            heat.Overheated = false;
            _messages.Notify(ent, Loc.GetString("cmu-ew-joe-heat-cooled"));
        }

        UpdateAlert(ent);
    }

    /// <summary>Adds heat; reaching the maximum latches the overheat.</summary>
    public void AddHeat(Entity<EWJoeHeatComponent> ent, float amount)
    {
        var heat = ent.Comp;
        heat.Heat = Math.Clamp(heat.Heat + amount, 0f, heat.Max);
        if (heat.Heat >= heat.Max && !heat.Overheated)
            Overheat(ent);

        UpdateAlert(ent);
    }

    /// <summary>
    /// Pays for an ability. Free below level 3 (no heat component yet); refused while overheated;
    /// otherwise always paid, and paying past the maximum is what triggers the overheat (A-18: heat
    /// is the limiter, not a hard gate).
    /// </summary>
    public bool TrySpendHeat(EntityUid joe, float amount)
    {
        if (!TryComp(joe, out EWJoeHeatComponent? heat))
            return true;

        if (heat.Overheated)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-heat-overheated"), joe, joe, PopupType.SmallCaution);
            return false;
        }

        AddHeat((joe, heat), amount);
        return true;
    }

    private void Overheat(Entity<EWJoeHeatComponent> ent)
    {
        ent.Comp.Overheated = true;

        // the Joe's own modifier set halves Heat; D11 wants the flat ten
        var damage = new DamageSpecifier(_proto.Index(HeatDamage), ent.Comp.OverheatDamage);
        _damageable.TryChangeDamage(ent.Owner, damage, ignoreResistances: true, origin: ent.Owner);
        // Evolution cuts ordinary slows to 0.4x; the self-penalty uses the super-slow track, which it leaves alone.
        // RMC removes the super-slow and refreshes speed from ComponentRemove, so it ends cleanly.
        _slow.TrySuperSlowdown(ent.Owner, ent.Comp.OverheatSlow, ignoreImmunity: true);

        if (HasComp<EWJoeOverclockComponent>(ent))
            RemComp<EWJoeOverclockComponent>(ent);

        _messages.Notify(ent, Loc.GetString("cmu-ew-joe-heat-overheat"));
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Early Warning Joe {ToPrettyString(ent):joe} overheated");
    }

    private void UpdateAlert(Entity<EWJoeHeatComponent> ent)
    {
        var max = _alerts.GetMaxSeverity(ent.Comp.Alert);
        var severity = (short) ContentHelpers.RoundToLevels(ent.Comp.Heat, ent.Comp.Max, max + 1);
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, severity, dynamicMessage: $"{(int) ent.Comp.Heat} / {(int) ent.Comp.Max}");
    }

    #endregion

    #region Evolution

    /// <summary>
    /// P3: the Joe already owns the stun-resistance and slow-modifier components, so the level table
    /// cannot grant new values. Applied once when level 3 is reached; the originals are recorded on the
    /// heat component and put back on stand-down.
    /// </summary>
    private void OnLevelChanged(ref EWJoeLevelChangedEvent args)
    {
        if (args.NewLevel < 3 || !TryComp(args.Joe, out EWJoeHeatComponent? heat) || heat.EvolutionApplied)
            return;

        heat.EvolutionApplied = true;

        if (TryComp(args.Joe, out RMCStunResistanceComponent? stun))
            heat.RestoreStunResistance = stun.Resistance;
        _statusEffects.GiveStunResistance(args.Joe, heat.EvolutionStunResistance);

        if (TryComp(args.Joe, out RMCSpeciesSlowdownModifierComponent? slow))
        {
            heat.RestoreSlowModifier = slow.SlowModifier;
            slow.SlowModifier = heat.EvolutionSlowModifier;
            Dirty(args.Joe, slow);
        }

        _messages.Notify(args.Joe, Loc.GetString("cmu-ew-joe-evolution"));
    }

    /// <summary>Restores only what Evolution set; a value another system changed since is left alone.</summary>
    private void RevertEvolution(Entity<EWJoeHeatComponent> ent)
    {
        if (!ent.Comp.EvolutionApplied)
            return;

        ent.Comp.EvolutionApplied = false;

        if (TryComp(ent, out RMCStunResistanceComponent? stun) && Math.Abs(stun.Resistance - ent.Comp.EvolutionStunResistance) < 0.001f)
            _statusEffects.GiveStunResistance(ent, ent.Comp.RestoreStunResistance);

        if (TryComp(ent, out RMCSpeciesSlowdownModifierComponent? slow) && Math.Abs(slow.SlowModifier - ent.Comp.EvolutionSlowModifier) < 0.001f)
        {
            slow.SlowModifier = ent.Comp.RestoreSlowModifier;
            Dirty(ent, slow);
        }
    }

    #endregion

    /// <summary>ABIL-07: an overheated core refuses its heat-hungry actions before their cooldown starts.</summary>
    private void OnGatedAttempt(Entity<EWJoeHeatGatedActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled || !TryComp(args.User, out EWJoeHeatComponent? heat) || !heat.Overheated)
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("cmu-ew-joe-heat-overheated");
    }

    #region Z-Level Leap

    /// <summary>ABIL-06: a leap the yautja system actually launched costs its heat; a refused one costs nothing.</summary>
    private void OnZLeapLaunched(Entity<EWJoeHeatComponent> ent, ref YautjaLeapDoAfterEvent args)
    {
        if (args.Cancelled || !args.Handled || !HasComp<YautjaLeapingComponent>(ent) || !TryComp(ent, out EWJoeComponent? joe))
            return;

        TrySpendHeat(ent.Owner, joe.ZLeapHeat);
    }

    #endregion

    #region Overclock

    private void OnOverclock(Entity<EWJoeComponent> ent, ref EWJoeOverclockActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<EWJoeOverclockComponent>(ent))
        {
            args.Handled = true;
            RemComp<EWJoeOverclockComponent>(ent);
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-overclock-off"), ent, ent);
            return;
        }

        // ABIL-07: refused without spending the cooldown
        if (TryComp(ent, out EWJoeHeatComponent? heat) && heat.Overheated)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-heat-overheated"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        StartOverclock(ent);

        // ABIL-05: the wheel shows a running overclock as active; the overclock's shutdown clears it
        if (Exists(args.Action) && TryComp(ent, out EWJoeOverclockComponent? overclock))
        {
            overclock.Action = args.Action;
            _actions.SetToggled(new Entity<ActionComponent?>(args.Action, args.Action.Comp), true);
        }
    }

    /// <summary>Starts Overclock: a cloud now, another every puff interval, speed while it lasts.</summary>
    public void StartOverclock(EntityUid joe)
    {
        var overclock = EnsureComp<EWJoeOverclockComponent>(joe);
        overclock.NextPuff = _timing.CurTime + overclock.PuffInterval;
        SpawnSmokeSeed(joe, overclock);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-overclock-on"), joe, joe);
    }

    /// <summary>
    /// The RMC spreader only guards neighbouring tiles against doubling up, so the Joe's own tile is
    /// checked here. Smoke is anchored on the tile centre, so the check and the spawn both use it.
    /// </summary>
    private void SpawnSmokeSeed(EntityUid joe, EWJoeOverclockComponent overclock)
    {
        var xform = Transform(joe);
        var coordinates = xform.Coordinates;
        if (xform.GridUid is { } grid && TryComp(grid, out MapGridComponent? mapGrid))
            coordinates = _map.GridTileToLocal(grid, mapGrid, _map.TileIndicesFor(grid, mapGrid, coordinates));

        _smokeSet.Clear();
        _lookup.GetEntitiesInRange(coordinates, 0.1f, _smokeSet);
        foreach (var existing in _smokeSet)
        {
            if (MetaData(existing).EntityPrototype?.ID == overclock.SmokePrototype.Id)
                return;
        }

        SpawnAtPosition(overclock.SmokePrototype, coordinates);
    }

    #endregion

    #region Emergency Coolant

    private void OnCoolant(Entity<EWJoeComponent> ent, ref EWJoeCoolantActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryComp(ent, out EWJoeHeatComponent? heat) && heat.Heat <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-coolant-cool"), ent, ent);
            return;
        }

        var cooled = Coolant(ent);
        _popup.PopupEntity(cooled > 0
                ? Loc.GetString("cmu-ew-joe-coolant-done", ("amount", (int) cooled))
                : Loc.GetString("cmu-ew-joe-coolant-none"),
            ent, ent);
    }

    /// <summary>
    /// Balance A-10 as tuned in playtest 3: blood droplet and splatter decals within range give five
    /// heat each, blood puddles half a heat per unit, and one use sheds at most
    /// <see cref="EWJoeComponent.CoolantCap"/> (never more than the heat the Joe has). Absorption stops
    /// once that budget is met, so blood is not wasted. Returns the heat removed.
    /// </summary>
    public float Coolant(Entity<EWJoeComponent> joe)
    {
        if (!TryComp(joe, out EWJoeHeatComponent? heat))
            return 0f;

        var budget = Math.Min(joe.Comp.CoolantCap, heat.Heat);
        if (budget <= 0f)
            return 0f;

        var absorbed = 0f;
        var xform = Transform(joe);

        if (xform.GridUid is { } grid)
        {
            var local = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(joe, xform)).Position;
            foreach (var (index, _) in _decals.GetDecalsInRange(grid, local, joe.Comp.CoolantRange, IsBloodDecal))
            {
                if (absorbed >= budget)
                    break;

                if (_decals.RemoveDecal(grid, index))
                    absorbed += joe.Comp.CoolantPerDecal;
            }
        }

        if (absorbed < budget)
        {
            _puddleSet.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, joe.Comp.CoolantRange, _puddleSet);
            foreach (var puddle in _puddleSet)
            {
                if (absorbed >= budget)
                    break;

                if (MetaData(puddle).EntityPrototype?.ID != BloodPuddlePrototype)
                    continue;

                absorbed += (float) _puddles.CurrentVolume(puddle, puddle.Comp) * joe.Comp.CoolantPerPuddleUnit;
                QueueDel(puddle);
            }
        }

        var cooled = Math.Min(absorbed, budget);
        if (cooled > 0f)
        {
            heat.Heat = Math.Max(0f, heat.Heat - cooled);
            UpdateAlert((joe, heat));
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"Early Warning Joe {ToPrettyString(joe):joe} absorbed blood for {cooled} heat");
        }

        return cooled;
    }

    private static bool IsBloodDecal(Decal decal)
    {
        return decal.Id.StartsWith("CMUBloodDroplet", StringComparison.Ordinal)
            || decal.Id.StartsWith("CMUBloodSplatter", StringComparison.Ordinal);
    }

    #endregion
}
