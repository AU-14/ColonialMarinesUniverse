using Content.Client.Guidebook;
using Content.Client.Guidebook.Richtext;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Content.IntegrationTests.Utility;
using Content.Shared.Guidebook;
using Robust.Shared.Configuration;
using Robust.UnitTesting;
using Robust.Shared.Log;

namespace Content.IntegrationTests.Tests.Guidebook;

[TestFixture]
[TestOf(typeof(GuidebookSystem))]
[TestOf(typeof(GuideEntryPrototype))]
[TestOf(typeof(DocumentParsingManager))]
public sealed class GuideEntryPrototypeTests : GameTest
{
    private static string[] _guideEntries = GameDataScrounger.PrototypesOfKind<GuideEntryPrototype>();

    [Test]
    [TestCaseSource(nameof(_guideEntries))]
    [Description("Ensures a given guidebook entry is valid, checking the document/etc.")]
    public async Task Validate(string protoKey)
    {
        var pair = Pair;
        var client = pair.Client;
        await client.WaitIdleAsync();
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var resMan = client.ResolveDependency<IResourceManager>();
        var parser = client.ResolveDependency<DocumentParsingManager>();
        var proto = protoMan.Index<GuideEntryPrototype>(protoKey);

        // RMC14: The "all reagents" page is larger than the arbitrary limit.
        //        This makes it so that it takes 2 ticks to render it.
        //        Which in turn logs a warning that would fail this test.
        var cfg = client.ResolveDependency<IConfigurationManager>();
        var originalFailLevel = cfg.GetCVar(RTCVars.FailureLogLevel);
        cfg.SetCVar(RTCVars.FailureLogLevel, LogLevel.Error);

        foreach (var proto in prototypes)
        {
            using var reader = resMan.ContentFileReadText(proto.Text);
            var text = reader.ReadToEnd();

            // Avoid styleguide update limit
            await client.WaitRunTicks(2);
        }

        cfg.SetCVar(RTCVars.FailureLogLevel, originalFailLevel);
        await pair.CleanReturnAsync();
    }
}
