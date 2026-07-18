using Content.Server._RMC14.Language.Systems;
using Content.Server.AU14.Round;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Language;

namespace Content.Server._CMU14.Platoon;

public sealed class PlatoonLanguageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly LanguageLearningSystem _learning = default!;
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoonSpawnRule = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<MarineComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
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

        // learnable languages still set at spawn only
        foreach (var lang in platoon.LearnableLanguages)
            _learning.AddLearnableLanguage(ev.Mob, lang);
    }
}