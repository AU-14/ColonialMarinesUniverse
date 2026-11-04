using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.Movement;

public sealed class MobCollisionRegressionTest : GameTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task OverlappingMarinesRespectCollisionConfiguration(bool useDefault)
    {
        var map = await Pair.CreateTestMap();
        EntityUid first = default;
        EntityUid second = default;
        var config = Server.ResolveDependency<IConfigurationManager>();
        var previous = false;
        try
        {
            await Server.WaitAssertion(() =>
            {
                previous = config.GetCVar(CCVars.MovementMobPushing);
                // The pool disables pushing for unrelated tests. Exercise release defaults without a development preset.
                config.SetCVar(CCVars.MovementMobPushing, useDefault && CCVars.MovementMobPushing.DefaultValue);
                first = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.3f, 0.5f)));
                second = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.6f, 0.5f)));
            });

            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(first).ContactCount, Is.GreaterThan(0));
                Assert.That(SEntMan.GetComponent<MobCollisionComponent>(first).Colliding, Is.EqualTo(useDefault),
                    "Living marines must push apart by default, including without the development preset.");
                Assert.That(SEntMan.GetComponent<MobCollisionComponent>(second).Colliding, Is.EqualTo(useDefault));
                if (useDefault)
                {
                    Assert.That(SEntMan.GetComponent<PhysicsComponent>(first).LinearVelocity.X, Is.LessThan(0));
                    Assert.That(SEntMan.GetComponent<PhysicsComponent>(second).LinearVelocity.X, Is.GreaterThan(0));
                }
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                SEntMan.DeleteEntity(first);
                SEntMan.DeleteEntity(second);
                config.SetCVar(CCVars.MovementMobPushing, previous);
            });
        }
    }
}
