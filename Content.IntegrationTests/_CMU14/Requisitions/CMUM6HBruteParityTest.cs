#nullable enable

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Brute;
using Content.Shared._RMC14.Wieldable.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Projectiles;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Requisitions;

[TestFixture]
public sealed class CMUM6HBruteParityTest : GameTest
{
    private static readonly EntProtoId Crate = "RMCCrateM6HBruteRockets";
    private static readonly EntProtoId Rig = "RMCM271A2BruteLauncherRig";
    private static readonly EntProtoId FilledRig = "RMCM271A2BruteLauncherRigFilled";
    private static readonly EntProtoId Launcher = "RMCWeaponLauncherM6HBrute";
    private static readonly EntProtoId Rocket = "RMCRocketM5510Brute";
    private static readonly EntProtoId Projectile = "RMCProjectileM5510Brute";
    private static readonly ResPath BruteTargetedRsi = new("/Textures/_CMU14/Effects/targeted_brute.rsi");
    private static readonly ResPath DefaultTargetedRsi = new("/Textures/_RMC14/Effects/targeted.rsi");
    private const string GuidedLockOnState = "sniper_lockon_guided";
    private const string GuidedLockOnDirectionState = "sniper_lockon_guided_direction";
    private const string DefaultLockOnState = "sniper_lockon";
    private const string DefaultLockOnDirectionState = "sniper_lockon_direction";

    private static readonly ExpectedRsi[] RequiredRsi =
    [
        new(
            new ResPath("/Textures/_CMU14/Objects/Clothing/Back/Backpacks/Marines/brute_rig.rsi"),
            ["icon", "equipped-BACKPACK", "open", "closed"]),
        new(
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/Ammunition/Explosives/m5510_brute.rsi"),
            ["brute_rocket"]),
        new(
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/Ammunition/Projectiles/m5510_brute.rsi"),
            ["brute"]),
        new(
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/RocketLaunchers/m6h/m6h_icon.rsi"),
            ["base", "icon"]),
        new(
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/RocketLaunchers/m6h/m6h_inhands.rsi"),
            [
                "wielded-inhand-left",
                "inhand-left",
                "inhand-right",
                "wielded-inhand-right",
                "equipped-BACKPACK",
                "equipped-SUITSTORAGE",
            ]),
        new(new ResPath("/Textures/_CMU14/Effects/beam_brute.rsi"), ["laser_beam_guided"]),
        new(
            BruteTargetedRsi,
            [
                "sniper_lockon",
                "spotter_lockon",
                "sniper_lockon_intense",
                "sniper_lockon_direction",
                "sniper_lockon_intense_direction",
                "sniper_lockon_guided",
                "sniper_lockon_guided_direction",
            ]),
    ];

    private static readonly ExpectedSprite[] ExpectedSprites =
    [
        new(
            Rig,
            new ResPath("/Textures/_CMU14/Objects/Clothing/Back/Backpacks/Marines/brute_rig.rsi"),
            "icon"),
        new(
            Launcher,
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/RocketLaunchers/m6h/m6h_icon.rsi"),
            "base"),
        new(
            Rocket,
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/Ammunition/Explosives/m5510_brute.rsi"),
            "brute_rocket"),
        new(
            Projectile,
            new ResPath("/Textures/_CMU14/Objects/Weapons/Guns/Ammunition/Projectiles/m5510_brute.rsi"),
            "brute"),
    ];

    private static readonly EntProtoId[] RequiredEffects =
    [
        "RMCTileFireBrute",
        "RMCBruteSparks",
        "RMCBruteSmoke",
    ];

    private static readonly ProtoId<TagPrototype>[] RequiredTags =
    [
        "RMCM271A2BruteLauncherRig",
        "RMCWeaponLauncherM6HBrute",
        "RMCRocketAmmoM5510Brute",
    ];

    [Test]
    public async Task CrateAndPayloadResolveHistoricalBehavior()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var requiredEntities = new[] { Crate, Rig, FilledRig, Launcher, Rocket, Projectile }
                .Concat(RequiredEffects);

