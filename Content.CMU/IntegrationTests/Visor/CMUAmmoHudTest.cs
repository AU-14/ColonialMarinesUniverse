using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Visor;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Visor;

[TestFixture]
public sealed class CMUAmmoHudTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static PoolSettings ConnectedSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task RedDotModelsAndAllSmartWeaponsAreCompatible()
    {
        await Server.WaitAssertion(() =>
        {
            var compatible = new[]
            {
                "RMCWeaponRifleM54C", "RMCWeaponRifleM54CMK1Unloaded", "WeaponRifleM4SPR",
                "AU14WeaponRifleXM4RA1", "CMUWeaponRifleXM43", "AU14WeaponRifleF44AA",
                "RMCWeaponRifleSSG45NoLockStripped", "AUWeaponRifleXM20A2HAZOPS",
                "AU14WeaponRifleKramerAR", "CMUWeaponRifleKramerKarbine", "AU14WeaponRifleKramerLSW",
                "AU14WeaponRifleprodigyrifleAR", "AUWeaponRifleM20A", "AU14WeaponRifleM16A2",
                "RMCWeaponRifleABR40Tactical", "AU14WeaponRifleAG80",
                "RMCWeaponRifleType71C", "RuMCWeaponRifleAK13", "RMCWeaponRifleL24",
                "RMCWeaponRifleL90A2", "AU14WeaponRifleL90M", "RMCL112SniperRifle",
                "AU14L64A3SniperRifle", "RMCType88SniperRifle", "RuMcSniperRifleSVAROG",
                "WeaponLauncherM83", "RMCWeaponLauncherM5ATL", "CMUWeaponLauncherEMBLR",
                "AU14WeaponLauncherL164A3", "RMCWeaponSniperM707Vulture", "RMCXM43E1AntiMaterielRifle",
                "RMCSmartGun", "AU14SmartGunUPP", "AU14SmartGunMinigunHAZOPS",
                "RMCWeaponShotgunMK481", "RMCWeaponShotgunType23",
                "RuMCRifleAH12", "RuMCMachineGunPKP_MS", "CMUWeaponRifleM250", "AU14WeaponXM99A",
                "RMCWeaponPistolSU6", "RMCSmartGunCLF", "RMCSmartGunRoyal", "RMCSmartGunRCMPVE",
                "RMCWeaponLauncherDisposable", "RuMCWeaponLauncherDisposableTermobaric",
            };
            var incompatible = new[]
            {
                "CMWeaponPistolM1984", "WeaponSMGM63", "RMCWeaponSMGType64",
                "AU14WeaponSMGL90A2", "WeaponShotgunM42A1", "RMCWeaponTaser",
                "CMUWeaponRifleKramerCivilian", "CMUWeaponRifleKramerEnforcer",
                "AU14WeaponRifleTwoBore", "RMCWeaponFlamerColony", "CMM96SSniperRifle",
                "RMCM96SBSniperRifle", "CMUSniperRifleTWE", "RMCWeaponFlamer", "RMCWeaponFlamerSpec",
                "AU14WeaponFlamerM240A2HAZOPS", "RMCWeaponFL3Flamer", "RMCWeaponLauncherM81",
                "CMUWeaponLauncherM81", "RMCWeaponLauncherHJRA12", "RMCWeaponLMGM60",
                "RMCWeaponLMGQYJ72", "RMCWeaponMar50LMG", "CMUGPMGTWE", "RuMCMachineGunTachanka",
                "RMCWeaponRifleMAR30", "WeaponRifleMAR40", "AU14WeaponRifleMAR40Tactical",
                "AU14WeaponRifleAR10", "WeaponRifleM5SPR2", "CMUWeaponShotgunType23Tactical",
                "WeaponShotgunM890", "RMCWeaponShotgunXM38", "AU14WeaponShotgunSPAS12HAZOPS",
                "RuMCWeaponLauncherDisposableIncen", "RuMCWeaponSMGsPP13M",
                "CMUWeaponShotgunLACNBreacher", "CMUWeaponShotgunLACNPenta",
            };

            foreach (var id in compatible)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<CMUAmmoHudWeaponComponent>(out var hud, SEntMan.ComponentFactory)
                            && hud.Enabled, Is.True, id);
            }

            foreach (var id in incompatible)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<CMUAmmoHudWeaponComponent>(out var hud, SEntMan.ComponentFactory)
                            && hud.Enabled, Is.False, id);
            }

            foreach (var prototype in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract ||
                    !prototype.Name.Contains("smart", StringComparison.OrdinalIgnoreCase) ||
                    !prototype.TryComp<GunComponent>(out _, SEntMan.ComponentFactory))
                {
                    continue;
                }

                Assert.That(prototype.TryComp<CMUAmmoHudWeaponComponent>(out var hud, SEntMan.ComponentFactory)
                            && hud.Enabled, Is.True, $"{prototype.ID}: every smart weapon must support the HUD.");
            }
        });
    }

    [Test]
    public async Task IndicatorFreeWeaponIsRejectedButSmartPistolUsesTheOptic()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hands = Server.System<SharedHandsSystem>();
            var inventory = Server.System<InventorySystem>();
            var hud = Server.System<CMUAmmoHudSystem>();
            var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var sniper = SEntMan.SpawnEntity("CMM96SSniperRifle", map.GridCoords);
            SEntMan.EnsureComponent<CMUSquadVisorWearerComponent>(marine);
            Assert.That(inventory.TryEquip(marine, sniper, "suitstorage", force: true), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out _), Is.False);
            Assert.That(hands.TryPickupAnyHand(marine, sniper, checkActionBlocker: false), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out _), Is.False);

            var smartPistol = SEntMan.SpawnEntity("RMCWeaponPistolSU6", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(marine, smartPistol, checkActionBlocker: false), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(smartPistol));
            SEntMan.RemoveComponent<CMUSquadVisorWearerComponent>(marine);
            Assert.That(hud.TryGetWeapon(marine, out _), Is.False, "Smart weapons still require a visor or sight.");
        });
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task HeldWeaponsTakePriorityOverSuitStorageAndRequireAnOptic()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hands = Server.System<SharedHandsSystem>();
            var inventory = Server.System<InventorySystem>();
            var hud = Server.System<CMUAmmoHudSystem>();
            var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var stored = SEntMan.SpawnEntity("RMCWeaponRifleM54CMK1Unloaded", map.GridCoords);
            Assert.That(inventory.TryEquip(marine, stored, "suitstorage", force: true), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out _), Is.False, "A gun alone must not grant an ammo HUD.");

            SEntMan.EnsureComponent<CMUSquadVisorWearerComponent>(marine);
            Assert.That(hud.TryGetWeapon(marine, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(stored));

            var pistol = SEntMan.SpawnEntity("CMWeaponPistolM1984", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(marine, pistol, checkActionBlocker: false), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out selected), Is.True);
            Assert.That(selected, Is.EqualTo(stored), "An ineligible active gun must not hide a stored rifle.");

            var rifle = SEntMan.SpawnEntity("WeaponRifleM4SPR", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(marine, rifle, checkActionBlocker: false), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out selected), Is.True);
            Assert.That(selected, Is.EqualTo(rifle), "An eligible off-hand gun takes priority over suit storage.");

            Assert.That(hands.TryDrop(marine, pistol, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(marine, stored, checkActionBlocker: false), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out selected), Is.True);
            Assert.That(selected, Is.EqualTo(stored), "The active hand takes priority when both guns qualify.");

            var toggle = new CMUToggleSquadVisorAmmoHudActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(marine, toggle);
            Assert.That(hud.TryGetWeapon(marine, out _), Is.False, "The ammo HUD toggle still applies.");

            SEntMan.RemoveComponent<CMUSquadVisorWearerComponent>(marine);
            var sight = SEntMan.SpawnEntity("RMCGlassesSmartGunSight", map.GridCoords);
            Assert.That(inventory.TryEquip(marine, sight, "eyes", force: true), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out selected), Is.True, "The existing smartgun sight remains supported.");
            Assert.That(selected, Is.EqualTo(stored));

            Assert.That(hands.TryDrop(marine, stored, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryDrop(marine, rifle, checkActionBlocker: false), Is.True);
            Assert.That(hud.TryGetWeapon(marine, out _), Is.False, "Dropped guns are not HUD candidates.");

            var ammo = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(stored, ref ammo);
            Assert.That(ammo.Count, Is.Zero, "An unloaded eligible gun must still be selected and show zero.");
        });
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    [PairConfig(nameof(ConnectedSettings))]
    public async Task ClientReadsSuitStoredAmmoAndWieldedWeaponWithoutAStandaloneCounter()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid marine = default;
        EntityUid rifle = default;
        NetEntity marineNet = default;
        NetEntity rifleNet = default;
        var expectedAmmo = 0;

        try
        {
            await Server.WaitAssertion(() =>
            {
                marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                rifle = SEntMan.SpawnEntity("WeaponRifleM4SPRFilled", map.GridCoords);
                SEntMan.RemoveComponent<Content.Server.Weapons.Ranged.Components.AmmoCounterComponent>(rifle);
                SEntMan.EnsureComponent<CMUSquadVisorWearerComponent>(marine);
                Assert.That(Server.System<InventorySystem>().TryEquip(marine, rifle, "suitstorage", force: true), Is.True);
                Server.PlayerMan.SetAttachedEntity(session, marine);
                marineNet = SEntMan.GetNetEntity(marine);
                rifleNet = SEntMan.GetNetEntity(rifle);
                var ammo = new GetAmmoCountEvent();
                SEntMan.EventBus.RaiseLocalEvent(rifle, ref ammo);
                expectedAmmo = ammo.Count;
                Assert.That(expectedAmmo, Is.GreaterThan(0));
            });
            await Pair.RunTicksSync(10);

            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<CMUAmmoHudSystem>().TryGetWeapon(CEntMan.GetEntity(marineNet), out var selected), Is.True);
                Assert.That(CEntMan.GetNetEntity(selected), Is.EqualTo(rifleNet));
                Assert.That(CEntMan.HasComponent<Content.Client.Weapons.Ranged.Components.AmmoCounterComponent>(selected),
                    Is.False, "The visor HUD must not grant or require a standalone gun counter.");
                var ammo = new GetAmmoCountEvent();
                CEntMan.EventBus.RaiseLocalEvent(selected, ref ammo);
                Assert.That(ammo.Count, Is.EqualTo(expectedAmmo), "Suit-stored magazine contents must reach the HUD client.");
            });

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(marine, rifle, checkActionBlocker: false), Is.True);
                Assert.That(Server.System<SharedWieldableSystem>().TryWield(rifle, marine), Is.True);
            });
            await Pair.RunTicksSync(10);

            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<CMUAmmoHudSystem>().TryGetWeapon(CEntMan.GetEntity(marineNet), out var selected), Is.True);
                Assert.That(CEntMan.GetNetEntity(selected), Is.EqualTo(rifleNet));
                var ammo = new GetAmmoCountEvent();
                CEntMan.EventBus.RaiseLocalEvent(selected, ref ammo);
                Assert.That(ammo.Count, Is.EqualTo(expectedAmmo));
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                Server.System<SharedMapSystem>().DeleteMap(map.MapId);
            });
        }
    }
}
