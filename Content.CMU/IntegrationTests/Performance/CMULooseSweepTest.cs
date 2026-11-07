using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Util;
using Content.Shared.CCVar;
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
            trash = SEntMan.SpawnEntity(null, map.GridCoords);
            contained = SEntMan.SpawnEntity(null, map.GridCoords);
            paper = SEntMan.SpawnEntity("Paper", map.GridCoords);
            ordinary = SEntMan.SpawnEntity(null, map.GridCoords);
            tags.AddTag(trash, TrashTag);
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
}
