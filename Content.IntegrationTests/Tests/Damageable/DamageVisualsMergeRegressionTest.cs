using Content.Client.Damage;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(DamageVisualsSystem))]
public sealed class DamageVisualsMergeRegressionTest : GameTest
{
    private static readonly EntProtoId NoHide = "DamageVisualMergeNoHide";
    private static readonly EntProtoId Hide = "DamageVisualMergeHide";
    private static readonly EntProtoId Overlay = "DamageVisualMergeOverlay";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DamageVisualMergeNoHide
  parent: CMBarricadeMetal
  components:
  - type: Damageable
    displacement: Dwarfism
  - type: Injurable
    damageContainer: Inorganic
  - type: DamageVisuals
    thresholds: [ 4 ]
    damageDivisor: 1
    trackAllDamage: true
    overlay: false
    targetLayers:
    - enum.RMCDamageOverlayVisuals.DamageOverlay
    damageGroup: Brute
    hideIfZero: false

- type: entity
  id: DamageVisualMergeHide
  parent: DamageVisualMergeNoHide
  components:
  - type: DamageVisuals
    hideIfZero: true

- type: entity
  id: DamageVisualMergeOverlay
  parent: CMBarricadeMetal
  components:
  - type: Damageable
    displacement: Dwarfism
  - type: Injurable
    damageContainer: Inorganic
  - type: DamageVisuals
    thresholds: [ 4 ]
    damageDivisor: 1
    trackAllDamage: true
    overlay: true
    targetLayers: null
    damageGroup: null
    hideIfZero: false
    damageOverlay:
      sprite: _RMC14/Structures/Walls/Barricades/metal_barricade_cracks.rsi
";

