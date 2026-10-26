using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Ghost;

/// <summary>
///     Observing from the lobby gives a ghost that looks like the player's selected character, dressed in their job's
///     clothes, rather than the default ghost. The body it copies rides on the ghost, hidden like the ghost is.
/// </summary>
[TestFixture]
public sealed class CMUObserverProfileAppearanceTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task ObserverLooksLikeSelectedCharacter(bool hasPreferredJob)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        await pair.CreateTestMap();
        var player = server.PlayerMan.Sessions.Single();

        await server.WaitAssertion(() =>
        {
            var preferences = server.ResolveDependency<IServerPreferencesManager>();
            var current = preferences.GetPreferences(player.UserId);
            var jobs = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
            if (hasPreferredJob)
                jobs.Add(SharedGameTicker.FallbackOverflowJob, JobPriority.High);

            preferences.SetProfile(player.UserId, current.SelectedCharacterIndex,
                current.SelectedCharacter.WithJobPriorities(jobs)).Wait();

            // Straight from the lobby: no mind and no body to copy yet.
            server.EntMan.System<SharedMindSystem>().WipeMind(player);
            server.EntMan.System<GameTicker>().JoinAsObserver(player);
        });
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var ghost = player.AttachedEntity;
            Assert.That(ghost, Is.Not.Null);
            Assert.That(entities.HasComponent<GhostComponent>(ghost), Is.True);

            Assert.That(entities.TryGetComponent<CMUGhostAppearanceComponent>(ghost, out var appearance), Is.True,
                "A lobby observer's ghost should take on their character's look.");
            var standIn = appearance!.Source;
            Assert.That(standIn, Is.Not.Null);
            Assert.That(entities.HasComponent<CMUGhostAppearanceStandInComponent>(standIn), Is.True);
            Assert.That(entities.GetComponent<TransformComponent>(standIn!.Value).ParentUid, Is.EqualTo(ghost),
                "The stand-in rides on the ghost and goes away with it.");
            Assert.That(entities.GetComponent<VisibilityComponent>(standIn.Value).Layer,
                Is.EqualTo(entities.GetComponent<VisibilityComponent>(ghost!.Value).Layer),
                "Whoever can't see the ghost mustn't be sent its stand-in either.");
            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(standIn.Value, "ears", out _), Is.True,
                "The stand-in wears the preferred or fallback job's equipment.");
        });

        await pair.CleanReturnAsync();
    }
}
