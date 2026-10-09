using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUDeletedContactTest : GameTest
{
    [Test]
    public async Task DeletedContactDoesNotBreakWeaponSelection()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            // The contact must be illuminated under the NPC's normal visibility rules.
            Server.System<SharedMapSystem>().SetAmbientLight(
                SEntMan.GetComponent<TransformComponent>(map.Grid.Owner).MapID, Color.White);
            var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            Server.System<NpcFactionSystem>().AddFaction(target, "GOVFOR");
            var system = Server.System<CMUExpeditionAgentSystem>();
            var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guard);
            system.Update(0);
            Assert.That(agent.Target, Is.EqualTo(target), "The guard must acquire a real visible contact first.");
            Assert.That(Server.System<GunSystem>().TryGetGun(guard, out var gun), Is.True);
            var before = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(gun.Owner, ref before);

            SEntMan.DeleteEntity(target);
            agent.NextThink = SGameTiming.CurTime;
            agent.NextWeaponChoice = SGameTiming.CurTime;
            // Resolve errors fail the integration harness. This exercises the same Think ->
            // ChooseWeapon -> Visible path as the production error, without invoking private methods.
            system.Update(0);
            var after = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(gun.Owner, ref after);
            Assert.That(after.Count, Is.EqualTo(before.Count), "A deleted contact must not consume ammunition.");
            Assert.That(agent.PendingWeapon, Is.Null, "Losing a contact must not spuriously switch the loaded rifle.");
            SEntMan.DeleteEntity(guard);
        });
    }
}
