#nullable enable

using System.Collections.Generic;
using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunSystem))]
public sealed class SpentCartridgeTrashTest
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CartridgePistol
          id: TestCartridgePistolNoTrash
          components:
          - type: CartridgeAmmo
            proto: BulletPistol
            markSpentAsTrash: false

        - type: entity
          parent: CartridgePistol
          id: TestCartridgePistolInitiallySpentTrash
          components:
          - type: CartridgeAmmo
            proto: BulletPistol
            spent: true
          - type: Tag
            tags:
            - CartridgePistol
            - Trash

        - type: entity
          parent: WeaponRevolverPirateEmpty
          id: TestRevolverTrashReset
          components:
          - type: RevolverAmmoProvider
            proto: TestCartridgePistolInitiallySpentTrash
            capacity: 1
            chambers: [ true ]
            ammoSlots: [ null ]
        """;

    [Test]
    public async Task CartridgeTrashTagTracksSpentState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var gun = server.System<GunSystem>();
        var tags = server.System<TagSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var gunUid = entMan.SpawnEntity("WeaponPistolMk58", map.GridCoords);
            var gunComponent = entMan.GetComponent<GunComponent>(gunUid);
            var target = map.GridCoords.Offset(Vector2.UnitX);

            var normal = entMan.SpawnEntity("CartridgePistol", map.GridCoords);
            gun.Shoot(gunUid, gunComponent, normal, map.GridCoords, target, out _);

            var normalComponent = entMan.GetComponent<CartridgeAmmoComponent>(normal);
            Assert.Multiple(() =>
            {
                Assert.That(normalComponent.Spent, Is.True);
                Assert.That(normalComponent.MarkSpentAsTrash, Is.True);
                Assert.That(tags.HasTag(normal, TrashTag), Is.True);
            });

            var optedOut = entMan.SpawnEntity("TestCartridgePistolNoTrash", map.GridCoords);
            gun.Shoot(gunUid, gunComponent, optedOut, map.GridCoords, target, out _);

            var optedOutComponent = entMan.GetComponent<CartridgeAmmoComponent>(optedOut);
            Assert.Multiple(() =>
            {
                Assert.That(optedOutComponent.Spent, Is.True);
                Assert.That(optedOutComponent.MarkSpentAsTrash, Is.False);
                Assert.That(tags.HasTag(optedOut, TrashTag), Is.False);
            });

            var preSpent = entMan.SpawnEntity("CartridgePistolSpent", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(preSpent).Spent, Is.True);
                Assert.That(tags.HasTag(preSpent, TrashTag), Is.True);
            });

            var revolver = entMan.SpawnEntity("TestRevolverTrashReset", map.GridCoords);
            var revolverComponent = entMan.GetComponent<RevolverAmmoProviderComponent>(revolver);
            var beforeReset = GetCartridges(entMan);

            gun.EmptyRevolver(revolver, revolverComponent);

            var afterReset = GetCartridges(entMan);
            afterReset.ExceptWith(beforeReset);

            Assert.That(afterReset, Has.Count.EqualTo(1));
            var reset = afterReset.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(reset).Spent, Is.False);
                Assert.That(tags.HasTag(reset, TrashTag), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static HashSet<EntityUid> GetCartridges(IEntityManager entMan)
    {
        var result = new HashSet<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<CartridgeAmmoComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            result.Add(uid);
        }

        return result;
    }
}