    [Test]
    public async Task ZeroVisibilityDisplacementAndGroupColorPreserveMergedSemantics()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var serverEntities = server.EntMan;
        var clientEntities = client.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid noHide = default;
        EntityUid hide = default;
        EntityUid overlay = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            noHide = serverEntities.SpawnEntity(NoHide, map.GridCoords);
            hide = serverEntities.SpawnEntity(Hide, map.GridCoords);
            overlay = serverEntities.SpawnEntity(Overlay, map.GridCoords);
            human = serverEntities.SpawnEntity("CMMobHuman", map.GridCoords);
        });
        await pair.RunUntilSynced();

        var clientNoHide = clientEntities.GetEntity(serverEntities.GetNetEntity(noHide));
        var clientHide = clientEntities.GetEntity(serverEntities.GetNetEntity(hide));
        var clientOverlay = clientEntities.GetEntity(serverEntities.GetNetEntity(overlay));
        var clientHuman = clientEntities.GetEntity(serverEntities.GetNetEntity(human));

        await client.WaitAssertion(() =>
        {
            AssertLayerVisibility(clientEntities, clientNoHide, RMCDamageOverlayVisuals.DamageOverlay, true,
                "HideIfZero=false keeps a non-overlay target layer visible at zero");
            AssertLayerVisibility(clientEntities, clientHide, RMCDamageOverlayVisuals.DamageOverlay, true,
                "the initial target sprite is visible before crossing a threshold");
            AssertLayerVisibility(clientEntities, clientOverlay, "DamageOverlay", false,
                "overlay layers always start hidden at zero even when HideIfZero=false");

            var spriteSystem = clientEntities.System<SpriteSystem>();
            var sprite = clientEntities.GetComponent<SpriteComponent>(clientHuman);
            var visuals = clientEntities.GetComponent<DamageVisualsComponent>(clientHuman);
            const string color = "#2cf274";
            clientEntities.System<DamageVisualsSystem>()
                .ChangeDamageGroupColor((clientHuman, sprite), visuals, "Brute", color);

            foreach (var layer in visuals.TargetLayerMapKeys)
            {
                var index = spriteSystem.LayerMapGet((clientHuman, sprite), $"{layer}Brute");
                Assert.That(sprite[index].Color, Is.EqualTo(Color.FromHex(color)),
                    $"live grouped layer {layer} uses the replacement color");
            }

            Assert.That(visuals.DamageOverlayGroups!["Brute"].Color, Is.EqualTo(color),
                "the grouped damage visual cache retains the replacement color");
        });

        await server.WaitPost(() =>
        {
            var damageable = serverEntities.System<DamageableSystem>();
            foreach (var ent in new[] { noHide, hide, overlay })
                damageable.TryChangeDamage(ent, Damage("Blunt", 4));
            damageable.TryChangeDamage(human, Damage("Blunt", 15));
        });
        await pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            var damageable = serverEntities.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(FixedPoint2.New(15)),
                "the server applies the full pre-visual damage");
        });

        await client.WaitAssertion(() =>
        {
            var damageable = clientEntities.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(clientHuman), Is.EqualTo(FixedPoint2.New(15)),
                "the client received the full damage state before visual assertions");
            AssertLayerVisibility(clientEntities, clientNoHide, RMCDamageOverlayVisuals.DamageOverlay, true,
                "non-overlay layer is visible after crossing a non-zero threshold");
            AssertLayerVisibility(clientEntities, clientHide, RMCDamageOverlayVisuals.DamageOverlay, true,
                "HideIfZero=true does not hide non-zero damage");
            AssertLayerVisibility(clientEntities, clientOverlay, "DamageOverlay", true,
                "overlay layer is visible at a non-zero threshold");
            AssertDisplacement(clientEntities, clientNoHide, true);

            var spriteSystem = clientEntities.System<SpriteSystem>();
            var sprite = clientEntities.GetComponent<SpriteComponent>(clientHuman);
            var visuals = clientEntities.GetComponent<DamageVisualsComponent>(clientHuman);
            var appearance = clientEntities.GetComponent<AppearanceComponent>(clientHuman);
            var appearanceSystem = clientEntities.System<AppearanceSystem>();
            var hasDamageUpdateGroups = appearanceSystem.TryGetData<DamageVisualizerGroupData>(
                clientHuman,
                DamageVisualizerKeys.DamageUpdateGroups,
                out var damageUpdateGroups,
                appearance);
            var hasDisabledAppearance = appearanceSystem.TryGetData<bool>(
                clientHuman,
                DamageVisualizerKeys.Disabled,
                out var appearanceDisabled,
                appearance);
            var layerDiagnostics = visuals.TargetLayerMapKeys.Select(layer =>
            {
                var hasBase = spriteSystem.LayerMapTryGet((clientHuman, sprite), layer, out var baseIndex, false);
                var hasBrute = spriteSystem.LayerMapTryGet(
                    (clientHuman, sprite),
                    $"{layer}Brute",
                    out var bruteIndex,
                    false);
                visuals.DisabledLayers.TryGetValue(layer, out var disabled);
                var baseState = hasBase ? sprite[baseIndex].RsiState.Name.ToString() : "missing";
                var bruteState = hasBrute ? sprite[bruteIndex].RsiState.Name.ToString() : "missing";
                return $"{layer}: disabled={disabled}, base={baseIndex}/{hasBase}/{(hasBase && sprite[baseIndex].Visible)}/{baseState}, " +
                       $"brute={bruteIndex}/{hasBrute}/{(hasBrute && sprite[bruteIndex].Visible)}/{bruteState}";
            });
            var diagnostics =
                $"Valid={visuals.Valid}; Disabled={visuals.Disabled}; Displacement={(visuals.Displacement is null ? "null" : "set")}; " +
                $"LastThresholds=[{string.Join(", ", visuals.LastThresholdPerGroup.Select(pair => $"{pair.Key}={pair.Value}"))}]; " +
                $"Targets=[{string.Join(", ", visuals.TargetLayerMapKeys)}]; " +
                $"AppearanceDamageGroups={(hasDamageUpdateGroups ? string.Join(", ", damageUpdateGroups.GroupList) : "missing")}; " +
                $"AppearanceDisabled={(hasDisabledAppearance ? appearanceDisabled.ToString() : "missing")}; " +
                $"Layers=[{string.Join("; ", layerDiagnostics)}]";
            Assert.That(visuals.TargetLayerMapKeys.Any(layer =>
                sprite[spriteSystem.LayerMapGet((clientHuman, sprite), $"{layer}Brute")].Visible),
                Is.True,
                $"grouped updates use the GetAllDamage-backed Brute threshold path. {diagnostics}");
        });

        await server.WaitPost(() =>
        {
            var damageable = serverEntities.System<DamageableSystem>();
            foreach (var ent in new[] { noHide, hide, overlay })
                damageable.TryChangeDamage(ent, Damage("Blunt", -100));
        });
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            AssertLayerVisibility(clientEntities, clientNoHide, RMCDamageOverlayVisuals.DamageOverlay, true,
                "HideIfZero=false survives a non-zero to exact-zero transition");
            AssertDisplacement(clientEntities, clientNoHide, true,
                "the displacement remains installed on the visible exact-zero target layer");
            AssertLayerVisibility(clientEntities, clientHide, RMCDamageOverlayVisuals.DamageOverlay, false,
                "HideIfZero=true hides only the non-overlay target layer at zero");
            AssertLayerVisibility(clientEntities, clientOverlay, "DamageOverlay", false,
                "overlay zero visibility remains independent of HideIfZero");
        });

        await server.WaitPost(() =>
        {
            serverEntities.DeleteEntity(noHide);
            serverEntities.DeleteEntity(hide);
            serverEntities.DeleteEntity(overlay);
            serverEntities.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    private static DamageSpecifier Damage(string type, float amount)
    {
        return new DamageSpecifier
        {
            DamageDict =
            {
                [type] = FixedPoint2.New(amount),
            },
        };
    }

    private static void AssertLayerVisibility(
        IEntityManager entities,
        EntityUid uid,
        object key,
        bool expected,
        string message)
    {
        var spriteSystem = entities.System<SpriteSystem>();
        var sprite = entities.GetComponent<SpriteComponent>(uid);
        var index = key switch
        {
            Enum enumKey => spriteSystem.LayerMapGet((uid, sprite), enumKey),
            string stringKey => spriteSystem.LayerMapGet((uid, sprite), stringKey),
            _ => throw new ArgumentException($"Unsupported sprite layer key {key}", nameof(key)),
        };
        Assert.That(sprite[index].Visible, Is.EqualTo(expected), message);
    }

    private static void AssertDisplacement(
        IEntityManager entities,
        EntityUid uid,
        bool expected,
        string? message = null)
    {
        var spriteSystem = entities.System<SpriteSystem>();
        var sprite = entities.GetComponent<SpriteComponent>(uid);
        Assert.That(spriteSystem.LayerMapTryGet(
                (uid, sprite),
                $"{RMCDamageOverlayVisuals.DamageOverlay}-displacement",
                out _,
                false),
            Is.EqualTo(expected),
            message);
    }
}
