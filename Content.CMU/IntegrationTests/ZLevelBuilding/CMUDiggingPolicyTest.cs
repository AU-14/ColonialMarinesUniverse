using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.Hands.EntitySystems;

namespace Content.IntegrationTests.CMU14.ZLevelBuilding;

[TestFixture]
public sealed class CMUDiggingPolicyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DiggingCVarRechecksToolsAndPendingExcavations()
    {
        var map = await Pair.CreateTestMap();
        EntityUid miner = default;
        EntityUid tool = default;
        EntityCoordinates location = default;
        await Server.WaitAssertion(() =>
        {
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.DepthBiomes.Clear();
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 16; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            location = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, new Vector2i(6, 5));
            miner = SEntMan.SpawnEntity("MobHuman", location);
            tool = SEntMan.SpawnEntity("Pickaxe", location);
            SEntMan.GetComponent<PitDiggingToolComponent>(tool).DigTimeMultiplier = 0.01f;
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(miner, tool), Is.True);
            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
            var digging = SEntMan.System<PitDiggingToolSystem>();
            Assert.That(digging.TryStartDig(tool, miner, location), Is.False,
                "Disabling the CVar must block downward excavation before a tool action starts.");
            Assert.That(SEntMan.System<ZLevelBuildingSystem>().DigDown(miner), Is.False);

            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
            Assert.That(digging.TryStartDig(tool, miner, location), Is.True,
                "Enabling the CVar must allow the real tool interaction on the same terrain.");
            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, false);
        });

        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<PitComponent>().Any(), Is.False,
                "An excavation started while allowed must not open a pit after the CVar is disabled.");
            Server.CfgMan.SetCVar(CCVars.AllowDiggingDown, true);
            Assert.That(SEntMan.System<PitDiggingToolSystem>().TryStartDig(tool, miner, location), Is.True,
                "Re-enabling the CVar must allow a new tool action on the same terrain.");
            SEntMan.DeleteEntity(miner);
            if (SEntMan.EntityExists(tool))
                SEntMan.DeleteEntity(tool);
        });
    }
}
