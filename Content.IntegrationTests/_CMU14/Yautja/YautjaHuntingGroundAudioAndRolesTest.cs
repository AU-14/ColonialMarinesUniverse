using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server.Maps;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Audio;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Localization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntingGroundAudioAndRolesTest
{
    private static readonly (ResPath MapPath, string Collection, string[] Files)[] HuntingGroundSoundscapes =
    [
        (
            new ResPath("/Maps/CMU14/HuntingGrounds/jungle_moon.yml"),
            "CMUYautjaHuntingGroundJungle",
            ["alien_creature1.ogg", "alien_creature2.ogg", "alien_creature3.ogg"]),
        // The CMSS13 desert_moon.dmm z-level names are historical: z1 is the cave
        // output and z2 is the surface output stored in the _caves file.
        (
            new ResPath("/Maps/CMU14/HuntingGrounds/desert_moon.yml"),
            "CMUYautjaHuntingGroundCaves",
            ["rocksfalling1.ogg", "rocksfalling2.ogg"]),
        (
            new ResPath("/Maps/CMU14/HuntingGrounds/desert_moon_caves.yml"),
            "CMUYautjaHuntingGroundDesert",
            ["wind1.ogg", "wind2.ogg"]),
    ];

    [Test]
    public async Task HuntingGroundsUseOriginalCmss13Soundscapes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var resources = server.ResolveDependency<IResourceManager>();
            var huntBeginPath = new ResPath("/Audio/CMU14/Yautja/HuntingGrounds/hunt_begin.ogg");

            Assert.That(resources.ContentFileExists(huntBeginPath), Is.True, huntBeginPath.ToString());
            Assert.That(ReadVorbisChannelCount(resources, huntBeginPath), Is.EqualTo(1),
                $"{huntBeginPath} is positional and must be mono.");

            foreach (var (mapPath, collectionId, expectedFileNames) in HuntingGroundSoundscapes)
            {
                var collection = prototypes.Index<SoundCollectionPrototype>(collectionId);
                var actualFileNames = collection.PickFiles.Select(path => path.Filename).ToArray();

                Assert.That(actualFileNames, Is.EquivalentTo(expectedFileNames), collectionId);
                Assert.That(ContainsLine(resources, mapPath, $"collection: {collectionId}"), Is.True, mapPath.ToString());

                foreach (var file in collection.PickFiles)
                {
                    Assert.That(resources.ContentFileExists(file), Is.True, file.ToString());
                    Assert.That(ReadVorbisChannelCount(resources, file), Is.EqualTo(1),
                        $"{file} is positional and must be mono.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingGroundMapRootsLoadTheirSoundscapeComponents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var loadedMaps = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();

            try
            {
                foreach (var (mapPath, collectionId, _) in HuntingGroundSoundscapes)
                {
                    Assert.That(loader.TryLoadMap(
                        mapPath,
                        out var map,
                        out _,
                        DeserializationOptions.Default with { InitializeMaps = true }), Is.True, mapPath.ToString());
                    Assert.That(map, Is.Not.Null, mapPath.ToString());

                    loadedMaps.Add(map!.Value.Owner);
                    Assert.That(entMan.TryGetComponent<AmbientSoundComponent>(map.Value.Owner, out var ambient), Is.True, mapPath.ToString());
                    Assert.That(ambient!.Sound, Is.TypeOf<SoundCollectionSpecifier>(), mapPath.ToString());
                    Assert.That(((SoundCollectionSpecifier) ambient.Sound).Collection, Is.EqualTo(collectionId), mapPath.ToString());
                }
            }
            finally
            {
                foreach (var loadedMap in loadedMaps)
                {
                    if (!entMan.Deleted(loadedMap))
                        entMan.DeleteEntity(loadedMap);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingGroundCallsUseCmuThirdPartyBodies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var consoles = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(prototype => prototype.TryComp<YautjaHuntConsoleComponent>(out var component, factory) &&
                                    component!.Kind == YautjaHuntConsoleKind.HuntGround)
                .ToArray();

            Assert.That(consoles, Is.Not.Empty);

            var spawns = new List<YautjaHuntSpawnEntry>();
            foreach (var console in consoles)
            {
                Assert.That(console.TryComp<YautjaHuntConsoleComponent>(out var component, factory), Is.True, console.ID);
                foreach (var option in component!.HuntCallOptions)
                {
                    spawns.AddRange(option.Spawns);
                    spawns.AddRange(option.Tiers.SelectMany(tier => tier.Spawns));
                }
            }

            Assert.That(spawns.Where(spawn => spawn.RandomHumanoidSettings is not null), Is.Empty,
                "Hunt prey spawn CMU third party bodies, not RMC random humanoid presets.");

            var humanBodies = 0;
            foreach (var id in spawns.Select(spawn => spawn.EntityPrototype).OfType<EntProtoId>().Select(id => id.Id).Distinct())
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var body), Is.True, id);
                if (!body!.TryComp<GhostRoleComponent>(out var ghostRole, factory) || ghostRole!.JobProto is not { } job)
                    continue;

                humanBodies++;
                Assert.That(prototypes.HasIndex(job), Is.True, $"{id} job {job}");
                Assert.That(job.Id, Does.Not.StartWith("RMC"), $"{id} still plays an RMC job.");

                if (!id.StartsWith("CMUYautjaHunt", StringComparison.Ordinal))
                    continue;

                Assert.That(ghostRole.Requirements, Is.Not.Null.And.Empty, $"{id} must not keep its job playtime gates.");
            }

            Assert.That(humanBodies, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntPreyGetPreyGhostRoleWithoutPlaytimeGates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid flightConsole = default;
        EntityUid huntsmasterConsole = default;
        var before = new HashSet<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entMan.EnsureComponent<YautjaComponent>(hunter);
                flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
                huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

                entMan.EventBus.RaiseLocalEvent(flightConsole, new YautjaHuntingGroundSelectedEvent(entMan.GetNetEntity(hunter), "jungle_moon"));

                var query = entMan.EntityQueryEnumerator<YautjaHuntPreyComponent>();
                while (query.MoveNext(out var uid, out _))
                    before.Add(uid);

                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), "mixed_small"));
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var prey = new List<EntityUid>();
                var query = entMan.EntityQueryEnumerator<YautjaHuntPreyComponent, GhostRoleComponent>();
                while (query.MoveNext(out var uid, out _, out var ghostRole))
                {
                    if (before.Contains(uid))
                        continue;

                    prey.Add(uid);
                    var name = entMan.ToPrettyString(uid);
                    Assert.That(ghostRole.JobProto, Is.Not.Null, $"{name}");
                    Assert.That(ghostRole.RoleDescription, Is.EqualTo(Loc.GetString("cmu-yautja-hunt-prey-ghost-description")), $"{name}");
                    Assert.That(ghostRole.RoleRules, Is.EqualTo(Loc.GetString("cmu-yautja-hunt-prey-ghost-rules")), $"{name}");
                    Assert.That(ghostRole.Requirements, Is.Not.Null.And.Empty, $"{name} must not keep its job playtime gates.");
                    Assert.That(inventory.TryGetSlotEntity(uid, "jumpsuit", out _), Is.True, $"{name} spawned without its kit.");
                    Assert.That(inventory.TryGetSlotEntity(uid, "ears", out _), Is.False, $"{name} kept a worn headset.");
                }

                Assert.That(prey, Has.Count.EqualTo(4));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var uid in new[] { hunter, flightConsole, huntsmasterConsole })
                {
                    if (uid != default && !server.EntMan.Deleted(uid))
                        server.EntMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static bool ContainsLine(IResourceManager resources, ResPath path, string expected)
    {
        using var stream = resources.ContentFileRead(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Contains(expected, StringComparison.Ordinal);
    }

    private static int ReadVorbisChannelCount(IResourceManager resources, ResPath path)
    {
        using var stream = resources.ContentFileRead(path);
        var header = new byte[512];
        var length = stream.Read(header, 0, header.Length);
        ReadOnlySpan<byte> marker = [0x01, (byte) 'v', (byte) 'o', (byte) 'r', (byte) 'b', (byte) 'i', (byte) 's'];

        for (var i = 0; i <= length - marker.Length - 5; i++)
        {
            if (!header.AsSpan(i, marker.Length).SequenceEqual(marker))
                continue;

            // Vorbis identification packet: marker, 32-bit version, channel count.
            return header[i + marker.Length + sizeof(uint)];
        }

        throw new AssertionException($"No Vorbis identification header found in {path}.");
    }
}
