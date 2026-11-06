using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Visor;
using Content.Shared.Buckle;
using Content.Shared.CombatMode;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDoorGunIFFPopupTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: CMMobHuman
  id: CMUTestDoorGunPopupUser
  components:
  - type: UserIFF
    factions: [ FactionMarine ]

- type: entity
  id: CMUTestDoorGunPopupFriendly
  components:
  - type: UserIFF
    factions: [ FactionMarine ]
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape: !type:PhysShapeAabb
          bounds: "-0.35,-0.35,0.35,0.35"
        layer:
        - BulletImpassable

- type: entity
  parent: ArmorHelmetM10
  id: CMUTestDoorGunPopupHelmet
  components:
  - type: IntegratedVisors
    visorsToAdd:
    - CMUVisorGunshipCrewAdvanced
""";

    [TestCase(false)]
    [TestCase(true)]
    public async Task LockoutPopupArrivesWithoutLeavingTheGun(bool gunshipIff)
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default, weapon = default;
        NetEntity userNet = default;
        var coordinates = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var message = string.Empty;
        await Server.WaitAssertion(() =>
        {
            message = Loc.GetString(gunshipIff ? "cmu-gunship-crew-iff-blocked" : "rmc-iff-friendly-in-line");
            for (var x = 1; x <= 4; x++)
                SEntMan.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid.Owner);
            var mount = SEntMan.SpawnEntity(gunshipIff ? "CMUM2CDoorGunDropship" : "RMCML66DNestDropship", coordinates);
            user = SEntMan.SpawnEntity("CMUTestDoorGunPopupUser", coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            SEntMan.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 1);
            SEntMan.System<SkillsSystem>().SetSkill(user, "CMUSkillAircrew", 1);
            SEntMan.System<SharedCombatModeSystem>().SetInCombatMode(user, true);
            if (gunshipIff)
            {
                var helmet = SEntMan.SpawnEntity("CMUTestDoorGunPopupHelmet", coordinates);
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(user, helmet, "head", force: true), Is.True);
                SEntMan.EventBus.RaiseLocalEvent(helmet, new CycleVisorActionEvent { Performer = user });
            }

            SEntMan.System<SharedTransformSystem>().SetLocalRotation(mount, Direction.East.ToAngle());
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, mount), Is.True);
            weapon = SEntMan.GetComponent<WeaponMountComponent>(mount).MountedEntity!.Value;
            SEntMan.SpawnEntity(gunshipIff ? "WallSolid" : "CMUTestDoorGunPopupFriendly", coordinates.Offset(new Vector2(4, 0)));
            userNet = SEntMan.GetNetEntity(user);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => CEntMan.System<CMUDoorGunPopupProbeSystem>().Popups.Clear());
        await Server.WaitAssertion(() =>
        {
            var gun = SEntMan.GetComponent<GunComponent>(weapon);
            Assert.That(SEntMan.System<SharedGunSystem>().AttemptShoot(user, (weapon, gun), coordinates.Offset(new Vector2(8, 0))), Is.False);
        });
        await Pair.RunTicksSync(2);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var popups = CEntMan.System<CMUDoorGunPopupProbeSystem>().Popups.Where(p => p.Message == message).ToArray();
            Assert.That(popups, Has.Length.EqualTo(1));
            Assert.That(popups[0].Uid, Is.EqualTo(userNet), "The contained weapon must not be the popup anchor.");
            Assert.That(CEntMan.System<Content.Client.Popups.PopupSystem>().WorldLabels.Any(p => p.Text.Contains(message)), Is.True);
        });
    }
}

public sealed partial class CMUDoorGunPopupProbeSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    public readonly List<PopupEntityEvent> Popups = new();

    public override void Initialize()
    {
        if (_net.IsClient)
            SubscribeNetworkEvent<PopupEntityEvent>(OnPopup);
    }

    private void OnPopup(PopupEntityEvent args) => Popups.Add(args);
}
