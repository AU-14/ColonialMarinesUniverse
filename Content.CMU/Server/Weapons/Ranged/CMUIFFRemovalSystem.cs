using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Weapons.Ranged.IFF;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Weapons.Ranged;

public sealed partial class CMUIFFRemovalSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedOverwatchConsoleSystem _overwatch = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TacticalMapSystem _tacticalMap = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<CMUIFFRemovalPrototype> SettingsId = "CMUIFFRemoval";
    private static readonly VerbCategory IFFCategory = new("cmu-iff-removal-category", null);

    private CMUIFFRemovalPrototype Settings => _prototypes.Index(SettingsId);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<MarineComponent, CMUIFFRemovalDoAfterEvent>(OnRemovalDoAfter);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract
            || args.Using is not { } knife
            || !CanRemoveIFF(args.User, args.Target, knife))
        {
            return;
        }

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-iff-removal-verb"),
            Category = IFFCategory,
            Act = () => StartRemoval(args.User, args.Target, knife),
        });
    }

    private bool CanRemoveIFF(EntityUid user, EntityUid target, EntityUid knife)
    {
        var settings = Settings;
        if (!HasComp<MarineComponent>(target)
            || HasComp<CMUIFFRemovedComponent>(target)
            || !_tags.HasTag(knife, settings.KnifeTag)
            || !IsFriendlyIFF(target, settings)
            || !IsHeld(user, knife))
        {
            return false;
        }

        return user == target || _standing.IsDown(target) || _mobState.IsIncapacitated(target)
               || (TryComp<CuffableComponent>(target, out var cuffed) && cuffed.CuffedHandCount > 0);
    }

    private bool IsFriendlyIFF(EntityUid target, CMUIFFRemovalPrototype settings)
    {
        foreach (var faction in settings.EligibleFactions)
        {
            if (_gunIFF.IsInFaction(target, faction))
                return true;
        }

        return false;
    }

    private bool IsHeld(EntityUid user, EntityUid item)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == item)
                return true;
        }

        return false;
    }

    private void StartRemoval(EntityUid user, EntityUid target, EntityUid knife)
    {
        if (!CanRemoveIFF(user, target, knife))
            return;

        var self = user == target;
        var settings = Settings;
        var trained = !self && _skills.HasSkill(user, settings.SurgerySkill, settings.SurgerySkillLevel);
        var duration = self ? settings.SelfDuration : trained ? settings.SurgeryDuration : settings.UntrainedDuration;
        var doAfter = new DoAfterArgs(EntityManager, user, duration, new CMUIFFRemovalDoAfterEvent(trained), target, target, knife)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            ExtraCheck = () => CanRemoveIFF(user, target, knife)
                               && (!trained || _skills.HasSkill(user, settings.SurgerySkill, settings.SurgerySkillLevel)),
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(self
                ? Loc.GetString("cmu-iff-removal-start-self")
                : Loc.GetString("cmu-iff-removal-start-other", ("target", Name(target))),
            target, user);
        if (!self)
            _popup.PopupEntity(Loc.GetString("cmu-iff-removal-start-target", ("user", Name(user))), target, target, PopupType.MediumCaution);
    }

    private void OnRemovalDoAfter(Entity<MarineComponent> ent, ref CMUIFFRemovalDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Used is not { } knife)
            return;

        var user = args.User;
        var target = ent.Owner;
        var settings = Settings;
        if (!CanRemoveIFF(user, target, knife)
            || (args.Trained && !_skills.HasSkill(user, settings.SurgerySkill, settings.SurgerySkillLevel)))
        {
            return;
        }

        args.Handled = true;
        var self = user == target;
        EnsureComp<CMUIFFRemovedComponent>(target);
        _gunIFF.ClearUserFactions(target);
        _npcFaction.ClearFactions(target);
        _tacticalMap.OnIFFRemoved(target);
        _overwatch.OnIFFRemoved(target);
        var damage = self ? settings.SelfDamage : args.Trained ? settings.SurgeryDamage : settings.UntrainedDamage;
        _damage.TryChangeDamage(target, damage, ignoreResistances: true, origin: user, tool: knife);

        var popup = self
            ? Loc.GetString("cmu-iff-removal-complete-self")
            : Loc.GetString("cmu-iff-removal-complete-other", ("target", Name(target)));
        _popup.PopupEntity(popup, target, user);
        if (!self)
            _popup.PopupEntity(Loc.GetString("cmu-iff-removal-complete-target", ("user", Name(user))), target, target, PopupType.MediumCaution);
    }
}
