using Content.Server._RMC14.Language.Systems;
using Content.Server.AU14.Round;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.GameTicking;
using Content.Shared._RMC14.Language;

namespace Content.Server._CMU14.Platoon;

public sealed partial class PlatoonLanguageSystem : EntitySystem
{
    [Dependency] private LanguageLearningSystem _learning = default!;
    [Dependency] private CMURoundDirectorSystem _roundDirector = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<MarineComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
    }

    private PlatoonPrototype? GetPlatoonForMarine(MarineComponent marine)
    {
        var side = marine.Faction switch
        {
            "govfor" => RoundSide.Govfor,
            "opfor" => RoundSide.Opfor,
            _ => (RoundSide?) null,
        };

        return side is { } resolved &&
               _roundDirector.TryGetCommittedLegacyForce(resolved, out var platoon)
            ? platoon
            : null;
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
