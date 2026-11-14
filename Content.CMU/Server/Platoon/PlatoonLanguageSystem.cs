using Content.Server._RMC14.Language.Systems;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Roles;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.util;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;

namespace Content.Server.CMU14.Platoon;

public sealed partial class PlatoonLanguageSystem : EntitySystem
{
    [Dependency] private  LanguageLearningSystem _learning = default!;
    [Dependency] private  LanguageSystem _language = default!;
    [Dependency] private  PlatoonSpawnRuleSystem _platoonSpawnRule = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<MarineComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
        SubscribeLocalEvent<RoundJobProfileAppliedComponent, ComponentStartup>(OnProfileApplied);
    }

    private void OnProfileApplied(Entity<RoundJobProfileAppliedComponent> ent, ref ComponentStartup args)
    {
        // The round profile is what sets Marine.Faction, and it lands after the spawn
        // language pass, so re-derive here where the faction is guaranteed current.
        if (!TryComp<MarineComponent>(ent.Owner, out var marine))
            return;

        var platoon = GetPlatoonForMarine(marine);
        if (platoon == null)
            return;

        _language.UpdateEntityLanguages(ent.Owner);
        ApplyDefaultLanguage(ent.Owner, platoon);

        foreach (var lang in platoon.LearnableLanguages)
            _learning.AddLearnableLanguage(ent.Owner, lang);
    }

    /// <summary>
    /// The language the platoon speaks by default: its <see cref="PlatoonPrototype.DefaultLanguage"/>,
    /// else the first of its languages.
    /// </summary>
    public static ProtoId<LanguagePrototype>? GetDefaultLanguage(PlatoonPrototype platoon)
    {
        if (platoon.DefaultLanguage is { } language)
            return language;

        if (platoon.Languages.Count > 0)
            return platoon.Languages[0];

        return null;
    }

    private void ApplyDefaultLanguage(EntityUid uid, PlatoonPrototype platoon)
    {
        if (GetDefaultLanguage(platoon) is { } language)
            _language.SetDefaultLanguage(uid, language);
    }

    private PlatoonPrototype? GetPlatoonForMarine(MarineComponent marine)
    {
        if (marine.Faction == "govfor")
            return _platoonSpawnRule.SelectedGovforPlatoon;
        if (marine.Faction == "opfor")
            return _platoonSpawnRule.SelectedOpforPlatoon;
        return null;
    }

    private void OnDetermineLanguages(Entity<MarineComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        var platoon = GetPlatoonForMarine(ent.Comp);
        if (platoon == null)
            return;

        // re-add platoon languages after trait removals
        foreach (var lang in platoon.Languages)
        {
            args.SpokenLanguages.Add(lang);
            args.UnderstoodLanguages.Add(lang);
        }

        if (platoon.DefaultLanguage is { } defaultLanguage)
        {
            args.SpokenLanguages.Add(defaultLanguage);
            args.UnderstoodLanguages.Add(defaultLanguage);
        }
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.Mob.IsValid())
            return;

        if (!TryComp<MarineComponent>(ev.Mob, out var marine))
            return;

        var platoon = GetPlatoonForMarine(marine);
        if (platoon == null)
            return;

        // the mob's languages were built at mapinit, before the job gave it a faction, so
        // platoon languages only showed up if a trait or origin happened to force a refresh later
        _language.UpdateEntityLanguages(ev.Mob);
        ApplyDefaultLanguage(ev.Mob, platoon);

        // learnable languages still set at spawn only
        foreach (var lang in platoon.LearnableLanguages)
            _learning.AddLearnableLanguage(ev.Mob, lang);
    }
}