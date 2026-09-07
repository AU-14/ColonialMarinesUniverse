using Content.IntegrationTests.Fixtures;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Botany.Components;
using Content.Shared.CMU14.EntityReferences;
using Content.Shared.CMU14.Fishing.Components;
using Content.Shared.CMU14.Threats.Mobs.Ape;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class EntityReferenceCleanupTest : GameTest
{
    [Test]
    public async Task DeletedTargetsClearNetworkedReferencesWithoutClearingReplacements()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var references = SEntMan.System<EntityReferenceSystem>();
            var target = SEntMan.SpawnEntity(null, map.GridCoords);
            var owner = SEntMan.SpawnEntity(null, map.GridCoords);
            var replacement = SEntMan.SpawnEntity(null, map.GridCoords);
            var jukebox = SEntMan.EnsureComponent<JukeboxComponent>(owner);
            var ape = SEntMan.EnsureComponent<ApeLeapComponent>(owner);
            var fishing = SEntMan.EnsureComponent<ActiveFishingSpotComponent>(owner);
            var tray = SEntMan.EnsureComponent<PlantTrayComponent>(owner);
#pragma warning disable RA0002 // Reproduce references recorded before the target starts terminating.
            jukebox.AudioStream = target;
            ape.LastHit = target;
            fishing.AttachedFishingLure = target;
            fishing.IsActive = true;
            tray.PlantEntity = replacement;
#pragma warning restore RA0002
            references.Watch(owner, target);
            references.Watch(owner, replacement);

            SEntMan.DeleteEntity(target);
            Assert.Multiple(() =>
            {
                Assert.That(jukebox.AudioStream, Is.Null);
                Assert.That(ape.LastHit, Is.Null);
                Assert.That(fishing.AttachedFishingLure, Is.Null);
                Assert.That(fishing.IsActive, Is.False);
                Assert.That(tray.PlantEntity, Is.EqualTo(replacement));
            });

            SEntMan.DeleteEntity(replacement);
            Assert.That(tray.PlantEntity, Is.Null);
            SEntMan.DeleteEntity(owner);
        });
    }
}
