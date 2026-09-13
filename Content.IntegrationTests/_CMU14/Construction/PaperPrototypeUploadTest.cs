using Content.IntegrationTests.Fixtures;
using Content.Shared.Sound.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Upload;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Construction;

[TestFixture]
public sealed class PaperPrototypeUploadTest : GameTest
{
    private const string UploadedPaperId = "CMUUploadedPaperTest";

    private const string UploadedPaper = $"""
        - type: entity
          id: {UploadedPaperId}
          parent: CMPaper
        """;

    [Test]
    public async Task PottedPlantChildCanBeUploadedWithCollectionBackedSounds()
    {
        const string id = "CMUUploadedPottedPlantTest";
        const string yaml = $"""
            - type: entity
              id: {id}
              parent: CMPottedPlant10
            """;
        var loader = Pair.Client.ResolveDependency<IGamePrototypeLoadManager>();
        await Pair.Client.WaitPost(() => loader.SendGamePrototype(yaml));
        await Pair.RunTicksSync(10);
        await Pair.Server.WaitAssertion(() => Assert.That(Pair.Server.ProtoMan.HasIndex<EntityPrototype>(id), Is.True));
        await Pair.Client.WaitAssertion(() => Assert.That(Pair.Client.ProtoMan.HasIndex<EntityPrototype>(id), Is.True));
    }

    [Test]
    public async Task PaperChildCanBeUploadedWithCollectionBackedHandlingSounds()
    {
        var pair = Pair;
        var prototypeLoader = pair.Client.ResolveDependency<IGamePrototypeLoadManager>();

        await pair.Client.WaitPost(() => prototypeLoader.SendGamePrototype(UploadedPaper));
        await pair.RunTicksSync(10);

        await AssertPaperSounds(pair.Server);
        await AssertPaperSounds(pair.Client);
    }

    private static async Task AssertPaperSounds(RobustIntegrationTest.IntegrationInstance instance)
    {
        await instance.WaitAssertion(() =>
        {
            var prototype = instance.ProtoMan.Index<EntityPrototype>(UploadedPaperId);
            var factory = instance.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                AssertSoundCollection<EmitSoundOnPickupComponent>(prototype, factory, "RMCPaperPickup");
                AssertSoundCollection<EmitSoundOnDropComponent>(prototype, factory, "RMCPaperDrop");
                AssertSoundCollection<EmitSoundOnLandComponent>(prototype, factory, "RMCPaperDrop");
            });
        });
    }

    private static void AssertSoundCollection<T>(
        EntityPrototype prototype,
        IComponentFactory factory,
        string expectedCollection)
        where T : BaseEmitSoundComponent, new()
    {
        Assert.That(prototype.TryComp<T>(out var component, factory), Is.True);
        Assert.That(component!.Sound, Is.TypeOf<SoundCollectionSpecifier>());
        Assert.That(((SoundCollectionSpecifier) component.Sound!).Collection?.Id, Is.EqualTo(expectedCollection));
    }
}
