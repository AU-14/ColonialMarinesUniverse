using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Ghost;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Item;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Ghost;

/// <summary>
///     Every ghost role that's handed job gear gets all of it on their person: in hand, worn or packed. Nothing is
///     left lying at their feet because a hand, pocket or bag couldn't take it.
/// </summary>
[TestFixture]
public sealed class CMUGhostRoleGearTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const float Spacing = 12f;

    [Test]
    public async Task GhostRolesSpawnWithNothingOnTheFloor()
    {
        var roles = new List<string>();
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryGetComponent<GhostRoleApplySpecialComponent>(out _, factory) ||
                    !proto.TryGetComponent<GhostRoleComponent>(out var ghostRole, factory) ||
                    ghostRole.JobProto is not { } job ||
                    !SProtoMan.TryIndex(job, out var jobProto) ||
                    jobProto.StartingGear is not { } gear ||
                    !SProtoMan.TryIndex<StartingGearPrototype>(gear, out _))
                {
                    continue;
                }

                roles.Add(proto.ID);
            }
        });

        Assert.That(roles, Is.Not.Empty);

        var spawned = new List<(string Role, EntityUid Mob, MapCoordinates At)>();
        EntityUid map = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(out var mapId, runMapInit: true);
            for (var i = 0; i < roles.Count; i++)
            {
                var at = new MapCoordinates(new Vector2(i * Spacing, 0), mapId);
                spawned.Add((roles[i], SEntMan.SpawnEntity(roles[i], at), at));
            }
        });

        await Server.WaitRunTicks(10);

        var loose = new List<string>();
        await Server.WaitAssertion(() =>
        {
            var lookup = SEntMan.System<EntityLookupSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            foreach (var (role, mob, at) in spawned)
            {
                var items = lookup.GetEntitiesInRange(at, Spacing / 3f)
                    .Where(e => e != mob &&
                                SEntMan.HasComponent<ItemComponent>(e) &&
                                !containers.IsEntityInContainer(e))
                    .Select(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID ?? "?")
                    .ToList();

                if (items.Count > 0)
                    loose.Add($"{role}: {string.Join(", ", items)}");
            }

            SEntMan.DeleteEntity(map);
        });

        Assert.That(loose, Is.Empty,
            $"{loose.Count} ghost role(s) spawn with gear on the floor instead of on them:\n  " + string.Join("\n  ", loose));
    }
}
