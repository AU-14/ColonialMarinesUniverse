// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameObjects;

namespace Content.Shared._AU14.ZLevelBuilding;

/// <summary>
/// Marks a dropship GRID whose structure was hit by a cave-in below it. A compromised dropship refuses
/// to launch (its flight systems report structural damage and its controls spark) because launching a
/// grid that a collapse has tangled into the planet's map grid drags the entire map along with the FTL,
/// destroying the round. Applied by ZCaveInSystem when cave-in surface effects touch a dropship grid;
/// checked in DropshipSystem.FlyTo. Permanent for the round (admins can remove the component to clear it).
/// </summary>
[RegisterComponent]
public sealed partial class ZCollapseCompromisedComponent : Component;
