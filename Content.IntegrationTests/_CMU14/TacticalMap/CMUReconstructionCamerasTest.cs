using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Client.Viewport;
using Content.Server.Atmos.Components;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Inventory;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
#pragma warning disable RA0002 // Build authorized and anonymous intel feeds independently of the viewer.

    [TestCase(false)]
    [TestCase(true)]
    public async Task PersonalNamesRemainReadOnlyEvenForTrainedViewers(bool ghost)
    {
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid viewer = default, marine = default, squad = default;
        NetEntity marineNet = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ui.CloseUi(_console, Key, _actor);
                SEntMan.EnsureComponent<TacticalMapComponent>(_upper);
                viewer = SEntMan.SpawnEntity(ghost ? "MobObserver" : "CMMobHuman", new EntityCoordinates(_upper, new Vector2(1.5f)));
                marine = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_upper, new Vector2(3.5f)));
                squad = SEntMan.SpawnEntity("SquadGovforBravo", MapCoordinates.Nullspace);
                SEntMan.System<SquadSystem>().AssignSquad(marine, squad, null);
                SEntMan.System<MetaDataSystem>().SetEntityName(marine, "Friendly Marine");
                var helmet = SEntMan.SpawnEntity("ArmorHelmetM10", new EntityCoordinates(_upper, new Vector2(3.5f)));
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(marine, helmet, "head", force: true), Is.True);
                SEntMan.System<SkillsSystem>().SetSkill(viewer, "RMCSkillOverwatch", 1);
                Server.PlayerMan.SetAttachedEntity(session, viewer);
                marineNet = SEntMan.GetNetEntity(marine);
            });
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(viewer, new OpenTacticalMapActionEvent { Performer = viewer }));
            await Pair.RunTicksSync(40);
            await Server.WaitPost(() =>
            {
                var user = SComp<TacticalMapUserComponent>(viewer);
                user.Marines = true;
                user.MarineBlips = new() { [marine.Id] = new TacticalMapBlip { Indices = new(3, 3), Color = Color.Green } };
            });
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                var view = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconstructionWindow>().Single().SurveyView;
                var contact = view.TrackedContacts.Single(c => c.Name == "Friendly Marine");
                Assert.That(contact.CameraTarget, Is.Null, "Personal maps, including ghost maps, cannot grant overwatch camera access.");
                view.OnCameraRequested!.Invoke(marineNet); // Simulate a forged request through the actual BUI.
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
                Assert.That(SEntMan.TryGetComponent(viewer, out OverwatchWatchingComponent? watching) && watching.Watching != null, Is.False));
            await Client.WaitAssertion(() =>
                Assert.That(Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children.OfType<CMUReconCameraWindow>(), Is.Empty));
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (viewer.IsValid()) _ui.CloseUi(viewer, TacticalMapUserUi.Key, viewer);
                Server.PlayerMan.SetAttachedEntity(session, original);
                foreach (var uid in new[] { viewer, marine, squad })
                    if (uid.IsValid() && SEntMan.EntityExists(uid)) SEntMan.DeleteEntity(uid);
            });
            await Pair.RunUntilSynced();
        }
    }

#pragma warning restore RA0002
}
