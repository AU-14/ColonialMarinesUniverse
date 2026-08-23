using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Popups;
using Content.IntegrationTests.Pair;
using Content.Server.Mind;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Roles;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRackAccessTest
{
    [Test]
    public void YautjaRackClientGateDoesNotEmitDeniedPopup()
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "Content.Client",
            "_CMU14",
            "Yautja",
            "YautjaGearRackClientSystem.cs");

        Assert.That(File.Exists(sourcePath), Is.True, sourcePath);
        var source = File.ReadAllText(sourcePath);
        Assert.That(source, Does.Not.Contain("_popup.PopupClient"),
            "The server must be the only source of a denied Gear Rack popup.");
    }

    [Test]
    public async Task YautjaRackClientOpenAttemptIsDeniedBeforePredictedOpen()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            try
            {
                var ev = new ActivatableUIOpenAttemptEvent(user);
                entMan.EventBus.RaiseLocalEvent(rack, ev);
                Assert.That(ev.Cancelled, Is.True);
            }
            finally
            {
                entMan.DeleteEntity(rack);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRacksUseOnlyTheirRoleAwareAccessGate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            foreach (var prototype in RackPrototypes)
            {
                var entityPrototype = client.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>(prototype);
                var factory = client.EntMan.ComponentFactory;
                Assert.That(entityPrototype.TryGetComponent<RemoveComponentsComponent>(out var remove, factory), Is.True,
                    prototype);
                Assert.That(remove!.Components.Any(component =>
                        component.Key.Contains("ActivatableUIRequiresAccess", StringComparison.Ordinal)), Is.True,
                    $"{prototype} must remove the generic ColMarTech access popup in addition to using its custom role gate.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRackDeniedPopupIsShownOnlyOnceWhenClientPredictionIsReconciled()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        await client.WaitPost(() =>
        {
            var entMan = client.EntMan;
            var user = entMan.SpawnEntity("CMMobHuman", map.CGridCoords);
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.CGridCoords);
            var ev = new ActivatableUIOpenAttemptEvent(user);
            entMan.EventBus.RaiseLocalEvent(rack, ev);
            Assert.That(ev.Cancelled, Is.True);
            Assert.That(entMan.System<PopupSystem>().WorldLabels,
                Is.Empty,
                "Client prediction must cancel the rack open without showing a denial popup.");
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var rack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", map.GridCoords);
            var session = server.PlayerMan.Sessions.Single();
            server.PlayerMan.SetAttachedEntity(session, user);

            var ev = new ActivatableUIOpenAttemptEvent(user);
            entMan.EventBus.RaiseLocalEvent(rack, ev);
            Assert.That(ev.Cancelled, Is.True);
        });

        await pair.ReallyBeIdle(10);

        await client.WaitAssertion(() =>
        {
            var denied = client.EntMan.System<PopupSystem>().WorldLabels
                .Count(label => label.Text == "Access denied.");
            Assert.That(denied, Is.EqualTo(1),
                "A denied rack click must produce one popup after client prediction and server reconciliation.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRackAccessOpenAttemptsMatchCmss13SourceGates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mind = entMan.System<MindSystem>();
            var roles = entMan.System<SharedRoleSystem>();
            var adultRack = entMan.SpawnEntity("CMUYautjaLoadoutVendor", MapCoordinates.Nullspace);
            var elderRack = entMan.SpawnEntity("CMUYautjaElderLoadoutVendor", MapCoordinates.Nullspace);
            var youngRack = entMan.SpawnEntity("CMUYautjaYoungbloodLoadoutVendor", MapCoordinates.Nullspace);
            var thrallRack = entMan.SpawnEntity("CMUYautjaThrallLoadoutVendor", MapCoordinates.Nullspace);
            var bloodedRack = entMan.SpawnEntity("CMUYautjaBloodedThrallLoadoutVendor", MapCoordinates.Nullspace);
            var badBloodRack = entMan.SpawnEntity("CMUYautjaBadBloodLoadoutVendor", MapCoordinates.Nullspace);
            var strandedRack = entMan.SpawnEntity("CMUYautjaStrandedLoadoutVendor", MapCoordinates.Nullspace);
            var spawned = new List<EntityUid> { adultRack, elderRack, youngRack, thrallRack, bloodedRack, badBloodRack, strandedRack };

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(RackOpenCancelled(entMan, adultRack, User(entMan, mind, roles, spawned)), Is.True,
                        "CMSS13 adult Yautja rack checks ACCESS_YAUTJA_SECURE before role.");
                    Assert.That(RackOpenCancelled(entMan, adultRack, User(entMan, mind, roles, spawned, job: "CMUYautjaHunter")), Is.True,
                        "CMSS13 adult Yautja rack denies JOB_PREDATOR users without ACCESS_YAUTJA_SECURE.");
                    Assert.That(RackOpenCancelled(entMan, adultRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure", "CMUYautjaYoungblood")), Is.True,
                        "CMSS13 adult Yautja rack denies access-valid users whose job is not JOB_PREDATOR.");
                    Assert.That(RackOpenCancelled(entMan, adultRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure", "CMUYautjaHunter")), Is.False,
                        "CMSS13 adult Yautja rack allows ACCESS_YAUTJA_SECURE plus JOB_PREDATOR.");

                    Assert.That(RackOpenCancelled(entMan, youngRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure", "CMUYautjaYoungblood")), Is.False,
                        "CMSS13 youngblood rack allows ACCESS_YAUTJA_SECURE plus ERT_JOB_YOUNGBLOOD.");
                    Assert.That(RackOpenCancelled(entMan, youngRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure", "CMUYautjaHunter")), Is.False,
                        "CMSS13 youngblood rack also allows adult JOB_PREDATOR.");
                    Assert.That(RackOpenCancelled(entMan, youngRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure")), Is.True,
                        "CMSS13 youngblood rack denies access-valid users outside ERT_JOB_YOUNGBLOOD/JOB_PREDATOR.");

                    Assert.That(RackOpenCancelled(entMan, elderRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure", "CMUYautjaHunter")), Is.True,
                        "CMSS13 elder rack requires one of ACCESS_YAUTJA_ELDER or ACCESS_YAUTJA_ANCIENT.");
                    Assert.That(RackOpenCancelled(entMan, elderRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaElder", "CMUYautjaHunter")), Is.False,
                        "CMSS13 elder rack allows ACCESS_YAUTJA_ELDER plus JOB_PREDATOR.");
                    Assert.That(RackOpenCancelled(entMan, elderRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaAncient", "CMUYautjaHunter")), Is.False,
                        "CMSS13 elder rack allows ACCESS_YAUTJA_ANCIENT plus JOB_PREDATOR.");
                    Assert.That(RackOpenCancelled(entMan, elderRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaElder", "CMUYautjaYoungblood")), Is.True,
                        "CMSS13 elder rack still requires JOB_PREDATOR after elder/ancient access passes.");

                    Assert.That(RackOpenCancelled(entMan, thrallRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure", "CMUYautjaHunter")), Is.True,
                        "CMSS13 thrall rack is gated by isthrall(user), not Yautja secure access.");
                    Assert.That(RackOpenCancelled(entMan, thrallRack, Thrall(entMan, mind, roles, spawned)), Is.False,
                        "CMSS13 thrall rack allows isthrall(user).");

                    Assert.That(RackOpenCancelled(entMan, bloodedRack, User(entMan, mind, roles, spawned)), Is.True,
                        "CMSS13 blooded thrall rack denies users without TRAIT_YAUTJA_TECH.");
                    Assert.That(RackOpenCancelled(entMan, bloodedRack, Tech(entMan, mind, roles, spawned)), Is.False,
                        "CMSS13 blooded thrall rack allows TRAIT_YAUTJA_TECH.");

                    Assert.That(RackOpenCancelled(entMan, badBloodRack, User(entMan, mind, roles, spawned)), Is.True,
                        "CMSS13 survivor rack requires one of ACCESS_YAUTJA_SECURE or ACCESS_YAUTJA_BADBLOOD before vendor_role selection.");
                    Assert.That(RackOpenCancelled(entMan, badBloodRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaBadBlood")), Is.False,
                        "Local Bad Blood rack represents the CMSS13 survivor rack's JOB_BADBLOOD product-list branch with ACCESS_YAUTJA_BADBLOOD.");
                    Assert.That(RackOpenCancelled(entMan, badBloodRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure")), Is.True,
                        "Local Bad Blood rack must not expose the CMSS13 JOB_BADBLOOD product list to the non-Bad-Blood survivor branch.");

                    Assert.That(RackOpenCancelled(entMan, strandedRack, User(entMan, mind, roles, spawned)), Is.True,
                        "CMSS13 survivor rack also gates stranded/pred-survivor equipment behind req_one_access.");
                    Assert.That(RackOpenCancelled(entMan, strandedRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure")), Is.False,
                        "Local stranded rack represents the CMSS13 survivor rack's non-Bad-Blood product-list branch with ACCESS_YAUTJA_SECURE.");
                    Assert.That(RackOpenCancelled(entMan, strandedRack, User(entMan, mind, roles, spawned, "CMUAccessYautjaBadBlood")), Is.True,
                        "Local stranded rack must not expose the CMSS13 stranded/pred-survivor product list to the JOB_BADBLOOD branch.");

                    var bothSurvivorAccesses = User(entMan, mind, roles, spawned, "CMUAccessYautjaSecure");
                    entMan.GetComponent<AccessComponent>(bothSurvivorAccesses).Tags.Add("CMUAccessYautjaBadBlood");

                    Assert.That(RackOpenCancelled(entMan, badBloodRack, bothSurvivorAccesses), Is.False,
                        "When a local actor carries both survivor access tags, the explicit Bad Blood access branch should win like CMSS13 user.job == JOB_BADBLOOD.");
                    Assert.That(RackOpenCancelled(entMan, strandedRack, bothSurvivorAccesses), Is.True,
                        "Local stranded rack must stay mutually exclusive from the Bad Blood product-list branch even when aggregate access groups include both tags.");
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaRackAccessDeniedPopupsMatchCmss13SourceOrder()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid? previousAttached = null;
        CultureInfo? previousCulture = null;
        var spawned = new List<EntityUid>();
        var offset = 0;

        try
        {
            await server.WaitPost(() =>
            {
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                previousCulture = loc.DefaultCulture;
                loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            });

            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaLoadoutVendor", "Access denied.", job: "CMUYautjaHunter");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaLoadoutVendor", "This machine isn't for you.", "CMUAccessYautjaSecure", "CMUYautjaYoungblood");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaYoungbloodLoadoutVendor", "Access denied.", job: "CMUYautjaHunter");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaYoungbloodLoadoutVendor", "This machine isn't for you.", "CMUAccessYautjaSecure");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaElderLoadoutVendor", "Access denied.", "CMUAccessYautjaSecure", "CMUYautjaHunter");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaElderLoadoutVendor", "This machine isn't for you.", "CMUAccessYautjaElder", "CMUYautjaYoungblood");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaThrallLoadoutVendor", "Access denied.", "CMUAccessYautjaSecure", "CMUYautjaHunter");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaBloodedThrallLoadoutVendor", "Access denied.", "CMUAccessYautjaSecure", "CMUYautjaHunter");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaBadBloodLoadoutVendor", "Access denied.", "CMUAccessYautjaSecure");
            offset = await AssertDeniedPopup(pair, map.GridCoords, spawned, offset, "CMUYautjaStrandedLoadoutVendor", "Access denied.", "CMUAccessYautjaBadBlood");
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loc = server.ResolveDependency<ILocalizationManager>();
                var session = server.PlayerMan.Sessions.Single();
                if (previousCulture != null)
                    loc.SetCulture(previousCulture);
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                foreach (var uid in spawned)
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static bool RackOpenCancelled(IEntityManager entMan, EntityUid rack, EntityUid user)
    {
        var ev = new ActivatableUIOpenAttemptEvent(user);
        entMan.EventBus.RaiseLocalEvent(rack, ev);
        return ev.Cancelled;
    }

    private static EntityUid Spawn(IEntityManager entMan, ICollection<EntityUid> spawned, string prototype)
    {
        var uid = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
        spawned.Add(uid);
        return uid;
    }

    private static EntityUid Spawn(
        IEntityManager entMan,
        ICollection<EntityUid> spawned,
        string prototype,
        EntityCoordinates coordinates)
    {
        var uid = entMan.SpawnEntity(prototype, coordinates);
        spawned.Add(uid);
        return uid;
    }

    private static async Task<int> AssertDeniedPopup(
        TestPair pair,
        EntityCoordinates origin,
        ICollection<EntityUid> spawned,
        int offset,
        string rackPrototype,
        string expected,
        ProtoId<AccessLevelPrototype>? access = null,
        ProtoId<JobPrototype>? job = null)
    {
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var mind = entMan.System<MindSystem>();
            var roles = entMan.System<SharedRoleSystem>();
            var rackCoords = origin.Offset(new Vector2(offset * 2, 0));
            var rack = Spawn(entMan, spawned, rackPrototype, rackCoords);
            var user = User(entMan, mind, roles, spawned, rackCoords.Offset(new Vector2(1, 0)), access, job);
            var session = server.PlayerMan.Sessions.Single();
            server.PlayerMan.SetAttachedEntity(session, user);

            var ev = new ActivatableUIOpenAttemptEvent(user);
            entMan.EventBus.RaiseLocalEvent(rack, ev);

            Assert.That(ev.Cancelled, Is.True, $"CMSS13 denied rack access should cancel opening and show `{expected}`.");
        });

        await pair.ReallyBeIdle(10);

        await client.WaitAssertion(() =>
        {
            var labels = client.EntMan.System<PopupSystem>().WorldLabels.Select(label => label.Text).ToList();
            var joinedLabels = string.Join("\n", labels);
            Assert.That(labels, Does.Contain(expected),
                $"CMSS13 denied rack access should show source popup text `{expected}`.\nActual labels:\n{joinedLabels}");
        });

        return offset + 1;
    }

    private static EntityUid User(
        IEntityManager entMan,
        MindSystem mind,
        SharedRoleSystem roles,
        ICollection<EntityUid> spawned,
        ProtoId<AccessLevelPrototype>? access = null,
        ProtoId<JobPrototype>? job = null)
    {
        var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        spawned.Add(user);

        if (access is { } accessId)
            entMan.EnsureComponent<AccessComponent>(user).Tags.Add(accessId);

        if (job is { } jobId)
        {
            var mindEnt = mind.CreateMind(null, entMan.GetComponent<MetaDataComponent>(user).EntityName);
            mind.TransferTo(mindEnt.Owner, user);
            roles.MindAddJobRole(mindEnt.Owner, jobPrototype: jobId.Id);
            spawned.Add(mindEnt.Owner);
        }

        return user;
    }

    private static EntityUid User(
        IEntityManager entMan,
        MindSystem mind,
        SharedRoleSystem roles,
        ICollection<EntityUid> spawned,
        EntityCoordinates coordinates,
        ProtoId<AccessLevelPrototype>? access = null,
        ProtoId<JobPrototype>? job = null)
    {
        var user = User(entMan, mind, roles, spawned, access, job);
        entMan.System<SharedTransformSystem>().SetCoordinates(user, coordinates);
        return user;
    }

    private static EntityUid Thrall(
        IEntityManager entMan,
        MindSystem mind,
        SharedRoleSystem roles,
        ICollection<EntityUid> spawned)
    {
        var user = User(entMan, mind, roles, spawned);
        entMan.EnsureComponent<YautjaThrallComponent>(user);
        return user;
    }

    private static EntityUid Tech(
        IEntityManager entMan,
        MindSystem mind,
        SharedRoleSystem roles,
        ICollection<EntityUid> spawned)
    {
        var user = User(entMan, mind, roles, spawned);
        entMan.EnsureComponent<YautjaTechAuthorizedComponent>(user);
        return user;
    }

    private static readonly string[] RackPrototypes =
    {
        "CMUYautjaLoadoutVendor",
        "CMUYautjaElderLoadoutVendor",
        "CMUYautjaYoungbloodLoadoutVendor",
        "CMUYautjaThrallLoadoutVendor",
        "CMUYautjaBloodedThrallLoadoutVendor",
        "CMUYautjaBadBloodLoadoutVendor",
        "CMUYautjaStrandedLoadoutVendor",
    };
}
