using System.Collections.Frozen;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;

namespace Content.Shared.Chat;

// CMU14: several channels can share a radio key, e.g. GOVFOR Command and OPFOR Victor on :v.
public abstract partial class SharedChatSystem
{
    /// <summary>
    /// Every channel behind a radio key that more than one channel uses.
    /// </summary>
    private FrozenDictionary<string, RadioChannelPrototype[]> _cmuSharedRadioKeys =
        FrozenDictionary<string, RadioChannelPrototype[]>.Empty;

    private void CMUCacheSharedRadioKeys(Dictionary<string, List<RadioChannelPrototype>> channelsByKey)
    {
        var shared = new Dictionary<string, RadioChannelPrototype[]>();
        foreach (var (key, channels) in channelsByKey)
        {
            if (channels.Count > 1)
                shared[key] = channels.ToArray();
        }

        _cmuSharedRadioKeys = shared.ToFrozenDictionary();
    }

    /// <summary>
    /// Picks the channel behind a shared radio key that the speaker can transmit on: their headset's
    /// channels first, then their built-in transmitter's. Keeps <paramref name="fallback"/> if they have none.
    /// </summary>
    private RadioChannelPrototype CMUResolveSharedRadioKey(
        EntityUid source,
        string lookupKey,
        RadioChannelPrototype fallback)
    {
        EncryptionKeyHolderComponent? keys = null;
        if (TryComp(source, out WearingHeadsetComponent? wearing))
            TryComp(wearing.Headset, out keys);

        // :h is Hivemind's key, so the engine's default-channel branch never runs.
        // A worn headset with a default channel takes it back, anyone else keeps Hivemind.
        if (lookupKey[1] == DefaultChannelKey
            && keys?.DefaultChannel is { } defaultChannel
            && ProtoMan.TryIndex<RadioChannelPrototype>(defaultChannel, out var defaultProto))
            return defaultProto;

        if (!_cmuSharedRadioKeys.TryGetValue(lookupKey, out var candidates))
            return fallback;

        if (keys != null)
        {
            foreach (var candidate in candidates)
            {
                if (keys.Channels.Contains(candidate.ID))
                    return candidate;
            }
        }

        if (TryComp(source, out IntrinsicRadioTransmitterComponent? intrinsic))
        {
            foreach (var candidate in candidates)
            {
                if (intrinsic.Channels.Contains(candidate.ID))
                    return candidate;
            }
        }

        return fallback;
    }
}
