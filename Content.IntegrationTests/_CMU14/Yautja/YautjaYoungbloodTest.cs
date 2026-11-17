using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Client.Popups;
using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Server.CMU14.Yautja;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Roles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Inventory;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaYoungbloodTest
{

    [Test]
    public async Task YoungbloodCannotArmSelfDestructLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();
            var youngblood = entMan.SpawnEntity("CMUMobYautjaYoungblood", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                Assert.That(inventory.TryEquip(youngblood, bracer, "gloves", silent: true, force: true), Is.True);
                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), youngblood, TimeSpan.FromSeconds(1)), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
            }
            finally
            {
                if (!entMan.Deleted(youngblood))
                    entMan.DeleteEntity(youngblood);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdminBypassSpawnsYoungbloodGhostRoleWithoutEligibilityGate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<YautjaHuntConsoleSystem>();
            var consoleUid = entMan.SpawnEntity("CMUHunterShipBloodingConsole", MapCoordinates.Nullspace);
            var console = entMan.GetComponent<YautjaHuntConsoleComponent>(consoleUid);
            console.DestinationId = "jungle_moon";
            entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", MapCoordinates.Nullspace);
            entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", MapCoordinates.Nullspace);

            var option = console.BloodingCallOptions.Single(option => option.Id == "youngblood_three_inexperienced");
            Assert.That(system.TryCreateYoungbloodCall((consoleUid, console), consoleUid, option, bypassEligibility: true), Is.True);

            var query = entMan.EntityQueryEnumerator<YautjaYoungbloodGhostRoleComponent, GhostRoleComponent>();
            Assert.That(query.MoveNext(out _, out var metadata, out var role), Is.True);
            Assert.That(metadata.BypassEligibility, Is.True);
            Assert.That(role.JobProto, Is.EqualTo("CMUYautjaYoungblood"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodTeleporterRequiresConfirmationBeforeDeploying()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var teleporter = entMan.SpawnEntity(null, map.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var destination = entMan.SpawnEntity("CMUYautjaYoungbloodDestinationJungleMoon", map.GridCoords.Offset(new Vector2(8, 0)));

            try
            {
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Young;
                teleporterComp.DestinationId = "jungle_moon";
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(hunter);

                var before = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.DialogType, Is.EqualTo(DialogType.Confirm));
                Assert.That(dialog.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                Assert.That(entMan.GetComponent<TransformComponent>(hunter).Coordinates, Is.EqualTo(before));

                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var after = entMan.GetComponent<TransformComponent>(hunter).Coordinates;
                Assert.That(after, Is.Not.EqualTo(before));
                Assert.That(after.EntityId, Is.EqualTo(entMan.GetComponent<TransformComponent>(destination).Coordinates.EntityId));
            }
            finally
            {
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(destination))
                    entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodStudentMarkClaimsAndReleasesMentor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var otherMentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var otherBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(otherMentor);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(otherMentor, otherBracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(youngblood.Mentor, Is.EqualTo(mentor));

                var secondMentorClaim = marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherMentor, pupil, YautjaMarkKind.Student, null);
                var releaseByOtherMentor = marks.TryClearMark(pupil, YautjaMarkKind.Student, otherMentor);
                var releaseByOwner = marks.TryClearMark(pupil, YautjaMarkKind.Student, mentor);

                Assert.That(secondMentorClaim, Is.False);
                Assert.That(releaseByOtherMentor, Is.False);
                Assert.That(releaseByOwner, Is.True);
                Assert.That(youngblood.Mentor, Is.Null);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(otherMentor))
                    entMan.DeleteEntity(otherMentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(otherBracer))
                    entMan.DeleteEntity(otherBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodMentorDeletionClearsStudentMarkLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(youngblood.Mentor, Is.EqualTo(mentor));

                entMan.DeleteEntity(mentor);

                Assert.That(youngblood.Mentor, Is.Null,
                    "CMSS13 huntdata.clean_data() clears the pupil-side youngblood_set when the mentor huntdata is cleaned.");
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Student, mentor), Is.False,
                    "Deleting the mentor must remove the local Student mark instead of leaving stale mark ownership.");
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodRoundRestartClearsStudentMarkLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(youngblood.Mentor, Is.EqualTo(mentor));

                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

                Assert.That(youngblood.Mentor, Is.Null,
                    "CMSS13 huntdata.clean_data() clears youngblood_marked/youngblood_set across round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Student, mentor), Is.False,
                    "Round restart cleanup must remove the local Student mark instead of leaving stale mark ownership.");
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingAuthorizesTechWithoutThrallConversion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Student, null), Is.True);
                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "completed the trial"), Is.True);

                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True);
                Assert.That(entMan.HasComponent<YautjaThrallComponent>(pupil), Is.False);
                Assert.That(entMan.GetComponent<YautjaYoungbloodComponent>(pupil).Blooded, Is.True);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingCannotBeRemovedLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "completed the trial"), Is.True);
                Assert.That(marks.TryClearMark(pupil, YautjaMarkKind.Blooded, mentor), Is.False,
                    "CMSS13 mark_blooded() has no mark_unblooded path; once a youngblood is blooded, the trial completion is permanent.");

                Assert.That(entMan.GetComponent<YautjaYoungbloodComponent>(pupil).Blooded, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True);
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, mentor), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingCannotBeOverwrittenLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var otherMentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var otherBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(otherMentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(otherMentor, otherBracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "completed the trial"), Is.True);
                Assert.That(marks.TryMark((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherMentor, pupil, YautjaMarkKind.Blooded, null), Is.False,
                    "CMSS13 mark_blooded() returns when the target's hunter_data.blooded flag is already set, preserving the first blooding hunter.");

                Assert.That(youngblood.Blooded, Is.True);
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True);
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, mentor), Is.True);
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, otherMentor), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(otherMentor))
                    entMan.DeleteEntity(otherMentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
                if (!entMan.Deleted(otherBracer))
                    entMan.DeleteEntity(otherBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodBloodingHunterDeletionClearsBloodedByLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var marks = entMan.System<YautjaMarkSystem>();

            var mentor = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(mentor);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                var youngblood = entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                Assert.That(inventory.TryEquip(mentor, bracer, "gloves", silent: true, force: true), Is.True);

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), mentor, pupil, YautjaMarkKind.Blooded, "survived the trial"), Is.True);
                Assert.That(youngblood.BloodedBy, Is.EqualTo(mentor));

                entMan.DeleteEntity(mentor);

                Assert.That(youngblood.Blooded, Is.True,
                    "CMSS13 clean_data() does not unblood the target when the blooding hunter is destroyed.");
                Assert.That(youngblood.BloodedBy, Is.Null,
                    "CMSS13 clean_data() clears newblood.hunter_data.blooded_set when the blooding hunter's huntdata is cleaned.");
                Assert.That(youngblood.BloodingReason, Is.EqualTo("survived the trial"),
                    "CMSS13 clean_data() leaves the target's blooded_reason intact on blooding-hunter cleanup.");
                Assert.That(entMan.HasComponent<YautjaTechAuthorizedComponent>(pupil), Is.True,
                    "CMSS13 keeps TRAIT_YAUTJA_TECH after blooding; cleanup only severs the stale reciprocal link.");
                Assert.That(marks.IsMarkedBy(pupil, YautjaMarkKind.Blooded, mentor), Is.False,
                    "Deleting the blooding hunter must not leave local Blooded mark ownership pointing at a destroyed entity.");
            }
            finally
            {
                if (!entMan.Deleted(mentor))
                    entMan.DeleteEntity(mentor);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoteYoungbloodExecutionRequiresAdultReasonAndKillsTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var youngbloods = entMan.System<YautjaYoungbloodSystem>();
            var mobState = entMan.System<MobStateSystem>();

            var adult = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var pupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var otherPupil = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(adult);
                entMan.EnsureComponent<YautjaComponent>(pupil);
                entMan.EnsureComponent<YautjaComponent>(otherPupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(pupil);
                entMan.EnsureComponent<YautjaYoungbloodComponent>(otherPupil);

                Assert.That(youngbloods.TryExecuteYoungblood(adult, pupil, ""), Is.False);
                Assert.That(youngbloods.TryExecuteYoungblood(pupil, otherPupil, "reason"), Is.False);
                Assert.That(youngbloods.TryExecuteYoungblood(adult, pupil, "dishonored the trial"), Is.True);
                Assert.That(mobState.IsDead(pupil), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(adult))
                    entMan.DeleteEntity(adult);
                if (!entMan.Deleted(pupil))
                    entMan.DeleteEntity(pupil);
                if (!entMan.Deleted(otherPupil))
                    entMan.DeleteEntity(otherPupil);
            }
        });

        await pair.CleanReturnAsync();
    }

}

[RegisterComponent]
public sealed partial class TestGhostRoleRequestGateComponent : Component;

public sealed partial class TestGhostRoleRequestGateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TestGhostRoleRequestGateComponent, GhostRoleRequestAttemptEvent>(OnAttempt);
    }

    private static void OnAttempt(Entity<TestGhostRoleRequestGateComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
