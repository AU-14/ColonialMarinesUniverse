using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Threats;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Threats;

public sealed class CMUDistressSignalThirdPartyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("DistressSignal", true, false)]
    [TestCase("ColonyFall", true, true)]
    [TestCase("ColonyFall", false, false)]
    public async Task GhostCallStateRespectsPresetAndCvar(string preset, bool enabled, bool visible)
    {
        await Server.WaitAssertion(() =>
        {
            // The integration ticker ignores SetGamePreset.
            typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
                .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>(preset));
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.GhostThirdPartyCallEnabled, enabled);

            var state = Server.System<GhostThirdPartyCallSystem>().GetState();
            Assert.That(state is not null, Is.EqualTo(visible));
        });
    }

    [TestCase("MissionariesParty", 50)]
    [TestCase("MissionariesPartyMedium", 100)]
    public async Task MissionariesAreIneligibleInDistressSignal(string id, int players)
    {
        await Server.WaitAssertion(() =>
        {
            var party = SProtoMan.Index<ThirdPartyPrototype>(id);
            Assert.That(AuRoundSelectionRules.IsThirdPartyAllowed(party, "DistressSignal", null, null, null, players), Is.False);
            Assert.That(AuRoundSelectionRules.IsThirdPartyAllowed(party, "ColonyFall", null, null, null, players), Is.True);
        });
    }
}
