using System.Linq;
using Content.Server._RMC14.Humanoid;
using Content.Server._RMC14.Language.Systems;
using Content.Server._RMC14.Traits;
using Content.Server.CMU14.Origin;
using Content.Server.CMU14.Platoon;
using Content.Server.CMU14.Roles;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Jobs;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Language;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Language;

/// <summary>
/// English is a language trait like any other, so it is taken off characters whose languages came from the
/// species default. Runs before the trait, origin and platoon passes so whatever they grant stays.
/// </summary>
public sealed partial class CMUSpeciesEnglishSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;

    private const string LanguageComponentName = "Language";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn,
            after: [typeof(RMCHumanoidSystem)],
            before: [typeof(RMCTraitSystem), typeof(OriginLanguageSystem), typeof(PlatoonLanguageSystem)]);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.Mob.IsValid() ||
            ev.JobId == null ||
            !_prototypes.TryIndex<JobPrototype>(ev.JobId, out var job) ||
            !job.ApplyTraits ||
            !TryComp<LanguageComponent>(ev.Mob, out var language))
        {
            return;
        }

        // a job that brings its own languages (a preset or an explicit list) keeps them as written
        if (language.Preset != null || JobDefinesLanguages(job))
            return;

        _language.RemoveLanguage((ev.Mob, language), SharedLanguageSystem.CommonLanguage);
    }

    private bool JobDefinesLanguages(JobPrototype job)
    {
        foreach (var profile in _roundJobProfiles.GetProfileComponents(job))
        {
            if (profile.Components.ContainsKey(LanguageComponentName))
                return true;
        }

        return job.Special.OfType<AddComponentSpecial>()
            .Any(special => special.Components.ContainsKey(LanguageComponentName));
    }
}

