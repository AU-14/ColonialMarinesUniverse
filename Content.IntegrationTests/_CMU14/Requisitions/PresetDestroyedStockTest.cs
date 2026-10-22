using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;

namespace Content.IntegrationTests.CMU14.Requisitions;

// BUG-1042: the night vision scope deletes itself in insurgency, but vendors and req still sold it
[TestFixture]
public sealed class PresetDestroyedStockTest
{
    private const string Scope = "CMUAttachmentNightVisionScope";
    private const string ScopeCrate = "CMUCrateAttachmentNightVisionScope";

    [TestCase("Insurgency", false)]
    [TestCase("ForceOnForce", true)]
    public async Task NightVisionScopeIsOnlyOfferedWhereItSurvives(string preset, bool offered)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            // the test ticker is a dummy that ignores SetGamePreset, so put the preset in place directly
            var ticker = entities.System<GameTicker>();
            typeof(GameTicker).GetProperty(nameof(GameTicker.Preset))!
                .SetValue(ticker, pair.Server.ProtoMan.Index<GamePresetPrototype>(preset));

            var vendor = entities.SpawnEntity("AU14UPPSquadLeaderVendor", map.GridCoords);
            var vendorEntries = entities.GetComponent<CMAutomatedVendorComponent>(vendor).Sections
                .SelectMany(s => s.Entries)
                .Select(e => e.Id.Id);
            Assert.That(vendorEntries.Contains(Scope), Is.EqualTo(offered), "squad leader vendor");

            var catalog = entities.SpawnEntity("UPPCargoCatalog", map.GridCoords);
            var crates = entities.GetComponent<RequisitionsComputerComponent>(catalog).Categories
                .SelectMany(c => c.Entries)
                .Select(e => e.Crate.Id);
            Assert.That(crates.Contains(ScopeCrate), Is.EqualTo(offered), "req catalog");
        });
        await pair.CleanReturnAsync();
    }
}
