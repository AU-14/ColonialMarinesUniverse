using Content.Server._RMC14.Telephone;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Radio;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRangeSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private ANPRCGarbleSystem _garble = default!;

    public const float FullSignalRange = 30f;
    public const float PartialSignalRange = 45f;

    private const float StationaryVelocityThresholdSquared = 0.01f;

    public override void Initialize()
    {
        SubscribeLocalEvent<RadioSendAttemptEvent>(
            OnSendAttempt,
            after: [typeof(RMCTelephoneSystem), typeof(JammerSystem)]);

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(
            OnReceiveAttempt,
            after: [typeof(RMCTelephoneSystem)]);
    }

    private void OnSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (TryComp(args.RadioSource, out RMCRadioFilterComponent? sourceFilter) &&
            sourceFilter.DisabledChannels.Contains(args.Channel.ID))
        {
            return;
        }

        if (HasComp<ANPRCRadioComponent>(args.RadioSource))
        {
            args.Cancelled = false;
            return;
        }

        if (HasComp<TelecomExemptComponent>(args.RadioSource))
            return;

        var tier = GetRangeTier(args.RadioSource, args.Channel.ID, out var hasAnchor);

        if (!hasAnchor)
            return;

        switch (tier)
        {
            case ANPRCRangeTier.OutOfRange:
            {
                args.Cancelled = true;

                var wearer = Transform(args.RadioSource).ParentUid;

                if (wearer.IsValid())
                {
                    _cmChat.ChatMessageToOne(
                        Loc.GetString("anprc-out-of-range", ("channel", args.Channel.LocalizedName)),
                        wearer);
                }

                return;
            }

            case ANPRCRangeTier.Partial:
            case ANPRCRangeTier.Full:
            {
                if (_garble.GetJamIntensity(args.RadioSource) == RadioJamIntensity.None)
                    args.Cancelled = false;

                var sendRange = EnsureComp<ANPRCInRangeComponent>(args.RadioSource);
                sendRange.IsPartial = tier == ANPRCRangeTier.Partial;

                RemCompDeferred<ANPRCInRangeComponent>(args.RadioSource);
                return;
            }
        }
    }

    private void OnReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (TryComp(args.RadioReceiver, out RMCRadioFilterComponent? receiverFilter) &&
            receiverFilter.DisabledChannels.Contains(args.Channel.ID))
        {
            return;
        }

        if (HasComp<ANPRCRadioComponent>(args.RadioSource))
        {
            args.Cancelled = false;
            return;
        }

        var tier = GetRangeTier(args.RadioReceiver, args.Channel.ID, out var hasAnchor);

        if (!hasAnchor)
            return;

        switch (tier)
        {
            case ANPRCRangeTier.OutOfRange:
                args.Cancelled = true;
                return;

            case ANPRCRangeTier.Partial:
            case ANPRCRangeTier.Full:
                args.Cancelled = false;

                var receiveRange = EnsureComp<ANPRCInRangeComponent>(args.RadioReceiver);
                receiveRange.IsPartial = tier == ANPRCRangeTier.Partial;

                RemCompDeferred<ANPRCInRangeComponent>(args.RadioReceiver);
                return;
        }
    }

    private ANPRCRangeTier GetRangeTier(EntityUid entity, string channelId, out bool anyAnchorFound)
    {
        var entityPos = _transform.GetWorldPosition(entity);
        var entityMap = Transform(entity).MapID;
        var channel = new ProtoId<RadioChannelPrototype>(channelId);

        anyAnchorFound = false;

        var bestTier = ANPRCRangeTier.OutOfRange;
        var query = EntityQueryEnumerator<ANPRCRelayAnchorComponent, TransformComponent>();

        while (query.MoveNext(out var anchorUid, out var anchor, out var anchorXform))
        {
            if (anchorXform.MapID != entityMap)
                continue;

            if (!anchor.Channels.Contains(channel))
                continue;

            anyAnchorFound = true;

            var anchorPos = _transform.GetWorldPosition(anchorXform);
            var distance = (entityPos - anchorPos).Length();
            var (fullRange, partialRange) = GetEffectiveRange(anchorUid, anchor);

            ANPRCRangeTier tier;

            if (distance <= fullRange)
                tier = ANPRCRangeTier.Full;
            else if (distance <= partialRange)
                tier = ANPRCRangeTier.Partial;
            else
                tier = ANPRCRangeTier.OutOfRange;

            if (tier > bestTier)
                bestTier = tier;
        }

        return anyAnchorFound
            ? bestTier
            : ANPRCRangeTier.OutOfRange;
    }

    public (float FullRange, float PartialRange) GetAnchorRanges(EntityUid radio)
    {
        return TryComp(radio, out ANPRCRelayAnchorComponent? anchor)
            ? GetEffectiveRange(radio, anchor)
            : (FullSignalRange, PartialSignalRange);
    }

    private (float FullRange, float PartialRange) GetEffectiveRange(
        EntityUid anchorUid,
        ANPRCRelayAnchorComponent anchor)
    {
        if (!anchor.RequiresStationary || anchor.Planted)
            return (anchor.FullRange, anchor.PartialRange);

        var wearer = Transform(anchorUid).ParentUid;

        if (wearer.IsValid() &&
            TryComp(wearer, out PhysicsComponent? physics) &&
            physics.LinearVelocity.LengthSquared() > StationaryVelocityThresholdSquared)
        {
            var movingMultiplier = anchor.RangeMultiplier * anchor.MovingRangeMultiplier;

            return (
                FullSignalRange * movingMultiplier,
                PartialSignalRange * movingMultiplier);
        }

        return (anchor.FullRange, anchor.PartialRange);
    }
}

public enum ANPRCRangeTier : byte
{
    OutOfRange = 0,
    Partial = 1,
    Full = 2
}
