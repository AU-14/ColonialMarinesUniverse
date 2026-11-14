using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Administration;
using Content.Shared.Antag;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Administration;

// Right-click "Make <antag>" verbs for every colony antag, built from the
// antagSpecifier prototypes instead of a hardcoded verb per rule like
// upstream AdminVerbSystem.Antags. New colony antags are covered as long as
// their role keeps the colony category and their rule entity keeps the
// specifier's id.
public sealed partial class ColonyAntagVerbSystem : EntitySystem
{
    private const string ColonyCategory = "roles-antag-category-colony";

    // Fitting placeholder icons for the Colony Antag submenu (iconsOnly). hudCLF fallback.
    private static readonly Dictionary<string, SpriteSpecifier> PlaceholderIcons = new()
    {
        ["Arsonist"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudsquad_spec_pyro"),
        ["BountyHunter"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "cmb_mar"),
        ["Cannibal"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Weapons/Melee/cleaver.rsi"), "butch"),
        ["CLFSaboteur"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "clf_engi"),
        ["CLFVeteran"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "clf_mil"),
        ["CorporateSpy"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "weya_execspec"),
        ["DrugDealer"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Specific/Chemistry/pills.rsi"), "pill"),
        // COUPLING-04: its own glyph instead of the CLF fallback
        ["EWJoe"] = new SpriteSpecifier.Rsi(new("/Textures/CMU14/Interface/au14colonyjobicons.rsi"), "working_joe"),
        ["Fugitive"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Misc/handcuffs.rsi"), "handcuff"),
        ["Replicant"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudsquad_syn"),
        ["RunawaySynth"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudsquad_original"),
        ["SerialKiller"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Weapons/Melee/kitchen_knife.rsi"), "icon"),
        ["StrikeOrganizer"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Objects/Tools/megaphone.rsi"), "megaphone"),
        ["Vigilante"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "tse_paconstable"),
        ["WeylandYutaniAgent"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudWE-YA"),
    };

    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private EWJoeRuleSystem _ewRule = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddMakeAntagVerbs);
    }

    private void AddMakeAntagVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        if (!_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target)
            || !TryComp(args.Target, out ActorComponent? targetActor))
            return;

        var target = targetActor.PlayerSession;
        var user = args.User;

        foreach (var specifier in _proto.EnumeratePrototypes<AntagSpecifierPrototype>())
        {
            var role = ColonyRole(specifier);
            if (role == null)
                continue;

            // ForceMakeAntag resolves the rule entity by the specifier's id, so
            // only offer antags whose same-id rule actually lists the specifier.
            if (!_proto.TryIndex<EntityPrototype>(specifier.ID, out var rule)
                || !rule.TryComp<AntagSelectionComponent>(out var selection, Factory)
                || !selection.Antags.Any(sel => sel.Proto == specifier.ID))
                continue;

            var ruleId = specifier.ID;
            var specId = specifier.ID;
            var antagName = Loc.GetString(role.Name);

            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-admin-verb-make-antag", ("antag", antagName)),
                Category = VerbCategory.Antag,
                Icon = PlaceholderIcons.GetValueOrDefault(specifier.ID, new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudCLF")),
                Message = Loc.GetString(role.Objective),
                Impact = LogImpact.High,
                Act = specId == EWJoeRuleSystem.AntagId
                    ? () => MakeEWJoe(user, target)
                    : () => _antag.ForceMakeAntag<GameRuleComponent>(target, ruleId, specId),
            });
        }
    }

    /// <summary>
    /// FLOW-07: an admin's Early Warning Joe ignores the ewjoemechanics switch, which only stops the antag being
    /// rolled. The Grand Machine stand-down (D10) still applies, and the admin is told when nothing was made.
    /// </summary>
    public void MakeEWJoe(EntityUid admin, ICommonSession target)
    {
        if (_ewRule.IsGrandMachineSelected())
        {
            _adminLog.Add(LogType.AntagSelection, LogImpact.Medium,
                $"{ToPrettyString(admin):admin} tried to make {target:player} an Early Warning Joe; refused: the Grand Machine threat is active");
            _popup.PopupEntity(Loc.GetString("ew-joe-admin-refused-grand-machine"), admin, admin, PopupType.MediumCaution);
            return;
        }

        using (_ewRule.AdminOverride())
        {
            _antag.ForceMakeAntag<GameRuleComponent>(target, EWJoeRuleSystem.AntagId, EWJoeRuleSystem.AntagId);
        }

        // ForceMakeAntag refuses silently, e.g. when the round's rule already has its one Joe
        if (target.AttachedEntity is not { } body || !HasComp<EWJoeComponent>(body))
        {
            _adminLog.Add(LogType.AntagSelection, LogImpact.Medium,
                $"{ToPrettyString(admin):admin} tried to make {target:player} an Early Warning Joe; nothing was made (antag cap reached or invalid body)");
            _popup.PopupEntity(Loc.GetString("ew-joe-admin-failed"), admin, admin, PopupType.MediumCaution);
        }
    }

    private AntagPrototype? ColonyRole(AntagSpecifierPrototype specifier)
    {
        foreach (var protoId in specifier.PrefRoles)
        {
            if (_proto.TryIndex(protoId, out var proto)
                && proto.Category == ColonyCategory)
                return proto;
        }

        return null;
    }
}
