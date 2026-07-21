using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client.Actions;
using Content.Client.Construction;
using Content.Client.Mapping;
using Content.Client.UserInterface;
using Content.IntegrationTests.Utility;
using Content.Shared.Access;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Input;
using Content.Shared.Maps;
using Content.Shared.Traits;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers.Implementations;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests;

public sealed class LocalizationWarningTest
{
    private static readonly ProtoId<ContentTileDefinition> RmcTile = "RMCFloorHybrisaEngineerShip";
    private static readonly string[] MarkingPrototypes = GameDataScrounger.PrototypesOfKind<MarkingPrototype>();

    [Test]
    public async Task ClientDoesNotLookUpLiteralTextAsMessageIds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var construction = client.System<ConstructionSystem>();
        var mapping = client.System<MappingSystem>();

        await client.WaitPost(() =>
        {
            var configuration = client.ResolveDependency<IConfigurationManager>();
            var localizationLog = client.ResolveDependency<ILogManager>().GetSawmill("loc");
            var previousLogLevel = localizationLog.Level;

            try
            {
                localizationLog.Level = LogLevel.Warning;
                configuration.SetCVar(RTCVars.FailureLogLevel, LogLevel.Warning);

                // Pooled clients suppress localization warnings during startup. Re-run the real cache warmup with
                // warnings promoted so passing literal recipe text to Loc.GetString fails this regression test.
                var cacheField = typeof(ConstructionSystem).GetField("_recipesMetadataCache",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var warmupMethod = typeof(ConstructionSystem).GetMethod("WarmupRecipesCache",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(cacheField, Is.Not.Null);
                Assert.That(warmupMethod, Is.Not.Null);

                var cache = (Dictionary<string, string>) cacheField!.GetValue(construction)!;
                cache.Clear();
                warmupMethod!.Invoke(construction, null);

                BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat);

                // Exercise the mapping action path with an RMC tile whose name was migrated from legacy literal text.
                var prototypeManager = client.ResolveDependency<IPrototypeManager>();
                var placementManager = client.ResolveDependency<IPlacementManager>();
                var localization = client.ResolveDependency<ILocalizationManager>();
                var fillActionMethod = typeof(MappingSystem).GetMethod("OnFillActionSlot",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(fillActionMethod, Is.Not.Null);

                var tile = prototypeManager.Index(RmcTile);
                placementManager.BeginPlacing(new PlacementInformation
                {
                    IsTile = true,
                    PlacementOption = "AlignTileAny",
                    TileType = tile.TileId,
                });

                var fillAction = new FillActionSlotEvent();
                fillActionMethod!.Invoke(mapping, [fillAction]);
                Assert.That(fillAction.Action, Is.Not.Null);

                if (fillAction.Action is { } action)
                    client.EntMan.DeleteEntity(action);

                placementManager.Clear();

                var invalidTileNames = prototypeManager.EnumeratePrototypes<ContentTileDefinition>()
                    .Where(tileDefinition => !tileDefinition.Abstract &&
                                             !tileDefinition.EditorHidden &&
                                             (string.IsNullOrWhiteSpace(tileDefinition.Name) ||
                                              !localization.TryGetString(tileDefinition.Name, out _)))
                    .Select(tileDefinition =>
                        $"{tileDefinition.ID}: {(string.IsNullOrWhiteSpace(tileDefinition.Name)
                            ? "<empty>"
                            : tileDefinition.Name)}")
                    .Distinct()
                    .Order()
                    .ToArray();

                Assert.That(invalidTileNames, Is.Empty,
                    $"Editor-visible tiles with invalid en-US names:\n{string.Join('\n', invalidTileNames)}");

                // Build the engine-owned tile spawn menu, which localizes every visible tile while sorting and
                // rendering.
                // Some legacy tile sprites independently warn when loaded as raw textures from an RSI.
                var resourceLog = client.ResolveDependency<ILogManager>().GetSawmill("res");
                var previousResourceLogLevel = resourceLog.Level;
                var tileSpawning = client.ResolveDependency<IUserInterfaceManager>()
                    .GetUIController<TileSpawningUIController>();
                try
                {
                    resourceLog.Level = LogLevel.Error;
                    tileSpawning.ToggleWindow();
                }
                finally
                {
                    tileSpawning.CloseWindow();
                    resourceLog.Level = previousResourceLogLevel;
                }

                var missingGuideNames = prototypeManager.EnumeratePrototypes<GuideEntryPrototype>()
                    .Where(guide => !localization.TryGetString(guide.Name, out _))
                    .Select(guide => $"{guide.ID}: {guide.Name}")
                    .Order()
                    .ToArray();

                // RMC's "protobaseaccess ..." names are control sentinels used to build console groups, not
                // player-facing localization IDs.
                var missingAccessNames = prototypeManager.EnumeratePrototypes<AccessLevelPrototype>()
                    .Where(access => access.Name is { } name &&
                                     !name.Contains("protobaseaccess", StringComparison.Ordinal) &&
                                     !localization.TryGetString(name, out _))
                    .Select(access => $"{access.ID}: {access.Name}")
                    .Order()
                    .ToArray();

                var missingTraitMessages = new List<string>();
                foreach (var trait in prototypeManager.EnumeratePrototypes<TraitPrototype>())
                {
                    if (!localization.TryGetString(trait.Name, out _))
                        missingTraitMessages.Add($"{trait.ID} name: {trait.Name}");

                    if (trait.Description is { } description &&
                        !localization.TryGetString(description, out _))
                    {
                        missingTraitMessages.Add($"{trait.ID} description: {description}");
                    }
                }

                var missingMarkingNames = MarkingPrototypes
                    .Where(id => !localization.TryGetString($"marking-{id}", out _))
                    .Order()
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(missingGuideNames, Is.Empty,
                        $"Guide entries without en-US names:\n{string.Join('\n', missingGuideNames)}");
                    Assert.That(missingAccessNames, Is.Empty,
                        $"Access levels without en-US names:\n{string.Join('\n', missingAccessNames)}");
                    Assert.That(missingTraitMessages, Is.Empty,
                        $"Traits without en-US messages:\n{string.Join('\n', missingTraitMessages)}");
                    Assert.That(missingMarkingNames, Is.Empty,
                        $"Markings without en-US names:\n{string.Join('\n', missingMarkingNames)}");
                });
            }
            finally
            {
                configuration.SetCVar(RTCVars.FailureLogLevel, LogLevel.Error);
                localizationLog.Level = previousLogLevel;
            }
        });

        await pair.CleanReturnAsync();
    }
}
