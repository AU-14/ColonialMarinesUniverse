using Content.Shared._CMU14.Input;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

public sealed partial class HumanSurgeryModeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_net.IsClient)
        {
            CommandBinds.Unregister<HumanSurgeryModeSystem>();
            CommandBinds.Builder
                .Bind(CMUKeyFunctions.CMUToggleSurgeryMode,
                    InputCmdHandler.FromDelegate(session =>
                        {
                            if (session?.AttachedEntity is not { })
                                return;

                            RaiseNetworkEvent(new HumanSurgeryModeToggleRequestEvent());
                        },
                        handle: true))
                .Register<HumanSurgeryModeSystem>();
        }

        SubscribeNetworkEvent<HumanSurgeryModeToggleRequestEvent>(OnToggleRequest);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_net.IsClient)
            CommandBinds.Unregister<HumanSurgeryModeSystem>();
    }

    public bool IsSurgeryModeEnabled(EntityUid user)
    {
        return TryComp<HumanSurgeryModeComponent>(user, out var mode) && mode.Enabled;
    }

    public void ToggleSurgeryMode(EntityUid user)
    {
        if (_net.IsClient)
        {
            RaiseNetworkEvent(new HumanSurgeryModeToggleRequestEvent());
            return;
        }

        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled) ||
            !_cfg.GetCVar(CMUMedicalCCVars.SurgeryEnabled))
        {
            return;
        }

        SetSurgeryMode(user, !IsSurgeryModeEnabled(user));
    }

    public void SetSurgeryMode(EntityUid user, bool enabled)
    {
        if (_net.IsClient)
            return;

        if (!enabled)
        {
            if (HasComp<HumanSurgeryModeComponent>(user))
                RemComp<HumanSurgeryModeComponent>(user);

            PopupSelf(user, "cmu-medical-surgery-mode-disabled");
            return;
        }

        var mode = EnsureComp<HumanSurgeryModeComponent>(user);
        if (mode.Enabled)
            return;

        mode.Enabled = true;
        DirtyField(user, mode, nameof(HumanSurgeryModeComponent.Enabled));
        PopupSelf(user, "cmu-medical-surgery-mode-enabled");
    }

    private void PopupSelf(EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), user, user, PopupType.SmallCaution);
    }

    private void OnToggleRequest(HumanSurgeryModeToggleRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_net.IsServer || args.SenderSession.AttachedEntity is not { } user)
            return;

        ToggleSurgeryMode(user);
    }
}

[Serializable, NetSerializable]
public sealed partial class HumanSurgeryModeToggleRequestEvent : EntityEventArgs
{
}
