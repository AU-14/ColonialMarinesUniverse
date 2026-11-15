using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Radio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Radio;

public sealed partial class ANPRCRadioSystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    private static readonly ProtoId<StackPrototype> RepairCable = "RMCCable";
    public const int RepairCableCount = 5;
    public static readonly TimeSpan RipWiresDelay = TimeSpan.FromSeconds(7);

    private void InitializeDamage()
    {
        SubscribeLocalEvent<ANPRCRadioComponent, GetVerbsEvent<Verb>>(OnRipPackVerbs);
        SubscribeLocalEvent<WearingANPRCComponent, GetVerbsEvent<Verb>>(OnRipWearerVerbs);
        SubscribeLocalEvent<ANPRCRadioComponent, CMURadioRipWiresDoAfterEvent>(OnRipWiresComplete);
        SubscribeLocalEvent<ANPRCRadioComponent, InteractUsingEvent>(OnRepairPack,
            before: [typeof(ItemSlotsSystem), typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<WearingANPRCComponent, InteractUsingEvent>(OnRepairWearer,
            before: [typeof(ItemSlotsSystem), typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<ANPRCRadioComponent, ExaminedEvent>(OnDamageExamined);
    }

    private void OnRipPackVerbs(Entity<ANPRCRadioComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        AddRipVerb(ent, ref args);
    }

    private void OnRipWearerVerbs(Entity<WearingANPRCComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio))
            AddRipVerb((ent.Comp.Radio, radio), ref args);
    }

    private void AddRipVerb(Entity<ANPRCRadioComponent> pack, ref GetVerbsEvent<Verb> args)
    {
        var tool = _hands.GetActiveItem(args.User);
        if (!args.CanAccess || !args.CanInteract || pack.Comp.WiresRipped || !CanRipWires(args.User, tool))
            return;

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-radio-rip-wires"),
            Act = () =>
            {
                if (pack.Comp.WiresRipped || !CanRipWires(user, tool) ||
                    !_interaction.InRangeUnobstructed(user, target) || !IsRipTarget(pack, target))
                    return;

                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, RipWiresDelay,
                    new CMURadioRipWiresDoAfterEvent(), pack, target, tool)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true,
                });
            },
        });
    }

    private bool IsRipTarget(EntityUid pack, EntityUid target)
    {
        return target == pack ||
            TryComp(target, out WearingANPRCComponent? wearing) && wearing.Radio == pack;
    }

    private void OnRipWiresComplete(Entity<ANPRCRadioComponent> ent, ref CMURadioRipWiresDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.WiresRipped || !CanRipWires(args.User, args.Used) ||
            args.Target is not { } target || !IsRipTarget(ent, target) ||
            !_interaction.InRangeUnobstructed(args.User, target))
            return;

        args.Handled = true;
        ent.Comp.WiresRipped = true;
        ent.Comp.Enabled = false;
        Dirty(ent);
        SetBatteryDrawEnabled(ent, false);
        RevokeReceiveChannels(ent);
        UpdateRelayAnchor(ent);
        if (TryComp(ent, out RTORelayComponent? relay))
        {
            relay.Active = false;
            Dirty(ent, relay);
        }
        if (_telephone.AU14InCall(ent))
            EndCall(ent, args.User, "cmu-radio-wires-ripped");
        UpdateBuiState(ent);
        _popup.PopupEntity(Loc.GetString("cmu-radio-wires-ripped"), target, PopupType.MediumCaution);
    }

    private bool CanRipWires(EntityUid user, EntityUid? tool)
    {
        if (HasComp<XenoComponent>(user))
            return true;
        return tool is { } sharp && _hands.GetActiveItem(user) == sharp &&
            (_tools.HasQuality(sharp, "Slicing") || _tools.HasQuality(sharp, "Cutting") ||
                TryComp(sharp, out MeleeWeaponComponent? melee) && melee.Damage.DamageDict.GetValueOrDefault("Slash") > 0);
    }

    private void OnRepairPack(Entity<ANPRCRadioComponent> ent, ref InteractUsingEvent args)
    {
        RepairWires(ent, ref args);
    }

    private void OnRepairWearer(Entity<WearingANPRCComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp(ent.Comp.Radio, out ANPRCRadioComponent? radio))
            RepairWires((ent.Comp.Radio, radio), ref args);
    }

    private void RepairWires(Entity<ANPRCRadioComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.WiresRipped || !TryComp(args.Used, out StackComponent? cable) ||
            cable.StackTypeId != RepairCable)
            return;

        args.Handled = true;
        if (!HasComp<ANPRCRadioUserComponent>(args.User) && !_skills.HasSkill(args.User, "RMCSkillEngineer", 1))
        {
            _popup.PopupEntity(Loc.GetString("cmu-radio-repair-untrained"), ent, args.User, PopupType.SmallCaution);
            return;
        }
        if (!_stack.TryUse(args.Used, RepairCableCount))
        {
            _popup.PopupEntity(Loc.GetString("cmu-radio-repair-cable", ("amount", RepairCableCount)), ent, args.User,
                PopupType.SmallCaution);
            return;
        }

        ent.Comp.WiresRipped = false;
        Dirty(ent);
        if (TryComp(ent, out RTORelayComponent? relay))
        {
            relay.Active = ent.Comp.IsEquipped;
            Dirty(ent, relay);
        }
        UpdateBuiState(ent);
        _popup.PopupEntity(Loc.GetString("cmu-radio-repaired"), ent, args.User);
    }

    private void OnDamageExamined(Entity<ANPRCRadioComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.WiresRipped)
            args.PushMarkup(Loc.GetString("cmu-radio-broken-examine", ("amount", RepairCableCount)));
    }

    private bool CheckWires(Entity<ANPRCRadioComponent> ent, EntityUid user)
    {
        if (!ent.Comp.WiresRipped)
            return true;
        _popup.PopupEntity(Loc.GetString("cmu-radio-wires-ripped"), ent, user, PopupType.SmallCaution);
        return false;
    }
}
