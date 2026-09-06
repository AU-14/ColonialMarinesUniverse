using Content.Shared._CMU14.Dropship.Integrity;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipRepairEligibilityTest
{
    [TestCase(false, false, false, false, false, DropshipFlightState.Landed)]
    [TestCase(true, false, false, false, false, DropshipFlightState.Hovering)]
    [TestCase(true, true, false, false, false, DropshipFlightState.ChangingAltitude)]
    [TestCase(true, true, true, false, false, DropshipFlightState.Ftl)]
    [TestCase(true, true, true, true, false, DropshipFlightState.Crashing)]
    [TestCase(true, true, true, true, true, DropshipFlightState.Wrecked)]
    public void StateUsesSafetyFirstPrecedence(
        bool hovering,
        bool changingAltitude,
        bool ftlActive,
        bool crashing,
        bool wrecked,
        DropshipFlightState expected)
    {
        Assert.That(
            DropshipRepairEligibility.ResolveState(hovering, changingAltitude, ftlActive, crashing, wrecked),
            Is.EqualTo(expected));
    }

    [TestCase(DropshipFlightState.Landed, true)]
    [TestCase(DropshipFlightState.Hovering, false)]
    [TestCase(DropshipFlightState.ChangingAltitude, false)]
    [TestCase(DropshipFlightState.Ftl, false)]
    [TestCase(DropshipFlightState.Crashing, false)]
    [TestCase(DropshipFlightState.Wrecked, false)]
    public void RepairOnlyAllowsLandedState(DropshipFlightState state, bool expected)
    {
        Assert.That(DropshipRepairEligibility.CanRepair(state), Is.EqualTo(expected));
    }
}
