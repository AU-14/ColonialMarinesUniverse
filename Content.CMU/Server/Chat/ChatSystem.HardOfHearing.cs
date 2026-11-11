using Content.Shared.Chat;
using Content.Shared.CMU14.Traits.Conditions;

namespace Content.Server.Chat.Systems;

/// <summary>
/// Someone hard of hearing loses more of what's said the further away it is.
/// </summary>
public sealed partial class ChatSystem
{
    private string CMUApplyHearingLoss(EntityUid listener, float range, string message)
    {
        if (range < 0 || !TryComp(listener, out CMUHardOfHearingComponent? hearing) || range <= hearing.ClearRange)
            return message;

        var far = Math.Max(SharedChatSystem.VoiceRange - hearing.ClearRange, 1f);
        var lost = Math.Clamp((range - hearing.ClearRange) / far, 0f, 1f);
        var clarity = 1f - lost * (1f - hearing.FarClarity);
        return ObfuscateMessageReadability(message, clarity);
    }
}
