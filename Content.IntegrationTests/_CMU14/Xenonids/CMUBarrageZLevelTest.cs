using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Xenonids.Despoiler;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUBarrageZLevelTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(0, true, false)]
    [TestCase(1, true, false)]
    [TestCase(-1, true, false)]
    [TestCase(-1, false, false)]
    [TestCase(1, true, true)]
    public async Task BarrageUsesRequestedLevelAndRejectsInvalidShotsBeforeSpendingPlasma(int offset, bool hasLevel, bool blocked)
    {
        var pair = Pair;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var sourceMap = maps.CreateMap(out var sourceId, runMapInit: true);
            var destinationMap = maps.CreateMap(out var destinationId, runMapInit: true);
            var z = entities.System<CMUZLevelsSystem>();
            var levels = new System.Collections.Generic.Dictionary<EntityUid, int> { [sourceMap] = 0 };
            if (offset != 0 && hasLevel)
                levels.Add(destinationMap, offset);
            Assert.That(z.TryAddMapsIntoZNetwork(z.CreateZNetwork(), levels), Is.True);
            if (blocked)
            {
                var grid = maps.CreateGridEntity(destinationId);
                var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
                for (var x = -4; x <= 9; x++)
                for (var y = -4; y <= 4; y++)
                    maps.SetTile(grid, new Vector2i(x, y), new Tile(tiles["Plating"].TileId));
            }

            var caster = entities.SpawnEntity("RMCXenoDespoiler", new MapCoordinates(Vector2.Zero, sourceId));
            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, caster);
            var action = entities.System<SharedRMCActionsSystem>()
                .GetActionsWithEvent<XenoDespoilerAcidBarrageActionEvent>(caster).Single();
            var barrage = entities.GetComponent<XenoDespoilerAcidBarrageActionComponent>(action);
            var target = new EntityCoordinates(sourceMap, new Vector2(5, 0));
            var charge = entities.AddComponent<XenoDespoilerChargingBarrageComponent>(caster);
            charge.StartedAt = pair.Server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(barrage.MaxChargeSeconds);
            charge.Target = entities.GetNetCoordinates(target);
            #pragma warning disable RA0002
            var plasma = entities.GetComponent<XenoPlasmaComponent>(caster);
            plasma.Plasma = plasma.MaxPlasma;
            if (offset > 0)
                entities.EnsureComponent<CMUZLevelViewerComponent>(caster).LookUp = true;
            #pragma warning restore RA0002
            if (offset < 0)
                entities.System<CMUZLevelShootingSystem>().SetShootDown(caster, true);

            var before = plasma.Plasma;
            var request = new XenoDespoilerBarrageFireRequest(entities.GetNetCoordinates(target));
            typeof(XenoDespoilerAcidBarrageSystem).GetMethod("OnFireRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(entities.System<XenoDespoilerAcidBarrageSystem>(), [request, new EntitySessionEventArgs(pair.Player!)]);
            var projectiles = entities.EntityQuery<XenoDespoilerAcidBarrageProjectileComponent, TransformComponent>().ToArray();
            if (!hasLevel || blocked)
            {
                Assert.That(projectiles, Is.Empty);
                Assert.That(plasma.Plasma, Is.EqualTo(before), "Rejected shots must not consume plasma.");
            }
            else
            {
                Assert.That(projectiles, Has.Length.EqualTo(barrage.MaxProjectiles));
                Assert.That(projectiles.All(p => p.Item2.MapID == (offset == 0 ? sourceId : destinationId)), Is.True,
                    "Every shot in the volley must spawn on the selected level.");
                Assert.That(plasma.Plasma, Is.LessThan(before));
            }
        });
    }
}
