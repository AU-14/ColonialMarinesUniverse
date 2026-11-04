using System.Reflection;
using Content.Client.StatusIcon;
using Content.IntegrationTests.Fixtures;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Moq;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Overlays;

[TestFixture]
public sealed class CMUStatusIconCacheTest : GameTest
{
    private static readonly ProtoId<FactionIconPrototype> AllyIcon = "CMUXenoHiveAlly";

    [Test]
    public async Task ArrivingEntitiesDoNotRebuildOtherIconsAndExpiryStillRefreshes()
    {
        await Client.WaitAssertion(() =>
        {
            const int capacity = 512;
            var system = CEntMan.System<StatusIconSystem>();
            CEntMan.System<CMUStatusIconCacheProbeSystem>();
            var clockField = typeof(StatusIconSystem).GetField("_timing", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var cacheField = typeof(StatusIconSystem).GetField("_iconCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var originalClock = clockField.GetValue(system);
            var originalCache = cacheField.GetValue(system);
            var now = TimeSpan.FromSeconds(10);
            var clock = new Mock<IGameTiming>();
            clock.SetupGet(timing => timing.RealTime).Returns(() => now);
            var entities = new List<EntityUid>();
            var output = new List<StatusIconData>();
            var firstIcon = CProtoMan.Index(AllyIcon);
            var replacementIcon = new StatusIconData { Icon = firstIcon.Icon, Priority = firstIcon.Priority + 1 };

            try
            {
                clockField.SetValue(system, clock.Object);
                cacheField.SetValue(system, Activator.CreateInstance(cacheField.FieldType, [capacity]));
                for (var i = 0; i < capacity; i++)
                    system.GetStatusIcons(Spawn(), output);

                var visible = entities[0];
                system.GetStatusIcons(visible, output);
                Assert.That(output.Single(), Is.SameAs(firstIcon));
                var probe = CEntMan.GetComponent<CMUStatusIconCacheProbeComponent>(visible);
                probe.Icon = replacementIcon;

                // Two new entities also overflow the old implementation's off-by-one limit.
                system.GetStatusIcons(Spawn(), output);
                system.GetStatusIcons(Spawn(), output);
                system.GetStatusIcons(visible, output);
                Assert.That(probe.Requests, Is.EqualTo(1),
                    "Capacity overflow must not rebuild a recently visible entity's icons.");
                Assert.That(output.Single(), Is.SameAs(firstIcon), "The caller's existing TTL is retained.");

                now += TimeSpan.FromSeconds(1);
                system.GetStatusIcons(visible, output);
                Assert.That(probe.Requests, Is.EqualTo(2));
                Assert.That(output.Single(), Is.SameAs(replacementIcon));
                TestContext.Progress.WriteLine($"CMU_STATUS_ICON_CACHE warmed={capacity} arrivals=2 retainedEntityRebuilds=0 expiryRebuilds=1");
            }
            finally
            {
                clockField.SetValue(system, originalClock);
                cacheField.SetValue(system, originalCache);
                foreach (var uid in entities)
                    CEntMan.DeleteEntity(uid);
            }

            EntityUid Spawn()
            {
                var uid = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                entities.Add(uid);
                CEntMan.AddComponent<CMUStatusIconCacheProbeComponent>(uid).Icon = firstIcon;
                return uid;
            }
        });
    }
}

[RegisterComponent]
public sealed partial class CMUStatusIconCacheProbeComponent : Component
{
    public StatusIconData Icon = default!;
    public int Requests;
}

public sealed class CMUStatusIconCacheProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUStatusIconCacheProbeComponent, GetStatusIconsEvent>(OnGetIcons);
    }

    private void OnGetIcons(Entity<CMUStatusIconCacheProbeComponent> ent, ref GetStatusIconsEvent args)
    {
        ent.Comp.Requests++;
        args.StatusIcons.Add(ent.Comp.Icon);
    }
}
