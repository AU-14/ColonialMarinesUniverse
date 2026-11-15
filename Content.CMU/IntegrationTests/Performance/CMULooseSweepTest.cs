using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Util;
using Content.Shared.CCVar;
using Content.Shared.CMU14.util;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMULooseSweepTest : GameTest
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task SweepDeletesLooseTrashButSparesContainedTrashPaperAndOrdinaryTags()
    {
        var map = await Pair.CreateTestMap();
        EntityUid trash = default;
        EntityUid contained = default;
        EntityUid paper = default;
        EntityUid ordinary = default;
        float oldMagazine = 0;
        float oldTrash = 0;
        await Server.WaitPost(() =>
        {
            oldMagazine = Server.CfgMan.GetCVar(CCVars.LooseMagazineDespawnTime);
            oldTrash = Server.CfgMan.GetCVar(CCVars.TrashDespawnTime);
            Server.CfgMan.SetCVar(CCVars.LooseMagazineDespawnTime, 0f);
            Server.CfgMan.SetCVar(CCVars.TrashDespawnTime, 20f);
            var tags = Server.System<TagSystem>();
            trash = SEntMan.SpawnEntity("FoodTinMRETrash", map.GridCoords);
            contained = SEntMan.SpawnEntity(null, map.GridCoords);
            paper = SEntMan.SpawnEntity("Paper", map.GridCoords);
            ordinary = SEntMan.SpawnEntity(null, map.GridCoords);
            tags.AddTag(contained, TrashTag);
            tags.AddTag(paper, TrashTag);
            SEntMan.EnsureComponent<TagComponent>(ordinary);
            var holder = SEntMan.SpawnEntity(null, map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(contained, containers.EnsureContainer<ContainerSlot>(holder, "trash")), Is.True);
        });
        try
        {
            await RunSeconds(6f);
            await Server.WaitAssertion(() =>
            {
                var collected = new List<EntityUid>();
                Server.System<CMULooseSweepSystem>().CollectLooseTrash(collected);
                Assert.That(collected, Does.Contain(trash));
                Assert.That(collected, Does.Not.Contain(contained));
                Assert.That(collected, Does.Not.Contain(paper));
                Assert.That(collected, Does.Not.Contain(ordinary));
            });
            await RunSeconds(25f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityExists(trash), Is.False, "Eligible loose trash must still expire.");
                Assert.That(SEntMan.EntityExists(contained), Is.True, "Container contents must survive the sweep.");
                Assert.That(SEntMan.EntityExists(paper), Is.True, "Readable paper stays protected even with a Trash tag.");
                Assert.That(SEntMan.EntityExists(ordinary), Is.True, "A TagComponent alone must not make an entity trash.");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.CfgMan.SetCVar(CCVars.LooseMagazineDespawnTime, oldMagazine);
                Server.CfgMan.SetCVar(CCVars.TrashDespawnTime, oldTrash);
            });
        }
    }

    [Test]
    public async Task PausedTrashIsNotStampedOrDeletedUntilResumed()
    {
        var map = await Pair.CreateTestMap();
        EntityUid aged = default;
        EntityUid unstamped = default;
        var oldTrash = 0f;
        await Server.WaitPost(() =>
        {
            oldTrash = Server.CfgMan.GetCVar(CCVars.TrashDespawnTime);
            Server.CfgMan.SetCVar(CCVars.TrashDespawnTime, 20f);
            aged = SEntMan.SpawnEntity(null, map.GridCoords);
            unstamped = SEntMan.SpawnEntity(null, map.GridCoords);
            var tags = Server.System<TagSystem>();
            tags.AddTag(aged, TrashTag);
            tags.AddTag(unstamped, TrashTag);
            Server.System<MetaDataSystem>().SetEntityPaused(unstamped, true);
        });
        try
        {
            await RunSeconds(6f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<CMULooseSinceComponent>(aged), Is.True);
                Assert.That(SEntMan.HasComponent<CMULooseSinceComponent>(unstamped), Is.False,
                    "Paused trash must not start its cleanup age.");
                Server.System<MetaDataSystem>().SetEntityPaused(aged, true);
            });
            await RunSeconds(25f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityExists(aged), Is.True, "Expired trash must survive while paused.");
                Assert.That(SEntMan.HasComponent<CMULooseSinceComponent>(unstamped), Is.False);
                var metadata = Server.System<MetaDataSystem>();
                metadata.SetEntityPaused(aged, false);
                metadata.SetEntityPaused(unstamped, false);
            });
            await RunSeconds(6f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityExists(aged), Is.False, "Resuming must preserve the original cleanup age.");
                Assert.That(SEntMan.EntityExists(unstamped), Is.True, "Newly unpaused trash gets its full lifetime.");
            });
            await RunSeconds(25f);
            await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(unstamped), Is.False));
        }
        finally
        {
            await Server.WaitPost(() => Server.CfgMan.SetCVar(CCVars.TrashDespawnTime, oldTrash));
        }
    }

    [Test]
    public async Task TagAndContainerChangesAreRecheckedWithoutResettingCleanupAge()
    {
        var map = await Pair.CreateTestMap();
        EntityUid retagged = default;
        EntityUid pickedUp = default;
        EntityUid newlyTagged = default;
        ContainerSlot slot = default!;
        var oldTrash = 0f;
        await Server.WaitPost(() =>
        {
            oldTrash = Server.CfgMan.GetCVar(CCVars.TrashDespawnTime);
            Server.CfgMan.SetCVar(CCVars.TrashDespawnTime, 20f);
            retagged = SEntMan.SpawnEntity(null, map.GridCoords);
            pickedUp = SEntMan.SpawnEntity(null, map.GridCoords);
            newlyTagged = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.System<TagSystem>().AddTag(retagged, TrashTag);
            Server.System<TagSystem>().AddTag(pickedUp, TrashTag);
            var holder = SEntMan.SpawnEntity(null, map.GridCoords);
            slot = Server.System<SharedContainerSystem>().EnsureContainer<ContainerSlot>(holder, "trash");
        });
        try
        {
            await RunSeconds(6f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<CMULooseSinceComponent>(retagged), Is.True);
                Assert.That(SEntMan.HasComponent<CMULooseSinceComponent>(pickedUp), Is.True);
                var tags = Server.System<TagSystem>();
                tags.RemoveTags(retagged, TrashTag);
                tags.AddTags(newlyTagged, TrashTag);
                Assert.That(Server.System<SharedContainerSystem>().Insert(pickedUp, slot), Is.True);
            });
            await RunSeconds(6f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<CMULooseSinceComponent>(newlyTagged), Is.True,
                    "Runtime tag additions must be discovered by the next sweep.");
                Assert.That(SEntMan.EntityExists(newlyTagged), Is.True, "Discovery must not immediately delete new trash.");
            });
            await RunSeconds(25f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityExists(retagged), Is.True, "Removing Trash must cancel eligibility.");
                Assert.That(SEntMan.EntityExists(pickedUp), Is.True, "Picking up aged trash protects it.");
                Assert.That(SEntMan.EntityExists(newlyTagged), Is.False, "Runtime tag additions must be discovered.");
                Server.System<TagSystem>().AddTag(retagged, TrashTag);
                Assert.That(Server.System<SharedContainerSystem>().Remove(pickedUp, slot), Is.True);
            });
            await RunSeconds(6f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityExists(retagged), Is.False, "Re-adding Trash must not reset its old age.");
                Assert.That(SEntMan.EntityExists(pickedUp), Is.False, "Dropping aged trash must not reset its old age.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.CfgMan.SetCVar(CCVars.TrashDespawnTime, oldTrash));
        }
    }
}
