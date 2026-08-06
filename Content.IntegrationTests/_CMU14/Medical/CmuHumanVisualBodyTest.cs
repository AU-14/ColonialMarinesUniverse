using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Station.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Preferences;
using Robust.Shared.Enums;
using Robust.Shared.Exceptions;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
[TestOf(typeof(HumanoidAppearanceComponent))]
public sealed class CmuHumanVisualBodyTest : GameTest
{
    [Test]
    public async Task PlayerProfileUsesLegacyCmuAppearancePipeline()
    {
        var map = await Pair.CreateTestMap();
        var human = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            human = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            var appearance = SEntMan.GetComponent<HumanoidAppearanceComponent>(human);
            var profile = HumanoidCharacterProfile.RandomWithSpecies(appearance.Species);
            var spawning = SEntMan.System<StationSpawningSystem>();

            var spawned = spawning.SpawnPlayerMob(map.GridCoords, null, profile, null, human);

            Assert.Multiple(() =>
            {
                Assert.That(spawned, Is.EqualTo(human));
                Assert.That(SEntMan.HasComponent<VisualBodyComponent>(human), Is.False);
                Assert.That(SEntMan.HasComponent<HumanoidProfileComponent>(human), Is.False);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(human).EntityName, Is.EqualTo(profile.Name));
            });
        });

        await Server.WaitPost(() => SDeleteNow(human));
        await Pair.RunUntilSynced();
    }

    [Test]
    public async Task HumanSpawnsWithRenderableBodyParts()
    {
        await Server.WaitIdleAsync();

        var human = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            human = SSpawn("CMMobHuman");
            var body = SEntMan.GetComponent<BodyComponent>(human);

            Assert.That(SEntMan.HasComponent<HumanoidAppearanceComponent>(human), Is.True);
            Assert.That(SEntMan.HasComponent<VisualBodyComponent>(human), Is.False,
                "The new organ renderer conflicts with CMMobHuman's legacy CMU body-part graph.");

            var parts = SEntMan.EntityQuery<BodyPartComponent>()
                .Count(part => part.Body == human);
            Assert.That(parts, Is.EqualTo(10), "CMMobHuman did not build its complete CMU body-part graph.");
        });

        await Server.WaitPost(() => SDeleteNow(human));
        await Pair.RunUntilSynced();

        var runtimeLog = Client.ResolveDependency<IRuntimeLog>();
        Assert.That(runtimeLog.ExceptionCount, Is.Zero, runtimeLog.Display());
    }

    [Test]
    public async Task HumanSpawnsWithUsableHands()
    {
        var map = await Pair.CreateTestMap();

        var human = EntityUid.Invalid;
        var pullTarget = EntityUid.Invalid;
        var item = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            human = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            pullTarget = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            item = SSpawnAtPosition("RMCWeaponRifleM54C", map.GridCoords);
            var hands = SEntMan.System<SharedHandsSystem>();
            var pulling = SEntMan.System<PullingSystem>();
            var handsComponent = SEntMan.GetComponent<HandsComponent>(human);

            Assert.Multiple(() =>
            {
                Assert.That(handsComponent.Count, Is.EqualTo(2));
                Assert.That(hands.TryPickupAnyHand(human, item), Is.True);
                Assert.That(pulling.TryStartPull(human, pullTarget), Is.True);
            });
        });

        await Server.WaitPost(() =>
        {
            foreach (var entity in new[] { item, pullTarget, human })
            {
                if (!SEntMan.Deleted(entity))
                    SDeleteNow(entity);
            }
        });
        await Pair.RunUntilSynced();

        var runtimeLog = Client.ResolveDependency<IRuntimeLog>();
        Assert.That(runtimeLog.ExceptionCount, Is.Zero, runtimeLog.Display());
    }
}
