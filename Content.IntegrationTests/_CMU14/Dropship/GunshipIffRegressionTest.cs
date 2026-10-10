using System.Reflection;
using Content.Client.CMU14.Dropship.TacticalLand;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipIffRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUGunshipIffTestWearer
  components:
  - type: Inventory
  - type: InventorySlots
  - type: ContainerContainer
  - type: Sprite
";

    [TestCase("GOVFOR", "FactionCLF", "CLF", "_friendlyShader")]
    [TestCase("FactionSurvivor", "FactionCLF", "CLF", "_neutralShader")]
    [TestCase("OPFOR", "FactionCLF", "CLF", "_neutralShader")]
    [TestCase("OPFOR", "GOVFOR", "GOVFOR", "_neutralShader")]
    [TestCase("FactionSurvivor", "FactionXeno", "RMCXeno", "_neutralShader")]
    public async Task EquippedIdHidesIntrinsicIffAndNpcAllegiance(
        string cardFaction,
        string intrinsicFaction,
        string npcFaction,
        string shaderField)
    {
        await Client.WaitAssertion(() =>
        {
            var pilot = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var target = CEntMan.SpawnEntity("CMUGunshipIffTestWearer", MapCoordinates.Nullspace);
            var card = CEntMan.SpawnEntity("AU14IDCardBaseCivilianNoIFF", MapCoordinates.Nullspace);
            var iff = Client.System<GunIFFSystem>();
            iff.SetUserFaction(pilot, "GOVFOR");
            iff.SetUserFaction(target, intrinsicFaction);
            iff.SetIdFaction((card, CEntMan.GetComponent<ItemIFFComponent>(card)), cardFaction);
            Assert.That(Client.System<InventorySystem>().TryEquip(target, card, "id", force: true), Is.True);
            var observer = CEntMan.AddComponent<NpcFactionMemberComponent>(pilot);
            observer.Factions.Add("GOVFOR");
            observer.HostileFactions.Add("CLF");
            var faction = CEntMan.AddComponent<NpcFactionMemberComponent>(target);
            faction.Factions.Add(npcFaction);
            faction.HostileFactions.Add("GOVFOR");

            var system = Client.System<GunshipPilotIffOutlineSystem>();
            var type = typeof(GunshipPilotIffOutlineSystem);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pilotIff = type.GetField("_pilotIff", flags)!.GetValue(system)!;
            type.GetMethod("GetIffFactions", flags)!.Invoke(system, new[] { (object) pilot, pilotIff });
            var shader = type.GetMethod("GetRelationshipShader", flags)!.Invoke(system, new object[] { pilot, target });
            Assert.That(shader, Is.SameAs(type.GetField(shaderField, flags)!.GetValue(system)));
            Assert.That(iff.IsInFaction(target, intrinsicFaction), Is.True,
                "The disguise must affect only the outline, not actual IFF.");

            CEntMan.DeleteEntity(card);
            CEntMan.DeleteEntity(target);
            CEntMan.DeleteEntity(pilot);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ClfOutlineUsesColonistRelations(bool colonistsHostile)
    {
        await Client.WaitAssertion(() =>
        {
            var pilot = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var target = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var observer = CEntMan.AddComponent<NpcFactionMemberComponent>(pilot);
            observer.Factions.Add("GOVFOR");
            observer.HostileFactions.Add("CLF");
            if (colonistsHostile)
                observer.HostileFactions.Add("AUColonist");
            var faction = CEntMan.AddComponent<NpcFactionMemberComponent>(target);
            faction.Factions.Add("CLF");
            faction.HostileFactions.Add("GOVFOR");
            var iff = CEntMan.AddComponent<UserIFFComponent>(target);
            Client.System<GunIFFSystem>().SetUserFaction((target, iff), "FactionCLF");

            var system = Client.System<GunshipPilotIffOutlineSystem>();
            var type = typeof(GunshipPilotIffOutlineSystem);
            var method = type.GetMethod("GetRelationshipShader", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var expected = type.GetField(colonistsHostile ? "_hostileShader" : "_neutralShader",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            Assert.That(method.Invoke(system, new object[] { pilot, target }), Is.SameAs(expected));
            Assert.That(iff.Factions, Does.Contain(new Robust.Shared.Prototypes.EntProtoId<IFFFactionComponent>("FactionCLF")));
            Assert.That(faction.Factions, Has.Count.EqualTo(1));
            CEntMan.DeleteEntity(target);
            CEntMan.DeleteEntity(pilot);
        });
    }
}
