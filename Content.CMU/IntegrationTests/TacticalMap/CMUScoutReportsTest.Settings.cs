using System.IO;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.CMU14.Radio;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Radio;
using Content.Shared.CMU14.TacticalMap.Scout;
using Content.Shared.Radio.Components;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUScoutReportsTest
{
    private static PoolSettings YamlSettingsPair => new() { Connected = true, Dirty = true };

    private const string OverriddenScoutSettings = """
- type: cmuScoutReportSettings
  id: CMUScoutReports
  maxActiveReports: 2
  lifetime: 7
  cooldown: 0.25
  reportRange: 5
  radioChannels:
    GOVFOR: [radioGovforCommand]
""";

    [Test]
    [PairConfig(nameof(YamlSettingsPair))]
    public async Task YamlSettingsControlReportsActionsAndRadio()
    {
        var changed = new Dictionary<Type, HashSet<string>>();
        await Server.WaitPost(() =>
            SProtoMan.LoadString(OverriddenScoutSettings, overwrite: true, changed: changed));
        await Server.WaitPost(() => SProtoMan.ReloadPrototypes(changed));
        await Scope();

        var session = ServerSession!;
        var original = session.AttachedEntity;
        NetEntity vehicleAction = default;
        int[] reportKeys = [];
        try
        {
            await Server.WaitAssertion(() =>
            {
                var optics = SComp<CMUScoutOpticsComponent>(_optics);
                foreach (var action in new[] { optics.InfantryAction, optics.VehicleAction, optics.EmplacementAction, optics.UnknownAction })
                {
                    var uid = action!.Value;
                    Assert.That(SComp<ActionComponent>(uid).UseDelay, Is.EqualTo(TimeSpan.FromSeconds(0.25)));
                    Assert.That(SComp<TargetActionComponent>(uid).Range, Is.LessThanOrEqualTo(0),
                        "The generic action must not impose a second, fixed reporting range.");
                    Assert.That(SComp<MetaDataComponent>(uid).EntityDescription,
                        Does.Contain("7 seconds").And.Contain("0.25 seconds").And.Contain("2 active reports").And.Contain("5 tiles"));
                }
                vehicleAction = SEntMan.GetNetEntity(optics.VehicleAction!.Value);

                var callsign = SEntMan.EnsureComponent<AU14CallsignComponent>(_actor);
                callsign.Faction = "govfor";
                callsign.Callsign = "PROPHET 2-1";
                var relay = SEntMan.SpawnAttachedTo(null, Target);
                var anchor = SEntMan.AddComponent<ANPRCRelayAnchorComponent>(relay);
                anchor.Channels.Add("radioGovforCommand");
                SEntMan.System<ANPRCRangeSystem>().RefreshAnchorChannels((relay, anchor));
                var receiver = SEntMan.SpawnAttachedTo(null, Target);
                SEntMan.AddComponent<CMUScoutRadioReceiverComponent>(receiver);
                SEntMan.AddComponent<ActiveRadioComponent>(receiver).Channels.Add("radioGovforCommand");
                var recorder = SEntMan.System<CMUScoutRadioRecorderSystem>();
                recorder.Sent.Clear();
                recorder.Received.Clear();

                Assert.That(Report(target: new EntityCoordinates(_map, new Vector2(5.51f, 0.5f))), Is.False,
                    "A location just outside the YAML range must be rejected.");
                Assert.That(Report(target: new EntityCoordinates(_map, new Vector2(5.5f, 0.5f))), Is.True,
                    "The exact configured range is inclusive and rejection must not consume cooldown.");
                var report = Reporter.Reports.Single();
                var info = report.Blip.ScoutReport!.Value;
                reportKeys = [report.Key];
                Assert.That(info.ExpiresAt - info.ReportedAt, Is.EqualTo(TimeSpan.FromSeconds(7)));
                Assert.That(Reporter.NextReport - info.ReportedAt, Is.EqualTo(TimeSpan.FromSeconds(0.25)));
                Assert.That(Report(), Is.False, "The configured cooldown must be enforced immediately.");
                Assert.That(recorder.Sent.Single().Channel.ID, Is.EqualTo("radioGovforCommand"));
                Assert.That(recorder.Received.Single().Receiver, Is.EqualTo(receiver));
                Server.PlayerMan.SetAttachedEntity(session, _actor);
            });

            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var action = CEntMan.GetEntity(vehicleAction);
                Assert.That(CEntMan.GetComponent<ActionComponent>(action).UseDelay, Is.EqualTo(TimeSpan.FromSeconds(0.25)));
                Assert.That(CEntMan.GetComponent<MetaDataComponent>(action).EntityDescription,
                    Does.Contain("7 seconds").And.Contain("0.25 seconds").And.Contain("2 active reports").And.Contain("5 tiles"));
            });

            await Pair.RunSeconds(0.3f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Report(CMUScoutContactType.Infantry), Is.True);
                Assert.That(Reporter.Reports, Has.Count.EqualTo(2));
                reportKeys = Reporter.Reports.Select(report => report.Key).ToArray();
            });
            await Pair.RunSeconds(0.3f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Report(CMUScoutContactType.Emplacement), Is.True);
                Assert.That(Reporter.Reports, Has.Count.EqualTo(2));
                Assert.That(Reporter.Reports.Select(report => report.Key), Does.Not.Contain(reportKeys[0]).And.Contain(reportKeys[1]));
                Assert.That(_tactical.TryGetIntelBlipGrid(reportKeys[0], out _), Is.False);
                reportKeys = Reporter.Reports.Select(report => report.Key).ToArray();
            });

            await Pair.RunSeconds(7);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Reporter.Reports, Is.Empty, "Reports must actually expire after the YAML lifetime.");
                foreach (var key in reportKeys)
                    Assert.That(_tactical.TryGetIntelBlipGrid(key, out _), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
        }
    }

    [TestCase(0, 7, 0.25, 5)]
    [TestCase(-1, 7, 0.25, 5)]
    [TestCase(2, 0, 0.25, 5)]
    [TestCase(2, -1, 0.25, 5)]
    [TestCase(2, 7, -0.25, 5)]
    [TestCase(2, 7, 0.25, 0)]
    [TestCase(2, 7, 0.25, -1)]
    [TestCase(2, 7, 0.25, float.NaN)]
    [TestCase(2, 7, 0.25, float.PositiveInfinity)]
    public async Task InvalidScoutSettingsAreRejected(int limit, double lifetime, double cooldown, float range)
    {
        await Server.WaitAssertion(() =>
        {
            var yaml = FormattableString.Invariant($$"""
id: CMUScoutReportInvalidSettings
maxActiveReports: {{limit}}
lifetime: {{lifetime}}
cooldown: {{cooldown}}
reportRange: {{range}}
radioChannels: {}
""");
            using var reader = new StringReader(yaml);
            var stream = new YamlStream();
            stream.Load(reader);
            var node = new MappingDataNode((YamlMappingNode) stream.Documents[0].RootNode);
            var serialization = Server.ResolveDependency<ISerializationManager>();
            Assert.Throws<InvalidDataException>(() =>
                serialization.Read<CMUScoutReportSettingsPrototype>(node, notNullableOverride: true));
        });
    }
}
