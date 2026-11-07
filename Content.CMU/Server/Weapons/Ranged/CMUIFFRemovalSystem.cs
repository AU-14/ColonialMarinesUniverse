using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Weapons.Ranged.IFF;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Weapons.Ranged;

public sealed class CMUIFFRemovalSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private CMUHostileIFFSystem _hostileIFF = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<CMUIFFRemovalPrototype> SettingsId = "CMUIFFRemoval";
    private static readonly VerbCategory IFFCategory = new("cmu-iff-removal-category", null);

    private CMUIFFRemovalPrototype Settings => _prototypes.Index(SettingsId);

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
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
            Act = () => RemoveIFF(args.User, args.Target, knife),
        });
    }

    private bool CanRemoveIFF(EntityUid user, EntityUid target, EntityUid knife)
    {
        var settings = Settings;
        if (!HasComp<MarineComponent>(target)
            || !_tags.HasTag(knife, settings.KnifeTag)
            || !IsFriendlyIFF(target, settings)
            || !IsHeld(user, knife))
        {
            return false;
        }

        return user == target || _skills.HasSkill(user, settings.SurgerySkill, settings.SurgerySkillLevel);
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

    private void RemoveIFF(EntityUid user, EntityUid target, EntityUid knife)
    {
        if (!CanRemoveIFF(user, target, knife))
            return;

        var self = user == target;
        var settings = Settings;
        _hostileIFF.StripIFF(target);
        _damage.TryChangeDamage(target, self ? settings.SelfDamage : settings.SurgeryDamage, origin: user, tool: knife);

        var popup = self
            ? Loc.GetString("cmu-iff-removal-complete-self")
            : Loc.GetString("cmu-iff-removal-complete-other", ("target", Name(target)));
        _popup.PopupEntity(popup, target, user);
    }
}