/// <summary>
/// Gives colony characters the languages set on their colony's planet (see <see cref="CMUColonyLanguageComponent"/>),
/// teaches Insurgency GOVFOR leadership the colony's language, and makes sure nobody spawns speaking nothing.
/// </summary>
public sealed partial class CMUCharacterLanguageSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    private const string InsurgencyPreset = "Insurgency";
    private const string ColonyForceClf = "CLF";

    /// <summary>
    /// Round roles of the GOVFOR leadership that also speaks the colony's language on Insurgency:
    /// the CO, XO, staff officers, intel officers, fireteam leaders and squad leaders.
    /// </summary>
    private static readonly HashSet<string> InsurgencyColonySpeakerRoles = new()
    {
        "PlatoonCommander",
        "ExecutiveOfficer",
        "JuniorOfficer",
        "IntelOfficer",
        "RadioTelephoneOperator",
        "SquadSergeant",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn,
            after:
            [
                typeof(CMUSpeciesEnglishSystem),
                typeof(RMCHumanoidSystem),
                typeof(RMCTraitSystem),
                typeof(OriginLanguageSystem),
                typeof(PlatoonLanguageSystem),
            ]);

        // ghost-role bodies (third parties such as the CLF reinforcements) never raise PlayerSpawnCompleteEvent;
        // their job is applied when the ghost-role body is set up
        SubscribeLocalEvent<LanguageComponent, CMURoundJobProfileAppliedEvent>(OnGhostRoleJobApplied);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.Mob.IsValid() || !TryComp<LanguageComponent>(ev.Mob, out var language))
            return;

        if (ev.JobId != null && _prototypes.TryIndex<JobPrototype>(ev.JobId, out var job))
            ApplyJobLanguages((ev.Mob, language), job);

        EnsureSpokenLanguage((ev.Mob, language));
    }

    private void OnGhostRoleJobApplied(Entity<LanguageComponent> ent, ref CMURoundJobProfileAppliedEvent args)
    {
        // lobby spawns are handled in OnPlayerSpawn, after traits and origins
        if (!HasComp<GhostRoleComponent>(ent))
            return;

        ApplyJobLanguages(ent, args.Job);
    }

    private void ApplyJobLanguages(Entity<LanguageComponent> ent, JobPrototype job)
    {
        var side = _roundJobProfiles.GetRoundSide(job);
        if (IsColonyJob(job, side))
            ApplyColonyLanguages(ent);
        else if (side == RoundJobSide.Govfor && IsInsurgencyColonySpeaker(job))
            TeachColonyLanguage(ent);

        ApplyExtraLanguages(ent);
    }

    /// <summary>
    /// Languages a job adds on top of everything else (see <see cref="CMUExtraLanguagesComponent"/>).
    /// </summary>
    private void ApplyExtraLanguages(Entity<LanguageComponent> ent)
    {
        if (!TryComp<CMUExtraLanguagesComponent>(ent, out var extra))
            return;

        foreach (var lang in extra.Languages)
            _language.AddLanguage(ent, lang);

        if (extra.DefaultLanguage is { } defaultLanguage)
        {
            _language.AddLanguage(ent, defaultLanguage);
            _language.SetDefaultLanguage(ent.AsNullable(), defaultLanguage);
        }
    }

    private void EnsureSpokenLanguage(Entity<LanguageComponent> ent)
    {
        var (uid, language) = ent;
        if (language.SpokenLanguages.Count == 0)
        {
            _language.AddLanguage(uid, SharedLanguageSystem.CommonLanguage);
            _language.SetDefaultLanguage((uid, language), SharedLanguageSystem.CommonLanguage);
        }
    }

    private static bool IsColonyJob(JobPrototype job, RoundJobSide side)
    {
        return side == RoundJobSide.Civilian ||
               string.Equals(job.RoundForce, ColonyForceClf, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInsurgencyColonySpeaker(JobPrototype job)
    {
        var preset = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;
        return string.Equals(preset, InsurgencyPreset, StringComparison.OrdinalIgnoreCase) &&
               job.RoundRole != null &&
               InsurgencyColonySpeakerRoles.Contains(job.RoundRole);
    }

    private void ApplyColonyLanguages(Entity<LanguageComponent> ent)
    {
        if (GetColonyLanguages() is not { } colony)
            return;

        foreach (var lang in colony.Languages)
            _language.AddLanguage(ent, lang);

        _language.AddLanguage(ent, colony.DefaultLanguage);

        // synthetics keep the default their own preset gives them
        if (!HasComp<SynthComponent>(ent))
            _language.SetDefaultLanguage(ent.AsNullable(), colony.DefaultLanguage);
    }

    /// <summary>
    /// Adds spoken and understood languages, optionally making one of them the default.
    /// </summary>
    public void AddLanguages(EntityUid uid, IEnumerable<ProtoId<LanguagePrototype>> languages,
        ProtoId<LanguagePrototype>? defaultLanguage = null)
    {
        if (!TryComp<LanguageComponent>(uid, out var language))
            return;

        foreach (var lang in languages)
            _language.AddLanguage(uid, lang);

        if (defaultLanguage is { } lang2)
        {
            _language.AddLanguage(uid, lang2);
            _language.SetDefaultLanguage((uid, language), lang2);
        }
    }

    /// <summary>
    /// Teaches the selected colony's default language without making it the default.
    /// </summary>
    public void TeachColonyLanguage(EntityUid uid)
    {
        if (TryComp<LanguageComponent>(uid, out var language))
            TeachColonyLanguage((uid, language));
    }

    private void TeachColonyLanguage(Entity<LanguageComponent> ent)
    {
        if (GetColonyLanguages() is not { } colony)
            return;

        // only learned: the platoon's language stays the default
        _language.AddLanguage(ent, colony.DefaultLanguage);
    }

    /// <summary>
    /// The languages set on the selected planet's prototype by its <see cref="CMUColonyLanguageComponent"/>.
    /// </summary>
    public CMUColonyLanguageComponent? GetColonyLanguages()
    {
        if (_auRound.GetSelectedPlanetId() is not { } planetId ||
            !_prototypes.TryIndex<EntityPrototype>(planetId, out var planet) ||
            !planet.TryComp(out CMUColonyLanguageComponent? colony, _compFactory))
        {
            return null;
        }

        return colony;
    }
}
