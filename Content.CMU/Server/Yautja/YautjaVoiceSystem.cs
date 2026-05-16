using Content.Server.Administration.Logs;
using Content.Server._RMC14.Emote;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Database;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaVoiceSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private RMCEmoteSystem _emote = default!;

    private static readonly ProtoId<EmotePrototype> ClickEmote = "CMUYautjaClick";
    private static readonly ProtoId<EmotePrototype> RoarEmote = "CMUYautjaRoar";
    private static readonly ProtoId<EmotePrototype> LaughEmote = "CMUYautjaLaugh";
    private static readonly ProtoId<EmotePrototype> GrowlEmote = "CMUYautjaGrowl";
    private static readonly ProtoId<EmotePrototype> PainEmote = "CMUYautjaPain";
    private static readonly ProtoId<EmotePrototype> DeathCryEmote = "CMUYautjaDeathCry";
    private static readonly ProtoId<EmotePrototype> DeathLaughEmote = "CMUYautjaDeathLaugh";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceClickActionEvent>(OnVoiceClick);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceRoarActionEvent>(OnVoiceRoar);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceLaughActionEvent>(OnVoiceLaugh);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceGrowlActionEvent>(OnVoiceGrowl);
        SubscribeLocalEvent<YautjaComponent, YautjaVoicePainActionEvent>(OnVoicePain);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceDeathCryActionEvent>(OnVoiceDeathCry);
        SubscribeLocalEvent<YautjaComponent, YautjaVoiceDeathLaughActionEvent>(OnVoiceDeathLaugh);
    }

    public void GrantVoiceActions(Entity<YautjaComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceClickAction, ent.Comp.VoiceClickActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceRoarAction, ent.Comp.VoiceRoarActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceLaughAction, ent.Comp.VoiceLaughActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceGrowlAction, ent.Comp.VoiceGrowlActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoicePainAction, ent.Comp.VoicePainActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceDeathCryAction, ent.Comp.VoiceDeathCryActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.VoiceDeathLaughAction, ent.Comp.VoiceDeathLaughActionId);
    }

    private void OnShutdown(Entity<YautjaComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceClickAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceRoarAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceLaughAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoiceGrowlAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.VoicePainAction);
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

    private void OnVoiceDeathCry(Entity<YautjaComponent> ent, ref YautjaVoiceDeathCryActionEvent args)
    {
        PlayVoice(ent, args, DeathCryEmote);
    }

    private void OnVoiceDeathLaugh(Entity<YautjaComponent> ent, ref YautjaVoiceDeathLaughActionEvent args)
    {
        PlayVoice(ent, args, DeathLaughEmote);
    }

    private void PlayVoice(Entity<YautjaComponent> ent, InstantActionEvent args, ProtoId<EmotePrototype> emote)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        _emote.TryEmoteWithChat(ent.Owner, emote, forceEmote: true);
        args.Handled = true;
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent.Owner):player} used Yautja voice {emote.Id}");
    }
}
