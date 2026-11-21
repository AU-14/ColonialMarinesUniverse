using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUMovementRefreshDirtyingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase(false)]
    [TestCase(true)]
    public async Task RepeatedScaledRefreshKeepsStateCleanButChangedDampingReplicates(bool weightless)
    {
        EntityUid map = default;
        Entity<MovementSpeedModifierComponent> mover = default;
        GameTick modified = default;
        float[] values = [];
        var cvar = weightless ? CCVars.AirFriction : CCVars.TileFrictionModifier;
        var originalDamping = weightless ? 0.2f : 8f;
        var previousDamping = 0f;

        void Refresh()
        {
            var system = Server.System<MovementSpeedModifierSystem>();
            if (weightless)
                system.RefreshWeightlessModifiers(mover.Owner);
            else
                system.RefreshFrictionModifiers(mover.Owner);
        }

        try
        {
            await Server.WaitAssertion(() =>
            {
                var config = Server.ResolveDependency<IConfigurationManager>();
                previousDamping = config.GetCVar(cvar);
                config.SetCVar(cvar, originalDamping);
                map = Server.System<SharedMapSystem>().CreateMap(runMapInit: true);
                var uid = SEntMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
                mover = (uid, SEntMan.AddComponent<MovementSpeedModifierComponent>(uid));
                Refresh();
                modified = mover.Comp.LastModifiedTick;
                values = ReadValues(mover.Comp);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                // Call the real refresh path after the clock advances, so an unnecessary Dirty is observable.
                for (var i = 0; i < 16; i++)
                    Refresh();
                Assert.That(ReadValues(mover.Comp), Is.EqualTo(values));
                Assert.That(mover.Comp.LastModifiedTick, Is.EqualTo(modified),
                    "Identical final movement values must not generate another replicated component state.");

                Server.ResolveDependency<IConfigurationManager>().SetCVar(cvar, originalDamping * 2);
                Refresh();
                Assert.That(mover.Comp.LastModifiedTick, Is.GreaterThan(modified),
                    "A real damping change must still dirty the movement component.");
                var friction = weightless ? mover.Comp.WeightlessFriction : mover.Comp.Friction;
                Assert.That(friction, Is.EqualTo((weightless ? values[5] : values[0]) * 2));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.ResolveDependency<IConfigurationManager>().SetCVar(cvar, previousDamping);
                if (SEntMan.EntityExists(map))
                    SEntMan.DeleteEntity(map);
            });
        }
    }

    private static float[] ReadValues(MovementSpeedModifierComponent component) =>
    [
        component.Friction,
        component.FrictionNoInput,
        component.Acceleration,
        component.WeightlessAcceleration,
        component.WeightlessModifier,
        component.WeightlessFriction,
        component.WeightlessFrictionNoInput,
    ];
}
