using Content.Server._RMC14.Telephone;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared._AU14.CCVar;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._AU14.Radio;
using Content.Shared.Ghost;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Radio;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCRangeSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private ANPRCGarbleSystem _garble = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public const float FullSignalRange = 30f;
    public const float PartialSignalRange = 45f;

    private const float StationaryVelocityThresholdSquared = 0.01f;

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, v => _commsEnabled = v, true);

        SubscribeLocalEvent<RadioSendAttemptEvent>(
            OnSendAttempt,
            after: [typeof(RMCTelephoneSystem), typeof(JammerSystem)]);

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(
            OnReceiveAttempt,
            after: [typeof(RMCTelephoneSystem)]);
    }

    private void OnSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (!_commsEnabled)
            return;

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

        // only combat nets are relay-gated, support and civilian channels keep stock
        // behavior. gated nets with no live anchor on the map are dead air, not unlimited
        if (!args.Channel.AnchorGated)
            return;

        var tier = GetRangeTier(args.RadioSource, args.Channel.ID, out var quality);

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
                sendRange.Quality = quality;

                RemCompDeferred<ANPRCInRangeComponent>(args.RadioSource);
                return;
            }
        }
    }

    private void OnReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (!_commsEnabled)
            return;

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

        if (!args.Channel.AnchorGated)
            return;

        // observers hear everything regardless of coverage. the telephone system may
        // already have cancelled this receive for want of a comms tower, so un-cancel
        // instead of just yielding or ghosts go deaf under array/manpack-only coverage
        if (HasComp<GhostHearingComponent>(args.RadioReceiver))
        {
            args.Cancelled = false;
            return;
        }

        var tier = GetRangeTier(args.RadioReceiver, args.Channel.ID, out var quality);

        switch (tier)
        {
            case ANPRCRangeTier.OutOfRange:
                args.Cancelled = true;
                return;

            case ANPRCRangeTier.Partial:
            case ANPRCRangeTier.Full:
                args.Cancelled = false;

                var receiveRange = EnsureComp<ANPRCInRangeComponent>(args.RadioReceiver);
                receiveRange.Quality = quality;

                RemCompDeferred<ANPRCInRangeComponent>(args.RadioReceiver);
                return;
        }
    }

    private ANPRCRangeTier GetRangeTier(EntityUid entity, string channelId, out float quality)
    {
        var entityPos = _transform.GetWorldPosition(entity);
        var entityMap = Transform(entity).MapID;
        var channel = new ProtoId<RadioChannelPrototype>(channelId);

        var bestTier = ANPRCRangeTier.OutOfRange;
        var bestQuality = 0f;
        var query = EntityQueryEnumerator<ANPRCRelayAnchorComponent, TransformComponent>();

        while (query.MoveNext(out var anchorUid, out var anchor, out var anchorXform))
        {
            if (!InVerticalReach(anchorXform.MapID, entityMap, anchor.LevelReach))
                continue;

            if (!anchor.Channels.Contains(channel))
                continue;

            // fixed arrays only anchor while powered
            if (TryComp(anchorUid, out ApcPowerReceiverComponent? power) && !power.Powered)
                continue;

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

            var linkQuality = GetLinkQuality(distance, fullRange, partialRange);

            if (linkQuality > bestQuality)
                bestQuality = linkQuality;
        }

        quality = bestQuality;
        return bestTier;
    }

    // continuous falloff instead of a clean/degraded coin flip: 1 on top of the
    // anchor sliding to 0.5 at the full-range edge, then 0.5 down to 0 across the
    // partial band. the garble system maps this onto hiss tiers
    private static float GetLinkQuality(float distance, float fullRange, float partialRange)
    {
        if (distance <= fullRange)
            return 1f - 0.5f * (distance / Math.Max(fullRange, 0.001f));

        if (distance <= partialRange)
            return 0.5f * (1f - (distance - fullRange) / Math.Max(partialRange - fullRange, 0.001f));

        return 0f;
    }

    // stacked z-level maps share the same 2D coordinate space, so anchors can cover
    // nearby levels of their network with the usual distance math. reach 0 = own level
    // only, -1 = the whole network
    public bool InVerticalReach(MapId a, MapId b, int levelReach)
    {
        if (a == b)
            return true;

        if (levelReach == 0)
            return false;

        if (!_map.TryGetMap(a, out var mapA) || mapA is not { } mapAUid ||
            !_map.TryGetMap(b, out var mapB) || mapB is not { } mapBUid)
        {
            return false;
        }

        if (!TryComp(mapAUid, out CMUZLevelMapComponent? zA) ||
            !TryComp(mapBUid, out CMUZLevelMapComponent? zB) ||
            !zA.NetworkUid.IsValid() ||
            zA.NetworkUid != zB.NetworkUid)
        {
            return false;
        }

        return levelReach < 0 || Math.Abs(zA.Depth - zB.Depth) <= levelReach;
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
