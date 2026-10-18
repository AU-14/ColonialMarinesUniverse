// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Smelting;

// From the window: set the target temperature of the smelter.
[Serializable, NetSerializable]
public sealed class ElectricSmelterSetTemperatureMessage(float temperature) : BoundUserInterfaceMessage
{
    public float Temperature = temperature;
}
