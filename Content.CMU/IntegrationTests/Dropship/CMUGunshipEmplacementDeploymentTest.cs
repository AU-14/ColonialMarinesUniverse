using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.CMU14.Emplacements;
using Content.Shared.Construction.Components;
using Content.Shared.DoAfter;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipEmplacementDeploymentTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("AU14MK86Mount", true)]
    [TestCase("AU14MK86Mount", false)]
    [TestCase("CMUATGMMount", true)]
    [TestCase("CMUATGMMount", false)]
    [TestCase("RMCM2CMount", true)]
    [TestCase("RMCM2CMount", false)]
    public async Task PortableEmplacementsRequireAnUnrestrictedGrid(string prototype, bool blocked)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, mount = default;
        NetEntity userNet = default, mountNet = default, gridNet = default;
        ushort index = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = 0; x < 4; x++)
            for (var y = 0; y < 4; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            if (blocked)
                SEntMan.AddComponent<CMUBlockEmplacementDeploymentComponent>(map.Grid.Owner);

            var coordinates = map.GridCoords.Offset(new Vector2(1.5f, 1.5f));
            user = SEntMan.SpawnAttachedTo("CMMobHuman", coordinates);
            mount = SEntMan.SpawnAttachedTo(prototype, coordinates);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(user, Direction.East.ToAngle());
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, mount), Is.True);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            index = SEntMan.GetComponent<DoAfterComponent>(user).NextId;
            userNet = SEntMan.GetNetEntity(user);
            mountNet = SEntMan.GetNetEntity(mount);
            gridNet = SEntMan.GetNetEntity(map.Grid.Owner);
        });
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EventBus.RaiseLocalEvent(mount, new UseInHandEvent(user));
            Assert.That(SEntMan.System<SharedDoAfterSystem>().IsRunning(user, index), Is.EqualTo(!blocked));
        });
        if (blocked)
        {
            await Client.WaitAssertion(() =>
            {
                var clientUser = CEntMan.GetEntity(userNet);
                Assert.That(CEntMan.HasComponent<CMUBlockEmplacementDeploymentComponent>(CEntMan.GetEntity(gridNet)),
                    Is.True);
                CEntMan.EventBus.RaiseLocalEvent(CEntMan.GetEntity(mountNet), new UseInHandEvent(clientUser));
                Assert.That(CEntMan.System<SharedDoAfterSystem>().IsRunning(clientUser, index), Is.False);
            });
        }

        await RunSeconds(12);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FoldableComponent>(mount).IsFolded, Is.EqualTo(blocked));
            Assert.That(SEntMan.GetComponent<TransformComponent>(mount).Anchored, Is.EqualTo(!blocked));
        });
    }

    [TestCase("AU14MK86Mount")]
    [TestCase("CMUATGMMount")]
    public async Task DeploymentCompletionRechecksTheGridRestriction(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid mount = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var coordinates = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var user = SEntMan.SpawnAttachedTo("CMMobHuman", coordinates);
            mount = SEntMan.SpawnAttachedTo(prototype, coordinates);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(user, Direction.East.ToAngle());
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(user, mount), Is.True);
            var index = SEntMan.GetComponent<DoAfterComponent>(user).NextId;
            SEntMan.EventBus.RaiseLocalEvent(mount, new UseInHandEvent(user));
            Assert.That(SEntMan.System<SharedDoAfterSystem>().IsRunning(user, index), Is.True);
            SEntMan.AddComponent<CMUBlockEmplacementDeploymentComponent>(map.Grid.Owner);
        });

        await RunSeconds(12);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FoldableComponent>(mount).IsFolded, Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(mount).Anchored, Is.False);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task EmptyTripodsCannotBypassTheRestriction(bool blocked)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            if (blocked)
                SEntMan.AddComponent<CMUBlockEmplacementDeploymentComponent>(map.Grid.Owner);
            var user = SEntMan.SpawnAttachedTo("CMMobHuman", map.GridCoords);
            var tool = SEntMan.SpawnAttachedTo("CMScrewdriver", map.GridCoords);
            var mount = SEntMan.SpawnAttachedTo("RMCML66DMount", map.GridCoords);
            var foldable = SEntMan.GetComponent<FoldableComponent>(mount);
            var folds = SEntMan.System<FoldableSystem>();
            Assert.That(folds.TrySetFolded(mount, foldable, false, user), Is.EqualTo(!blocked));

            // Also cover a tripod that was unfolded before it was carried aboard.
            folds.SetFolded(mount, foldable, false, user);
            var attempt = new AnchorAttemptEvent(user, tool);
            SEntMan.EventBus.RaiseLocalEvent(mount, attempt);
            Assert.That(attempt.Cancelled, Is.EqualTo(blocked));
        });
    }
}
