using Content.Shared._RMC14.BlurredVision;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.PepperSpray;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Content.Shared.StatusEffect;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.PepperSpray;

public sealed class CMUPepperSpraySystem : EntitySystem
{
    private const string BlindKey = "Blinded";
    private const float FaceCoveringMultiplier = 0.5f;

    /// <summary>Each level of police training builds up this much tolerance, up to <see cref="MaxTolerance"/>.</summary>
    private const float TolerancePerPoliceLevel = 0.15f;
    private const float MaxTolerance = 0.6f;

    private static readonly EntProtoId<SkillDefinitionComponent> PoliceSkill = "RMCSkillPolice";
    private static readonly ProtoId<ReagentPrototype> Water = "Water";
    private static readonly FixedPoint2 SplashAmount = 10;
    private static readonly TimeSpan SplashCooldown = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan SplashTimeOff = TimeSpan.FromSeconds(4);
    private const float SplashPainKept = 0.75f;
    private static readonly string[] FaceSlots = ["mask", "head", "eyes"];
    private static readonly TimeSpan PainInterval = TimeSpan.FromSeconds(1);

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPepperSprayComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUPepperSprayComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUPepperSprayedComponent, GetVerbsEvent<UtilityVerb>>(OnGetSplashVerbs);
    }

    private void OnExamined(Entity<CMUPepperSprayComponent> ent, ref ExaminedEvent args)
    {
        var sprays = 0;
        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            sprays = (int) (solution.Volume / ent.Comp.PerSpray).Float();

        args.PushMarkup(Loc.GetString("cmu-pepper-spray-examine", ("charges", sprays)));
    }

    private void OnAfterInteract(Entity<CMUPepperSprayComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;

        var user = args.User;
        if (!_interaction.InRangeUnobstructed(user, target, ent.Comp.Range, popup: false))
            return;

        args.Handled = true;

        if (TryComp(ent, out UseDelayComponent? delay) && _useDelay.IsDelayed((ent, delay)))
            return;

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEnt, out var solution) ||
            solution.Volume < ent.Comp.PerSpray)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-empty"), ent, user);
            return;
        }

        if (delay != null)
            _useDelay.TryResetDelay((ent, delay));

        var sprayed = _solutions.SplitSolution(solutionEnt.Value, ent.Comp.PerSpray);
        var strength = sprayed.Volume > FixedPoint2.Zero
            ? (sprayed.GetTotalPrototypeQuantity(ent.Comp.Reagent) / sprayed.Volume).Float()
            : 0f;
        _audio.PlayPvs(ent.Comp.Sound, ent);

        var targetName = Identity.Entity(target, EntityManager);
        var userName = Identity.Entity(user, EntityManager);

        if (ent.Comp.Antidote)
        {
            WashOut(target, user, strength, targetName, userName);
            return;
        }

        // Watered down, it barely stings.
        if (strength < 0.25f)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-weak"), target, target);
            return;
        }

        var protection = new GetEyeProtectionEvent();
        RaiseLocalEvent(target, protection);
        if (protection.Protection > TimeSpan.Zero || WearingGasMask(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-protected", ("target", targetName)), target, user);
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-protected-self"), target, target);
            return;
        }

        if (FaceCovered(target))
            strength *= FaceCoveringMultiplier;

        strength *= 1f - Math.Min(MaxTolerance, _skills.GetSkill(target, PoliceSkill) * TolerancePerPoliceLevel);

        var now = _timing.CurTime;
        var burning = EnsureComp<CMUPepperSprayedComponent>(target);
        burning.Sprays++;
        burning.EndAt = now + ent.Comp.Duration * strength;
        burning.PendingPain += ent.Comp.Pain * strength;
        burning.PainPerTick = Math.Max(burning.PainPerTick, ent.Comp.Pain / Math.Max(1, ent.Comp.PainTicks));
        if (burning.NextPain < now)
            burning.NextPain = now;
        Dirty(target, burning);

        _status.TryAddStatusEffect<RMCBlindedComponent>(target, BlindKey, ent.Comp.BlurTime * strength, true);

        _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-eyes-burn"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-sprayed", ("user", userName), ("target", targetName)),
            target,
            Filter.PvsExcept(target),
            true,
            PopupType.MediumCaution);
    }

    private void OnGetSplashVerbs(Entity<CMUPepperSprayedComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } used ||
            HasComp<CMUPepperSprayComponent>(used) ||
            _openable.IsClosed(used, predicted: false) ||
            !_solutions.TryGetDrainableSolution(used, out _, out var solution) ||
            solution.GetTotalPrototypeQuantity(Water) < SplashAmount / 2)
        {
            return;
        }

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString(user == target ? "cmu-pepper-spray-splash-self-verb" : "cmu-pepper-spray-splash-verb"),
            Act = () => Splash(target, user, used),
        });
    }

    /// <summary>
    ///     Splashing water in the eyes washes some of the pepper spray out: it takes a good few splashes to clear it,
    ///     but each one makes it wear off sooner and takes the edge off the pain still to come.
    /// </summary>
    private void Splash(EntityUid target, EntityUid user, EntityUid container)
    {
        if (!TryComp(target, out CMUPepperSprayedComponent? burning) ||
            !_solutions.TryGetDrainableSolution(container, out var solutionEnt, out _))
        {
            return;
        }

        var now = _timing.CurTime;
        if (now < burning.NextSplash)
            return;

        var splashed = _solutions.SplitSolution(solutionEnt.Value, SplashAmount);
        var water = splashed.Volume > FixedPoint2.Zero
            ? (splashed.GetTotalPrototypeQuantity(Water) / SplashAmount).Float()
            : 0f;
        if (water <= 0)
            return;

        burning.NextSplash = now + SplashCooldown;
        burning.EndAt -= SplashTimeOff * water;
        burning.PendingPain *= 1f - (1f - SplashPainKept) * water;
        Dirty(target, burning);

        _status.TryRemoveTime(target, BlindKey, SplashTimeOff * water);

        _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-splash-self"), target, target);
        if (user != target)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-splash-other",
                    ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
                target,
                Filter.PvsExcept(target),
                true);
        }
    }

    private static bool Covers(EntityUid item, IEntityManager entities)
    {
        return entities.TryGetComponent(item, out IdentityBlockerComponent? blocker) &&
               blocker.Enabled &&
               !(entities.TryGetComponent(item, out MaskComponent? mask) && mask.IsToggled);
    }

    /// <summary>A gas mask that's on properly seals the face off completely.</summary>
    private bool WearingGasMask(EntityUid target)
    {
        return _inventory.TryGetSlotEntity(target, "mask", out var mask) &&
               HasComp<BreathToolComponent>(mask) &&
               Covers(mask.Value, EntityManager);
    }

    /// <summary>A balaclava, bandana or full-face helmet takes some of the spray.</summary>
    private bool FaceCovered(EntityUid target)
    {
        foreach (var slot in FaceSlots)
        {
            if (_inventory.TryGetSlotEntity(target, slot, out var item) && Covers(item.Value, EntityManager))
                return true;
        }

        return false;
    }

    private void WashOut(EntityUid target, EntityUid user, float strength, EntityUid targetName, EntityUid userName)
    {
        if (strength < 0.25f || !TryComp(target, out CMUPepperSprayedComponent? burning))
        {
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-antidote-nothing"), target, user);
            return;
        }

        RemComp(target, burning);
        _status.TryRemoveStatusEffect(target, BlindKey);

        _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-antidote-relief"), target, target, PopupType.Medium);
        if (user != target)
        {
            _popup.PopupEntity(Loc.GetString("cmu-pepper-spray-antidote-sprayed", ("user", userName), ("target", targetName)),
                target,
                Filter.PvsExcept(target),
                true);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPepperSprayedComponent>();
        while (query.MoveNext(out var uid, out var sprayed))
        {
            if (sprayed.PendingPain > 0 && now >= sprayed.NextPain)
            {
                var amount = Math.Min(sprayed.PainPerTick, sprayed.PendingPain);
                sprayed.PendingPain -= amount;
                sprayed.NextPain = now + PainInterval;
                _pain.AddPainPulse(uid, amount);
            }

            if (sprayed.PendingPain <= 0 && now >= sprayed.EndAt)
                RemCompDeferred<CMUPepperSprayedComponent>(uid);
        }
    }
}
