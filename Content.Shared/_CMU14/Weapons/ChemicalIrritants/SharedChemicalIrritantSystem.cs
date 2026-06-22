using Content.Shared._AU14.Abominations;
using Content.Shared._CMU14.GasMask;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffect;
using Content.Shared.Storage;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.ChemicalIrritants;

public abstract partial class SharedChemicalIrritantSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStutteringSystem _stutter = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedContainerSystem _con = default!;
    [Dependency] private ItemSlotsSystem _itemSlot = default!;
    [Dependency] private SharedGasMaskSystem _mask = default!;
    [Dependency] private StatusEffectQuerySystem _statusEffects = default!;
    [Dependency] private RMCDazedSystem _daze = default!;
    [Dependency] private RMCSlowSystem _slow = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalIrritantComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<ChemicalIrritantInjectorComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ChemicalIrritantInjectorComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnRejuvenate(Entity<ChemicalIrritantComponent> ent, ref RejuvenateEvent args)
    {
        RemCompDeferred<ChemicalIrritantComponent>(ent);
    }

    private void OnStartCollide(Entity<ChemicalIrritantInjectorComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<MobStateComponent>(args.OtherEntity))
            return;

        if (IsImmuneToIrritants(args.OtherEntity))
            return;

        // Server-only tracking set; no Dirty() needed.
        ent.Comp.ContactedEntities.Add(args.OtherEntity);
    }

    private void OnEndCollide(Entity<ChemicalIrritantInjectorComponent> ent, ref EndCollideEvent args)
    {
        // Server-only tracking set; no Dirty() needed.
        ent.Comp.ContactedEntities.Remove(args.OtherEntity);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        // Update injectors
        var injectorQuery = EntityQueryEnumerator<ChemicalIrritantInjectorComponent>();

        while (injectorQuery.MoveNext(out var uid, out var injector))
        {
            if (time < injector.NextGasInjectionAt)
                continue;

            injector.NextGasInjectionAt = time + injector.TimeBetweenGasInjects;

            // Enforce finite capacity if set
            if (injector.IrritantCapacity >= 0 && injector.IrritantUsed >= injector.IrritantCapacity)
                continue;

            foreach (var victim in injector.ContactedEntities)
            {
                if (!injector.AffectsDead && _mobState.IsDead(victim))
                    continue;

                if (!injector.AffectsInfectedNested &&
                    HasComp<XenoNestedComponent>(victim) &&
                    HasComp<VictimInfectedComponent>(victim))
                    continue;

                ApplyIrritant(victim, injector);
            }
        }

        // Update victims
        var victimQuery = EntityQueryEnumerator<ChemicalIrritantComponent>();

        while (victimQuery.MoveNext(out var uid, out var chem))
        {
            if (time < chem.NextIrritantEffectAt)
                continue;

            chem.NextIrritantEffectAt = time + chem.UpdateEvery;
            UpdateIrritantExposure(uid, chem);
        }
    }

    private void ApplyIrritant(EntityUid victim, ChemicalIrritantInjectorComponent injector)
    {
        if (IsImmuneToIrritants(victim))
            return;

        if (TryGetFilterFromMask(victim, out var filterId, out var filter))
        {
            var filterDamage = new GasMaskFilterDamageComponent
            {
                Damage = injector.FilterDamage * filter.ChemicalIrritantDamageMultiplier,
                Neurotoxin = injector.NeurotoxinFilterDamage
            };

            _mask.DamageFilter(filterId, filter, filterDamage);
            return;
        }

        var time = _timing.CurTime;
        var alreadyExposed = EnsureComp<ChemicalIrritantComponent>(victim, out var chem);

        if (!alreadyExposed)
        {
            chem.LastMessage = time;
            chem.NextIrritantEffectAt = time;
            chem.Profile = injector.Profile;
            chem.DepletionPerTick = injector.DepletionPerTick;
        }

        chem.IrritantAmount += injector.IrritantPerSecond;

        if (injector.IrritantCapacity >= 0)
            injector.IrritantUsed += injector.IrritantPerSecond;

        Dirty(victim, chem);
    }

    private void UpdateIrritantExposure(EntityUid victim, ChemicalIrritantComponent chem)
    {

        if (_mobState.IsDead(victim))
        {
            RemCompDeferred<ChemicalIrritantComponent>(victim);
            return;
        }

        var time = _timing.CurTime;
        var profile = chem.Profile;

        chem.IrritantAmount -= chem.DepletionPerTick;

        if (chem.IrritantAmount <= 0)
        {
            RemCompDeferred<ChemicalIrritantComponent>(victim);
            return;
        }

        Dirty(victim, chem);

        if (chem.IrritantAmount < profile.EffectThreshold)
            return;

        // Eye irritation / screen darkening
        _statusEffects.TryAddStatusEffect<RMCBlindedComponent>(
            victim,
            "Blinded",
            profile.BlurTime,
            true);

        // Random pain jitter
        if (_random.Prob(profile.JitterChance))
            _jitter.DoJitter(victim, profile.JitterTime, true);

        // Mild daze and coughing
        if (_random.Prob(profile.DazeChance))
        {
            _daze.TryDaze(victim, profile.DazeTime, true, stutter: false);
            _stutter.DoStutter(victim, profile.StutterTime, true);
        }

        // Breathing difficulty
        if (_random.Prob(profile.SlowChance))
            _slow.TrySlowdown(victim, profile.SlowTime);

        // Damage (only if damage dict is non-empty)
        if (profile.IrritantDamage.DamageDict.Count > 0)
            _damage.TryChangeDamage(victim, profile.IrritantDamage);

        // Heavy exposure: actual blindness
        if (chem.IrritantAmount >= profile.BlindThreshold)
        {
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
                victim,
                "TemporaryBlindness",
                profile.BlindTime,
                true);
        }

        // Severe exposure
        if (chem.IrritantAmount >= profile.SevereThreshold)
        {
            _daze.TryDaze(victim, profile.DazeTime, true, stutter: false);
            _stutter.DoStutter(victim, profile.StutterTime, true);

            if (_random.Prob(profile.SevereSlowChance))
                _slow.TrySlowdown(victim, profile.SevereSlowTime);
        }
        // High-dose trip/fall
        if (chem.IrritantAmount >= profile.TripThreshold &&
            _random.Prob(profile.TripChance) &&
            time - chem.LastTripTime >= profile.MinimumDelayBetweenTrips)
        {
            chem.LastTripTime = time;
            _stun.TryParalyze(victim, profile.TripStunTime, true);

            _popup.PopupEntity(Loc.GetString("You stumble and trip."), victim, victim, PopupType.MediumCaution);
        }

        // Exposure message (rate-limited)
        if (time >= chem.LastMessage + chem.TimeBetweenMessages)
        {
            chem.LastMessage = time;

            var message = _random.Pick(profile.ExposureMessages);

            _popup.PopupEntity(
                message,
                victim,
                victim,
                PopupType.SmallCaution);
        }
    }

    private bool IsImmuneToIrritants(EntityUid victim)
    {
        return HasComp<SynthComponent>(victim)
            || HasComp<XenoComponent>(victim)
            || HasComp<AbominationComponent>(victim);
    }
    private bool TryGetFilterFromMask(EntityUid victim, out EntityUid filterId, out GasMaskFilterComponent filter)
    {
        filterId = EntityUid.Invalid;
        filter = null!;

        if (!TryComp<ContainerManagerComponent>(victim, out var manager))
            return false;

        // Check direct mask slot
        if (_con.TryGetContainer(victim, "mask", out var maskContainer, manager))
        {
            foreach (var maskItem in maskContainer.ContainedEntities)
            {
                if (TryGetFilterFromItem(maskItem, out filterId, out filter))
                    return true;
            }
        }

        // Check gas mask stored inside helmet accessory (head slot -> storage -> item with filter)
        if (_con.TryGetContainer(victim, "head", out var headContainer, manager))
        {
            foreach (var headItem in headContainer.ContainedEntities)
            {
                if (!TryComp<StorageComponent>(headItem, out var storage) || storage.Container is null)
                    continue;

                foreach (var accessory in storage.Container.ContainedEntities)
                {
                    if (TryGetFilterFromItem(accessory, out filterId, out filter))
                        return true;
                }
            }
        }

        return false;
    }
    public void ReduceIrritant(EntityUid victim, float amount)
    {
        if (!TryComp<ChemicalIrritantComponent>(victim, out var chem))
            return;

        var actual = amount * chem.Profile.DyloveneEfficiency;
        chem.IrritantAmount = MathF.Max(0, chem.IrritantAmount - actual);

        if (chem.IrritantAmount <= 0)
        {
            RemCompDeferred<ChemicalIrritantComponent>(victim);
            return;
        }

        Dirty(victim, chem);
    }
    private bool TryGetFilterFromItem(EntityUid item, out EntityUid filterId, out GasMaskFilterComponent filter)
    {
        filterId = EntityUid.Invalid;
        filter = null!;

        if (!TryComp<ItemSlotsComponent>(item, out var slots))
            return false;

        if (!_itemSlot.TryGetSlot(item, "filter", out var slot, slots))
            return false;

        if (slot.ContainerSlot?.ContainedEntity is not EntityUid filterUid)
            return false;

        if (!TryComp<GasMaskFilterComponent>(filterUid, out var filterComp))
            return false;

        if (filterComp.Integrity <= 0)
            return false;

        filterId = filterUid;
        filter = filterComp;
        return true;
    }

    /// <summary>
    /// Applies irritant directly to a victim without going through an injector entity.
    /// Uses the victim's existing profile if already exposed, or default values otherwise.
    /// </summary>
    public void ApplyIrritantDirect(
        EntityUid victim,
        float amount,
        ChemicalIrritantProfile profile,
        float? depletionPerTick = null)
    {
        if (IsImmuneToIrritants(victim))
            return;

        if (TryGetFilterFromMask(victim, out _, out _))
            return;

        var alreadyExposed = EnsureComp<ChemicalIrritantComponent>(victim, out var chem);

        if (!alreadyExposed)
        {
            var time = _timing.CurTime;
            chem.LastMessage = time;
            chem.NextIrritantEffectAt = time;
            chem.Profile = profile;

            if (depletionPerTick != null)
                chem.DepletionPerTick = depletionPerTick.Value;
        }

        chem.IrritantAmount += amount;
        Dirty(victim, chem);
    }
}
