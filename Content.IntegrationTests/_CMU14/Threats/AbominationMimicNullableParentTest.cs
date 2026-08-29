#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._CMU14.Threats.Mobs.Abomination;
using Content.Server.Polymorph.Components;
using Content.Shared._CMU14.Threats.Mobs.Abomination;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Threats;

[TestFixture]
[TestOf(typeof(AbominationMimicSystem))]
public sealed class AbominationMimicNullableParentTest : GameTest
{
    [Test]
    public async Task ExpiredRevertWithNullParentIsRepeatableNoOp()
    {
        var map = await Pair.CreateTestMap();
        EntityUid disguised = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mimicSystem = Server.System<AbominationMimicSystem>();
                disguised = SEntMan.SpawnEntity(null, map.GridCoords);

                var profile = new AbominationAssimilationProfile
                {
                    Name = "null-parent-sentinel",
                };
                var mimic = SEntMan.EnsureComponent<AbominationMimicComponent>(disguised);
                mimic.AssimilatedPool = [profile];
                mimic.TransformActionEntity = disguised;

                var transformed = SEntMan.EnsureComponent<AbominationMimicTransformedComponent>(disguised);
                transformed.ExpiresAt = Server.Timing.CurTime + TimeSpan.FromMinutes(1);
                transformed.Profile = profile;

                var reverting = SEntMan.EnsureComponent<AbominationMimicRevertingComponent>(disguised);
                reverting.RevertAt = TimeSpan.Zero;

                var polymorphed = SEntMan.EnsureComponent<PolymorphedEntityComponent>(disguised);

                var originalPool = mimic.AssimilatedPool;
                Assert.DoesNotThrow(() =>
                {
                    mimicSystem.Update(0f);
                    mimicSystem.Update(0f);
                });

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.EntityExists(disguised), Is.True);
                    Assert.That(polymorphed.Parent, Is.Null);
                    Assert.That(polymorphed.Reverted, Is.False);
                    Assert.That(SEntMan.HasComponent<AbominationMimicRevertingComponent>(disguised), Is.True);
                    Assert.That(mimic.AssimilatedPool, Is.SameAs(originalPool));
                    Assert.That(mimic.AssimilatedPool, Has.Count.EqualTo(1));
                    Assert.That(mimic.AssimilatedPool[0], Is.SameAs(profile));
                    Assert.That(mimic.TransformActionEntity, Is.EqualTo(disguised));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(disguised))
                    SEntMan.DeleteEntity(disguised);
            });
        }
    }
}

#pragma warning restore RA0002
