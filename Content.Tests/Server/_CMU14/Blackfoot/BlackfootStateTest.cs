using Content.Server._CMU14.Blackfoot;
using Content.Shared._CMU14.Blackfoot;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Blackfoot;

[TestFixture]
public sealed class BlackfootStateTest
{
    [Test]
    public void CrashedBlackfootCanTaxiWithTow()
    {
        Assert.That(SharedBlackfootFlightSystem.CanRunInState(BlackfootFlightState.Crashed, true), Is.True);
        Assert.That(SharedBlackfootFlightSystem.CanRunInState(BlackfootFlightState.Crashed, false), Is.False);
    }

    [Test]
    public void LandingPadCanServiceCrashedAircraft()
    {
        Assert.That(BlackfootLandingPadSystem.CanParkAircraftState(BlackfootFlightState.Crashed), Is.True);
    }

    [Test]
    public void CrashedBlackfootRecoversAfterFuelAndThrustersAreRestored()
    {
        var flight = new BlackfootFlightComponent { State = BlackfootFlightState.Crashed };
        var fuel = new BlackfootFuelPowerComponent
        {
            Fuel = 0f,
            CrashOnZeroFuel = true,
        };

        Assert.That(BlackfootFlightSystem.ShouldRecoverFromCrash(flight, fuel, true), Is.False);

        fuel.Fuel = 1f;
        Assert.That(BlackfootFlightSystem.ShouldRecoverFromCrash(flight, fuel, false), Is.False);
        Assert.That(BlackfootFlightSystem.ShouldRecoverFromCrash(flight, fuel, true), Is.True);
    }
}
