using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Server.Station.Systems;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Damage;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaFeedbackRegressionTest
{

    [Test]
    public async Task MedicompClampRepairsResidualWoundsWithoutAggregateDamage()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid patient = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var body = entMan.System<SharedBodySystem>();
                EntityUid part = default;
                foreach (var (partUid, _) in body.GetBodyChildren(patient))
                {
                    if (entMan.HasComponent<BodyPartComponent>(partUid))
                    {
                        part = partUid;
                        break;
                    }
                }

                Assert.That(part, Is.Not.EqualTo(EntityUid.Invalid));
                var partHealth = entMan.System<SharedBodyPartHealthSystem>();
                Assert.That(partHealth.TryApplyPartDamage(patient, part,
                    new DamageSpecifier { DamageDict = { ["Slash"] = 12 } }), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(part);
                var woundSystem = entMan.System<CMUWoundLedgerSystem>();
                Assert.That(woundSystem.GetEntries(wounds), Is.Not.Empty);
                Assert.That(woundSystem.TryUpdateExternalBleeding(part, ExternalBleedTier.Arterial, wounds), Is.True);

                var surgery = entMan.System<SharedCMSurgerySystem>();
                var valid = new CMSurgeryValidEvent(patient, part);
                entMan.EventBus.RaiseLocalEvent(surgery.GetSingleton("CMUSurgeryMcompWounds")!.Value, ref valid);
                Assert.That(valid.Cancelled, Is.False, "Residual local injury must remain treatable after aggregate healing.");
                var step = surgery.GetSingleton("CMUSurgeryStepMcompClampWound");
                Assert.That(step, Is.Not.Null);
                var ev = new CMSurgeryStepEvent(patient, patient, part, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);

                Assert.That(entMan.HasComponent<BodyPartWoundComponent>(part), Is.False,
                    "Closing the medicomp treatment must remove wounds, not only their bleeding.");
                var health = entMan.GetComponent<BodyPartHealthComponent>(part);
                Assert.That(health.Current, Is.EqualTo(health.Max));
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task MedicompClampClosesIncisionAndSurgicalBleeding()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid patient = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                patient = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
                var body = entMan.System<SharedBodySystem>();
                var part = body.GetBodyChildren(patient)
                    .Select(entry => entry.Id)
                    .First(uid => entMan.HasComponent<BodyPartComponent>(uid));

                entMan.EnsureComponent<CMIncisionOpenComponent>(part);
                entMan.EnsureComponent<CMBleedersClampedComponent>(part);
                entMan.EnsureComponent<CMSkinRetractedComponent>(part);
                var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
                var ledger = entMan.System<CMUWoundLedgerSystem>();
                ledger.TryUpdateExternalBleeding(part, ExternalBleedTier.Severe, wounds);

                var woundSystem = entMan.System<SharedCMUWoundsSystem>();
                woundSystem.SeedSurgicalInternalBleed(part);

                var surgery = entMan.System<SharedCMSurgerySystem>();
                var step = surgery.GetSingleton("CMUSurgeryStepMcompClampWound");
                Assert.That(step, Is.Not.Null);
                var ev = new CMSurgeryStepEvent(patient, patient, part, new List<EntityUid>());
                entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<CMBleedersClampedComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<CMSkinRetractedComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgicalInternalBleedingComponent>(part), Is.False);
                    Assert.That(entMan.HasComponent<BodyPartWoundComponent>(part), Is.False);
                });
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task YautjaVendorHasInfiniteSharedStockAndPerPlayerCap()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid rack = default;
        EntityUid firstUser = default;
        EntityUid secondUser = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
                firstUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                secondUser = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(rack);
                var sectionIndex = vendor.Sections.FindIndex(section => section.Name == "Essential Hunting Supplies");
                Assert.That(sectionIndex, Is.GreaterThanOrEqualTo(0));
                var entryIndex = vendor.Sections[sectionIndex].Entries.FindIndex(
                    entry => entry.Id.Id == "CMUYautjaHuntingEquipmentBundle");
                Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0));
                var entry = vendor.Sections[sectionIndex].Entries[entryIndex];

                Assert.Multiple(() =>
                {
                    Assert.That(entry.Amount, Is.Null);
                    Assert.That(entry.MaxPerUser, Is.EqualTo(1));
                });

                var spareIndex = vendor.Sections.FindIndex(section => section.Name == "Spare Equipment");
                Assert.That(spareIndex, Is.GreaterThanOrEqualTo(0));
                var arrow = vendor.Sections[spareIndex].Entries.Single(
                    spareEntry => spareEntry.Id.Id == "CMUYautjaArrow");
                Assert.Multiple(() =>
                {
                    Assert.That(arrow.Amount, Is.Null);
                    Assert.That(arrow.MaxPerUser, Is.EqualTo(10));
                });

                static void Vend(IEntityManager entityManager, EntityUid vendorUid, EntityUid userUid,
                    int section, int item)
                {
                    entityManager.EventBus.RaiseLocalEvent(vendorUid,
                        new CMVendorVendBuiMsg(section, item, new())
                        {
                            Actor = userUid,
                            UiKey = CMAutomatedVendorUI.Key,
                        });
                }

                Vend(entMan, rack, firstUser, sectionIndex, entryIndex);
                var firstState = entMan.GetComponent<CMVendorUserComponent>(firstUser);
                Assert.That(firstState.PurchaseCounts[entry.Id.Id], Is.EqualTo(1));

                Vend(entMan, rack, firstUser, sectionIndex, entryIndex);
                Assert.That(firstState.PurchaseCounts[entry.Id.Id], Is.EqualTo(1));

                Vend(entMan, rack, secondUser, sectionIndex, entryIndex);
                Assert.That(entMan.GetComponent<CMVendorUserComponent>(secondUser).PurchaseCounts[entry.Id.Id],
                    Is.EqualTo(1));
            });
        }
        finally
        {
            server.Dispose();
        }
    }

}
