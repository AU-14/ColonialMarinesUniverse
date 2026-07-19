#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Content.Server.GuideGenerator;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReactionEntry))]
public sealed class ReactionEntryJsonTest
{
    private const string ReactionId = "ReactionEntryJsonTestReaction";
    private const string ReactantId = "ReactionEntryJsonTestReactant";
    private const string ProductId = "ReactionEntryJsonTestProduct";

    [TestPrototypes]
    private const string Prototypes = @"
- type: reagent
  id: ReactionEntryJsonTestReactant
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing

- type: reagent
  id: ReactionEntryJsonTestProduct
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing

- type: reaction
  id: ReactionEntryJsonTestReaction
  reactants:
    ReactionEntryJsonTestReactant:
      amount: 2
  products:
    ReactionEntryJsonTestProduct: 1
";

    [Test]
    public async Task PublishJson_UsesReagentIdsAsObjectKeys()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        string json = default!;

        await server.WaitAssertion(() =>
        {
            var prototype = server.ResolveDependency<IPrototypeManager>()
                .Index<ReactionPrototype>(ReactionId);

            IReadOnlyDictionary<ProtoId<ReagentPrototype>, ReactantInfo> typedReactants =
                prototype.Reactants;
            IReadOnlyDictionary<ProtoId<ReagentPrototype>, FixedPoint2> typedProducts =
                prototype.Products;

            Assert.Multiple(() =>
            {
                Assert.That(typedReactants.ContainsKey(ReactantId), Is.True);
                Assert.That(typedProducts.ContainsKey(ProductId), Is.True);
            });

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true);

            ReactionJsonGenerator.PublishJson(writer);
            writer.Flush();
            json = Encoding.UTF8.GetString(stream.ToArray());
        });

        using var document = JsonDocument.Parse(json);
        var reaction = document.RootElement.GetProperty(ReactionId);
        var reactant = reaction
            .GetProperty("reactants")
            .GetProperty(ReactantId);
        var product = reaction
            .GetProperty("products")
            .GetProperty(ProductId);

        Assert.Multiple(() =>
        {
            Assert.That(reactant.GetProperty("amount").GetSingle(), Is.EqualTo(2f));
            Assert.That(reactant.GetProperty("catalyst").GetBoolean(), Is.False);
            Assert.That(product.GetSingle(), Is.EqualTo(1f));
        });

        await pair.CleanReturnAsync();
    }
}
