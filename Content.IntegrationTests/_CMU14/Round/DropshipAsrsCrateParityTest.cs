#nullable enable

using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Scorch;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Effect;
using Content.Shared._RMC14.Smoke;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class DropshipAsrsCrateParityTest : GameTest
{
    private static readonly EntProtoId JdamParticle = "AU14EffectExplosionParticleJDAM";
    private static readonly EntProtoId JdamSmoke = "AU14SmokeExplosionProofJDAM";

    private static readonly ExpectedCrate[] ExpectedCrates =
    [
        new(
            "AU14DropshipAttachmentAmmoRocketThermobaricTwo",
            "chubby",
            new ResPath("/Textures/_CMU14/Objects/dropship_ammo_64.rsi"),
            1,
            0,
            5,
            450,
            null,
            new ExpectedImplosion(4, 2.5f, 16),
            new ExpectedFire("RMCTileFireThermobaric", 1, 2, 2),
            ["RMCSmokeExplosionProofSmall"],
            new ResPath("/Audio/_RMC14/Effects/rocketpod_fire.ogg"),
            true),
        new(
            "AU14DropshipAttachmentAmmoMk102JDAM",
            "jdam",
            new ResPath("/Textures/_CMU14/Objects/dropship_ammo_64.rsi"),
            1,
            0,
            12,
            7500,
            new ExpectedExplosion(10000, 4.5f, 100000000),
            null,
            null,
            [JdamParticle]),
        new(
            "AU14DropshipAttachmentAmmoGBU89I",
            "jdami",
            new ResPath("/Textures/_CMU14/Objects/dropship_ammo_64.rsi"),
            1,
            0,
            11,
            8000,
            new ExpectedExplosion(10000, 4.5f, 100000000),
            null,
            new ExpectedFire("RMCTileFireNapalm", 3, 5, 0),
            [JdamParticle, JdamSmoke]),
        new(
            "AU14DropshipAttachmentAmmoCBU250",
            "cluster",
            new ResPath("/Textures/_CMU14/Objects/dropship_ammo_64.rsi"),
            12,
            17,
            6,
            400,
            new ExpectedExplosion(110, 4, 8),
            null,
            null,
            ["RMCEffectExplosionParticle", "RMCSmokeExplosionProofSmall"]),
        new(
            "AU14DropshipAttachmentAmmoRocketWidowmakerDouble",
            "double",
            new ResPath("/Textures/_RMC14/Objects/dropship_ammo_64.rsi"),
            2,
            9,
            3,
            900,
            new ExpectedExplosion(3000, 5.5f, 55),
            null,
            null,
            []),
        new(
            "AU14DropshipAttachmentAmmoCBU607",
            "sgw",
            new ResPath("/Textures/_CMU14/Objects/dropship_ammo_64.rsi"),
            6,
            5,
            4,
            500,
            new ExpectedExplosion(100, 12, 11),
            null,
            null,
            ["RMCEffectExplosionParticle", "RMCSmokeExplosionProofSmall"]),
    ];

    [Test]
    public async Task CratesKeepHistoricalFillAndEffects()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var expected in ExpectedCrates)
                {
                    var found = SProtoMan.TryIndex<EntityPrototype>(expected.Id, out var crate);
                    Assert.That(found, Is.True, $"Missing historical dropship ASRS crate {expected.Id}");
                    if (!found)
                        continue;

                    Assert.That(
                        crate!.Parents,
                        Is.EqualTo(new[] { "RMCDropshipAttachmentAmmoRocket" }),
                        $"{expected.Id} changed its historical parent");

                    var hasAmmo = crate.TryComp<DropshipAmmoComponent>(out var ammo, factory);
                    Assert.That(hasAmmo, Is.True, $"{expected.Id} has no DropshipAmmo component");
                    if (!hasAmmo)
                        continue;

                    Assert.That(ammo!.Rounds, Is.EqualTo(expected.Rounds), $"{expected.Id} changed its fill");
                    Assert.That(ammo.MaxRounds, Is.EqualTo(expected.Rounds), $"{expected.Id} changed its capacity");
                    Assert.That(ammo.RoundsPerShot, Is.EqualTo(expected.Rounds), $"{expected.Id} changed its salvo size");
                    Assert.That(ammo.BulletSpread, Is.EqualTo(expected.BulletSpread), $"{expected.Id} changed its spread");
                    Assert.That(ammo.TravelTime, Is.EqualTo(TimeSpan.FromSeconds(expected.TravelSeconds)),
                        $"{expected.Id} changed its travel time");
                    Assert.That(ammo.ImpactEffects, Is.EqualTo(expected.ImpactEffects),
                        $"{expected.Id} changed its impact effects");
                    Assert.That(ammo.MarkerWarning, Is.EqualTo(expected.MarkerWarning),
                        $"{expected.Id} changed its warning marker");
                    if (expected.SoundWarning == null)
                    {
                        Assert.That(ammo.SoundWarning, Is.Null, $"{expected.Id} gained a warning sound");
                    }
                    else
                    {
                        Assert.That(ammo.SoundWarning, Is.TypeOf<SoundPathSpecifier>(),
                            $"{expected.Id} changed its warning sound type");
                        Assert.That((ammo.SoundWarning as SoundPathSpecifier)?.Path, Is.EqualTo(expected.SoundWarning),
                            $"{expected.Id} changed its warning sound");
                    }

                    AssertExplosion(expected.Id, ammo, expected.Explosion);
                    AssertImplosion(expected.Id, ammo, expected.Implosion);
                    AssertFire(expected.Id, ammo, expected.Fire);

                    var hasPrintable = crate.TryComp<DropshipFabricatorPrintableComponent>(
                        out var printable,
                        factory);
                    Assert.That(hasPrintable, Is.True, $"{expected.Id} is no longer printable as dropship ammo");
                    Assert.That(printable?.Cost, Is.EqualTo(expected.PrintableCost),
                        $"{expected.Id} changed its printable cost");
                    Assert.That(printable?.Category, Is.EqualTo(DropshipFabricatorPrintableComponent.CategoryType.Ammo),
                        $"{expected.Id} changed its printable category");

                    foreach (var effect in expected.ImpactEffects)
                    {
                        Assert.That(
                            SProtoMan.TryIndex<EntityPrototype>(effect, out _),
                            Is.True,
                            $"{expected.Id} references missing impact effect {effect}");
                    }
                }

                AssertJdamEffects(factory);
            });
        });
    }

    [Test]
    public async Task CrateSpritesResolveHistoricalStates()
    {
        await Client.WaitAssertion(() =>
        {
            var spriteSystem = CEntMan.System<SpriteSystem>();

            Assert.Multiple(() =>
            {
                foreach (var expected in ExpectedCrates)
                {
                    var found = CProtoMan.TryIndex<EntityPrototype>(expected.Id, out _);
                    Assert.That(found, Is.True, $"Missing client prototype for {expected.Id}");
                    if (!found)
                        continue;

                    var uid = CEntMan.SpawnEntity(expected.Id, MapCoordinates.Nullspace);
                    try
                    {
                        var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                        var hasLayer = spriteSystem.LayerMapTryGet(
                            (uid, sprite),
                            DropshipAmmoVisuals.Fill,
                            out var layerId,
                            false);
                        Assert.That(hasLayer, Is.True, $"{expected.Id} has no mapped fill layer");
                        if (!hasLayer ||
                            !spriteSystem.TryGetLayer((uid, sprite), layerId, out var layer, false) ||
                            layer == null)
                            continue;

                        var rsi = layer.ActualRsi;
                        Assert.That(rsi, Is.Not.Null, $"{expected.Id} has no resolved RSI");
                        if (rsi == null)
                            continue;

                        Assert.That(rsi.Path, Is.EqualTo(expected.Rsi),
                            $"{expected.Id} uses the wrong RSI");
                        Assert.That(rsi.TryGetState(expected.State, out _), Is.True,
                            $"{expected.Id} is missing sprite state {expected.State}");
                        Assert.That(spriteSystem.LayerGetRsiState((uid, sprite), layerId).Name, Is.EqualTo(expected.State),
                            $"{expected.Id} starts with the wrong sprite state");
                    }
                    finally
                    {
                        CEntMan.DeleteEntity(uid);
                    }
                }

                AssertJdamLight(CEntMan.ComponentFactory);
            });
        });
    }

    private void AssertJdamEffects(IComponentFactory factory)
    {
        var smokeFound = SProtoMan.TryIndex<EntityPrototype>(JdamSmoke, out var smokePrototype);
        Assert.That(smokeFound, Is.True, $"Missing JDAM smoke effect {JdamSmoke}");
        if (smokeFound)
        {
            Assert.That(smokePrototype!.Parents, Is.EqualTo(new[] { "RMCSmokeExplosionProof" }));
            Assert.That(smokePrototype.TryComp<EvenSmokeComponent>(out var smoke, factory), Is.True);
            Assert.That(smoke?.Spawn, Is.EqualTo((EntProtoId) "RMCSmoke"));
            Assert.That(smoke?.Range, Is.EqualTo(5));
        }

        var particleFound = SProtoMan.TryIndex<EntityPrototype>(JdamParticle, out var particlePrototype);
        Assert.That(particleFound, Is.True, $"Missing JDAM particle effect {JdamParticle}");
        if (!particleFound)
            return;

        Assert.That(
            particlePrototype!.TryComp<RMCScorchEffectOnSpawnComponent>(out var scorch, factory),
            Is.True);
        Assert.That(scorch?.Probability, Is.EqualTo(0.9f));
        Assert.That(scorch?.TileLimit, Is.EqualTo(10));
        Assert.That(scorch?.Scatter, Is.True);
        Assert.That(scorch?.RandomRotation, Is.True);
        Assert.That(particlePrototype.TryComp<TransformComponent>(out var transform, factory), Is.True);
        Assert.That(transform?.Anchored, Is.True);
        Assert.That(particlePrototype.TryComp<TimedDespawnComponent>(out var despawn, factory), Is.True);
        Assert.That(despawn?.Lifetime, Is.EqualTo(0.5f));
        Assert.That(particlePrototype.TryComp<EffectAlphaAnimationComponent>(out _, factory), Is.True);
    }

    private void AssertJdamLight(IComponentFactory factory)
    {
        var particleFound = CProtoMan.TryIndex<EntityPrototype>(JdamParticle, out var particlePrototype);
        Assert.That(particleFound, Is.True, $"Missing client JDAM particle effect {JdamParticle}");
        if (!particleFound)
            return;

        var uid = CEntMan.SpawnEntity(JdamParticle, MapCoordinates.Nullspace);
        try
        {
            Assert.That(CEntMan.TryGetComponent(uid, out PointLightComponent? light), Is.True);
            Assert.That(light?.Enabled, Is.True);
            Assert.That(light?.Radius, Is.EqualTo(7));

            var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
            var spriteSystem = CEntMan.System<SpriteSystem>();
            var hasLayer = spriteSystem.TryGetLayer((uid, sprite), 0, out var layer, false);
            Assert.That(hasLayer, Is.True, $"{JdamParticle} has no sprite layer");
            if (!hasLayer || layer == null)
                return;

            Assert.That(layer.ActualRsi?.Path, Is.EqualTo(new ResPath("/Textures/_RMC14/Effects/effects.rsi")));
            Assert.That(spriteSystem.LayerGetRsiState((uid, sprite), 0).Name, Is.EqualTo("explosion_particle"));
        }
        finally
        {
            CEntMan.DeleteEntity(uid);
        }
    }

    private static void AssertExplosion(string id, DropshipAmmoComponent ammo, ExpectedExplosion? expected)
    {
        if (expected == null)
        {
            Assert.That(ammo.Explosion, Is.Null, $"{id} gained an explosion");
            return;
        }

        Assert.That(ammo.Explosion, Is.Not.Null, $"{id} lost its explosion");
        Assert.That(ammo.Explosion?.Total, Is.EqualTo(expected.Total), $"{id} changed explosion total");
        Assert.That(ammo.Explosion?.Slope, Is.EqualTo(expected.Slope), $"{id} changed explosion slope");
        Assert.That(ammo.Explosion?.Max, Is.EqualTo(expected.Max), $"{id} changed explosion maximum");
    }

    private static void AssertImplosion(string id, DropshipAmmoComponent ammo, ExpectedImplosion? expected)
    {
        if (expected == null)
        {
            Assert.That(ammo.Implosion, Is.Null, $"{id} gained an implosion");
            return;
        }

        Assert.That(ammo.Implosion, Is.Not.Null, $"{id} lost its implosion");
        Assert.That(ammo.Implosion?.PullRange, Is.EqualTo(expected.PullRange));
        Assert.That(ammo.Implosion?.PullDistance, Is.EqualTo(expected.PullDistance));
        Assert.That(ammo.Implosion?.PullSpeed, Is.EqualTo(expected.PullSpeed));
    }

    private static void AssertFire(string id, DropshipAmmoComponent ammo, ExpectedFire? expected)
    {
        if (expected == null)
        {
            Assert.That(ammo.Fire, Is.Null, $"{id} gained a fire payload");
            return;
        }

        Assert.That(ammo.Fire, Is.Not.Null, $"{id} lost its fire payload");
        Assert.That(ammo.Fire?.Type, Is.EqualTo((EntProtoId) expected.Type));
        Assert.That(ammo.Fire?.Range, Is.EqualTo(expected.Range));
        Assert.That(ammo.Fire?.CardinalRange, Is.EqualTo(expected.CardinalRange));
        Assert.That(ammo.Fire?.OrdinalRange, Is.EqualTo(expected.OrdinalRange));
    }

    private sealed record ExpectedCrate(
        EntProtoId Id,
        string State,
        ResPath Rsi,
        int Rounds,
        int BulletSpread,
        double TravelSeconds,
        int PrintableCost,
        ExpectedExplosion? Explosion,
        ExpectedImplosion? Implosion,
        ExpectedFire? Fire,
        EntProtoId[] ImpactEffects,
        ResPath? SoundWarning = null,
        bool MarkerWarning = false);

    private sealed record ExpectedExplosion(float Total, float Slope, float Max);

    private sealed record ExpectedImplosion(float PullRange, float PullDistance, float PullSpeed);

    private sealed record ExpectedFire(string Type, int Range, int CardinalRange, int OrdinalRange);
}
