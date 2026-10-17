using Content.Shared.CMU14.Vocal;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Speech.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class VocalSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private INetConfigurationManager _netConfig = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VocalComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<CMUScreamOnHotbarPreferenceMessage>(OnScreamOnHotbarPreference);
    }

    private void OnPlayerAttached(EntityUid uid, VocalComponent component, PlayerAttachedEvent args)
    {
        // scream is off the hotbar by default; players can opt back in under CMU settings
        var enabled = _netConfig.GetClientCVar(args.Player.Channel, CCVars.CMUScreamOnHotbarEnabled);
        SetScreamOnHotbar(uid, component, enabled);
    }

    private void OnScreamOnHotbarPreference(CMUScreamOnHotbarPreferenceMessage msg, EntitySessionEventArgs args)
    {
        // lets the toggle take effect immediately, instead of waiting for the player's next spawn
        if (args.SenderSession.AttachedEntity is not { } uid || !TryComp<VocalComponent>(uid, out var component))
            return;

        SetScreamOnHotbar(uid, component, msg.Enabled);
    }

    private void SetScreamOnHotbar(EntityUid uid, VocalComponent component, bool enabled)
    {
        if (enabled)
        {
            _actions.AddAction(uid, ref component.EmoteActionEntity, component.EmoteAction);
        }
        else if (component.EmoteActionEntity != null)
        {
            _actions.RemoveAction(component.EmoteActionEntity);
        }

        Dirty(uid, component);
    }
}
