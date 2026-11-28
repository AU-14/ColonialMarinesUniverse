using System.Collections.Generic;
using System.Linq;
using Content.Server.Humanoid.Components;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Humanoid;

/// <summary>
///     Whitelisted third-party mobs used to keep whatever beard the random profile rolled
///     whenever they didn't roll one of their allowed beards, in any length and color.
/// </summary>
[TestFixture]
public sealed class CMUWhitelistedAppearanceTest
{
    private const string Mob = "CMUJobWeYuGoon";
    private const int Spawns = 40;

    [Test]
    public async Task FacialHairStaysWithinWhitelist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            for (var i = 0; i < Spawns; i++)
            {
                var mob = entities.SpawnEntity(Mob, map.GridCoords);
                try
                {
                    var whitelist = entities.GetComponent<RandomHumanoidAppearanceWhitelistedComponent>(mob);
                    var allowed = whitelist.AllowedBeardStyles!.ToHashSet();
                    var markings = MarkingsOn(entities, mob);
                    var hair = markings.GetValueOrDefault(HumanoidVisualLayers.Hair);
                    var beards = markings.GetValueOrDefault(HumanoidVisualLayers.FacialHair) ?? new List<Marking>();

                    foreach (var beard in beards)
                    {
                        Assert.That(allowed, Does.Contain(beard.MarkingId.Id), $"{Mob} grew a beard outside its whitelist.");
                        if (hair is { Count: > 0 })
                            Assert.That(beard.MarkingColors, Is.EqualTo(hair[0].MarkingColors), "The beard color doesn't match the hair.");
                    }
                }
                finally
                {
                    entities.DeleteEntity(mob);
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    // Markings live on the body's organ entities, so walk up from each organ to its mob.
    private static Dictionary<HumanoidVisualLayers, List<Marking>> MarkingsOn(IEntityManager entities, EntityUid mob)
    {
        var result = new Dictionary<HumanoidVisualLayers, List<Marking>>();
        var query = entities.EntityQueryEnumerator<VisualOrganMarkingsComponent, TransformComponent>();
        while (query.MoveNext(out _, out var organ, out var xform))
        {
            var parent = xform.ParentUid;
            while (parent.IsValid() && parent != mob)
                parent = entities.GetComponent<TransformComponent>(parent).ParentUid;

            if (parent != mob)
                continue;

            foreach (var (layer, list) in organ.Markings)
            {
                if (!result.TryGetValue(layer, out var existing))
                    result[layer] = existing = new List<Marking>();
                existing.AddRange(list);
            }
        }

        return result;
    }
}
