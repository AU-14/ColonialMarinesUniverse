using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Vehicle.Supply;

[Serializable, NetSerializable]
public enum VehicleSupplyUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyEntryState
{
    public string Id;
    public string Name;
    public int Count;

    public VehicleSupplyEntryState(string id, string name, int count)
    {
        Id = id;
        Name = name;
        Count = count;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyBuiState : BoundUserInterfaceState
{
    public VehicleSupplyLiftMode? LiftMode;
    public bool Busy;
    public string? ActiveVehicleId;
    public string? SelectedVehicleId;
    public int SelectedCopyIndex;
    public VehicleSupplyPreviewState? Preview;
    public List<VehicleSupplyEntryState> Available;
    public List<VehicleSupplyLoadoutCategoryState> Loadouts;

    public VehicleSupplyBuiState(
        VehicleSupplyLiftMode? liftMode,
        bool busy,
        string? activeVehicleId,
        string? selectedVehicleId,
        int selectedCopyIndex,
        VehicleSupplyPreviewState? preview,
        List<VehicleSupplyEntryState> available,
        List<VehicleSupplyLoadoutCategoryState>? loadouts = null)
    {
        LiftMode = liftMode;
        Busy = busy;
        ActiveVehicleId = activeVehicleId;
        SelectedVehicleId = selectedVehicleId;
        SelectedCopyIndex = selectedCopyIndex;
        Preview = preview;
        Available = available;
        Loadouts = loadouts ?? new List<VehicleSupplyLoadoutCategoryState>();
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyLoadoutOptionState
{
    public string Id;
    public string Name;

    public VehicleSupplyLoadoutOptionState(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyLoadoutCategoryState
{
    public string Id;
    public string Name;
    public string SelectedOption;
    public List<VehicleSupplyLoadoutOptionState> Options;

    public VehicleSupplyLoadoutCategoryState(
        string id,
        string name,
        string selectedOption,
        List<VehicleSupplyLoadoutOptionState> options)
    {
        Id = id;
        Name = name;
        SelectedOption = selectedOption;
        Options = options;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyPreviewState
{
    public string VehicleId;
    public int CopyIndex;
    public List<VehicleHardpointLayerState> Layers;
    public List<VehicleSupplyPreviewOverlay> Overlays;

    public VehicleSupplyPreviewState(
        string vehicleId,
        int copyIndex,
        List<VehicleHardpointLayerState> layers,
        List<VehicleSupplyPreviewOverlay> overlays)
    {
        VehicleId = vehicleId;
        CopyIndex = copyIndex;
        Layers = layers;
        Overlays = overlays;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyPreviewOverlay
{
    public string Rsi;
    public string State;
    public int Order;
    public Vector2 BaseOffset;
    public bool UseDirectional;
    public Vector2 North;
    public Vector2 East;
    public Vector2 South;
    public Vector2 West;

    public VehicleSupplyPreviewOverlay(
        string rsi,
        string state,
        int order,
        Vector2 baseOffset,
        bool useDirectional,
        Vector2 north,
        Vector2 east,
        Vector2 south,
        Vector2 west)
    {
        Rsi = rsi;
        State = state;
        Order = order;
        BaseOffset = baseOffset;
        UseDirectional = useDirectional;
        North = north;
        East = east;
        South = south;
        West = west;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplySelectMsg : BoundUserInterfaceMessage
{
    public string VehicleId;
    public int CopyIndex;

    public VehicleSupplySelectMsg(string vehicleId, int copyIndex)
    {
        VehicleId = vehicleId;
        CopyIndex = copyIndex;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplySelectLoadoutMsg : BoundUserInterfaceMessage
{
    public string CategoryId;
    public string OptionId;

    public VehicleSupplySelectLoadoutMsg(string categoryId, string optionId)
    {
        CategoryId = categoryId;
        OptionId = optionId;
    }
}

[Serializable, NetSerializable]
public sealed class VehicleSupplyLiftMsg : BoundUserInterfaceMessage
{
    public bool Raise;

    public VehicleSupplyLiftMsg(bool raise)
    {
        Raise = raise;
    }
}
