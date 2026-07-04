using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Alchemist;

public sealed partial class XenoAlchemistSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMArmorSystem _armor = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private RMCSlowSystem _rmcSlow = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private RMCDazedSystem _dazed = default!;
    [Dependency] private XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private XenoSystem _xeno = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoAlchemistComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<XenoAlchemistComponent, XenoSelectChemicalActionEvent>(OnSelectChemicalAction);
        SubscribeLocalEvent<XenoAlchemistComponent, XenoProduceChemicalActionEvent>(OnProduceChemicalAction);
        SubscribeLocalEvent<XenoAlchemistComponent, XenoProduceChemicalDoAfterEvent>(OnProduceChemicalDoAfter);
        SubscribeLocalEvent<XenoAlchemistComponent, XenoRemoveChemicalActionEvent>(OnRemoveChemicalAction);
        SubscribeLocalEvent<XenoAlchemistComponent, XenoTailInjectionActionEvent>(OnTailInjectionAction);

        Subs.BuiEvents<XenoAlchemistComponent>(XenoAlchemistUI.Key, subs =>
        {
            subs.Event<XenoAlchemistChooseBuiMsg>(OnChooseChemicalBui);
        });

        SubscribeLocalEvent<XenoTemporaryStatModifierComponent, CMGetArmorEvent>(OnTemporaryModifierGetArmor);
        SubscribeLocalEvent<XenoTemporaryStatModifierComponent, GetMeleeDamageEvent>(OnTemporaryModifierGetMeleeDamage);
        SubscribeLocalEvent<XenoTemporaryStatModifierComponent, RefreshMovementSpeedModifiersEvent>(OnTemporaryModifierRefreshSpeed);
    }

    private void OnMeleeHit(Entity<XenoAlchemistComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, hit))
                continue;

            if (xeno.Comp.SelectedChemical != AlchemistChemical.None)
                AddChemical(xeno, xeno.Comp.SelectedChemical, xeno.Comp.SlashGenerateAmount);

            break;
        }
    }

    private void OnSelectChemicalAction(Entity<XenoAlchemistComponent> xeno, ref XenoSelectChemicalActionEvent args)
    {
        if (xeno.Comp.Producing)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-producing"), xeno, xeno);
            return;
        }

        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        _ui.TryOpenUi(xeno.Owner, XenoAlchemistUI.Key, xeno);
    }

    private void OnChooseChemicalBui(Entity<XenoAlchemistComponent> xeno, ref XenoAlchemistChooseBuiMsg args)
    {
        if (xeno.Comp.Producing)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-producing"), xeno, xeno);
            return;
        }

        if (!Enum.IsDefined(args.Chemical))
            return;

        xeno.Comp.SelectedChemical = args.Chemical;
        Dirty(xeno);

        _ui.CloseUi(xeno.Owner, XenoAlchemistUI.Key, xeno);
        _popup.PopupClient(Loc.GetString($"cm-xeno-alchemist-selected-{xeno.Comp.SelectedChemical.ToString().ToLowerInvariant()}"), xeno, xeno);
    }

    private void OnProduceChemicalAction(Entity<XenoAlchemistComponent> xeno, ref XenoProduceChemicalActionEvent args)
    {
        if (xeno.Comp.Producing)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-producing"), xeno, xeno);
            return;
        }

        if (xeno.Comp.SelectedChemical == AlchemistChemical.None)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-no-selected"), xeno, xeno);
            return;
        }

        if (GetTotalStockpile(xeno.Comp) >= xeno.Comp.MaxStockpile)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-stockpile-full"), xeno, xeno);
            return;
        }

        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        xeno.Comp.Producing = true;
        xeno.Comp.ProducingChemical = xeno.Comp.SelectedChemical;
        Dirty(xeno);

        var doAfter = new DoAfterArgs(EntityManager, xeno.Owner, xeno.Comp.ProduceDelay, new XenoProduceChemicalDoAfterEvent(), xeno.Owner)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            xeno.Comp.Producing = false;
            xeno.Comp.ProducingChemical = AlchemistChemical.None;
            Dirty(xeno);
        }
    }

    private void OnProduceChemicalDoAfter(Entity<XenoAlchemistComponent> xeno, ref XenoProduceChemicalDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            xeno.Comp.Producing = false;
            xeno.Comp.ProducingChemical = AlchemistChemical.None;
            Dirty(xeno);
            return;
        }

        args.Handled = true;
        xeno.Comp.Producing = false;
        var chemical = xeno.Comp.ProducingChemical;
        xeno.Comp.ProducingChemical = AlchemistChemical.None;
        Dirty(xeno);

        AddChemical(xeno, chemical, xeno.Comp.ProduceAmount);
    }

    private void OnRemoveChemicalAction(Entity<XenoAlchemistComponent> xeno, ref XenoRemoveChemicalActionEvent args)
    {
        if (xeno.Comp.Producing)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-producing"), xeno, xeno);
            return;
        }

        if (xeno.Comp.SelectedChemical == AlchemistChemical.None)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-no-selected"), xeno, xeno);
            return;
        }

        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        SetChemical(xeno, xeno.Comp.SelectedChemical, 0);
        _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-removed"), xeno, xeno);
    }

    private void OnTailInjectionAction(Entity<XenoAlchemistComponent> xeno, ref XenoTailInjectionActionEvent args)
    {
        if (args.Handled)
            return;

        var total = xeno.Comp.Sagunine + xeno.Comp.Cholinine + xeno.Comp.Noctine;
        if (total <= 0)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-empty"), xeno, xeno);
            return;
        }

        if (!_xeno.CanAbilityAttackTarget(xeno, args.Target))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        _audio.PlayPredicted(xeno.Comp.TailInjectionSound, xeno, xeno);
        _rmcMelee.DoLunge(xeno.Owner, args.Target);

        var mixture = GetMixture(xeno.Comp);
        var injected = TryInjectMixture(args.Target, mixture, total);

        _damageable.TryChangeDamage(args.Target, xeno.Comp.TailInjectionDamage, armorPiercing: 10, origin: xeno, tool: xeno);

        ApplyMixture(xeno, args.Target, mixture, total, injected);
        SetTailInjectionCooldown(xeno, total);
        xeno.Comp.Sagunine = 0;
        xeno.Comp.Cholinine = 0;
        xeno.Comp.Noctine = 0;
        Dirty(xeno);
    }

    private void OnTemporaryModifierGetArmor(Entity<XenoTemporaryStatModifierComponent> ent, ref CMGetArmorEvent args)
    {
        args.XenoArmor += ent.Comp.Armor;
    }

    private void OnTemporaryModifierGetMeleeDamage(Entity<XenoTemporaryStatModifierComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage.ExclusiveAdd(ent.Comp.MeleeDamage);
    }

    private void OnTemporaryModifierRefreshSpeed(Entity<XenoTemporaryStatModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoTemporaryStatModifierComponent>();
        while (query.MoveNext(out var uid, out var temp))
        {
            if (time < temp.ExpiresAt)
                continue;

            RemCompDeferred<XenoTemporaryStatModifierComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
            _armor.UpdateArmorValue((uid, null));
        }
    }

    private void AddChemical(Entity<XenoAlchemistComponent> xeno, AlchemistChemical chemical, int amount)
    {
        if (chemical == AlchemistChemical.None)
            return;

        var available = xeno.Comp.MaxStockpile - GetTotalStockpile(xeno.Comp);
        if (available <= 0)
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-stockpile-full"), xeno, xeno);
            return;
        }

        SetChemical(xeno, chemical, GetChemical(xeno.Comp, chemical) + Math.Min(amount, available));
        _popup.PopupClient(Loc.GetString("cm-xeno-alchemist-stockpile", ("amount", GetChemical(xeno.Comp, chemical))), xeno, xeno);
    }

    private void SetChemical(Entity<XenoAlchemistComponent> xeno, AlchemistChemical chemical, int amount)
    {
        switch (chemical)
        {
            case AlchemistChemical.Sagunine:
                xeno.Comp.Sagunine = amount;
                break;
            case AlchemistChemical.Cholinine:
                xeno.Comp.Cholinine = amount;
                break;
            case AlchemistChemical.Noctine:
                xeno.Comp.Noctine = amount;
                break;
        }

        Dirty(xeno);
    }

    private static int GetChemical(XenoAlchemistComponent comp, AlchemistChemical chemical)
    {
        return chemical switch
        {
            AlchemistChemical.Sagunine => comp.Sagunine,
            AlchemistChemical.Cholinine => comp.Cholinine,
            AlchemistChemical.Noctine => comp.Noctine,
            _ => 0,
        };
    }

    private static int GetTotalStockpile(XenoAlchemistComponent comp)
    {
        return comp.Sagunine + comp.Cholinine + comp.Noctine;
    }

    private static AlchemistMixture GetMixture(XenoAlchemistComponent comp)
    {
        var sag = comp.Sagunine > 0;
        var chol = comp.Cholinine > 0;
        var noc = comp.Noctine > 0;

        return (sag, chol, noc) switch
        {
            (true, true, true) => AlchemistMixture.Xenosterine,
            (true, true, false) => AlchemistMixture.Pyrinine,
            (true, false, true) => AlchemistMixture.Vapinine,
            (false, true, true) => AlchemistMixture.Crynine,
            (true, false, false) => AlchemistMixture.Sagunine,
            (false, true, false) => AlchemistMixture.Cholinine,
            _ => AlchemistMixture.Noctine,
        };
    }

    private bool TryInjectMixture(EntityUid target, AlchemistMixture mixture, int potency)
    {
        if (!HasComp<CMUHumanMedicalComponent>(target) ||
            HasComp<SynthComponent>(target) ||
            HasComp<XenoComponent>(target))
        {
            return false;
        }

        if (!_solution.TryGetInjectableSolution(target, out var solutionEnt, out _))
            return false;

        var reagent = GetReagent(mixture);
        var amount = FixedPoint2.New(potency);
        var available = solutionEnt.Value.Comp.Solution.AvailableVolume;
        if (available < amount)
            _solution.SplitSolution(solutionEnt.Value, amount - available);

        _solution.TryAddReagent(solutionEnt.Value, reagent, amount, out var accepted);
        return accepted > FixedPoint2.Zero;
    }

    private static string GetReagent(AlchemistMixture mixture)
    {
        return mixture switch
        {
            AlchemistMixture.Sagunine => "RMCXenoAlchBrute",
            AlchemistMixture.Cholinine => "RMCXenoAlchBurn",
            AlchemistMixture.Noctine => "RMCXenoAlchPain",
            AlchemistMixture.Pyrinine => "RMCXenoAlchFire",
            AlchemistMixture.Vapinine => "RMCXenoAlchBloodloss",
            AlchemistMixture.Crynine => "RMCXenoAlchFreeze",
            AlchemistMixture.Xenosterine => "RMCXenoAlchPurge",
            _ => "RMCXenoAlchPain",
        };
    }

    private void ApplyMixture(Entity<XenoAlchemistComponent> xeno, EntityUid target, AlchemistMixture mixture, int potency, bool injected)
    {
        if (injected)
        {
            ApplyInjectedMixture(xeno, target, mixture, potency);
            return;
        }

        if (!HasComp<XenoComponent>(target))
            return;

        var damage = mixture switch
        {
            AlchemistMixture.Sagunine => new DamageSpecifier { DamageDict = { ["Blunt"] = 8 + potency } },
            AlchemistMixture.Cholinine => new DamageSpecifier { DamageDict = { ["Heat"] = 8 + potency } },
            AlchemistMixture.Pyrinine => new DamageSpecifier { DamageDict = { ["Heat"] = 10 + potency } },
            AlchemistMixture.Vapinine => new DamageSpecifier { DamageDict = { ["Poison"] = 8 + potency } },
            AlchemistMixture.Xenosterine => new DamageSpecifier { DamageDict = { ["Cellular"] = 10 + potency } },
            _ => new DamageSpecifier { DamageDict = { ["Poison"] = 6 + potency } },
        };
        _damageable.TryChangeDamage(target, damage, armorPiercing: 10, origin: xeno, tool: xeno);

        var debuff = EnsureComp<XenoTemporaryStatModifierComponent>(target);
        debuff.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(8);
        debuff.Armor = mixture is AlchemistMixture.Noctine or AlchemistMixture.Xenosterine ? -10 : 0;
        debuff.SpeedMultiplier = mixture is AlchemistMixture.Crynine ? 0.75f : 1f;
        debuff.MeleeDamage = mixture is AlchemistMixture.Pyrinine
            ? new DamageSpecifier { DamageDict = { ["Slash"] = -8 } }
            : new DamageSpecifier();
        Dirty(target, debuff);
        _speed.RefreshMovementSpeedModifiers(target);
        _armor.UpdateArmorValue((target, null));

        if (mixture == AlchemistMixture.Vapinine && TryComp(target, out XenoPlasmaComponent? plasma))
            _xenoPlasma.RemovePlasma((target, plasma), potency * 4);
    }

    private void ApplyInjectedMixture(Entity<XenoAlchemistComponent> xeno, EntityUid target, AlchemistMixture mixture, int potency)
    {
        var multiplier = Math.Clamp((float) potency / xeno.Comp.MaxStockpile, 0.25f, 1f);

        switch (mixture)
        {
            case AlchemistMixture.Noctine:
                _dazed.TryDaze(target, ScaleDuration(xeno.Comp.NoctineDazeTime, multiplier), true, stutter: true);
                break;
            case AlchemistMixture.Pyrinine:
                _dazed.TryDaze(target, ScaleDuration(xeno.Comp.PyrinineDazeTime, multiplier), true);
                break;
            case AlchemistMixture.Crynine:
                _rmcSlow.TrySlowdown(target, ScaleDuration(xeno.Comp.CrynineSlowTime, multiplier), ignoreDurationModifier: true);
                break;
        }
    }

    private static TimeSpan ScaleDuration(TimeSpan duration, float multiplier)
    {
        return TimeSpan.FromSeconds(duration.TotalSeconds * multiplier);
    }

    private void SetTailInjectionCooldown(Entity<XenoAlchemistComponent> xeno, int total)
    {
        var multiplier = total > 14 ? total * 0.07f : 1f;
        var cooldown = TimeSpan.FromSeconds(xeno.Comp.TailInjectionCooldown.TotalSeconds * multiplier);

        foreach (var (actionId, _) in _rmcActions.GetActionsWithEvent<XenoTailInjectionActionEvent>(xeno))
        {
            _actions.SetCooldown(actionId, cooldown);
        }
    }
}
