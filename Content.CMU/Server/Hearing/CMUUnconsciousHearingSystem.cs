using System.Text;
using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Chat;
using Content.Shared.CMU14.Hearing;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.CMU14.Hearing;

/// <summary>
///     Being unconscious no longer makes someone deaf. Speech from within
///     <see cref="CMUMuffledHearingComponent.Range"/> tiles comes through as a few scattered words,
///     anything further is lost, and radio is not heard at all. The client makes every sound quiet
///     and heavily echoed while they are out.
/// </summary>
public sealed class CMUUnconsciousHearingSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.2);
    private const string LegacyUnconsciousKey = "Unconscious";

    private static readonly EntProtoId[] UnconsciousEffects =
    [
        SharedStunSystem.RMCUnconsciousId,
        "StatusEffectCMUUnconscious",
        "StatusEffectCMUAnesthesia",
    ];

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NewStatusEffectsSystem _newStatus = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var players = EntityQueryEnumerator<ActorComponent>();
        while (players.MoveNext(out var uid, out _))
        {
            var unconscious = IsUnconscious(uid);
            var muffled = HasComp<CMUMuffledHearingComponent>(uid);
            if (unconscious && !muffled)
                AddComp<CMUMuffledHearingComponent>(uid);
            else if (!unconscious && muffled)
                RemComp<CMUMuffledHearingComponent>(uid);
        }

        var stale = EntityQueryEnumerator<CMUMuffledHearingComponent>();
        while (stale.MoveNext(out var uid, out _))
        {
            if (!HasComp<ActorComponent>(uid))
                RemComp<CMUMuffledHearingComponent>(uid);
        }
    }

    public bool IsUnconscious(EntityUid uid)
    {
        if (_mobState.IsCritical(uid))
            return true;

        // A parasite over the face hears the same as being out cold.
        if (TryComp(uid, out InfectableComponent? infectable) && infectable.BeingInfected)
            return true;

        if (_status.HasStatusEffect(uid, LegacyUnconsciousKey))
            return true;

        foreach (var effect in UnconsciousEffects)
        {
            if (_newStatus.HasStatusEffect(uid, effect))
                return true;
        }

        return false;
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (HasComp<CMUMuffledHearingComponent>(Transform(args.RadioReceiver).ParentUid))
            args.Cancelled = true;
    }

    /// <summary>
    ///     Called for every chat message on its way to one player. Returns false when an unconscious
    ///     listener should not get it at all; otherwise the message may have been cut down to the
    ///     few words they caught.
    /// </summary>
    public bool FilterForListener(ChatChannel channel, EntityUid source, INetChannel client, ref string message, ref string wrappedMessage)
    {
        if (channel is not (ChatChannel.Local or ChatChannel.Whisper))
            return true;

        if (!_player.TryGetSessionByChannel(client, out var session) ||
            session.AttachedEntity is not { } listener ||
            listener == source ||
            !HasComp<CMUMuffledHearingComponent>(listener))
        {
            return true;
        }

        // Proper deafness has its own handling.
        if (HasComp<DeafComponent>(listener))
            return true;

        if (!Exists(source))
            return false;

        var sourcePos = _transform.GetMapCoordinates(source);
        var listenerPos = _transform.GetMapCoordinates(listener);
        if (sourcePos.MapId != listenerPos.MapId ||
            (sourcePos.Position - listenerPos.Position).Length() > CMUMuffledHearingComponent.Range)
        {
            return false;
        }

        var fragment = Fragment(FormattedMessage.RemoveMarkupPermissive(message));
        if (fragment.Length == 0)
        {
            message = "...";
            wrappedMessage = Loc.GetString("cmu-unconscious-hear-murmur");
            return true;
        }

        message = fragment;
        wrappedMessage = Loc.GetString("cmu-unconscious-hear", ("message", FormattedMessage.EscapeText(fragment)));
        return true;
    }

    /// <summary>
    ///     Keeps a few of the words and turns the gaps between them into trailing dots.
    /// </summary>
    private string Fragment(string text)
    {
        var builder = new StringBuilder();
        var gap = false;
        foreach (var raw in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw.Trim(',', '.', '!', '?', ';', ':', '"');
            if (word.Length == 0 || !_random.Prob(CMUMuffledHearingComponent.WordChance))
            {
                gap = true;
                continue;
            }

            if (builder.Length > 0)
                builder.Append(gap ? "... " : " ");

            builder.Append(word);
            gap = false;
        }

        return builder.ToString();
    }
}
