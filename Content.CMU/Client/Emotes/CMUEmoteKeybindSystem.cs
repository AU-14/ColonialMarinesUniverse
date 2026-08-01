using Content.Shared._CMU14.Input;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Emotes;

// Lets a player fire a chosen emote directly from a keybind, instead of going through the
// emotes wheel. Which emote (if any) each of the 8 slots triggers is picked in the keybinds tab
// and stored in CCVars.EmoteSlot1-8.
[UsedImplicitly]
public sealed class CMUEmoteKeybindSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly (BoundKeyFunction Function, CVarDef<string> CVar)[] Slots =
    {
        (CMUKeyFunctions.CMUEmoteSlot1, CCVars.EmoteSlot1),
        (CMUKeyFunctions.CMUEmoteSlot2, CCVars.EmoteSlot2),
        (CMUKeyFunctions.CMUEmoteSlot3, CCVars.EmoteSlot3),
        (CMUKeyFunctions.CMUEmoteSlot4, CCVars.EmoteSlot4),
        (CMUKeyFunctions.CMUEmoteSlot5, CCVars.EmoteSlot5),
        (CMUKeyFunctions.CMUEmoteSlot6, CCVars.EmoteSlot6),
        (CMUKeyFunctions.CMUEmoteSlot7, CCVars.EmoteSlot7),
        (CMUKeyFunctions.CMUEmoteSlot8, CCVars.EmoteSlot8),
    };

    public override void Initialize()
    {
        base.Initialize();

        var builder = CommandBinds.Builder;
        foreach (var (function, cvar) in Slots)
        {
            builder = builder.Bind(function, InputCmdHandler.FromDelegate(_ => TryFireEmote(cvar)));
        }

        builder.Register<CMUEmoteKeybindSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CMUEmoteKeybindSystem>();
    }

    private void TryFireEmote(CVarDef<string> cvar)
    {
        var emoteId = _cfg.GetCVar(cvar);
        if (string.IsNullOrEmpty(emoteId) || !_prototypeManager.HasIndex<EmotePrototype>(emoteId))
            return;

        RaisePredictiveEvent(new PlayEmoteMessage(emoteId));
    }
}
