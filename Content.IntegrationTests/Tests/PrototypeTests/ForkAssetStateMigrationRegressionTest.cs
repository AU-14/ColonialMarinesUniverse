using System.Linq;
using Content.Client.Atmos.Components;
using Content.Client.IconSmoothing;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.PrototypeTests;

[TestFixture]
public sealed class ForkAssetStateMigrationRegressionTest : GameTest
{
    private static readonly IReadOnlyDictionary<string, string> TankSprites =
        new Dictionary<string, string>
        {
            ["CMAnestheticTank"] = "/Textures/_RMC14/Objects/Tanks/anesthetic.rsi",
            ["RMCGasTankOxygen"] = "/Textures/_RMC14/Objects/Tanks/oxygen_tank.rsi",
            ["RMCEmergencyOxygenTank"] = "/Textures/_RMC14/Objects/Tanks/emergency.rsi",
            ["RMCExtendedEmergencyOxygenTank"] = "/Textures/_RMC14/Objects/Tanks/emergency_extended.rsi",
            ["RMCDoubleEmergencyOxygenTank"] = "/Textures/_RMC14/Objects/Tanks/emergency_double.rsi",
            ["RMCGasTankPhoron"] = "/Textures/_RMC14/Objects/Tanks/phoron.rsi",
        };

    private static readonly IReadOnlyDictionary<string, string> CleanerStates =
        new Dictionary<string, string>
        {
            ["AU14SprayCleaner"] = "cleaner",
            ["AU14SprayCleanerBig"] = "cleaner_large",
            ["AU14SprayCleanerExperimental"] = "cleaner_special",
        };

    [Test]
    public async Task ForkSpritesUseCurrentRsiStatesAndOwnerLayers()
    {
        await Client.WaitAssertion(() =>
        {
            var sprites = Client.System<SpriteSystem>();

            foreach (var (prototype, baseRsi) in TankSprites)
            {
                WithEntity(prototype, uid =>
                {
                    var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                    Assert.Multiple(() =>
                    {
                        Assert.That(sprite[0].ActualRsi?.Path, Is.EqualTo(new ResPath(baseRsi)), prototype);
                        Assert.That(sprite[0].RsiState.Name, Is.EqualTo("icon"), prototype);
                    });

                    AssertLayer(sprite, MaxPressureVisualLayers.BaseUnshaded,
                        "/Textures/Objects/Tanks/generic.rsi", "integrity-unshaded-0");
                    AssertLayer(sprite, MaxPressureVisualLayers.Base,
                        "/Textures/Objects/Tanks/generic.rsi", "mask");

                    void AssertLayer(SpriteComponent component, MaxPressureVisualLayers key, string rsi, string state)
                    {
                        Assert.That(sprites.LayerMapTryGet((uid, component), key, out var index, false),
                            Is.True, $"{prototype} {key}");
                        Assert.Multiple(() =>
                        {
                            Assert.That(component[index].ActualRsi?.Path, Is.EqualTo(new ResPath(rsi)),
                                $"{prototype} {key}");
                            Assert.That(component[index].RsiState.Name, Is.EqualTo(state),
                                $"{prototype} {key}");
                        });
                    }
                });
            }

            WithEntity("RMCJug", uid =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                var visuals = CEntMan.GetComponent<SolutionContainerVisualsComponent>(uid);
                Assert.That(sprites.LayerMapTryGet((uid, sprite), SolutionContainerLayers.Fill, out var fill, false),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(sprite[0].RsiState.Name, Is.EqualTo("icon_empty"));
                    Assert.That(sprite[fill].RsiState.Name, Is.EqualTo("fill-1"));
                    Assert.That(visuals.MaxFillLevels, Is.EqualTo(6));
                    Assert.That(visuals.FillBaseName, Is.EqualTo("fill-"));
                    Assert.That(visuals.InHandsMaxFillLevels, Is.EqualTo(5));
                    Assert.That(visuals.InHandsFillBaseName, Is.EqualTo("-fill-"));
                });
            });

            foreach (var (prototype, state) in CleanerStates)
            {
                WithEntity(prototype, uid =>
                {
                    var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                    var visuals = CEntMan.GetComponent<SolutionContainerVisualsComponent>(uid);
                    Assert.Multiple(() =>
                    {
                        Assert.That(sprite.AllLayers.Count(), Is.EqualTo(1), prototype);
                        Assert.That(sprite[0].ActualRsi?.Path,
                            Is.EqualTo(new ResPath("/Textures/Objects/Tools/cleaner_spray.rsi")), prototype);
                        Assert.That(sprite[0].RsiState.Name, Is.EqualTo(state), prototype);
                        Assert.That(visuals.MaxFillLevels, Is.Zero, prototype);
                        Assert.That(visuals.FillBaseName, Is.Null, prototype);
                        Assert.That(visuals.InHandsMaxFillLevels, Is.Zero, prototype);
                        Assert.That(visuals.InHandsFillBaseName, Is.Null, prototype);
                    });
                });
            }

            WithEntity("mineablesolarisrock", uid =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                var smooth = CEntMan.GetComponent<IconSmoothComponent>(uid);
                Assert.Multiple(() =>
                {
                    Assert.That(sprite.BaseRSI?.Path,
                        Is.EqualTo(new ResPath("/Textures/Structures/Walls/stone.rsi")));
                    Assert.That(sprite.BaseRSI?.TryGetState("full", out _), Is.True);
                    Assert.That(smooth.SmoothKey, Is.EqualTo("stone"));
                    Assert.That(smooth.AdditionalKeys, Is.EqualTo(new[] { "doors" }));
                    Assert.That(smooth.Index, Is.Zero);
                    Assert.That(smooth.StateBase, Is.EqualTo("stone"));
                });
            });

            WithEntity("AUWeylandYutaniDoorknob", uid =>
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
                Assert.That(sprite[0].RsiState.Name, Is.EqualTo("coinGold_1"));
            });
        });
    }

    private void WithEntity(string prototype, Action<EntityUid> assertion)
    {
        var uid = CEntMan.Spawn(prototype);
        try
        {
            assertion(uid);
        }
        finally
        {
            CEntMan.DeleteEntity(uid);
        }
    }
}
