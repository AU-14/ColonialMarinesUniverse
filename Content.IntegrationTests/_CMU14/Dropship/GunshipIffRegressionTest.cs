using System.Reflection;
using Content.Client.CMU14.Dropship.TacticalLand;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipIffRegressionTest : GameTest
{
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
