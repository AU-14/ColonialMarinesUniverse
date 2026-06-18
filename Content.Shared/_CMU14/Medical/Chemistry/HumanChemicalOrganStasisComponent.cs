using System;
using Content.Shared._CMU14.Medical.Chemistry.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Chemistry;

[RegisterComponent, Access(typeof(SharedHumanChemicalLedgerSystem))]
public sealed partial class HumanChemicalOrganStasisComponent : Component
{
    [DataField]
    public TimeSpan ExpiresAt;
}
