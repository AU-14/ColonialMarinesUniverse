#pragma warning disable RA0002 // setup pokes gun cooldown/aim and the shooter's view directly

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberEighteenGearRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: CMUOct18TestLauncher
  components:
  - type: Gun
    projectileSpeed: 20
    selectedMode: SemiAuto
    availableModes: [SemiAuto]
  - type: BallisticAmmoProvider
    capacity: 1
    whitelist:
      components:
      - Ammo
  - type: ShootAtFixedPoint
    shootArcProj: true
    maxFixedRange: 7

- type: entity
  parent: BaseItem
  id: CMUOct18TestShell
  components:
  - type: Ammo
";

    // max range fires the trigger with no key and the scatter payload only listened for "timer",
    // so the round got deleted with no flare ever coming out
    [Test]
    public async Task FlareRoundDropsALitFlareAtMaxRange()
    {
        var map = await Pair.CreateTestMap();
        EntityUid bullet = default;
        await Server.WaitPost(() =>
        {
            bullet = SEntMan.SpawnEntity("RMCFlareBullet", map.GridCoords);
            var ev = new ProjectileFixedDistanceStopEvent();
            SEntMan.EventBus.RaiseLocalEvent(bullet, ref ev);
        });

        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(bullet), Is.True, "the round should be used up");

            var flares = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID != "CMFlare" || xform.MapID != map.MapId)
                    continue;

                flares++;
                SEntMan.DeleteEntity(uid);
            }

            Assert.That(flares, Is.EqualTo(1), "a flare should land where the round stopped");
        });
    }
}
