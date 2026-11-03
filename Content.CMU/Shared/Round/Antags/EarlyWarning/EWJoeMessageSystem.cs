using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>A numbered set of locale lines, <c>{Prefix}-0</c> to <c>{Prefix}-{Count - 1}</c>.</summary>
public readonly record struct EWJoeLines(string Prefix, int Count);

/// <summary>
/// The Early Warning Joe's voice in other people's heads, and its own log. Server side only (every
/// method is a no-op on the client), so it can be called from predicted shared code after an
/// <c>IsServer</c> check or from server systems directly.
/// <list type="bullet">
/// <item><see cref="Sense"/>: what a victim feels, as a popup over them.</item>
/// <item><see cref="Whisper"/>: the signal speaking to a victim, in their chat as a whisper.</item>
/// <item><see cref="Notify"/>: progression and system notices for the Joe, in its chat log.</item>
/// </list>
/// </summary>
public sealed partial class EWJoeMessageSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCMChatSystem _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public static readonly Color VoiceColor = Color.FromHex("#ff7a4a");
    public static readonly Color SignalColor = Color.FromHex("#ff9a3c");

    // Test of Faith, on the victim
    public static readonly EWJoeLines FaithJolt = new("cmu-ew-joe-tof-jolt", 2);
    public static readonly EWJoeLines FaithFumble = new("cmu-ew-joe-tof-fumble", 2);
    public static readonly EWJoeLines FaithEyes = new("cmu-ew-joe-tof-eyes", 3);

    // Judgement, on the victim
    public static readonly EWJoeLines JudgementSenses = new("cmu-ew-joe-judgement-senses", 4);
    public static readonly EWJoeLines JudgementFumble = new("cmu-ew-joe-judgement-fumble", 1);
    public static readonly EWJoeLines JudgementGun = new("cmu-ew-joe-judgement-gun", 1);
    public static readonly EWJoeLines JudgementVoice = new("cmu-ew-joe-judgement-voice", 4);

    // Drown, on the victim
    public static readonly EWJoeLines DrownAirway = new("cmu-ew-joe-drown-airway", 3);
    public static readonly EWJoeLines DrownVoice = new("cmu-ew-joe-drown-voice", 2);

    // THE LIGHT, on the victim, by stage
    public static readonly EWJoeLines LightHear = new("cmu-ew-joe-light-hear", 3);
    public static readonly EWJoeLines LightWonder = new("cmu-ew-joe-light-wonder", 3);
    public static readonly EWJoeLines LightOverwhelm = new("cmu-ew-joe-light-overwhelm", 3);
    public static readonly EWJoeLines LightVoice = new("cmu-ew-joe-light-voice", 6);

    /// <summary>A random line from <paramref name="lines"/>.</summary>
    public string Pick(EWJoeLines lines)
    {
        return Loc.GetString($"{lines.Prefix}-{_random.Next(lines.Count)}");
    }

    /// <summary>A sensation: a popup over the victim that only the victim sees.</summary>
    public void Sense(EntityUid victim, string text, PopupType type = PopupType.LargeCaution)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(text, victim, victim, type);
    }

    /// <summary>The signal speaking: an italic line in the victim's chat, nobody else's.</summary>
    public void Whisper(EntityUid victim, string text)
    {
        if (!_net.IsServer || !TryComp(victim, out ActorComponent? actor))
            return;

        var wrapped = $"[italic]{FormattedMessage.EscapeText(text)}[/italic]";
        _chat.ChatMessageToOne(ChatChannel.Local, text, wrapped, default, false, actor.PlayerSession.Channel, VoiceColor);
    }

    /// <summary>
    /// A progression or system notice for the Joe (level, objectives, buffs, the river): it goes to the
    /// Joe's chat log so it can be read back, not a popup that fades.
    /// </summary>
    public void Notify(EntityUid joe, string text)
    {
        if (!_net.IsServer || !TryComp(joe, out ActorComponent? actor))
            return;

        var wrapped = $"[bold][color=#ff9a3c]{Loc.GetString("cmu-ew-joe-notice-prefix")}[/color][/bold] {FormattedMessage.EscapeText(text)}";
        _chat.ChatMessageToOne(ChatChannel.Notifications, text, wrapped, default, false, actor.PlayerSession.Channel, SignalColor);
    }
}
