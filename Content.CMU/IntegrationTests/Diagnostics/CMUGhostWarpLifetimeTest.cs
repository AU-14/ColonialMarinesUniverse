using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Ghost;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Ghost.Systems;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUGhostWarpLifetimeTest : GameTest
{
    [Test]
    public async Task XenoWithoutARoleStillAppearsInGhostWarps()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var xeno = SEntMan.SpawnEntity("RMCXenoDespoiler", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, xeno);
#pragma warning disable RA0002 // Reproduce the incomplete role data seen in production, bypassing normal xeno setup.
            SEntMan.GetComponent<XenoComponent>(xeno).Role = default;
#pragma warning restore RA0002
            // Enumerate the real player-warp builder without a second observer connection.
            var builder = typeof(GhostSystem).GetMethod("GetPlayerWarps", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var warps = (IEnumerable<GhostWarp>) builder.Invoke(Server.System<GhostSystem>(), new object?[] { null })!;
            Assert.That(warps.Count(), Is.EqualTo(1));
        });
    }
}
