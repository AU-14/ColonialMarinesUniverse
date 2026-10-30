using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Localization;

/// <summary>
///     RMC's names show up under their lore names on both sides, from prototype text alone.
/// </summary>
[TestFixture]
public sealed class CMULoreNamesTest : GameTest
{
    [Test]
    public async Task EntityTextIsConverted()
    {
        await Server.WaitAssertion(() => AssertConverted(SProtoMan));
        await Client.WaitAssertion(() => AssertConverted(CProtoMan));
    }

    [TestCase("UNMC flair and the RCM-P outpost", "USCM flair and the RCM-P outpost")]
    [TestCase("a Xenonid's xenonids; WeYa, We-Ya, Weston-Yamada", "a Xenomorph's xenomorphs; WeYu, We-Yu, Weyland-Yutani")]
    [TestCase("TSE, SPP and RCM", "TWE, UPP and RMC")]
    [TestCase("UNMCX TSEA", "UNMCX TSEA")]
    public void Convert(string input, string expected)
    {
        Assert.That(CMULoreNamesSystem.Convert(input), Is.EqualTo(expected));
    }

    private static void AssertConverted(IPrototypeManager protos)
    {
        var flair = protos.Index<EntityPrototype>("RMCHelmetGarbUNMCFlair");
        Assert.That(flair.Name, Is.EqualTo("USCM flair"));

        var file = protos.Index<EntityPrototype>("CMPamphletLanguageXenonid");
        Assert.That(file.Description, Does.Contain("Weyland-Yutani research summary"));
        Assert.That(file.Description, Does.Contain("'Xenomorph'"));
        Assert.That(file.Description, Does.Not.Contain("Xenonid"));
    }
}
