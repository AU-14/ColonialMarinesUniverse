using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Item.Stain;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.FootPrint;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Item;

[TestFixture]
[TestOf(typeof(CMUItemStainSystem))]
public sealed class CMUItemStainTest
{
    private const string StainableItem = "CMUItemStainTestItem";
    private const string CleanerItem = "CMUItemStainTestCleaner";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: BaseItem
  id: {StainableItem}

- type: entity
  parent: BaseItem
  id: {CleanerItem}
  components:
  - type: CleansForensics
    cleanDelay: 0.1
  - type: Forensics

";

    [Test]
    public async Task ForensicCleanerUsesOneCanonicalDoAfterForEvidenceAndStains()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid forensicUser = default;
        EntityUid forensicItem = default;
        EntityUid forensicCleaner = default;
        EntityUid stainUser = default;
        EntityUid stainItem = default;
        EntityUid stainCleaner = default;
        EntityUid bothUser = default;
        EntityUid bothItem = default;
        EntityUid bothCleaner = default;
        EntityUid neitherUser = default;
        EntityUid neitherItem = default;
        EntityUid neitherCleaner = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stains = entMan.System<CMUItemStainSystem>();
            var forensics = entMan.System<ForensicsSystem>();

            forensicUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            forensicItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            forensicCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);
            var forensicEvidence = entMan.EnsureComponent<ForensicsComponent>(forensicItem);
            forensicEvidence.Fingerprints.Add("forensic-only");
            forensicEvidence.CleanDistance = 2.75f;

            stainUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            stainItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            stainCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);
            Assert.That(stains.TryStain(stainItem, CMUItemStainKind.Blood, Color.Red), Is.True);

            bothUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            bothItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            bothCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);
            var bothEvidence = entMan.EnsureComponent<ForensicsComponent>(bothItem);
            bothEvidence.Fibers.Add("both");
            bothEvidence.CleanDistance = 3.25f;
            Assert.That(stains.TryStain(bothItem, CMUItemStainKind.Oil, Color.Black), Is.True);

            neitherUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            neitherItem = entMan.SpawnEntity(StainableItem, MapCoordinates.Nullspace);
            neitherCleaner = entMan.SpawnEntity(CleanerItem, MapCoordinates.Nullspace);

            Assert.Multiple(() =>
            {
                Assert.That(StartCleaning(forensicCleaner, forensicUser, forensicItem), Is.True);
                Assert.That(StartCleaning(stainCleaner, stainUser, stainItem), Is.True);
                Assert.That(StartCleaning(bothCleaner, bothUser, bothItem), Is.True);
                Assert.That(StartCleaning(neitherCleaner, neitherUser, neitherItem), Is.False);
            });

            AssertCleaningDoAfter(forensicUser, forensicCleaner, forensicItem, 2.75f);
            AssertCleaningDoAfter(stainUser, stainCleaner, stainItem, 1.5f);
            AssertCleaningDoAfter(bothUser, bothCleaner, bothItem, 3.25f);

            var neitherDoAfters = entMan.GetComponent<DoAfterComponent>(neitherUser);
            Assert.That(neitherDoAfters.DoAfters.Values.Count(IsActive), Is.Zero);

            bool StartCleaning(EntityUid cleaner, EntityUid user, EntityUid target)
            {
                var cleanerComponent = entMan.GetComponent<CleansForensicsComponent>(cleaner);
                return forensics.TryStartCleaning((cleaner, cleanerComponent), user, target);
            }

            void AssertCleaningDoAfter(
                EntityUid user,
                EntityUid cleaner,
                EntityUid target,
                float expectedDistance)
            {
                var component = entMan.GetComponent<DoAfterComponent>(user);
                var active = component.DoAfters.Values.Where(IsActive).ToArray();
                Assert.That(active, Has.Length.EqualTo(1));
                Assert.Multiple(() =>
                {
                    Assert.That(active[0].Args.Event, Is.TypeOf<CleanForensicsDoAfterEvent>());
                    Assert.That(active[0].Args.EventTarget, Is.EqualTo(cleaner));
                    Assert.That(active[0].Args.Target, Is.EqualTo(target));
                    Assert.That(active[0].Args.Used, Is.EqualTo(cleaner));
                    Assert.That(active[0].Args.DistanceThreshold, Is.EqualTo(expectedDistance).Within(0.001f));
                });
            }

            static bool IsActive(DoAfter doAfter) => !doAfter.Cancelled && !doAfter.Completed;
        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stains = entMan.System<CMUItemStainSystem>();
            var forensicEvidence = entMan.GetComponent<ForensicsComponent>(forensicItem);
            var bothEvidence = entMan.GetComponent<ForensicsComponent>(bothItem);

            Assert.Multiple(() =>
            {
                Assert.That(forensicEvidence.Fingerprints, Is.Empty);
                Assert.That(entMan.GetComponent<CMUItemStainComponent>(stainItem).Color, Is.Null);
                Assert.That(bothEvidence.Fibers, Is.Empty);
                Assert.That(entMan.GetComponent<CMUItemStainComponent>(bothItem).Color, Is.Null);
                Assert.That(stains.TryClean(stainItem), Is.False,
                    "The stain must have been cleaned by the single canonical completion.");
                Assert.That(stains.TryClean(bothItem), Is.False,
                    "The shared completion must not leave a second stain-cleaning pass.");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[]
                     {
                         forensicUser, forensicItem, forensicCleaner,
                         stainUser, stainItem, stainCleaner,
                         bothUser, bothItem, bothCleaner,
                         neitherUser, neitherItem, neitherCleaner,
                     })
            {
                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

}
