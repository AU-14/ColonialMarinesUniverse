using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server._RMC14.Emote;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaVoiceSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private RMCEmoteSystem _emote = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private YautjaPowerSystem _power = default!;

    private static readonly ProtoId<EmotePrototype> ClickEmote = "CMUYautjaClick";
    private static readonly ProtoId<EmotePrototype> RoarEmote = "CMUYautjaRoar";
    private static readonly ProtoId<EmotePrototype> LaughEmote = "CMUYautjaLaugh";
    private static readonly ProtoId<EmotePrototype> GrowlEmote = "CMUYautjaGrowl";
    private static readonly ProtoId<EmotePrototype> PainEmote = "CMUYautjaPain";
    private static readonly ProtoId<EmotePrototype> DistractEmote = "CMUYautjaDistract";
    private static readonly ProtoId<EmotePrototype> DeathCryEmote = "CMUYautjaDeathCry";
    private static readonly ProtoId<EmotePrototype> DeathLaughEmote = "CMUYautjaDeathLaugh";
    private static readonly ProtoId<EmotePrototype> AudioClickEmote = "CMUYautjaAudioClick";
    private static readonly ProtoId<EmotePrototype> AudioClick2Emote = "CMUYautjaAudioClick2";
    private static readonly ProtoId<EmotePrototype> AudioGrowlEmote = "CMUYautjaAudioGrowl";
    private static readonly ProtoId<EmotePrototype> AudioLaugh1Emote = "CMUYautjaAudioLaugh1";
    private static readonly ProtoId<EmotePrototype> AudioLaugh2Emote = "CMUYautjaAudioLaugh2";
    private static readonly ProtoId<EmotePrototype> AudioLaugh3Emote = "CMUYautjaAudioLaugh3";
    private static readonly ProtoId<EmotePrototype> AudioLaugh4Emote = "CMUYautjaAudioLaugh4";
    private static readonly ProtoId<EmotePrototype> AudioLaugh5Emote = "CMUYautjaAudioLaugh5";
    private static readonly ProtoId<EmotePrototype> AudioLaugh6Emote = "CMUYautjaAudioLaugh6";
    private static readonly ProtoId<EmotePrototype> AudioRoarEmote = "CMUYautjaAudioRoar";
    private static readonly ProtoId<EmotePrototype> AudioRoar2Emote = "CMUYautjaAudioRoar2";
    private static readonly ProtoId<EmotePrototype> FakeAlienGrowlEmote = "CMUYautjaFakeAlienGrowl";
    private static readonly ProtoId<EmotePrototype> FakeAlienHelpEmote = "CMUYautjaFakeAlienHelp";
    private static readonly ProtoId<EmotePrototype> FakeMaleScreamEmote = "CMUYautjaFakeMaleScream";
    private static readonly ProtoId<EmotePrototype> FakeFemaleScreamEmote = "CMUYautjaFakeFemaleScream";
    private static readonly ProtoId<EmotePrototype> VoiceSynthAnytimeEmote = "CMUYautjaVoiceSynthAnytime";
    private static readonly ProtoId<EmotePrototype> VoiceSynthHelpMeEmote = "CMUYautjaVoiceSynthHelpMe";
    private static readonly ProtoId<EmotePrototype> VoiceSynthISeeYouEmote = "CMUYautjaVoiceSynthISeeYou";
    private static readonly ProtoId<EmotePrototype> VoiceSynthItsATrapEmote = "CMUYautjaVoiceSynthItsATrap";
    private static readonly ProtoId<EmotePrototype> VoiceSynthOverHereEmote = "CMUYautjaVoiceSynthOverHere";
    private static readonly ProtoId<EmotePrototype> VoiceSynthTurnAroundEmote = "CMUYautjaVoiceSynthTurnAround";
    private static readonly ProtoId<EmotePrototype> VoiceSynthComeOnOutEmote = "CMUYautjaVoiceSynthComeOnOut";
    private static readonly ProtoId<EmotePrototype> VoiceSynthOverThereEmote = "CMUYautjaVoiceSynthOverThere";
    private static readonly ProtoId<EmotePrototype> VoiceSynthUglyFreakEmote = "CMUYautjaVoiceSynthUglyFreak";
    private static readonly ProtoId<EmotePrototype> VoiceSynthLuckyYouEmote = "CMUYautjaVoiceSynthLuckyYou";
    private static readonly ProtoId<EmotePrototype> VoiceSynthJustYouEmote = "CMUYautjaVoiceSynthJustYou";
    private static readonly ProtoId<EmotePrototype> VoiceSynthTellMeEmote = "CMUYautjaVoiceSynthTellMe";
    private static readonly ProtoId<EmotePrototype> VoiceSynthDoItRookieEmote = "CMUYautjaVoiceSynthDoItRookie";
    private static readonly ProtoId<EmotePrototype> VoiceSynthForwardMarineEmote = "CMUYautjaVoiceSynthForwardMarine";
    private static readonly ProtoId<EmotePrototype> VoiceSynthBurnYouFuckerEmote = "CMUYautjaVoiceSynthBurnYouFucker";
    private const string YautjaAudioCategory = "cmu-yautja-audio-panel-category-yautja";
    private const string VoiceSynthCategory = "cmu-yautja-audio-panel-category-voice-synthesizer";
    private const string FakeAudioCategory = "cmu-yautja-audio-panel-category-fake-sound";

    private static readonly List<AudioPanelEmote> AudioPanelEmotes = new()
    {
        new("click", AudioClickEmote, "cmu-yautja-audio-panel-emote-click", YautjaAudioCategory),
        new("click2", AudioClick2Emote, "cmu-yautja-audio-panel-emote-click2", YautjaAudioCategory),
        new("growl", AudioGrowlEmote, "cmu-yautja-audio-panel-emote-growl", YautjaAudioCategory),
        new("laugh1", AudioLaugh1Emote, "cmu-yautja-audio-panel-emote-laugh1", YautjaAudioCategory),
        new("laugh2", AudioLaugh2Emote, "cmu-yautja-audio-panel-emote-laugh2", YautjaAudioCategory),
        new("laugh3", AudioLaugh3Emote, "cmu-yautja-audio-panel-emote-laugh3", YautjaAudioCategory),
        new("laugh4", AudioLaugh4Emote, "cmu-yautja-audio-panel-emote-laugh4", YautjaAudioCategory),
        new("laugh5", AudioLaugh5Emote, "cmu-yautja-audio-panel-emote-laugh5", YautjaAudioCategory),
        new("laugh6", AudioLaugh6Emote, "cmu-yautja-audio-panel-emote-laugh6", YautjaAudioCategory),
        new("roar", AudioRoarEmote, "cmu-yautja-audio-panel-emote-roar", YautjaAudioCategory),
        new("roar2", AudioRoar2Emote, "cmu-yautja-audio-panel-emote-roar2", YautjaAudioCategory),
        new("anytime", VoiceSynthAnytimeEmote, "cmu-yautja-audio-panel-emote-anytime", VoiceSynthCategory),
        new("helpme", VoiceSynthHelpMeEmote, "cmu-yautja-audio-panel-emote-helpme", VoiceSynthCategory),
        new("iseeyou", VoiceSynthISeeYouEmote, "cmu-yautja-audio-panel-emote-iseeyou", VoiceSynthCategory),
        new("itsatrap", VoiceSynthItsATrapEmote, "cmu-yautja-audio-panel-emote-itsatrap", VoiceSynthCategory),
        new("overhere", VoiceSynthOverHereEmote, "cmu-yautja-audio-panel-emote-overhere", VoiceSynthCategory),
        new("turnaround", VoiceSynthTurnAroundEmote, "cmu-yautja-audio-panel-emote-turnaround", VoiceSynthCategory),
        new("comeonout", VoiceSynthComeOnOutEmote, "cmu-yautja-audio-panel-emote-comeonout", VoiceSynthCategory),
        new("overthere", VoiceSynthOverThereEmote, "cmu-yautja-audio-panel-emote-overthere", VoiceSynthCategory),
        new("uglyfreak", VoiceSynthUglyFreakEmote, "cmu-yautja-audio-panel-emote-uglyfreak", VoiceSynthCategory),
        new("luckyyou", VoiceSynthLuckyYouEmote, "cmu-yautja-audio-panel-emote-luckyyou", VoiceSynthCategory),
        new("justyou", VoiceSynthJustYouEmote, "cmu-yautja-audio-panel-emote-justyou", VoiceSynthCategory),
        new("tellme", VoiceSynthTellMeEmote, "cmu-yautja-audio-panel-emote-tellme", VoiceSynthCategory),
        new("doitrookie", VoiceSynthDoItRookieEmote, "cmu-yautja-audio-panel-emote-doitrookie", VoiceSynthCategory),
        new("forwardmarine", VoiceSynthForwardMarineEmote, "cmu-yautja-audio-panel-emote-forwardmarine", VoiceSynthCategory),
        new("burnyoufucker", VoiceSynthBurnYouFuckerEmote, "cmu-yautja-audio-panel-emote-burnyoufucker", VoiceSynthCategory),
        new("aliengrowl", FakeAlienGrowlEmote, "cmu-yautja-audio-panel-emote-aliengrowl", FakeAudioCategory),
        new("alienhelp", FakeAlienHelpEmote, "cmu-yautja-audio-panel-emote-alienhelp", FakeAudioCategory),
        new("malescream", FakeMaleScreamEmote, "cmu-yautja-audio-panel-emote-malescream", FakeAudioCategory),
        new("femalescream", FakeFemaleScreamEmote, "cmu-yautja-audio-panel-emote-femalescream", FakeAudioCategory),
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, YautjaAudioPanelActionEvent>(OnAudioPanel);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceClickActionEvent>(OnVoiceClick);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceRoarActionEvent>(OnVoiceRoar);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceLaughActionEvent>(OnVoiceLaugh);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceGrowlActionEvent>(OnVoiceGrowl);
        SubscribeLocalEvent<YautjaComponent, YautjaVoicePainActionEvent>(OnVoicePain);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceDistractActionEvent>(OnVoiceDistract);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceDeathCryActionEvent>(OnVoiceDeathCry);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceDeathLaughActionEvent>(OnVoiceDeathLaugh);

        Subs.BuiEvents<YautjaBracerComponent>(YautjaAudioPanelUIKey.Key, subs =>
        {
            subs.Event<YautjaAudioPanelEmoteMsg>(OnAudioPanelEmote);
        });
    }

    public void GrantAudioPanelAction(Entity<YautjaComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.AudioPanelAction, ent.Comp.AudioPanelActionId);
    }

    public void RemoveAudioPanelAction(Entity<YautjaComponent> ent)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.AudioPanelAction);
    }

    public void GrantVoiceActions(Entity<YautjaComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceClickAction, ent.Comp.VoiceClickActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceRoarAction, ent.Comp.VoiceRoarActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceLaughAction, ent.Comp.VoiceLaughActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceGrowlAction, ent.Comp.VoiceGrowlActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoicePainAction, ent.Comp.VoicePainActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceDistractAction, ent.Comp.VoiceDistractActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceDeathCryAction, ent.Comp.VoiceDeathCryActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceDeathLaughAction, ent.Comp.VoiceDeathLaughActionId);
    }

    public void RemoveVoiceActions(Entity<YautjaComponent> ent)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceClickAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceRoarAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceLaughAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceGrowlAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoicePainAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceDistractAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceDeathCryAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceDeathLaughAction);
    }

    private void OnVoiceClick(Entity<YautjaComponent> ent, ref YautjaVoiceClickActionEvent args)
    {
        PlayVoice(ent, args, ClickEmote);
    }

    private void OnVoiceRoar(Entity<YautjaComponent> ent, ref YautjaVoiceRoarActionEvent args)
    {
        PlayVoice(ent, args, RoarEmote);
    }

    private void OnVoiceLaugh(Entity<YautjaComponent> ent, ref YautjaVoiceLaughActionEvent args)
    {
        PlayVoice(ent, args, LaughEmote);
    }

    private void OnVoiceGrowl(Entity<YautjaComponent> ent, ref YautjaVoiceGrowlActionEvent args)
    {
        PlayVoice(ent, args, GrowlEmote);
    }

    private void OnVoicePain(Entity<YautjaComponent> ent, ref YautjaVoicePainActionEvent args)
    {
        PlayVoice(ent, args, PainEmote);
    }

    private void OnVoiceDistract(Entity<YautjaComponent> ent, ref YautjaVoiceDistractActionEvent args)
    {
        PlayVoice(ent, args, DistractEmote);
    }

    private void OnVoiceDeathCry(Entity<YautjaComponent> ent, ref YautjaVoiceDeathCryActionEvent args)
    {
        PlayVoice(ent, args, DeathCryEmote);
    }

    private void OnVoiceDeathLaugh(Entity<YautjaComponent> ent, ref YautjaVoiceDeathLaughActionEvent args)
    {
        PlayVoice(ent, args, DeathLaughEmote);
    }

    private void OnAudioPanel(Entity<YautjaComponent> ent, ref YautjaAudioPanelActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;
        if (!_power.TryGetWornBracer(ent.Owner, out var bracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-required"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        _ui.TryOpenUi(bracer.Owner, YautjaAudioPanelUIKey.Key, ent.Owner);
        UpdateAudioPanelUi(bracer, ent);
    }

    private void OnAudioPanelEmote(Entity<YautjaBracerComponent> bracer, ref YautjaAudioPanelEmoteMsg args)
    {
        var actor = args.Actor;
        if (!_power.TryGetWornBracer(actor, out var worn) ||
            worn.Owner != bracer.Owner ||
            !TryComp<YautjaComponent>(actor, out var yautja))
        {
            return;
        }

        var emoteId = args.EmoteId;
        var emote = AudioPanelEmotes.FirstOrDefault(e => e.Id == emoteId);
        if (emote == default)
            return;

        var time = _timing.CurTime;
        if (time < yautja.NextAudioPanelEmote)
        {
            UpdateAudioPanelUi(bracer, (actor, yautja));
            return;
        }

        yautja.NextAudioPanelEmote = time + yautja.AudioPanelCooldown;
        _emote.TryEmoteWithChat(actor, emote.Emote, forceEmote: true, cooldown: yautja.AudioPanelCooldown);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(actor):player} used Yautja audio panel emote {emote.Emote.Id}");
        UpdateAudioPanelUi(bracer, (actor, yautja));
    }

    private void UpdateAudioPanelUi(Entity<YautjaBracerComponent> bracer, Entity<YautjaComponent> yautja)
    {
        var remaining = TimeSpan.Zero;
        if (_timing.CurTime < yautja.Comp.NextAudioPanelEmote)
            remaining = yautja.Comp.NextAudioPanelEmote - _timing.CurTime;

        var entries = new List<YautjaAudioPanelEntry>(AudioPanelEmotes.Count);
        foreach (var emote in AudioPanelEmotes)
            entries.Add(new YautjaAudioPanelEntry(emote.Id, Loc.GetString(emote.Name), Loc.GetString(emote.Category)));

        _ui.SetUiState(bracer.Owner, YautjaAudioPanelUIKey.Key, new YautjaAudioPanelState(entries, remaining));
    }

    private void PlayVoice(Entity<YautjaComponent> ent, InstantActionEvent args, ProtoId<EmotePrototype> emote)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        _emote.TryEmoteWithChat(ent.Owner, emote, forceEmote: true);
        args.Handled = true;
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent.Owner):player} used Yautja voice {emote.Id}");
    }

    private readonly record struct AudioPanelEmote(
        string Id,
        ProtoId<EmotePrototype> Emote,
        string Name,
        string Category);
}
