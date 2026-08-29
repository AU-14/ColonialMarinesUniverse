using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.IdentityManagement;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Identity;

[TestFixture]
[TestOf(typeof(IdentitySystem))]
public sealed class IdentityTest : GameTest
{
    [Test]
    public async Task IdentityLookupHandlesLifecycleFixedIdentityAndYautjaRules()
    {
        var map = await Pair.CreateTestMap();
        EntityUid basic = default;
        EntityUid fixedTarget = default;
        EntityUid fixedViewer = default;
        EntityUid ordinaryViewer = default;
        EntityUid yautjaViewer = default;

        await Server.WaitAssertion(() =>
        {
            var metadata = Server.System<MetaDataSystem>();
            var invalid = Content.Shared.IdentityManagement.Identity.Name(EntityUid.Invalid, SEntMan);
            Assert.Multiple(() =>
            {
                Assert.That(invalid.Entity, Is.EqualTo(EntityUid.Invalid));
                Assert.That(invalid.Name, Is.Empty);
            });

            var preInit = SEntMan.CreateEntityUninitialized(null);
            try
            {
                metadata.SetEntityName(preInit, "Pre-init identity");
                var preInitIdentity = Content.Shared.IdentityManagement.Identity.Name(preInit, SEntMan);
                Assert.Multiple(() =>
                {
                    Assert.That(preInitIdentity.Entity, Is.EqualTo(preInit));
                    Assert.That(preInitIdentity.Name, Is.EqualTo("Pre-init identity"));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(preInit);
            }

            basic = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            metadata.SetEntityName(basic, "Basic identity");

            fixedTarget = SEntMan.SpawnEntity(null, map.GridCoords);
            fixedViewer = SEntMan.SpawnEntity(null, map.GridCoords);
            ordinaryViewer = SEntMan.SpawnEntity(null, map.GridCoords);
            metadata.SetEntityName(fixedTarget, "Fixed target true name");

            var fixedIdentity = SEntMan.AddComponent<FixedIdentityComponent>(fixedTarget);
            fixedIdentity.Name = "rmc-host";
            fixedIdentity.Whitelist = new EntityWhitelist
            {
                Components = ["IdentityEventProbe"],
            };

            var probe = SEntMan.AddComponent<IdentityEventProbeComponent>(fixedViewer);
            probe.NameOverride = "event-adjusted identity";

            yautjaViewer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var identitySystem = Server.System<IdentitySystem>();
            var localization = Server.ResolveDependency<ILocalizationManager>();
            var basicIdentity = Content.Shared.IdentityManagement.Identity.Name(basic, SEntMan);
            var basicIdentityEntity = Content.Shared.IdentityManagement.Identity.Entity(basic, SEntMan);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<IdentityComponent>(basic), Is.True);
                Assert.That(basicIdentityEntity, Is.Not.EqualTo(basic));
                Assert.That(basicIdentity.Entity, Is.EqualTo(basic));
                Assert.That(basicIdentity.Name, Is.EqualTo("Basic identity"));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(basicIdentityEntity).EntityName,
                    Is.EqualTo(basicIdentity.Name));
                Assert.That(identitySystem.GetEntityIdentity(basic), Is.EqualTo(basicIdentity.Name));
            });

            var ordinaryIdentity = Content.Shared.IdentityManagement.Identity.Name(fixedTarget, SEntMan, ordinaryViewer);
            Assert.That(ordinaryIdentity.Name, Is.EqualTo("Fixed target true name"));

            var adjustedIdentity = Content.Shared.IdentityManagement.Identity.Name(fixedTarget, SEntMan, fixedViewer);
            var probe = SEntMan.GetComponent<IdentityEventProbeComponent>(fixedViewer);
            Assert.Multiple(() =>
            {
                Assert.That(adjustedIdentity.Entity, Is.EqualTo(fixedTarget));
                Assert.That(adjustedIdentity.Name, Is.EqualTo("event-adjusted identity"));
                Assert.That(probe.Calls, Is.EqualTo(1));
                Assert.That(probe.LastIdentified, Is.EqualTo(fixedTarget));
            });

            probe.NameOverride = null;
            probe.Cancel = true;
            var cancelledIdentity = Content.Shared.IdentityManagement.Identity.Name(fixedTarget, SEntMan, fixedViewer);
            Assert.Multiple(() =>
            {
                Assert.That(cancelledIdentity.Name, Is.EqualTo("Fixed target true name"));
                Assert.That(probe.Calls, Is.EqualTo(2));
            });

            SEntMan.AddComponent<YautjaComponent>(basic);
            SEntMan.AddComponent<YautjaComponent>(yautjaViewer);
            var yautjaFixedIdentity = SEntMan.EnsureComponent<FixedIdentityComponent>(basic);
            yautjaFixedIdentity.Name = "cmu-yautja-identity-unknown";
            yautjaFixedIdentity.Whitelist = new EntityWhitelist
            {
                Components = ["Yautja"],
            };
            Assert.That(Server.System<EntityWhitelistSystem>()
                .IsWhitelistPass(yautjaFixedIdentity.Whitelist, yautjaViewer), Is.True);

            var yautjaIdentity = Content.Shared.IdentityManagement.Identity.Name(basic, SEntMan, yautjaViewer);
            Assert.Multiple(() =>
            {
                Assert.That(yautjaIdentity.Entity, Is.EqualTo(basic));
                Assert.That(yautjaIdentity.Name, Is.EqualTo("Basic identity"));
                Assert.That(yautjaIdentity.Name,
                    Is.Not.EqualTo(localization.GetString("cmu-yautja-identity-unknown")),
                    "Yautja viewers should see another Yautja's true name instead of its fixed unknown identity.");
            });
        });
    }
}

[RegisterComponent]
public sealed partial class IdentityEventProbeComponent : Component
{
    public int Calls;
    public bool Cancel;
    public string? NameOverride;
    public EntityUid LastIdentified;
}

public sealed class IdentityEventProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdentityEventProbeComponent, RMCGetFixedIdentityEvent>(OnFixedIdentity);
    }

    private static void OnFixedIdentity(
        Entity<IdentityEventProbeComponent> ent,
        ref RMCGetFixedIdentityEvent args)
    {
        ent.Comp.Calls++;
        ent.Comp.LastIdentified = args.Identified;
        if (ent.Comp.NameOverride is { } name)
            args.Name = name;
        if (ent.Comp.Cancel)
            args.Cancelled = true;
    }
}
