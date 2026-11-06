#pragma warning disable RA0002 // Arrange depleted ammunition and selected console targets.
using Content.Client.Gameplay;
using Content.Client.Popups;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Rules;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Client.State;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipEmptyAmmoFeedbackTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase("CMUDropshipAttachmentBOFORS", "CMUDropshipAmmoBOFORSMagHE", -1)]
    [TestCase("CMUDropshipAttachmentBOFORS", "CMUDropshipAmmoBOFORSMagHE", 0)]
    [TestCase("CMUDropshipAttachmentBOFORS", "CMUDropshipAmmoBOFORSMagHE", 4)]
    [TestCase("CMUDropshipAttachmentHowitzer", "CMUDropshipAmmoHowitzerHE", -1)]
    [TestCase("CMUDropshipAttachmentHowitzer", "CMUDropshipAmmoHowitzerHE", 0)]
    public async Task EmptyWeaponsReportAmmoBeforeTargetVisibility(string weaponPrototype, string ammoPrototype, int rounds)
    {
        var map = await Pair.CreateTestMap();
        EntityUid actor = default;
        EntityUid point = default;
        EntityUid weapon = default;
        EntityUid terminal = default;
        EntityUid target = default;
        string expected = string.Empty;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.Grid);
            point = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", map.GridCoords);
            var mount = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            weapon = SEntMan.SpawnEntity(weaponPrototype, map.GridCoords);
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, mount.UtilitySlotId)), Is.True);
            if (rounds >= 0)
            {
                var ammo = SEntMan.SpawnEntity(ammoPrototype, map.GridCoords);
                SEntMan.GetComponent<DropshipAmmoComponent>(ammo).Rounds = rounds;
                Assert.That(containers.Insert(ammo,
                    containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId)), Is.True);
            }

            terminal = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", map.GridCoords);
            var component = SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(terminal);
            component.ScreenOne.Weapon = SEntMan.GetNetEntity(weapon);
            target = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<DropshipTargetComponent>(target);
            component.Target = target;
            actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 2);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
            expected = Loc.GetString("rmc-dropship-weapons-fire-no-ammo", ("weapon", weapon));
        });
        await Pair.RunUntilSynced();
        await Client.WaitPost(() => Client.ResolveDependency<IStateManager>().RequestStateChange<GameplayState>());
        await Server.WaitAssertion(() =>
        {
            var ftl = SEntMan.EnsureComponent<FTLComponent>(map.Grid);
            ftl.State = FTLState.Travelling;
            SEntMan.EnsureComponent<DropshipInFlyByComponent>(map.Grid);
            SEntMan.EventBus.RaiseLocalEvent(terminal, new DropshipTerminalWeaponsFireMsg(true)
            {
                Actor = actor,
                UiKey = DropshipTerminalWeaponsUi.Key,
            });
            Assert.That(SEntMan.QueryEntities<AmmoInFlightComponent>(), Is.Empty);
            SEntMan.RemoveComponent<FTLComponent>(map.Grid);
        });
        await Pair.RunTicksSync(3);
        await Client.WaitAssertion(() =>
        {
            var labels = Client.System<PopupSystem>().CursorLabels;
            Assert.That(labels.Any(label => label.Text == expected), Is.True);
            Assert.That(labels.Any(label => label.Text == Loc.GetString("rmc-laser-designator-not-cas")), Is.False);
        });
    }
}
