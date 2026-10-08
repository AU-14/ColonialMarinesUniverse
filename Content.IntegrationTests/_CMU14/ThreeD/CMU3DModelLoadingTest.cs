using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ThreeD;
using Robust.Shared.Prototypes;
using Robust.Client.ResourceManagement;
using Serilog.Events;

namespace Content.IntegrationTests.CMU14.ThreeD;

/// <summary>Exercises the actual resource loader, including vector/color deserialization and source IDs.</summary>
[TestFixture]
public sealed class CMU3DModelLoadingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Fresh = true, Dirty = true };

    [Test]
    public async Task OptionalLibraryLoadsOnlyOnClientAndCanReloadAfterPrototypeReset()
    {
        await Client.WaitAssertion(() =>
        {
            Assert.That(CProtoMan.EnumeratePrototypes<CMU3DModelPrototype>(), Is.Empty,
                "Joining in 2D must not parse or retain optional model geometry.");
            var library = Client.ResolveDependency<CMU3DModelLibrary>();
            library.LoadWorld();
            Validate(CProtoMan);
            Assert.That(CProtoMan.EnumeratePrototypes<CMU3DEquipmentPosePrototype>(), Is.Empty,
                "The live renderer uses mob sprites and does not need attachment definitions.");
            var model = CProtoMan.EnumeratePrototypes<CMU3DModelPrototype>().First();
            library.LoadWorld();
            Assert.That(CProtoMan.Index<CMU3DModelPrototype>(model.ID), Is.SameAs(model),
                "Repeated opt-ins must reuse the parsed library.");

            // CMU ignores map definitions on clients. Reset re-registers the shared
            // kind and logs this existing warning; all other loader warnings still fail.
            bool IgnoredMapWarning(string sawmill, LogEvent message) =>
                sawmill == "proto" && message.Level == LogEventLevel.Warning &&
                message.RenderMessage() == "Registering an ignored prototype Content.Shared.Maps.GameMapPrototype";
            Pair.ClientLogHandler.JudgeLog += IgnoredMapWarning;
            try
            {
                CProtoMan.Reset();
            }
            finally
            {
                Pair.ClientLogHandler.JudgeLog -= IgnoredMapWarning;
            }
            library.LoadWorld();
            Validate(CProtoMan);
            Assert.That(CProtoMan.Index<CMU3DModelPrototype>(model.ID), Is.Not.SameAs(model),
                "Replay/prototype resets must not leave the loader believing removed models are still available.");
        });
        await Server.WaitAssertion(() =>
            Assert.That(SProtoMan.EnumeratePrototypes<CMU3DModelPrototype>(), Is.Empty,
                "A client's 3D opt-in must not load presentation geometry on the shared server."));
    }

    [Test]
    public async Task WorkbenchCanSelectEveryModelAndCloseAndReopen()
    {
        await Client.WaitAssertion(() =>
        {
            var preview = CEntMan.System<CMU3DPreviewSystem>();
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            try
            {
                Assert.That(preview.Open(), Is.True);
                Validate(prototypes);
                Assert.That(prototypes.EnumeratePrototypes<CMU3DModelPrototype>().Any(model => model.EquipmentOnly), Is.True,
                    "The authoring workbench must still make equipment drafts available.");
                foreach (var model in prototypes.EnumeratePrototypes<CMU3DModelPrototype>())
                    Assert.That(preview.Open(model.ID), Is.True, model.ID);
                Assert.That(preview.Open("CMU3DThisModelDoesNotExist"), Is.False);
                preview.Close();
                Assert.That(preview.Open(), Is.True);
            }
            finally
            {
                preview.Close();
            }
        });
    }

    private static void Validate(IPrototypeManager prototypes)
    {
        var count = 0;
        var slots = new HashSet<ushort>();
        foreach (var surface in prototypes.EnumeratePrototypes<CMU3DSurfacePrototype>())
        {
            Assert.That(surface.AtlasIndex, Is.InRange(1, CMU3DSurfacePrototype.MaximumAtlasIndex), surface.ID);
            Assert.That(slots.Add(surface.AtlasIndex), Is.True, $"{surface.ID}: duplicate atlas slot");
        }
        foreach (var model in prototypes.EnumeratePrototypes<CMU3DModelPrototype>())
        {
            count++;
            Assert.That(model.Label, Is.Not.Empty, model.ID);
            Assert.That(model.Status, Is.AnyOf("draft", "reviewed"), model.ID);
            Assert.That(model.Parts.Count, Is.InRange(1, model.EquipmentOnly ? 512 : 128), model.ID);
            if (model.EquipmentOnly)
            {
                Assert.That(model.SourcePrototypes, Is.Empty, model.ID);
                Assert.That(model.RandomSpritePrototypes, Is.Empty, model.ID);
            }
            foreach (var part in model.Parts)
            {
                Assert.That(part.Valid, Is.True, $"{model.ID}: {part.Label}");
                if (part.Surface is { } surface)
                    Assert.That(prototypes.HasIndex(surface), Is.True, $"{model.ID}: missing surface {surface}");
            }
            foreach (var source in model.SourcePrototypes)
                Assert.That(prototypes.HasIndex<EntityPrototype>(source), Is.True, $"{model.ID}: missing source {source}");
            if (model.ReferencePrototype is { } reference)
                Assert.That(prototypes.HasIndex<EntityPrototype>(reference), Is.True, $"{model.ID}: missing state art reference");
            if (model.AlternateDoorModel is { } alternateId)
            {
                Assert.That(prototypes.TryIndex(alternateId, out var alternate), Is.True, $"{model.ID}: missing alternate pose");
                Assert.That(alternate!.DoorState, Is.Not.EqualTo(model.DoorState), model.ID);
                Assert.That(alternate.AlternateDoorModel?.Id, Is.EqualTo(model.ID), model.ID);
                Assert.That(model.ReferenceRsi, Is.Not.Null.And.Not.Empty, model.ID);
                Assert.That(model.ReferenceState, Is.Not.Null.And.Not.Empty, model.ID);
            }
            if (model.AlternateFoldModel is { } alternateFoldId)
            {
                Assert.That(prototypes.TryIndex(alternateFoldId, out var alternate), Is.True, $"{model.ID}: missing fold pose");
                Assert.That(alternate!.Folded, Is.Not.EqualTo(model.Folded), model.ID);
                Assert.That(alternate.AlternateFoldModel?.Id, Is.EqualTo(model.ID), model.ID);
                Assert.That(model.ReferenceRsi, Is.Not.Null.And.Not.Empty, model.ID);
                Assert.That(model.ReferenceState, Is.Not.Null.And.Not.Empty, model.ID);
            }
        }
        Assert.That(count, Is.GreaterThan(0), "The CMU resource mount must include the model library.");
    }

    [Test]
    public async Task PrintedArtworkLoadsFromTheActualClientResourceMount()
    {
        await Client.WaitAssertion(() =>
        {
            Client.ResolveDependency<CMU3DModelLibrary>().LoadWorld();
            var surfaces = CMU3DSceneSurfaces.Load(Client.ResolveDependency<IPrototypeManager>(),
                Client.ResolveDependency<IResourceCache>());
            Assert.That(surfaces.Width, Is.GreaterThan(1));
            Assert.That(surfaces.AtlasPixels().Any(pixel => pixel.A >= 128), Is.True);
        });
    }
}
