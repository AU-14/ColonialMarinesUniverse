using System.Numerics;
using Content.Server.CMU14.Threats.Mobs.Wendigo.Scent;
using Content.Shared.Alert;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Scent;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoScentTest
{
    private const string Lab = "CMUWendigoLab";
    private const string Human = "CMMobHuman";
    private const string Item = "CMScrewdriver";


    // (a) sniffing a human tracks it; after a few ticks the arrow shows and the trail has a point.

    // (b) sniffing an item a human picked up tracks that human; (c) an item nobody ever held has no scent.
    [Test]
    public async Task ItemScentLeadsToLastHolder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid wendigo = default, human = default, held = default, untouched = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            wendigo = entMan.SpawnEntity(Lab, map.GridCoords);
            human = entMan.SpawnEntity(Human, map.GridCoords.Offset(new Vector2(1, 0)));
            held = entMan.SpawnEntity(Item, map.GridCoords.Offset(new Vector2(1, 0)));
            untouched = entMan.SpawnEntity(Item, map.GridCoords.Offset(new Vector2(0, 1)));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scent = entMan.System<CMUWendigoScentSystem>();

            // (c) first, so the tracker can't still hold an earlier target.
            Assert.That(scent.GetScentSource(untouched), Is.Null);
            Assert.That(scent.TryTrack(wendigo, untouched), Is.False, "an item nobody held gave a scent");
            entMan.TryGetComponent(wendigo, out CMUWendigoScentTrackerComponent? before);
            Assert.That(before?.Target, Is.Null);

            // (b)
            Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(human, held), Is.True);
            Assert.That(entMan.GetComponent<CMULastHolderComponent>(held).Holder, Is.EqualTo(human));
            Assert.That(scent.TryTrack(wendigo, held), Is.True);
            Assert.That(entMan.GetComponent<CMUWendigoScentTrackerComponent>(wendigo).Target, Is.EqualTo(human));
        });

        await pair.CleanReturnAsync();
    }
}
