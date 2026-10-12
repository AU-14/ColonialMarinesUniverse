using System.Linq;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Blood;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Blood;

/// <summary>
/// CMU blood: blood types and compatibility (red cells and plasma follow their real-life rules), squeezing blood packs, IV pressure bags, plasma that slows bleeding and
/// saline that stands in for blood but poisons the patient when too much of their blood is saline.
/// </summary>
public sealed class CMUBloodSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<ReagentPrototype> PlasmaReagent = "CMUBloodPlasma";
    private static readonly ProtoId<ReagentPrototype> SalineReagent = "Saline";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    /// <summary>Toxin damage per unit of mismatched blood transfused.</summary>
    private const float MismatchToxinPerUnit = 0.2f;

    /// <summary>Seconds of slowed bleeding per unit of plasma, up to <see cref="MaxPlasmaClotting"/>.</summary>
    private const float PlasmaClottingSecondsPerUnit = 2f;
    private static readonly TimeSpan MaxPlasmaClotting = TimeSpan.FromMinutes(5);

    /// <summary>Above this share of blood volume, saline starts poisoning the patient.</summary>
    public const float SalineSafeFraction = 0.25f;

    /// <summary>Toxin per second for each whole blood volume of saline past the safe share.</summary>
    private const float SalineToxinPerSecond = 6f;

    /// <summary>The body slowly replaces saline with real blood.</summary>
    private static readonly FixedPoint2 SalineClearedPerSecond = FixedPoint2.New(0.6f);

    private static readonly TimeSpan MismatchPopupCooldown = TimeSpan.FromSeconds(10);
    private readonly Dictionary<EntityUid, TimeSpan> _nextMismatchPopup = new();

    private TimeSpan _nextSalineTick;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHumanMedicalComponent, MapInitEvent>(OnHumanMapInit);
        SubscribeLocalEvent<CMUBloodPackTypeComponent, SolutionChangedEvent>(OnTypedPackSolutionChanged);
        SubscribeLocalEvent<CMUBloodPackTypeComponent, ExaminedEvent>(OnTypedPackExamined);
        SubscribeLocalEvent<CMUPerishableBloodComponent, SolutionChangedEvent>(OnPerishableSolutionChanged);
        SubscribeLocalEvent<CMUPerishableBloodComponent, ExaminedEvent>(OnPerishableExamined);
        SubscribeLocalEvent<BloodPackComponent, UseInHandEvent>(OnPackUseInHand);

        SubscribeLocalEvent<IVDripComponent, InteractUsingEvent>(OnIVInteractUsing);
        SubscribeLocalEvent<IVDripComponent, GetVerbsEvent<AlternativeVerb>>(OnIVAltVerbs);
        SubscribeLocalEvent<CMUIVPressureBagFittedComponent, ExaminedEvent>(OnPressureBagFittedExamined);
    }

    #region Blood types

    private void OnHumanMapInit(Entity<CMUHumanMedicalComponent> ent, ref MapInitEvent args)
    {
        // Spawned characters have this replaced by their profile's type.
        if (!HasComp<CMUBloodTypeComponent>(ent))
            GetBloodType(ent);
    }

    public CMUBloodType GetBloodType(EntityUid mob)
    {
        if (TryComp<CMUBloodTypeComponent>(mob, out var existing))
            return existing.Type;

        var comp = AddComp<CMUBloodTypeComponent>(mob);
        comp.Type = CMUBloodTypes.DefaultTypes[_random.Next(CMUBloodTypes.DefaultTypes.Length)];
        Dirty(mob, comp);
        return comp.Type;
    }

    /// <summary>Drawing blood into a pack labels it with the donor's type, or marks it mixed.</summary>
    public void OnBloodDrawn(EntityUid pack, EntityUid donor)
    {
        if (!HasComp<BloodstreamComponent>(donor))
            return;

        var donorType = GetBloodType(donor);
        EnsureComp<CMUPerishableBloodComponent>(pack);
        var label = EnsureComp<CMUBloodPackTypeComponent>(pack);
        if (label.Mixed)
            return;

        if (label.Type == null)
            label.Type = donorType;
        else if (label.Type != donorType)
        {
            label.Type = null;
            label.Mixed = true;
        }

        Dirty(pack, label);
    }

    private void OnTypedPackSolutionChanged(Entity<CMUBloodPackTypeComponent> ent, ref SolutionChangedEvent args)
    {
        // An emptied pack can be refilled with any type.
        if (args.Solution.Comp.Solution.Volume > FixedPoint2.Zero || ent.Comp.Type == null && !ent.Comp.Mixed)
            return;

        ent.Comp.Type = null;
        ent.Comp.Mixed = false;
        Dirty(ent);
    }

    private void OnTypedPackExamined(Entity<CMUBloodPackTypeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Mixed)
            args.PushMarkup(Loc.GetString("cmu-blood-pack-examine-mixed"));
        else if (ent.Comp.Type is { } type)
            args.PushMarkup(Loc.GetString("cmu-blood-pack-examine-type", ("type", CMUBloodTypes.Name(type))));
    }

    private void OnPerishableSolutionChanged(Entity<CMUPerishableBloodComponent> ent, ref SolutionChangedEvent args)
    {
        // An emptied pack can hold fresh blood again.
        if (args.Solution.Comp.Solution.Volume > FixedPoint2.Zero || ent.Comp.Warm == TimeSpan.Zero && !ent.Comp.Spoiled)
            return;

        ent.Comp.Warm = TimeSpan.Zero;
        ent.Comp.Spoiled = false;
        Dirty(ent);
    }

    private void OnPerishableExamined(Entity<CMUPerishableBloodComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Spoiled)
        {
            args.PushMarkup(Loc.GetString("cmu-blood-pack-examine-spoiled"));
            return;
        }

        if (IsRefrigerated(ent))
            args.PushMarkup(Loc.GetString("cmu-blood-pack-examine-cold"));
        else if (ent.Comp.Warm > ent.Comp.ShelfLife / 2)
            args.PushMarkup(Loc.GetString("cmu-blood-pack-examine-warm"));
    }

    /// <summary>Whether something is stored inside a refrigerator, however deep.</summary>
    public bool IsRefrigerated(EntityUid uid)
    {
        var current = uid;
        while (_container.TryGetContainingContainer((current, null), out var container))
        {
            if (HasComp<CMUBloodRefrigeratorComponent>(container.Owner))
                return true;

            current = container.Owner;
        }

        return false;
    }

    private void UpdatePerishables(TimeSpan elapsed)
    {
        var query = EntityQueryEnumerator<CMUPerishableBloodComponent>();
        while (query.MoveNext(out var uid, out var perishable))
        {
            if (perishable.Spoiled || IsRefrigerated(uid))
                continue;

            // An empty pack has nothing to go bad.
            if (TryComp<BloodPackComponent>(uid, out var pack) &&
                (!_solution.TryGetSolution(uid, pack.Solution, out _, out var contents) || contents.Volume <= FixedPoint2.Zero))
            {
                continue;
            }

            perishable.Warm += elapsed;
            if (perishable.Warm >= perishable.ShelfLife)
            {
                perishable.Spoiled = true;
                Dirty(uid, perishable);
            }
        }
    }

    #endregion

    #region Transfusion

    public float GetFlowMultiplier(EntityUid? iv, EntityUid pack)
    {
        var multiplier = 1f;
        if (TryComp<CMUIVFlowComponent>(pack, out var flow))
            multiplier *= flow.Multiplier;

        if (TryComp<CMUBloodPackSqueezeComponent>(pack, out var squeeze) && _timing.CurTime < squeeze.Until)
            multiplier *= squeeze.Multiplier;

        if (iv is { } stand && TryComp<CMUIVPressureBagFittedComponent>(stand, out var pressure))
            multiplier *= pressure.Multiplier;

        return multiplier;
    }

    /// <summary>
    /// Turns plasma and saline in a transfusion into blood volume, and destroys blood or plasma the recipient can't take.
    /// </summary>
    public void ProcessTransfusion(EntityUid pack, EntityUid recipient, BloodstreamComponent bloodstream, Solution transferred)
    {
        var references = _bloodstream.GetReferenceReagentPrototypes((recipient, bloodstream));
        if (references.Length == 0)
            return;

        var reference = references[0];

        // Blood and plasma that went bad out of the fridge do no good and poison the patient.
        if (TryComp<CMUPerishableBloodComponent>(pack, out var perishable) && perishable.Spoiled)
        {
            var spoiled = Destroy(transferred, PlasmaReagent);
            foreach (var reagent in references)
            {
                spoiled += Destroy(transferred, reagent);
            }

            if (spoiled > FixedPoint2.Zero)
                ApplyMismatchReaction(recipient, spoiled, "cmu-blood-spoiled-reaction");
        }

        // Red cells and plasma each follow their own compatibility rules.
        if (TryComp<CMUBloodPackTypeComponent>(pack, out var label) && (label.Mixed || label.Type != null))
        {
            var recipientType = GetBloodType(recipient);
            var bloodOk = !label.Mixed && label.Type is { } bloodDonor && CMUBloodTypes.CanReceive(recipientType, bloodDonor);
            var plasmaOk = !label.Mixed && label.Type is { } plasmaDonor &&
                           CMUBloodTypes.CanReceivePlasma(recipientType, plasmaDonor);

            var destroyed = FixedPoint2.Zero;
            if (!bloodOk)
            {
                foreach (var reagent in references)
                {
                    destroyed += Destroy(transferred, reagent);
                }
            }

            if (!plasmaOk)
                destroyed += Destroy(transferred, PlasmaReagent);

            if (destroyed > FixedPoint2.Zero)
                ApplyMismatchReaction(recipient, destroyed, "cmu-blood-mismatch-reaction");
        }

        var plasma = ConvertToBlood(transferred, PlasmaReagent, reference);
        if (plasma > FixedPoint2.Zero)
        {
            var clotting = EnsureComp<CMUPlasmaClottingComponent>(recipient);
            var now = _timing.CurTime;
            var start = clotting.Until > now ? clotting.Until : now;
            var until = start + TimeSpan.FromSeconds(plasma.Float() * PlasmaClottingSecondsPerUnit);
            clotting.Until = until > now + MaxPlasmaClotting ? now + MaxPlasmaClotting : until;
            Dirty(recipient, clotting);
        }

        var saline = ConvertToBlood(transferred, SalineReagent, reference);
        if (saline > FixedPoint2.Zero)
        {
            var dilution = EnsureComp<CMUSalineDilutionComponent>(recipient);
            dilution.Saline += saline;
            Dirty(recipient, dilution);
        }
    }

    private static FixedPoint2 Destroy(Solution solution, ProtoId<ReagentPrototype> reagent)
    {
        var quantity = solution.GetTotalPrototypeQuantity(reagent.Id);
        if (quantity > FixedPoint2.Zero)
            solution.RemoveReagent(new ReagentId(reagent.Id, null), quantity, ignoreReagentData: true);

        return quantity;
    }

    private static FixedPoint2 ConvertToBlood(Solution solution, ProtoId<ReagentPrototype> from, ProtoId<ReagentPrototype> to)
    {
        var quantity = solution.GetTotalPrototypeQuantity(from.Id);
        if (quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        solution.RemoveReagent(new ReagentId(from.Id, null), quantity, ignoreReagentData: true);
        solution.AddReagent(new ReagentId(to.Id, null), quantity);
        return quantity;
    }

    private void ApplyMismatchReaction(EntityUid recipient, FixedPoint2 destroyed, LocId popup)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = destroyed * MismatchToxinPerUnit;
        _damageable.TryChangeDamage(recipient, damage, true, interruptsDoAfters: false);

        var now = _timing.CurTime;
        if (_nextMismatchPopup.TryGetValue(recipient, out var next) && now < next)
            return;

        _nextMismatchPopup[recipient] = now + MismatchPopupCooldown;
        _popup.PopupEntity(Loc.GetString(popup), recipient, recipient, PopupType.MediumCaution);
    }

    /// <summary>How much of a body's bleeding gets through, slowed by a recent plasma transfusion.</summary>
    public float GetBleedMultiplier(EntityUid body)
    {
        return TryComp<CMUPlasmaClottingComponent>(body, out var clotting) && _timing.CurTime < clotting.Until
            ? clotting.BleedMultiplier
            : 1f;
    }

    #endregion

    #region Squeezing and pressure bags

    private void OnPackUseInHand(Entity<BloodPackComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.AttachedTo == null || !ent.Comp.Injecting)
            return;

        args.Handled = true;
        var squeeze = EnsureComp<CMUBloodPackSqueezeComponent>(ent);
        squeeze.Until = _timing.CurTime + squeeze.Duration;
        Dirty(ent, squeeze);

        _popup.PopupEntity(Loc.GetString("cmu-blood-pack-squeeze", ("pack", ent.Owner)), args.User, args.User);
    }

    private void OnIVInteractUsing(Entity<IVDripComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<CMUIVPressureBagComponent>(args.Used, out var bag))
            return;

        args.Handled = true;
        var slot = _container.EnsureContainer<ContainerSlot>(ent, CMUIVPressureBagFittedComponent.ContainerId);
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-iv-pressure-bag-already"), ent, args.User);
            return;
        }

        if (!_container.Insert(args.Used, slot))
            return;

        var fitted = EnsureComp<CMUIVPressureBagFittedComponent>(ent);
        fitted.Multiplier = bag.Multiplier;
        Dirty(ent, fitted);
        _popup.PopupEntity(Loc.GetString("cmu-iv-pressure-bag-fitted", ("iv", ent.Owner)), ent, args.User);
    }

    private void OnIVAltVerbs(Entity<IVDripComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<CMUIVPressureBagFittedComponent>(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-iv-pressure-bag-remove-verb"),
            Act = () => RemovePressureBag(ent, user),
        });
    }

    private void RemovePressureBag(EntityUid iv, EntityUid user)
    {
        if (!_container.TryGetContainer(iv, CMUIVPressureBagFittedComponent.ContainerId, out var container))
            return;

        foreach (var bag in container.ContainedEntities.ToArray())
        {
            _container.Remove(bag, container);
            _hands.PickupOrDrop(user, bag);
        }

        RemComp<CMUIVPressureBagFittedComponent>(iv);
        _popup.PopupEntity(Loc.GetString("cmu-iv-pressure-bag-removed", ("iv", iv)), iv, user);
    }

    private void OnPressureBagFittedExamined(Entity<CMUIVPressureBagFittedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-iv-pressure-bag-examine"));
    }

    #endregion

    #region Saline

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSalineTick)
            return;

        _nextSalineTick = now + TimeSpan.FromSeconds(1);
        UpdatePerishables(TimeSpan.FromSeconds(1));

        var query = EntityQueryEnumerator<CMUSalineDilutionComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var dilution, out var bloodstream))
        {
            var reference = _bloodstream.GetReferenceVolume((uid, bloodstream));
            if (reference <= FixedPoint2.Zero)
                continue;

            // Saline can't outlast the blood volume it's part of, and the body slowly replaces it.
            var currentBlood = reference * _bloodstream.GetBloodLevel((uid, bloodstream));
            dilution.Saline = FixedPoint2.Max(FixedPoint2.Zero,
                FixedPoint2.Min(dilution.Saline, currentBlood) - SalineClearedPerSecond);

            if (dilution.Saline <= FixedPoint2.Zero)
            {
                RemCompDeferred<CMUSalineDilutionComponent>(uid);
                continue;
            }

            Dirty(uid, dilution);

            var fraction = dilution.Saline.Float() / reference.Float();
            if (fraction <= SalineSafeFraction || _mobState.IsDead(uid))
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict[PoisonType] = FixedPoint2.New((fraction - SalineSafeFraction) * SalineToxinPerSecond);
            _damageable.TryChangeDamage(uid, damage, true, interruptsDoAfters: false);
        }
    }

    #endregion
}
