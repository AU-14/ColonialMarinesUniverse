using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// ARCH-06: Bad Blood castes. CMU14 ARCH-604: a caste is a <see cref="BadBloodCastePrototype"/>. A Bad Blood chooses
/// one at the Lodge ("Choose your caste", a dialog of every caste and its cap) and keeps it for the rest of that life.
/// The clan may have one of each caste per CapDivisor living Bad Bloods, and always MinCap.
/// The caste's powers are the components its prototype adds, read where they apply: the mask visor
/// (<see cref="BadBloodHunterVisorComponent"/>), the quarry's build range (<see cref="BadBloodBuildAnywhereComponent"/>),
/// the surgery flow (<see cref="BadBloodMedicSpeedComponent"/>) and the caste's own systems. A caste station is a
/// Lodge armory only its caste may open (BadBloodCampSystem.Armory).
/// </summary>
public sealed class BadBloodCasteSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private YautjaMaskSystem _mask = default!;
    [Dependency] private Content.Shared.Mind.SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodCasteChoiceComponent, GetVerbsEvent<Verb>>(OnLodgeVerbs);
        SubscribeLocalEvent<BadBloodCasteChosenEvent>(OnCasteChosen);
        SubscribeLocalEvent<BadBloodLeaderChangedEvent>(OnLeaderChanged);
    }

    // CMU14 ARCH-704 (D1): the Hunt Leader is a caste of its own. A caste-holder who takes the lead sheds the caste
    private void OnLeaderChanged(ref BadBloodLeaderChangedEvent ev)
    {
        if (ev.New is { } leader && !TerminatingOrDeleted(leader) && TryDropCaste(leader))
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-caste-shed-for-leader"), leader, leader, PopupType.Medium);
    }

    public bool IsCaste(EntityUid uid, ProtoId<BadBloodCastePrototype> caste)
    {
        return TryComp(uid, out BadBloodCasteComponent? comp) && comp.Caste == caste;
    }

    /// <summary>
    /// How many Bad Bloods may hold <paramref name="caste"/> right now: one per CapDivisor living, at least MinCap.
    /// </summary>
    public int GetCasteCap(ProtoId<BadBloodCastePrototype> caste)
    {
        if (!_proto.TryIndex(caste, out var proto))
            return 0;

        return Math.Max(proto.MinCap, _rule.CountLivingBadBloods() / Math.Max(proto.CapDivisor, 1));
    }

    /// <summary>
    /// Living Bad Bloods who hold <paramref name="caste"/>.
    /// </summary>
    public int CountCaste(ProtoId<BadBloodCastePrototype> caste)
    {
        var count = 0;
        var query = EntityQueryEnumerator<BadBloodCasteComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // a body nobody is in (ghosted out, or never taken) does not hold the slot. a player who only lost
            // their connection keeps it, their mind is still in the body
            if (comp.Caste == caste && !_mobState.IsDead(uid) && _mind.TryGetMind(uid, out _, out _))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Whether <paramref name="user"/> may take up <paramref name="caste"/>. <paramref name="reason"/> is a loc key.
    /// </summary>
    public bool CanTakeCaste(EntityUid user, ProtoId<BadBloodCastePrototype> caste, out string reason)
    {
        reason = "cmu-bad-blood-caste-not-clan";
        if (!_badBlood.IsBadBlood(user) || !_rule.IsRuleActive() || _mobState.IsDead(user) || !_proto.HasIndex(caste))
            return false;

        reason = "cmu-bad-blood-caste-already";
        if (HasComp<BadBloodCasteComponent>(user))
            return false;

        // CMU14 ARCH-704: the Hunt Leader holds no caste
        reason = "cmu-bad-blood-caste-leader";
        if (HasComp<BadBloodLeaderComponent>(user))
            return false;

        reason = "cmu-bad-blood-caste-full";
        return CountCaste(caste) < GetCasteCap(caste);
    }

    /// <summary>
    /// Makes <paramref name="user"/> a Bad Blood of <paramref name="caste"/> for the rest of this life.
    /// </summary>
    public bool TryTakeCaste(EntityUid user, ProtoId<BadBloodCastePrototype> caste)
    {
        if (!CanTakeCaste(user, caste, out var reason) || !_proto.TryIndex(caste, out var proto))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("cap", GetCasteCap(caste))), user, user, PopupType.SmallCaution);
            return false;
        }

        var comp = EnsureComp<BadBloodCasteComponent>(user);
        comp.Caste = caste;
        comp.SkillsBefore.Clear();
        comp.AddedComponents.Clear();
        Dirty(user, comp);

        // an Engineer plants charges, which need RMC engineering
        foreach (var (skill, level) in proto.Skills)
        {
            var before = _skills.GetSkill(user, skill);
            if (before >= level)
                continue;

            comp.SkillsBefore[skill] = before;
            _skills.SetSkill(user, skill, level);
        }

        // CMU14 ARCH-701: remember what the caste added, so dropping it removes only that
        foreach (var (name, entry) in proto.AddComponents)
        {
            if (!HasComp(user, entry.Component.GetType()))
                comp.AddedComponents.Add(name);
        }

        EntityManager.AddComponents(user, proto.AddComponents, removeExisting: false);

        // an Engineer sees as an honourable hunter does at once, without toggling the visor
        if (HasComp<BadBloodHunterVisorComponent>(user))
            _mask.RefreshVisorGlasses(user);

        _popup.PopupEntity(Loc.GetString(proto.Taken), user, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} took up the Bad Blood {caste.Id} caste");

        var ev = new BadBloodCasteTakenEvent(user, caste);
        RaiseLocalEvent(ref ev);
        return true;
    }

    /// <summary>
    /// CMU14 ARCH-701: takes <paramref name="user"/>'s caste away: the components it added go, the skills it raised
    /// go back to what they were, and the slot frees. Used when a caste-holder becomes the Hunt Leader (ARCH-704).
    /// Returns false when they hold no caste.
    /// </summary>
    public bool TryDropCaste(EntityUid user)
    {
        if (!TryComp(user, out BadBloodCasteComponent? comp))
            return false;

        var caste = comp.Caste;
        foreach (var name in comp.AddedComponents)
        {
            if (_componentFactory.TryGetRegistration(name, out var registration))
                RemComp(user, registration.Type);
        }

        foreach (var (skill, level) in comp.SkillsBefore)
        {
            _skills.SetSkill(user, skill, level);
        }

        RemComp<BadBloodCasteComponent>(user);

        // an Engineer's visor goes back to the clan's own sight
        _mask.RefreshVisorGlasses(user);

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} dropped the Bad Blood {caste.Id} caste");

        var ev = new BadBloodCasteDroppedEvent(user, caste);
        RaiseLocalEvent(ref ev);
        return true;
    }

    /// <summary>
    /// Opens the Lodge's caste dialog for <paramref name="user"/>: every caste, how many hold it and its cap.
    /// </summary>
    public void OpenCasteDialog(EntityUid user, EntityUid lodge)
    {
        var options = new List<DialogOption>();
        foreach (var proto in _proto.EnumeratePrototypes<BadBloodCastePrototype>().OrderBy(p => p.ID, StringComparer.Ordinal))
        {
            options.Add(new DialogOption(
                Loc.GetString("cmu-bad-blood-caste-option",
                    ("verb", Loc.GetString(proto.Verb)),
                    ("held", CountCaste(proto.ID)),
                    ("cap", GetCasteCap(proto.ID))),
                new BadBloodCasteChosenEvent(GetNetEntity(user), proto.ID, GetNetEntity(lodge))));
        }

        _dialog.OpenOptions(
            user,
            Loc.GetString("cmu-bad-blood-caste-dialog-title"),
            options,
            Loc.GetString("cmu-bad-blood-caste-dialog-message"));
    }

    private void OnLodgeVerbs(Entity<BadBloodCasteChoiceComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !_badBlood.IsBadBlood(args.User) ||
            HasComp<BadBloodCasteComponent>(args.User) ||
            HasComp<BadBloodLeaderComponent>(args.User))
        {
            return;
        }

        var user = args.User;
        var lodge = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-caste-choose-verb"),
            Message = Loc.GetString("cmu-bad-blood-caste-dialog-message"),
            Act = () => OpenCasteDialog(user, lodge),
        });
    }

    // the dialog's options are built here on the server, so the user in the event is the one it was opened for
    // CMU14 ARCH-604: a dialog left open does not travel. The choice counts only at a standing Lodge within reach
    private void OnCasteChosen(BadBloodCasteChosenEvent ev)
    {
        if (!TryGetEntity(ev.User, out var user) || TerminatingOrDeleted(user) ||
            !TryGetEntity(ev.Lodge, out var lodge) || TerminatingOrDeleted(lodge) ||
            !_interaction.InRangeUnobstructed(user.Value, lodge.Value))
        {
            return;
        }

        TryTakeCaste(user.Value, ev.Caste);
    }
}
