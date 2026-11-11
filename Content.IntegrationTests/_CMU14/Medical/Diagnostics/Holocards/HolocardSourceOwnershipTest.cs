#pragma warning disable RA0002 // Public commands drive changes; inspect committed sources and anatomy.
using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Systems;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics.Holocards;

[TestFixture]
public sealed class HolocardSourceOwnershipTest
{
    [TestPrototypes]
    private const string ParasitePrototype = """
        - type: entity
          parent: CMXenoParasite
          id: CMUTestHolocardParasite
          components:
          - type: XenoParasite
            fallOffDelay: 0
            # cmu edit start
            cmuMinHugTime: 0
            cmuMaxHugTime: 0
            # cmu edit end
        """;

    [Test]
    public async Task AuthenticatedHolocardNetworkCommandsRejectForgedMedicAndUndefinedStatus()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var oldActor = player.AttachedEntity;
        var entities = pair.Server.EntMan;
        EntityUid patient = default, actor = default, otherMedic = default;
        NetEntity patientNet = default, actorNet = default, otherNet = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                actor = CreateMedic(entities, map.GridCoords);
                otherMedic = CreateMedic(entities, map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, actor);
                var hud = entities.SpawnEntity("RMCGlassesMedicalHUDGlasses", map.GridCoords);
                Assert.That(entities.System<InventorySystem>().TryEquip(actor, hud, "eyes", silent: true, force: true), Is.True);
                var verbs = entities.System<SharedVerbSystem>().GetLocalVerbs(patient, actor, typeof(ExamineVerb));
                var verb = verbs.Single(value => value.Text == Loc.GetString("scannable-holocard-verb-text"));
                Assert.That(verb.Act, Is.Not.Null);
                verb.Act!();
                Assert.That(entities.System<SharedUserInterfaceSystem>().IsUiOpen(patient, HolocardChangeUIKey.Key, actor), Is.True);
                patientNet = entities.GetNetEntity(patient);
                actorNet = entities.GetNetEntity(actor);
                otherNet = entities.GetNetEntity(otherMedic);
                entities.System<SkillsSystem>().SetSkill(actor, HolocardSystem.SkillType, 0);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(patientNet), HolocardChangeUIKey.Key,
                new HolocardChangeEvent(otherNet, HolocardStatus.Permadead)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                AssertCard(entities, patient, HolocardStatus.None, HolocardStatus.None, HolocardStatus.None);
                entities.System<SkillsSystem>().SetSkill(actor, HolocardSystem.SkillType, HolocardSystem.MinimumRequiredSkill);
            });
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(patientNet), HolocardChangeUIKey.Key,
                new HolocardChangeEvent(actorNet, (HolocardStatus)255)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => AssertCard(entities, patient, HolocardStatus.None, HolocardStatus.None, HolocardStatus.None));
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(patientNet), HolocardChangeUIKey.Key,
                new HolocardChangeEvent(actorNet, HolocardStatus.Emergency)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => AssertCard(entities, patient, HolocardStatus.Emergency, HolocardStatus.Emergency, HolocardStatus.None));
            // The first barrier targets the server tick before the inbound
            // command commits. Now wait for the resulting server state too.
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan
                .GetComponent<HolocardStateComponent>(pair.Client.EntMan.GetEntity(patientNet)).HolocardStatus, Is.EqualTo(HolocardStatus.Emergency)));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, oldActor);
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                if (entities.EntityExists(actor)) entities.DeleteEntity(actor);
                if (entities.EntityExists(otherMedic)) entities.DeleteEntity(otherMedic);
            });
        }
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    private static EntityUid CreateMedic(IEntityManager entities, EntityCoordinates coordinates)
    {
        var medic = entities.SpawnEntity("CMMobHuman", coordinates);
        entities.System<SkillsSystem>().SetSkill(medic, HolocardSystem.SkillType, HolocardSystem.MinimumRequiredSkill);
        return medic;
    }

    private static void AssertCard(IEntityManager entities, EntityUid patient, HolocardStatus visible, HolocardStatus manual, HolocardStatus automatic)
    {
        var card = entities.GetComponent<HolocardStateComponent>(patient);
        Assert.Multiple(() =>
        {
            Assert.That(card.HolocardStatus, Is.EqualTo(visible));
            Assert.That(card.ManualStatus, Is.EqualTo(manual));
            Assert.That(card.AutomaticStatus, Is.EqualTo(automatic));
        });
    }

}
