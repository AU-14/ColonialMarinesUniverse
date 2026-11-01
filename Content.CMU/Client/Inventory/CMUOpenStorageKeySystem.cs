using Content.Shared.CMU14.Input;
using Content.Shared.CMU14.Inventory;
using Content.Shared.Storage;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.CMU14.Inventory;

/// <summary>
/// The open storage keybind on anything that isn't an inventory slot: storage on the ground under the cursor. Hand
/// slots and items inside open storage call <see cref="TryOpenStorage"/> from their UI.
/// </summary>
public sealed class CMUOpenStorageKeySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUOpenWornStorage, new PointerInputCmdHandler(HandleOpenStorage, outsidePrediction: true))
            .Register<CMUOpenStorageKeySystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CMUOpenStorageKeySystem>();
    }

    private bool HandleOpenStorage(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State != BoundKeyState.Down || !args.EntityUid.IsValid())
            return false;

        return TryOpenStorage(args.EntityUid);
    }

    public bool TryOpenStorage(EntityUid item)
    {
        if (!HasComp<StorageComponent>(item))
            return false;

        RaisePredictiveEvent(new CMUOpenStorageRequestEvent(GetNetEntity(item)));
        return true;
    }
}
