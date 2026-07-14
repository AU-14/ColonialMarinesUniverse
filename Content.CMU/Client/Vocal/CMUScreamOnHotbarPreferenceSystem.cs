using Content.Shared._CMU14.Vocal;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._CMU14.Vocal;

/// <summary>
///     Notifies the server immediately when the local player's "pin scream to hotbar" preference changes,
///     so the toggle takes effect mid-round instead of only on next spawn.
/// </summary>
public sealed partial class CMUScreamOnHotbarPreferenceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVars.CMUScreamOnHotbarEnabled, OnPreferenceChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCVars.CMUScreamOnHotbarEnabled, OnPreferenceChanged);
    }

    private void OnPreferenceChanged(bool enabled)
    {
        RaiseNetworkEvent(new CMUScreamOnHotbarPreferenceMessage(enabled));
    }
}
