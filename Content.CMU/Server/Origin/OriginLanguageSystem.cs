using Content.Server._RMC14.Language.Systems;
using Content.Shared.AU14.Origin;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Origin;

public sealed class OriginLanguageSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly LanguageLearningSystem _learning = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.Mob.IsValid())
            return;

        var profile = ev.Profile;

        if (profile.Origin == null)
            return;

        if (!_proto.TryIndex(profile.Origin.Value, out OriginPrototype? origin))
            return;

        foreach (var lang in origin.Languages)
        {
            _language.AddLanguage(
                ev.Mob,
                lang,
                addSpoken: true,
                addUnderstood: true);
        }

        foreach (var lang in origin.LearnableLanguages)
        {
            _learning.AddLearnableLanguage(ev.Mob, lang);
        }
    }
}