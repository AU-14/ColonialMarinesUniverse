using System;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Chemistry.Data;

public readonly record struct HumanChemicalTick(
    string ReagentId,
    FixedPoint2 Scale,
    FixedPoint2 TotalQuantity);

public readonly record struct HumanChemicalLedgerPlan(
    MedicalTransaction Transaction,
    TimeSpan OrganStasisDuration);
