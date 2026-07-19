using System.Numerics;
using System.Text;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Radio;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCGarbleSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ANPRCCryptoSystem _crypto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _config = default!;

    private static readonly string[] LightNoise = ["~~~~", "~~~~", "~~~~", "---"];
    private static readonly string[] MediumNoise = ["~~~~", "*static*", "kzzkt", "---", "~~~~"];
    private static readonly string[] HeavyNoise = ["*static*", "kzzkt", "fzzzt", "~~~~", "krrkk", "----", "*static*"];

    private GameTick _jamCacheTick;
    private readonly Dictionary<EntityUid, RadioJamIntensity> _jamCache = new();

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, v => _commsEnabled = v, true);

        SubscribeLocalEvent<TunableHeadsetComponent, RadioReceiveEvent>(
            OnRadioReceive,
            before: [typeof(HeadsetSystem)]);
    }

    private void OnRadioReceive(Entity<TunableHeadsetComponent> receiver, ref RadioReceiveEvent args)
    {
        if (!_commsEnabled)
            return;

        RadioMode? txMode = null;

        if (TryComp(args.MessageSource, out WearingANPRCComponent? transmitterWearing) &&
            TryComp(transmitterWearing.Radio, out ANPRCRadioComponent? txRadio))
        {
            txMode = txRadio.Mode;
        }

        var receiverAnprc = GetReceiverAnprc(receiver.Owner);
        ANPRCRadioComponent? receiverRadio = null;

        if (receiverAnprc != null)
            TryComp(receiverAnprc.Value, out receiverRadio);

        // the link is only as good as its worst end
        var rangeQuality = 1f;

        if (TryComp(receiver.Owner, out ANPRCInRangeComponent? rxRange))
            rangeQuality = Math.Min(rangeQuality, rxRange.Quality);

        if (TryComp(args.RadioSource, out ANPRCInRangeComponent? txRange))
            rangeQuality = Math.Min(rangeQuality, txRange.Quality);

        // clean near the anchor, hiss over the outer full band and inner partial
        // band, broken fragments at the partial fringe
        var rangeDegradation = rangeQuality >= 0.65f
            ? RadioJamIntensity.None
            : rangeQuality >= 0.25f
                ? RadioJamIntensity.Light
                : RadioJamIntensity.Medium;

        var rxJam = GetJamIntensity(receiver.Owner);
        var txJam = GetJamIntensity(args.RadioSource);
        var rawJam = rxJam > txJam ? rxJam : txJam;
        var jamIntensity = ApplyModeToJam(rawJam, txMode);

        var needsCryptoGarble = false;

        if (txMode != RadioMode.PlainText &&
            !string.IsNullOrEmpty(args.Channel.Faction) &&
            transmitterWearing != null &&
            _crypto.HasMatchingCrypto(transmitterWearing.Radio, args.Channel))
        {
            if (receiverAnprc != null && !_crypto.HasMatchingCrypto(receiverAnprc.Value, args.Channel))
                needsCryptoGarble = true;

            if (txMode == RadioMode.CipherText && receiverAnprc == null)
                needsCryptoGarble = true;
        }

        var effectiveIntensity = needsCryptoGarble
            ? RadioJamIntensity.Heavy
            : rangeDegradation > jamIntensity
                ? rangeDegradation
                : jamIntensity;

        var original = args.Message;
        string garbled;

        if (needsCryptoGarble)
        {
            garbled = FullStatic(args.Message);
        }
        else if (effectiveIntensity != RadioJamIntensity.None)
        {
            garbled = GarbleMessage(args.Message, effectiveIntensity);
        }
        else
        {
            garbled = args.Message;
        }

        var suppressed = receiverRadio != null &&
                         IsSquelched(receiverRadio.SquelchLevel, needsCryptoGarble, jamIntensity, rangeDegradation);

        if (suppressed)
            garbled = Loc.GetString("anprc-squelch-suppressed");

        if (garbled == original && !suppressed)
            return;

        var garbledChat = RebuildChatMessage(args.ChatMsg.Message, garbled);
        args = args with
        {
            Message = garbled,
            ChatMsg = new MsgChatMessage { Message = garbledChat }
        };
    }

    // jamming mutes at lower settings than plain distance: the default setting
    // drops jammed traffic but keeps fringe stations audible until cranked up
    private static bool IsSquelched(int squelch, bool comsecGarble, RadioJamIntensity jam, RadioJamIntensity rangeDegradation)
    {
        if (squelch <= 0)
            return false;

        if (comsecGarble || jam == RadioJamIntensity.Heavy)
            return squelch >= 1;

        if (jam == RadioJamIntensity.Medium)
            return squelch >= 2;

        if (jam == RadioJamIntensity.Light)
            return squelch >= 3;

        if (rangeDegradation >= RadioJamIntensity.Medium)
            return squelch >= 3;

        if (rangeDegradation == RadioJamIntensity.Light)
            return squelch >= 4;

        return false;
    }

    private RadioJamIntensity ApplyModeToJam(RadioJamIntensity raw, RadioMode? mode)
    {
        if (raw == RadioJamIntensity.None || mode == null)
            return raw;

        return mode switch
        {
            RadioMode.FrequencyHopping => raw switch
            {
                RadioJamIntensity.Heavy => RadioJamIntensity.Medium,
                RadioJamIntensity.Medium => RadioJamIntensity.Light,
                RadioJamIntensity.Light => RadioJamIntensity.None,
                _ => RadioJamIntensity.None
            },

            RadioMode.PlainText => raw switch
            {
                RadioJamIntensity.Light => RadioJamIntensity.Medium,
                RadioJamIntensity.Medium => RadioJamIntensity.Heavy,
                RadioJamIntensity.Heavy => RadioJamIntensity.Heavy,
                _ => RadioJamIntensity.None
            },

            _ => raw
        };
    }

    public RadioJamIntensity GetJamIntensity(EntityUid radioSource)
    {
        if (_timing.CurTick != _jamCacheTick)
        {
            _jamCache.Clear();
            _jamCacheTick = _timing.CurTick;
        }

        if (_jamCache.TryGetValue(radioSource, out var cached))
            return cached;

        var intensity = ComputeJamIntensity(radioSource);
        _jamCache[radioSource] = intensity;

        return intensity;
    }

    private RadioJamIntensity ComputeJamIntensity(EntityUid radioSource)
    {
        if (FindClosestCoveringJammer(radioSource) is not { } jammer)
            return RadioJamIntensity.None;

        var normalised = jammer.Distance / jammer.Range;

        if (normalised < 0.33f)
            return RadioJamIntensity.Heavy;

        if (normalised < 0.66f)
            return RadioJamIntensity.Medium;

        return RadioJamIntensity.Light;
    }

    public bool TryGetNearestJammerDirection(EntityUid source, out Direction direction)
    {
        direction = Direction.North;

        if (FindClosestCoveringJammer(source) is not { } jammer)
            return false;

        var offset = jammer.Position - _transform.GetWorldPosition(source);

        if (offset.LengthSquared() < 0.01f)
            return false;

        direction = offset.ToWorldAngle().GetDir();
        return true;
    }

    private (float Distance, float Range, Vector2 Position)? FindClosestCoveringJammer(EntityUid radioSource)
    {
        var closestDist = float.MaxValue;
        var closestRange = 1f;
        var closestPos = Vector2.Zero;

        var sourcePos = _transform.GetWorldPosition(radioSource);
        var sourceMap = Transform(radioSource).MapID;

        var query = EntityQueryEnumerator<ActiveRadioJammerComponent, RadioJammerComponent, TransformComponent>();

        while (query.MoveNext(out _, out _, out var jam, out var xform))
        {
            if (xform.MapID != sourceMap)
                continue;

            if (jam.Settings.Length == 0)
                continue;

            var index = Math.Clamp(jam.SelectedPowerLevel, 0, jam.Settings.Length - 1);
            var range = jam.Settings[index].Range;
            var jamPos = _transform.GetWorldPosition(xform);
            var dist = (sourcePos - jamPos).Length();

            if (dist > range || dist >= closestDist)
                continue;

            closestDist = dist;
            closestRange = range;
            closestPos = jamPos;
        }

        if (closestDist == float.MaxValue)
            return null;

        return (closestDist, closestRange, closestPos);
    }

    public string GarbleMessage(string message, RadioJamIntensity intensity)
    {
        if (string.IsNullOrWhiteSpace(message) || intensity == RadioJamIntensity.None)
            return message;

        var words = message.Split(' ');

        var corruptChance = intensity switch
        {
            RadioJamIntensity.Light => 0.30f,
            RadioJamIntensity.Medium => 0.55f,
            RadioJamIntensity.Heavy => 0.80f,
            _ => 0f
        };

        var pool = intensity switch
        {
            RadioJamIntensity.Light => LightNoise,
            RadioJamIntensity.Medium => MediumNoise,
            RadioJamIntensity.Heavy => HeavyNoise,
            _ => LightNoise
        };

        var corrupted = 0;
        var result = new StringBuilder();

        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                result.Append(' ');

            if (_random.NextFloat() < corruptChance)
            {
                result.Append(_random.Pick(pool));
                corrupted++;
            }
            else
            {
                result.Append(words[i]);
            }
        }

        if (corrupted == 0)
        {
            var index = _random.Next(words.Length);
            words[index] = _random.Pick(pool);

            return string.Join(' ', words);
        }

        return result.ToString();
    }

    public string ApplyComsecGarble(
        EntityUid messageSource,
        EntityUid receiverAnprc,
        RadioChannelPrototype channel,
        string message)
    {
        if (string.IsNullOrEmpty(channel.Faction))
            return message;

        if (_crypto.HasMatchingCrypto(receiverAnprc, channel))
            return message;

        if (TryComp(messageSource, out WearingANPRCComponent? txWearing) &&
            TryComp(txWearing.Radio, out ANPRCRadioComponent? txRadio))
        {
            if (txRadio.Mode == RadioMode.PlainText)
                return message;

            if (_crypto.HasMatchingCrypto(txWearing.Radio, channel))
                return FullStatic(message);

            return txRadio.Mode == RadioMode.FrequencyHopping
                ? GarbleMessage(message, RadioJamIntensity.Light)
                : message;
        }

        return FullStatic(message);
    }

    public string FullStatic(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var words = message.Split(' ');
        var result = new StringBuilder();

        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                result.Append(' ');

            result.Append(_random.Pick(HeavyNoise));
        }

        return result.ToString();
    }

    private EntityUid? GetReceiverAnprc(EntityUid headset)
    {
        var wearer = Transform(headset).ParentUid;

        if (!wearer.IsValid())
            return null;

        return TryComp(wearer, out WearingANPRCComponent? wearing)
            ? wearing.Radio
            : null;
    }

    private ChatMessage RebuildChatMessage(ChatMessage original, string garbledBody)
    {
        var newWrapped = ReplaceMessageBody(original.WrappedMessage, original.Message, garbledBody);

        return new ChatMessage(
            original.Channel,
            garbledBody,
            newWrapped,
            original.SenderEntity,
            original.SenderKey,
            repeatCheckSender: false,
            display: original.Display);
    }

    private static string ReplaceMessageBody(string wrapped, string original, string replacement)
    {
        var escaped = FormattedMessage.EscapeText(original);
        var index = wrapped.LastIndexOf(escaped, StringComparison.Ordinal);

        if (index < 0)
            return wrapped;

        return string.Concat(
            wrapped.AsSpan(0, index),
            FormattedMessage.EscapeText(replacement),
            wrapped.AsSpan(index + escaped.Length));
    }
}
