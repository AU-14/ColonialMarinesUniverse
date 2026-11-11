using Content.Client.Eui;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._RMC14.Admin;

// CMU14: the target actions editor uses the CMU layout and controller partial.
[UsedImplicitly]
public sealed partial class RMCAdminEui : BaseEui
{
    public override void Opened() => OpenCMUActions();

    public override void HandleState(EuiStateBase state) => UpdateCMUActions(state);

    public override void Closed() => _cmuWindow.Close();
}
