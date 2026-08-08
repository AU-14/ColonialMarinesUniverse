using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Server.Maps;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Audio;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntingGroundAudioAndRolesTest
{
    private static readonly (ResPath MapPath, string Collection, string[] Files)[] HuntingGroundSoundscapes =
    [
        (
            new ResPath("/Maps/_CMU14/HuntingGrounds/jungle_moon.yml"),
            "CMUYautjaHuntingGroundJungle",
            ["alien_creature1.ogg", "alien_creature2.ogg", "alien_creature3.ogg"]),
        (
            new ResPath("/Maps/_CMU14/HuntingGrounds/desert_moon.yml"),
            "CMUYautjaHuntingGroundDesert",
            ["wind1.ogg", "wind2.ogg"]),
        (
            new ResPath("/Maps/_CMU14/HuntingGrounds/desert_moon_caves.yml"),
            "CMUYautjaHuntingGroundCaves",
            ["rocksfalling1.ogg", "rocksfalling2.ogg"]),
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
            var huntBeginPath = new ResPath("/Audio/_CMU14/Yautja/HuntingGrounds/hunt_begin.ogg");

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
    public async Task HuntingGroundCallsUseCmuRandomHumanoidSettings()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var consoles = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(prototype => prototype.TryGetComponent<YautjaHuntConsoleComponent>(out var component, factory) &&
                                    component!.Kind == YautjaHuntConsoleKind.HuntGround)
                .ToArray();

            Assert.That(consoles, Is.Not.Empty);

            var options = new List<YautjaHuntCallOption>();
            foreach (var console in consoles)
            {
                Assert.That(console.TryGetComponent<YautjaHuntConsoleComponent>(out var component, factory), Is.True, console.ID);
                options.AddRange(component!.HuntCallOptions);
            }

            var settings = options
                .SelectMany(option => option.Spawns)
                .Where(spawn => spawn.RandomHumanoidSettings is not null)
                .Select(spawn => spawn.RandomHumanoidSettings!.Value.Id)
                .Distinct()
                .ToArray();

            Assert.That(settings, Is.Not.Empty);
            foreach (var setting in settings)
            {
                Assert.That(setting, Does.StartWith("CMUYautjaHunt"), setting);
                Assert.That(setting, Does.Not.Contain("RMC"), setting);
                Assert.That(prototypes.HasIndex<RandomHumanoidSettingsPrototype>(setting), Is.True, setting);

                var randomSettings = prototypes.Index<RandomHumanoidSettingsPrototype>(setting);
                Assert.That(randomSettings.Components, Is.Not.Null, setting);
                var ghostRoleId = factory.GetComponentName<GhostRoleComponent>();
                Assert.That(randomSettings.Components!.TryGetComponent(ghostRoleId, out var ghostRoleValue),
                    Is.True, $"{setting} must define a CMU ghost-role override.");
                Assert.That(ghostRoleValue, Is.TypeOf<GhostRoleComponent>(), setting);
                Assert.That(((GhostRoleComponent) ghostRoleValue!).Requirements, Is.Empty,
                    $"{setting} must not inherit RMC playtime requirements.");
            }
        });

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
