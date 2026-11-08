using System.Linq;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Marines;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Yautja;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Damage.Systems;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using ServerHandsSystem = Content.Server.Hands.Systems.HandsSystem;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaPlaytestRegressionTest
{

    [Test]
    public async Task ThweiMendsAnUnsplintedFracture()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid treatedArm = default, untreatedArm = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            EntityUid BreakArm(bool thwei)
            {
                var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(hunter,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
                Assert.That(entMan.System<SharedBoneSystem>().SeedFracture(arm, FractureSeverity.Simple), Is.True);
                if (thwei)
                    Assert.That(entMan.System<BloodstreamSystem>().TryAddToBloodstream(hunter, new([new("thwei", FixedPoint2.New(15))])), Is.True);

                return arm;
            }

            treatedArm = BreakArm(true);
            // control, so the test can't pass off the back of natural healing
            untreatedArm = BreakArm(false);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(30));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            FractureSeverity Severity(EntityUid arm)
                => entMan.TryGetComponent(arm, out FractureComponent? fracture) ? fracture.Severity : FractureSeverity.None;

            Assert.Multiple(() =>
            {
                Assert.That(Severity(untreatedArm), Is.Not.EqualTo(FractureSeverity.None), "the arm healed on its own, the test proves nothing");
                Assert.That(Severity(treatedArm), Is.EqualTo(FractureSeverity.None), "thwei left a pred's broken arm alone because it wasn't splinted");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntersAggressiveGrabCantBeClickedOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid grabber = default, victim = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            grabber = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(entMan.System<PullingSystem>().TryStartPull(grabber, victim), Is.True);
        });

        // wait out the grab-upgrade delay, then go aggressive
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await server.WaitPost(() =>
        {
            var toggle = new RMCPullToggleEvent();
            server.EntMan.EventBus.RaiseLocalEvent(grabber, ref toggle);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.True);

            // what the being-pulled alert does
            var pullable = entMan.GetComponent<PullableComponent>(victim);
            Assert.That(entMan.System<PullingSystem>().TryStopPull(victim, pullable, victim), Is.False);
            Assert.That(pullable.Puller, Is.EqualTo(grabber), "one alert click broke an aggressive grab");
        });

        await pair.CleanReturnAsync();
    }

    // a full hunter kit used to stack its armor into near immunity, it should soften hazards, not erase them

}
