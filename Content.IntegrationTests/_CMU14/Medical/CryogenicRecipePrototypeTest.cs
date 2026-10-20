using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CryogenicRecipePrototypeTest : GameTest
{
    private static readonly ProtoId<ReactionPrototype> MarineCryoxadone = "CMCryoxadone";

    [Test]
    [RunOnSide(Side.Server)]
    public void CryogenicMedicinesRequireMarineCryoxadone()
    {
        foreach (var id in new[]
                 {
                     "CMUAloxadone", "CMUTraumoxadone", "CMUUrishoxadone", "CMUDoxarubixadone", "CMUNecrosol",
                     "Aloxadone", "Arcryox", "Urishoxadone", "Doxarubixadone", "Necrosol",
                     "CMClonexadone"
                 })
        {
            var reaction = SProtoMan.Index<ReactionPrototype>(id);
            Assert.That(reaction.Reactants.ContainsKey("CMCryoxadone"), Is.True, $"{id} must use Marine Cryoxadone");
            Assert.That(reaction.Reactants.ContainsKey("Cryoxadone"), Is.False, $"{id} must not use standard Cryoxadone");
        }

        var marineCryoxadone = SProtoMan.Index(MarineCryoxadone);
        Assert.That(marineCryoxadone.Products.ContainsKey("CMCryoxadone"), Is.True);
    }
}
