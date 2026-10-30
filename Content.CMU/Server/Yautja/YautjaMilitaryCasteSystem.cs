using Content.Server.Chat.Managers;
using Content.Server.Players.JobWhitelist;
using Content.Server.Preferences.Managers;
using Content.Shared.Chat;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Dataset;
using Content.Shared.Mind.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja;

/// <summary>
/// Sets up a Military Caste body when a player takes it, like cmss13's /datum/equipment_preset/yautja/soldier:
/// the player's pred appearance, a random codename instead of their pred name, full stomach and the deathsquad briefing.
/// </summary>
public sealed partial class YautjaMilitaryCasteSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private YautjaProfileApplySystem _profileApply = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    private static readonly ProtoId<JobPrototype> HunterJob = "CMUYautjaHunter";
    private static readonly ProtoId<LocalizedDatasetPrototype> Codenames = "CMUNamesYautjaMilitaryCaste";
    private static readonly ProtoId<SatiationTypePrototype> Hunger = "Hunger";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaMilitaryCasteComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaMilitaryCasteComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMapInit(Entity<YautjaMilitaryCasteComponent> ent, ref MapInitEvent args)
    {
        // the hunter entry lines tell you to follow the honor code, the military caste doesn't
        if (TryComp<YautjaComponent>(ent, out var yautja))
            yautja.EntryMessageSent = true;
    }

    private void OnMindAdded(Entity<YautjaMilitaryCasteComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.SetupComplete ||
            args.Mind.Comp.UserId is not { } userId ||
            !_players.TryGetSessionById(userId, out var session))
        {
            return;
        }

        ent.Comp.SetupComplete = true;

        // cmss13 load_race still takes skin and hair from the player's pred prefs, only the name is replaced
        if (_preferences.GetPreferencesOrNull(userId)?.SelectedCharacter is HumanoidCharacterProfile profile)
            _profileApply.ApplyProfile(ent, profile.YautjaProfile, YautjaRank.Blooded, equipProfileGear: false);

        // whitelisted preds look more senior, no gear difference
        var whitelisted = _jobWhitelist.IsWhitelisted(userId, HunterJob);
        ent.Comp.WhitelistIcon = whitelisted;
        Dirty(ent);

        _metaData.SetEntityName(ent, GetCodename(ent.Comp.Caste, whitelisted));

        if (TryComp<SatiationComponent>(ent, out var satiation) &&
            _satiation.GetMaximumValue((ent, satiation), Hunger) is { } max)
        {
            _satiation.SetValue((ent, satiation), Hunger, (float) max);
        }

        SendBriefing(ent.Comp.Caste, session);
    }

    public string GetCodename(YautjaMilitaryCaste caste, bool whitelisted)
    {
        var name = _random.Pick(_prototypes.Index(Codenames));
        if (caste == YautjaMilitaryCaste.Enforcer)
        {
            name = Loc.GetString("cmu-yautja-mcaste-enforcer-name", ("name", name));
            return whitelisted ? Loc.GetString("cmu-yautja-mcaste-high-name", ("name", name)) : name;
        }

        return whitelisted ? Loc.GetString("cmu-yautja-mcaste-elite-name", ("name", name)) : name;
    }

    private void SendBriefing(YautjaMilitaryCaste caste, ICommonSession session)
    {
        SendLine(session, caste == YautjaMilitaryCaste.Enforcer
            ? "cmu-yautja-mcaste-briefing-enforcer"
            : "cmu-yautja-mcaste-briefing-soldier");
        SendLine(session, "cmu-yautja-mcaste-briefing-answer");
        SendLine(session, "cmu-yautja-mcaste-briefing-prejudice");
        SendLine(session, "cmu-yautja-mcaste-briefing-hostile");
    }

    private void SendLine(ICommonSession session, string key)
    {
        var text = Loc.GetString(key);
        _chat.ChatMessageToOne(ChatChannel.Server, text, text, default, false, session.Channel);
    }
}
