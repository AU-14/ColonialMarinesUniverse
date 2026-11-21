using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

// FLOW-03: vendor alcohol burns with a reach of 2, not the 4 it inherited from RMC's BaseAlcohol.
[TestFixture]
public sealed class CMUAlcoholFireReachTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task AlcoholFireReachIsTwo()
    {
        await Server.WaitAssertion(() =>
        {
            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var vodka = new Solution();
            vodka.AddReagent("Vodka", FixedPoint2.New(30));
            Assert.That(fuel.TryResolveFire(vodka, CMUFireSource.Container, out var spec), Is.True);
            Assert.That(spec.Radius, Is.LessThanOrEqualTo(2), "30u of vodka must not burn wider than the alcohol reach");

            Assert.That(CMUFuelProfileSystem.NonSpecialistReach(2), Is.EqualTo(1), "a standard tank reaches 1 with alcohol");
        });
    }
}
