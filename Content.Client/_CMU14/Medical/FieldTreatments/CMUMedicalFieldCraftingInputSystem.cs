using Content.Shared._CMU14.Input;
using Content.Shared._CMU14.Medical.FieldTreatments;
using JetBrains.Annotations;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client._CMU14.Medical.FieldTreatments;

[UsedImplicitly]
public sealed class CMUMedicalFieldCraftingInputSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUOpenMedicalCraftingMenu,
                new PointerInputCmdHandler(HandleOpenCraftingMenu, outsidePrediction: true))
            .Register<CMUMedicalFieldCraftingInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CMUMedicalFieldCraftingInputSystem>();
    }

    private bool HandleOpenCraftingMenu(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State == BoundKeyState.Down)
            RaiseNetworkEvent(new CMUMedicalFieldCraftingOpenRequestEvent());

        return true;
    }
}
