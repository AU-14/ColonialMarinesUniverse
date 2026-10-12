using Content.IntegrationTests.Fixtures;
using Content.Client._RMC14.Dropship.Weapon;
using Content.Client.UserInterface.ControlExtensions;
using Robust.Client.UserInterface;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.Eye;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkMappedGunneryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("midway")]
    [TestCase("midway_navy")]
    public async Task MappedUpperDeckGunnerCanControlLowerDeckTurret(string variant)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(Server.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out _), Is.True);
        });
        await Pair.RunSeconds(1);
        NetEntity consoleNet = default;
        NetEntity actorNet = default;
        await Server.WaitAssertion(() =>
        {
            var point = SEntMan.QueryEntities<MohawkChinGunComponent>().Single();
            var seat = SEntMan.QueryEntities<MohawkGunnerySeatComponent>().Single();
            var console = SEntMan.QueryEntities<DropshipTerminalWeaponsComponent>()
                .Single(ent => ent.Comp.Gunnery);
            var actor = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetComponent<TransformComponent>(seat).Coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
            Server.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 1);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(actor, null, seat, popup: false), Is.True);
            var ui = Server.System<SharedUserInterfaceSystem>();
            ui.OpenUi(console.Owner, DropshipTerminalWeaponsUi.Key, actor);
            Assert.That(ui.IsUiOpen(console.Owner, DropshipTerminalWeaponsUi.Key, actor), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new MohawkChinGunControlMessage
            {
                Actor = actor,
                UiKey = DropshipTerminalWeaponsUi.Key,
            });
            Assert.That(point.Comp.Operator, Is.EqualTo(actor));
            Assert.That(Server.System<MohawkChinGunSystem>().CanOperate(point, actor,
                SEntMan.GetComponent<RemoteWeaponOperatorComponent>(actor).SelectedWeapon!.Value, out _), Is.True);
            consoleNet = SEntMan.GetNetEntity(console);
            actorNet = SEntMan.GetNetEntity(actor);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var console = CEntMan.GetComponent<DropshipTerminalWeaponsComponent>(CEntMan.GetEntity(consoleNet));
            Assert.That(console.ChinGunOperator, Is.EqualTo(CEntMan.GetEntity(actorNet)));
            Assert.That(console.ChinGunEye, Is.Not.Null);
            var window = Client.ResolveDependency<IUserInterfaceManager>().RootControl
                .GetControlOfType<DropshipWeaponsWindow>(fullTreeSearch: true).Single();
            Assert.That(window.ScreenOne.Viewport.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Stop),
                "The embedded camera must receive clicks instead of silently ignoring target marks.");
            Assert.That(window.ConfirmTrackingButton.Visible, Is.True);
            Assert.That(window.ConfirmTrackingButton.Disabled, Is.True,
                "Authorization must be discoverable but unavailable before a hostile is marked.");
            Assert.That(window.AcquireTargetButton.Visible, Is.True);
            Assert.That(window.PriorityButton.Visible, Is.True);
        });
    }
}