            Assert.Multiple(() =>
            {
                foreach (var id in requiredEntities)
                {
                    Assert.That(
                        SProtoMan.TryIndex<EntityPrototype>(id, out _),
                        Is.True,
                        $"Missing historical M6H-BRUTE entity {id}");
                }

                foreach (var id in RequiredTags)
                {
                    Assert.That(
                        SProtoMan.TryIndex<TagPrototype>(id, out _),
                        Is.True,
                        $"Missing historical M6H-BRUTE tag {id}");
                }
            });

            var crate = SProtoMan.Index<EntityPrototype>(Crate);
            var rig = SProtoMan.Index<EntityPrototype>(Rig);
            var filledRig = SProtoMan.Index<EntityPrototype>(FilledRig);
            var launcher = SProtoMan.Index<EntityPrototype>(Launcher);
            var rocket = SProtoMan.Index<EntityPrototype>(Rocket);
            var projectile = SProtoMan.Index<EntityPrototype>(Projectile);
            var bruteFire = SProtoMan.Index<EntityPrototype>(RequiredEffects[0]);

            Assert.Multiple(() =>
            {
                Assert.That(crate.Parents, Is.EqualTo(new[] { "RMCCrateExplosives" }));
                AssertStorageFill(crate, factory, [new ExpectedContent(Rocket, 6)]);

                Assert.That(filledRig.Parents, Is.EqualTo(new[] { Rig.Id }));
                AssertStorageFill(
                    filledRig,
                    factory,
                    [new ExpectedContent(Launcher, 1), new ExpectedContent(Rocket, 3)]);

                Assert.That(rig.Parents, Is.EqualTo(new[] { "CMBackpack" }));
                Assert.That(rig.TryComp<StorageComponent>(out var storage, factory), Is.True);
                Assert.That(storage?.MaxItemSize, Is.EqualTo((ProtoId<ItemSizePrototype>) "Huge"));
                Assert.That(storage?.Grid, Is.EqualTo(new[] { new Box2i(0, 0, 15, 1) }));
                Assert.That(
                    storage?.Whitelist?.Tags,
                    Is.EquivalentTo(new ProtoId<TagPrototype>[]
                    {
                        "RMCWeaponLauncherM6HBrute",
                        "RMCRocketAmmoM5510Brute",
                    }));
                Assert.That(rig.TryComp<CMHolsterComponent>(out var holster, factory), Is.True);
                Assert.That(
                    holster?.Whitelist?.Tags,
                    Is.EquivalentTo(new ProtoId<TagPrototype>[] { "RMCWeaponLauncherM6HBrute" }));
                AssertTag(rig, factory, "RMCM271A2BruteLauncherRig");

                Assert.That(
                    launcher.Parents,
                    Is.EqualTo(new[] { "BaseWeaponLauncher", "CMBaseWeaponGun", "RMCBaseAttachableHolder" }));
                Assert.That(
                    launcher.TryComp<RMCBruteLauncherComponent>(out _, factory),
                    Is.True);
                Assert.That(
                    launcher.TryComp<RMCBackblastOnShootComponent>(out _, factory),
                    Is.True);
                Assert.That(
                    launcher.TryComp<AssistedReloadWeaponComponent>(out _, factory),
                    Is.True);

                Assert.That(launcher.TryComp<GunComponent>(out var gun, factory), Is.True);
                Assert.That(gun?.FireRate, Is.EqualTo(0.83f));
                Assert.That(gun?.ResetOnHandSelected, Is.False);

                Assert.That(
                    launcher.TryComp<GunRequiresSkillsComponent>(out var skills, factory),
                    Is.True);
                var skillLevel = 0;
#pragma warning disable RA0002 // This parity test intentionally inspects historical skill configuration.
                Assert.That(
                    skills != null && skills.Skills.TryGetValue("RMCSkillEngineer", out skillLevel),
                    Is.True);
#pragma warning restore RA0002
                Assert.That(skillLevel, Is.EqualTo(3));

                Assert.That(
                    launcher.TryComp<WieldDelayComponent>(out var wieldDelay, factory),
                    Is.True);
                Assert.That(wieldDelay?.BaseDelay, Is.EqualTo(TimeSpan.FromSeconds(1.2)));
                Assert.That(wieldDelay?.PreventFiring, Is.True);

                Assert.That(
                    launcher.TryComp<ExplosionResistanceComponent>(out var resistance, factory),
                    Is.True);
                Assert.That(resistance?.DamageCoefficient, Is.Zero);

                Assert.That(
                    launcher.TryComp<MeleeWeaponComponent>(out var melee, factory),
                    Is.True);
                Assert.That(melee?.Damage.DamageDict["Blunt"], Is.EqualTo((FixedPoint2) 15));

                Assert.That(
                    launcher.TryComp<BallisticAmmoProviderComponent>(out var provider, factory),
                    Is.True);
                Assert.That(provider?.Proto, Is.EqualTo(Rocket));
                Assert.That(provider?.Capacity, Is.EqualTo(1));
                Assert.That(provider?.Cycleable, Is.True);
                Assert.That(provider?.MayTransfer, Is.False);
                Assert.That(provider?.InsertDelay, Is.EqualTo(TimeSpan.FromSeconds(6)));
                Assert.That(provider?.CycleDelay, Is.EqualTo(TimeSpan.FromSeconds(6)));
                Assert.That(
                    provider?.Whitelist?.Tags,
                    Is.EqualTo(new ProtoId<TagPrototype>[] { "RMCRocketAmmoM5510Brute" }));

                Assert.That(
                    launcher.TryComp<TargetingLaserComponent>(out var targetingLaser, factory),
                    Is.True);
                Assert.That(targetingLaser?.LaserState, Is.EqualTo("laser_beam_guided"));
                Assert.That(
                    targetingLaser?.RsiPath,
                    Is.EqualTo(new ResPath("/Textures/_CMU14/Effects/beam_brute.rsi")));
                Assert.That(targetingLaser?.LaserAlpha, Is.EqualTo(0.5f));
                Assert.That(targetingLaser?.GradualAlpha, Is.True);

                Assert.That(rocket.TryComp<CartridgeAmmoComponent>(out var cartridge, factory), Is.True);
                Assert.That(cartridge?.Prototype, Is.EqualTo(Projectile));
                Assert.That(cartridge?.DeleteOnSpawn, Is.True);
                Assert.That(
                    rocket.TryComp<AssistedReloadAmmoComponent>(out _, factory),
                    Is.True);
                AssertTag(rocket, factory, "RMCRocketAmmoM5510Brute");

                Assert.That(
                    projectile.TryComp<ProjectileComponent>(out var projectileComponent, factory),
                    Is.True);
                Assert.That(projectileComponent?.DeleteOnCollide, Is.False);
                Assert.That(projectileComponent?.ImpactEffect, Is.EqualTo((EntProtoId) "BulletImpactEffect"));
                Assert.That(projectileComponent?.MaxFixedRange, Is.EqualTo(6));
                Assert.That(
                    projectileComponent?.Damage.DamageDict["Blunt"],
                    Is.EqualTo((FixedPoint2) 15));
                Assert.That(
                    projectile.TryComp<RMCBruteProjectileComponent>(out _, factory),
                    Is.True);
                Assert.That(
                    projectile.TryComp<RMCProjectileSkipXenosComponent>(out _, factory),
                    Is.True);
                Assert.That(
                    projectile.TryComp<RMCProjectileAccuracyComponent>(out var accuracy, factory),
                    Is.True);
#pragma warning disable RA0002 // This parity test intentionally inspects historical projectile configuration.
                Assert.That(accuracy?.Accuracy.Int(), Is.EqualTo(95));
                Assert.That(accuracy?.Thresholds, Has.Count.EqualTo(1));
                Assert.That(accuracy?.Thresholds[0].Range, Is.EqualTo(7));
                Assert.That(accuracy?.Thresholds[0].Falloff.Int(), Is.EqualTo(10));
#pragma warning restore RA0002

                Assert.That(
                    projectile.TryComp<RMCBruteProjectileComponent>(out var brute, factory),
                    Is.True);
                Assert.That(brute?.FirePrototype, Is.EqualTo((EntProtoId) "RMCTileFireBrute"));
                Assert.That(brute?.SparkPrototype, Is.EqualTo((EntProtoId) "RMCBruteSparks"));
                Assert.That(brute?.SmokePrototype, Is.EqualTo((EntProtoId) "RMCBruteSmoke"));

                Assert.That(bruteFire.TryComp<TileFireComponent>(out var tileFire, factory), Is.True);
                Assert.That(tileFire?.Id, Is.EqualTo((EntProtoId<TileFireComponent>) "RMCTileFireBrute"));
                Assert.That(tileFire?.Duration, Is.EqualTo(TimeSpan.FromSeconds(1)));

                Assert.That(
                    bruteFire.TryComp<RMCIgniteOnCollideComponent>(out var ignite, factory),
                    Is.True);
                Assert.That(ignite?.MaxStacks, Is.EqualTo(1));
                Assert.That(ignite?.Intensity, Is.EqualTo(10));
                Assert.That(ignite?.Duration, Is.EqualTo(1));
                Assert.That(ignite?.TileDamage?.DamageDict["Heat"], Is.EqualTo((FixedPoint2) 0.5f));
                Assert.That(ignite?.BurnColor, Is.EqualTo(Color.FromHex("#00FF00")));
            });
        });
    }

    [Test]
    public async Task SpriteStatesResolveHistoricalArt()
    {
        await Client.WaitAssertion(() =>
        {
            var resourceCache = Client.ResolveDependency<IResourceCache>();
            var spriteSystem = CEntMan.System<SpriteSystem>();

            Assert.Multiple(() =>
            {
                foreach (var expected in RequiredRsi)
                {
                    var rsi = resourceCache.GetResource<RSIResource>(expected.Path).RSI;
                    foreach (var state in expected.States)
                    {
                        Assert.That(
                            rsi.TryGetState(state, out _),
                            Is.True,
                            $"Historical M6H-BRUTE RSI {expected.Path} is missing state {state}");
                    }
                }

                foreach (var expected in ExpectedSprites)
                {
                    var uid = CEntMan.SpawnEntity(expected.Id, MapCoordinates.Nullspace);
                    try
                    {
                        var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                        var hasLayer = spriteSystem.TryGetLayer((uid, sprite), 0, out var layer, false);
                        Assert.That(hasLayer, Is.True, $"{expected.Id} has no base sprite layer");
                        if (!hasLayer || layer == null)
                            continue;

                        Assert.That(
                            layer.ActualRsi?.Path,
                            Is.EqualTo(expected.Path),
                            $"{expected.Id} uses the wrong historical RSI");
                        Assert.That(
                            spriteSystem.LayerGetRsiState((uid, sprite), 0).Name,
                            Is.EqualTo(expected.State),
                            $"{expected.Id} uses the wrong historical sprite state");
                    }
                    finally
                    {
                        CEntMan.DeleteEntity(uid);
                    }
                }

                var fire = CEntMan.SpawnEntity(RequiredEffects[0], MapCoordinates.Nullspace);
                try
                {
                    Assert.That(CEntMan.TryGetComponent(fire, out PointLightComponent? light), Is.True);
                    Assert.That(light?.Color, Is.EqualTo(Color.FromHex("#069420")));
                }
                finally
                {
                    CEntMan.DeleteEntity(fire);
                }
            });
        });
    }

    [Test]
    public async Task GuidedTargetingStartupUsesCmuOwnedArt()
    {
        await Client.WaitAssertion(() =>
        {
            var launcher = CEntMan.SpawnEntity(Launcher, MapCoordinates.Nullspace);
            var target = CEntMan.CreateEntityUninitialized(null, MapCoordinates.Nullspace);
            try
            {
                var targeted = CEntMan.EnsureComponent<RMCTargetedComponent>(target);
                targeted.TargetedBy.Add(launcher);
                CEntMan.InitializeAndStartEntity(target);

                AssertGuidedTargeting(targeted, "ComponentStartup");
            }
            finally
            {
                CEntMan.DeleteEntity(target);
                CEntMan.DeleteEntity(launcher);
            }
        });
    }

    [Test]
    public async Task GuidedTargetingNetworkStateSelectsAndRestoresCmuOwnedArt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid serverLauncher = default;
        EntityUid serverTarget = default;
        NetEntity targetNet = default;

        await Server.WaitAssertion(() =>
        {
            serverLauncher = SEntMan.SpawnEntity(Launcher, map.GridCoords);
            serverTarget = SEntMan.SpawnEntity(null, map.GridCoords);
            targetNet = SEntMan.GetNetEntity(serverTarget);

            var targeted = SEntMan.EnsureComponent<RMCTargetedComponent>(serverTarget);
            targeted.TargetedBy.Add(serverLauncher);
            SEntMan.Dirty(serverTarget, targeted);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var clientTarget = CEntMan.GetEntity(targetNet);
            AssertGuidedTargeting(
                CEntMan.GetComponent<RMCTargetedComponent>(clientTarget),
                "server-to-client component state");
        });

        await Server.WaitAssertion(() =>
        {
            var targeted = SEntMan.GetComponent<RMCTargetedComponent>(serverTarget);
            targeted.TargetedBy.Clear();
            SEntMan.Dirty(serverTarget, targeted);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var clientTarget = CEntMan.GetEntity(targetNet);
            var targeted = CEntMan.GetComponent<RMCTargetedComponent>(clientTarget);
            Assert.Multiple(() =>
            {
                Assert.That(targeted.RsiPath, Is.EqualTo(DefaultTargetedRsi));
                Assert.That(targeted.LockOnState, Is.EqualTo(DefaultLockOnState));
                Assert.That(
                    targeted.LockOnStateDirection,
                    Is.EqualTo(DefaultLockOnDirectionState));
            });
        });

        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(serverTarget);
            SEntMan.DeleteEntity(serverLauncher);
        });
        await Pair.RunUntilSynced();
    }

    private static void AssertGuidedTargeting(RMCTargetedComponent targeted, string eventName)
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                targeted.RsiPath,
                Is.EqualTo(BruteTargetedRsi),
                $"{eventName} did not select the CMU-owned guided art");
            Assert.That(targeted.LockOnState, Is.EqualTo(GuidedLockOnState));
            Assert.That(
                targeted.LockOnStateDirection,
                Is.EqualTo(GuidedLockOnDirectionState));
        });
    }

    private static void AssertStorageFill(
        EntityPrototype prototype,
        IComponentFactory factory,
        ExpectedContent[] expected)
    {
        Assert.That(prototype.TryComp<StorageFillComponent>(out var fill, factory), Is.True);
        var actual = fill?.Contents
            .Select(entry => new ExpectedContent(
                entry.PrototypeId?.Id ?? string.Empty,
                entry.Amount,
                entry.SpawnProbability,
                entry.MaxAmount,
                entry.GroupId))
            .ToArray();
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    private static void AssertTag(
        EntityPrototype prototype,
        IComponentFactory factory,
        ProtoId<TagPrototype> expected)
    {
        Assert.That(prototype.TryComp<TagComponent>(out var tags, factory), Is.True);
        Assert.That(tags?.Tags, Does.Contain(expected));
    }

    private sealed record ExpectedContent(
        EntProtoId Id,
        int Amount,
        float SpawnProbability = 1,
        int MaxAmount = 1,
        string? GroupId = null);

    private sealed record ExpectedRsi(ResPath Path, string[] States);

    private sealed record ExpectedSprite(EntProtoId Id, ResPath Path, string State);
}
