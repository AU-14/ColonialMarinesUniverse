#nullable enable

using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Round;

[TestFixture]
public sealed class CMURoundDirectorStateTest
{
    [Test]
    public void FirstSelectionWinsUntilTheGenerationResets()
    {
        var state = new CMURoundDirectorState();
        var first = Selection("DistressSignal", "LV624", "LV624Map");
        var later = Selection("Insurgency", "Solaris", "SolarisMap");

        Assert.Multiple(() =>
        {
            Assert.That(state.TryFreezeSelection(first, out var frozen), Is.True);
            Assert.That(frozen, Is.EqualTo(first));
            Assert.That(state.TryFreezeSelection(later, out frozen), Is.False);
            Assert.That(frozen, Is.EqualTo(first));
            Assert.That(state.Phase, Is.EqualTo(CMURoundPhase.SelectionFrozen));
        });
    }

    [Test]
    public void WorldStagesOnlyAdvanceWhenTheirPrerequisitesAreReady()
    {
        var state = new CMURoundDirectorState();

        Assert.Multiple(() =>
        {
            Assert.That(state.TryMarkMapsLoaded(), Is.False);
            Assert.That(state.TryMarkWorldInitialized(), Is.False);
            Assert.That(state.TryMarkPlayersSpawned(), Is.False);
            Assert.That(state.TryEnterRound(), Is.False);
        });

        state.TryFreezeSelection(Selection("ColonyFall", "LV624", "LV624Map"), out _);

        Assert.Multiple(() =>
        {
            Assert.That(state.TryMarkMapsLoaded(), Is.True);
            Assert.That(state.TryMarkWorldInitialized(), Is.True);
            Assert.That(state.TryMarkPlayersSpawned(), Is.True);
            Assert.That(state.TryEnterRound(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(CMURoundPhase.InRound));
            Assert.That(
                state.Prerequisites,
                Is.EqualTo(
                    CMURoundPrerequisite.SelectionFrozen |
                    CMURoundPrerequisite.MapsLoaded |
                    CMURoundPrerequisite.WorldInitialized |
                    CMURoundPrerequisite.PlayersSpawned));
        });
    }

    [Test]
    public void ResetStartsANewGenerationAndDropsFrozenState()
    {
        var state = new CMURoundDirectorState();
        state.TryFreezeSelection(Selection("DistressSignal", "LV624", "LV624Map"), out _);
        var generation = state.Generation;

        state.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(state.Generation, Is.EqualTo(generation + 1));
            Assert.That(state.Phase, Is.EqualTo(CMURoundPhase.AwaitingSelection));
            Assert.That(state.Prerequisites, Is.EqualTo(CMURoundPrerequisite.None));
            Assert.That(state.Selection, Is.Null);
            Assert.That(
                state.TryFreezeSelection(Selection("Insurgency", "Solaris", "SolarisMap"), out var frozen),
                Is.True);
            Assert.That(frozen.PresetId, Is.EqualTo("Insurgency"));
        });
    }

    private static RoundPlanSelectionSnapshot Selection(string preset, string planet, string map)
    {
        return new RoundPlanSelectionSnapshot(
            preset,
            80,
            "GovforPlatoon",
            "OpforPlatoon",
            planet,
            map,
            null,
            "GovforShip",
            "OpforShip");
    }
}
