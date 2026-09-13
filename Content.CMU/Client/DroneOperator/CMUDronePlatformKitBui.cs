using Content.Shared.CMU14.DroneOperator;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.DroneOperator;

[UsedImplicitly]
public sealed class CMUDronePlatformKitBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();
        var window = this.CreateWindow<CMUDronePlatformKitWindow>();
        window.OnSelected += platform => SendPredictedMessage(new CMUDronePlatformSelectedMessage(platform));
    }
}
