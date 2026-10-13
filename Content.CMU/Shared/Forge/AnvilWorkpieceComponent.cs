// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Forge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AnvilWorkpieceComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Forging;

    [ViewVariables]
    public ProtoId<AnvilRecipePrototype>? Recipe;

    [DataField, AutoNetworkedField]
    public int StepIndex;

    [DataField]
    public float MinWorkTemperature = 700f;

    [DataField]
    public float HeatLossPerHit = 60f;
}
