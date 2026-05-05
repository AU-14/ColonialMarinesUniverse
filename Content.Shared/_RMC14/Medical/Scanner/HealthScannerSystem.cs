using Content.Shared.Atmos.Rotting;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Temperature;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Medical.Scanner;

public sealed class HealthScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private readonly RMCHandsSystem _rmcHands = default!;
    [Dependency] private readonly SharedRMCTemperatureSystem _rmcTemperature = default!;
    [Dependency] private readonly RMCUnrevivableSystem _rmcUnrevivable = default!;
    [Dependency] private readonly SharedRottingSystem _rotting = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedWoundsSystem _wounds = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HealthScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HealthScannerComponent, DoAfterAttemptEvent<HealthScannerDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<HealthScannerComponent, HealthScannerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<HealthScannerComponent> scanner, ref AfterInteractEvent args)
    {
        if (!args.CanReach ||
            args.Target is not { } target ||
            !CanUseHealthScannerPopup(scanner, args.User, ref target))
        {
            return;
        }

        var delay = _skills.GetDelay(args.User, scanner);
        var ev = new HealthScannerDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, ev, scanner, target, scanner)
        {
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick
        };

        if (delay > TimeSpan.Zero)
        {
            var name = Loc.GetString("zzzz-the", ("ent", target));
            _popup.PopupClient($"You start fumbling around with {name}...", target, args.User);
        }

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfterAttempt(Entity<HealthScannerComponent> ent, ref DoAfterAttemptEvent<HealthScannerDoAfterEvent> args)
    {
        var doAfter = args.DoAfter.Args;
        if (doAfter.Target is not { } target)
            return;

        if (!CanUseHealthScannerPopup(ent, doAfter.User, ref target))
        {
            args.Cancel();
            return;
        }

        var userCoords = Transform(doAfter.User).Coordinates;
        if (!_transform.InRange(userCoords, args.DoAfter.UserPosition, doAfter.MovementThreshold))
            args.Cancel();
    }

    private void OnDoAfter(Entity<HealthScannerComponent> scanner, ref HealthScannerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (TryComp(scanner, out UseDelayComponent? useDelay))
            _useDelay.TryResetDelay((scanner, useDelay));

        scanner.Comp.Target = target;
        Dirty(scanner);

        _audio.PlayPredicted(scanner.Comp.Sound, scanner, args.User);
        UpdateUI(scanner);

        if (scanner.Comp.Target != null)
            _ui.OpenUi(scanner.Owner, HealthScannerUIKey.Key, args.User);
    }

    /// <param name="scanner">The Health Scanner</param>
    /// <param name="user"> The entity using the Health Scanner</param>
    /// <param name="target">The entity being scanned by the Health Scanner. May be changed</param>
    /// <returns></returns>
    private bool CanUseHealthScannerPopup(Entity<HealthScannerComponent> scanner, EntityUid user, ref EntityUid target)
    {
        SharedEntityStorageComponent? entityStorage = null;
        if (HasComp<HealthScannableContainerComponent>(target) && _entityStorage.ResolveStorage(target, ref entityStorage))
        {
            foreach (var entity in entityStorage.Contents.ContainedEntities)
            {
                if (HasComp<DamageableComponent>(entity) &&
                HasComp<MobStateComponent>(entity) &&
                HasComp<MobThresholdsComponent>(entity))
                {
                    target = entity;
                    break;
                }
            }
        }

        if (!HasComp<DamageableComponent>(target) ||
            !HasComp<MobStateComponent>(target) ||
            !HasComp<MobThresholdsComponent>(target))
        {
            _popup.PopupClient("You can't analyze that!", target, user);
            return false;
        }

        if (TryComp(scanner, out UseDelayComponent? useDelay) &&
            _useDelay.IsDelayed((scanner, useDelay)))
        {
            return false;
        }

        var ev = new HealthScannerAttemptTargetEvent();
        RaiseLocalEvent(target, ref ev);
        if (ev.Cancelled)
        {
            if (ev.Popup != null)
                _popup.PopupClient(ev.Popup, target, user);

            return false;
        }

        return true;
    }

    private void UpdateUI(Entity<HealthScannerComponent> scanner)
    {
        if (scanner.Comp.Target is not { } target)
            return;

        if (TerminatingOrDeleted(target))
        {
            if (!TerminatingOrDeleted(scanner))
                _ui.CloseUi(scanner.Owner, HealthScannerUIKey.Key);

            scanner.Comp.Target = null;
            return;
        }

        if (!_rmcHands.TryGetHolder(scanner, out _))
            return;

        FixedPoint2 blood = 0;
        FixedPoint2 maxBlood = 0;
        if (_rmcBloodstream.TryGetBloodSolution(target, out var bloodstream))
        {
            blood = bloodstream.Volume;
            maxBlood = bloodstream.MaxVolume;
        }

        _rmcBloodstream.TryGetChemicalSolution(target, out _, out var chemicals);
        _rmcTemperature.TryGetCurrentTemperature(target, out var temperature);

        var bleeding = _rmcBloodstream.IsBleeding(target);
        var state = new HealthScannerBuiState(GetNetEntity(target), blood, maxBlood, temperature, chemicals, bleeding);
        FillBaseMedicalReadout(target, state);

        EntityUid? examiner = null;
        if (_rmcHands.TryGetHolder(scanner, out var holder))
            examiner = holder;
        var buildEv = new HealthScannerBuildStateEvent(scanner.Owner, target, examiner, state);
        RaiseLocalEvent(scanner.Owner, ref buildEv);

        _ui.SetUiState(scanner.Owner, HealthScannerUIKey.Key, state);
    }

    private void FillBaseMedicalReadout(EntityUid target, HealthScannerBuiState state)
    {
        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            state.Damage.Brute = damageable.DamagePerGroup.GetValueOrDefault("Brute");
            state.Damage.Burn = damageable.DamagePerGroup.GetValueOrDefault("Burn");
            state.Damage.Toxin = damageable.DamagePerGroup.GetValueOrDefault("Toxin");
            state.Damage.Airloss = damageable.DamagePerGroup.GetValueOrDefault("Airloss");
            state.Damage.Genetic = damageable.DamagePerGroup.GetValueOrDefault("Genetic");
            state.Damage.Total = damageable.TotalDamage;
        }

        if (TryComp<WoundedComponent>(target, out var wounded))
        {
            state.Damage.UntreatedBruteWounds = _wounds.HasUntreated((target, wounded), wounded.BruteWoundGroup);
            state.Damage.UntreatedBurnWounds = _wounds.HasUntreated((target, wounded), wounded.BurnWoundGroup);
        }

        if (TryComp<MobStateComponent>(target, out var mob))
            state.MobState = mob.CurrentState;

        if (_thresholds.TryGetIncapThreshold(target, out var incap))
        {
            state.HasIncapThreshold = true;
            state.IncapThreshold = incap.Value;
        }

        if (_thresholds.TryGetDeadThreshold(target, out var dead))
        {
            state.HasDeadThreshold = true;
            state.DeadThreshold = dead.Value;
        }

        state.VictimBurst = HasComp<VictimBurstComponent>(target);
        state.VictimInfected = HasComp<VictimInfectedComponent>(target);
        state.HolocardXeno = TryComp<HolocardStateComponent>(target, out var holocard) &&
            holocard.HolocardStatus == HolocardStatus.Xeno;
        state.PermaDead = _mob.IsDead(target) &&
            (state.VictimBurst ||
             _rotting.IsRotten(target) ||
             _rmcUnrevivable.IsUnrevivable(target) ||
             HasComp<RMCDefibrillatorBlockedComponent>(target));

        FillAdviceReadout(state);
    }

    private void FillAdviceReadout(HealthScannerBuiState state)
    {
        var advice = state.Advice;
        var damage = state.Damage;
        var isDead = state.MobState == MobState.Dead;
        var isCritical = state.MobState == MobState.Critical;
        var chemicals = state.Chemicals;

        if (isDead)
        {
            if (state.HasDeadThreshold)
            {
                if (state.DeadThreshold + 30 < damage.Total &&
                    chemicals != null &&
                    !chemicals.ContainsReagent("CMEpinephrine", null))
                {
                    advice.NeedsEpinephrine = true;
                }
                else
                {
                    advice.ShowRepeatedDefibWarning = state.DeadThreshold - 20 <= damage.Total &&
                        !damage.UntreatedBruteWounds &&
                        !damage.UntreatedBurnWounds;
                    advice.ShowDefib = !advice.ShowRepeatedDefibWarning && state.DeadThreshold > damage.Total;
                }
            }

            advice.ShowCpr = true;
        }

        advice.ShowLarvaBursted = state.VictimBurst;
        advice.ShowLarvaSurgery = !state.VictimBurst && (state.VictimInfected || state.HolocardXeno);
        advice.ShowBruteWounds = damage.UntreatedBruteWounds;
        advice.ShowBurnWounds = damage.UntreatedBurnWounds;

        if (state.Blood < state.MaxBlood)
        {
            var bloodPercent = state.Blood / state.MaxBlood;
            advice.ShowBloodPack = bloodPercent < 0.85;
            advice.ShowFood = bloodPercent < 0.9 &&
                chemicals != null &&
                !chemicals.ContainsReagent("Nutriment", null);
        }

        if (damage.Airloss > 0 && !isDead)
        {
            advice.ShowCprCrit = damage.Airloss > 10 && isCritical;
            advice.ShowDexalin = damage.Airloss > 30 &&
                chemicals != null &&
                !chemicals.ContainsReagent("CMDexalin", null);
        }

        advice.ShowBicaridine = damage.Brute > 30 &&
            chemicals != null &&
            !chemicals.ContainsReagent("CMBicaridine", null) &&
            !isDead;
        advice.ShowKelotane = damage.Burn > 30 &&
            chemicals != null &&
            !chemicals.ContainsReagent("CMKelotane", null) &&
            !isDead;
        advice.ShowDylovene = damage.Toxin > 10 &&
            chemicals != null &&
            !chemicals.ContainsReagent("CMDylovene", null) &&
            !chemicals.ContainsReagent("Inaprovaline", null) &&
            !isDead;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var scanners = EntityQueryEnumerator<HealthScannerComponent>();
        while (scanners.MoveNext(out var uid, out var active))
        {
            if (time < active.UpdateAt)
                continue;

            active.UpdateAt = time + active.UpdateCooldown;
            UpdateUI((uid, active));
        }
    }
}
