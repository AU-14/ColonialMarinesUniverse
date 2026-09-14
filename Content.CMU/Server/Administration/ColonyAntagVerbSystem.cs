using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Shared.Administration;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
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
    // AntagPrototype.Category inherited from CMUAntagColonyBase.
    private const string ColonyCategory = "roles-antag-category-colony";

    // Fitting placeholder icons for the Colony Antag submenu (iconsOnly). hudCLF fallback.
    private static readonly Dictionary<string, SpriteSpecifier> PlaceholderIcons = new()
    {
        // Antag identifiers → RSI paths / states
        ["Arsonist"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudsquad_spec_pyro"),
        ["BountyHunter"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "cmb_mar"),
        ["Cannibal"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Weapons/Melee/cleaver.rsi"), "butch"),
        ["CLFSaboteur"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "clf_engi"),
        ["CLFVeteran"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "clf_mil"),
        ["CorporateSpy"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "weya_execspec"),
        ["DrugDealer"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Specific/Chemistry/pills.rsi"), "pill"),
        ["Fugitive"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Misc/handcuffs.rsi"), "handcuff"),
        ["Replicant"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudsquad_syn"),
        ["RunawaySynth"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudsquad_original"),
        ["SerialKiller"] = new SpriteSpecifier.Rsi(new("/Textures/Objects/Weapons/Melee/kitchen_knife.rsi"), "icon"),
        ["StrikeOrganizer"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Objects/Tools/megaphone.rsi"), "megaphone"),
        ["Vigilante"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "tse_paconstable"),
        ["WeylandYutaniAgent"] = new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/cm_job_icons.rsi"), "hudWE-YA"),
    };

    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

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

        foreach (var specifier in _proto.EnumeratePrototypes<AntagSpecifierPrototype>())
        {
            var role = ColonyRole(specifier);
            if (role == null)
                continue;

            // ForceMakeAntag resolves the rule entity by the specifier's id, so
            // only offer antags whose same-id rule actually lists the specifier.
            if (!_proto.TryIndex<EntityPrototype>(specifier.ID, out var rule)
                || !rule.TryGetComponent<AntagSelectionComponent>(out var selection)
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
                Act = () => _antag.ForceMakeAntag<GameRuleComponent>(target, ruleId, specId),
            });
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
