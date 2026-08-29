#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Prototypes;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Storage;

[TestFixture]
public sealed class CMUStorageTests : GameTest
{
    private static readonly EntProtoId InfantryIfak = "AU14PouchIFAK";
    private static readonly EntProtoId InfantryIfakFill = "AU14PouchIFAKFill";
    private static readonly EntProtoId MedicalPouch = "RMCPouchMedical";
    private static readonly EntProtoId InfantryIfakTramadolPacket = "AU14PacketPillsTramadol";
    private static readonly EntProtoId EpinephrineAutoInjector = "CMEpinephrineAutoInjector";

    [Test]
    public async Task HemostaticGauzePacketFitsProdigyIfak()
    {
        var pair = Pair;
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var storageSystem = server.System<SharedStorageSystem>();

        await server.WaitAssertion(() =>
        {
            var pouch = entMan.SpawnEntity("AU14PouchIFAKProdigy", testMap.GridCoords);
            var packet = entMan.SpawnEntity("AU14HemostaticGauzePacket", testMap.GridCoords);

            Assert.That(storageSystem.CanInsert(pouch, packet, out var reason), Is.True, reason);
        });
    }

    [Test]
    public async Task NormalInfantryIfakUsesTramadolPacketInsteadOfEpinephrine()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var protoManager = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(protoManager.TryIndex<EntityPrototype>(InfantryIfakFill, out var ifak), Is.True);
            Assert.That(ifak!.TryComp<StorageFillComponent>(out var fill, factory), Is.True);

            var contents = fill!.Contents
                .Where(entry => entry.PrototypeId != null)
                .Select(entry => entry.PrototypeId!.Value)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(contents, Does.Contain(InfantryIfakTramadolPacket));
                Assert.That(contents, Does.Not.Contain(EpinephrineAutoInjector));
            });
        });
    }

    [Test]
    public async Task MedicalPouchMatchesInfantryIfakStorageSpace()
    {
        var server = Pair.Server;

        await server.WaitAssertion(() =>
        {
            var protoManager = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(protoManager.TryIndex<EntityPrototype>(InfantryIfak, out var ifak), Is.True);
            Assert.That(protoManager.TryIndex<EntityPrototype>(MedicalPouch, out var medical), Is.True);
            Assert.That(ifak!.TryComp<StorageComponent>(out var ifakStorage, factory), Is.True);
            Assert.That(medical!.TryComp<StorageComponent>(out var medicalStorage, factory), Is.True);

            Assert.That(medicalStorage!.Grid.GetArea(), Is.EqualTo(ifakStorage!.Grid.GetArea()));
        });
    }
}
