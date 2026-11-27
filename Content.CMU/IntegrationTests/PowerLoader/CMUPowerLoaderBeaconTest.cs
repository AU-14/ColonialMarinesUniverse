using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using PointLightComponent = Robust.Server.GameObjects.PointLightComponent;

namespace Content.IntegrationTests.CMU14.PowerLoader;

[TestFixture]
public sealed class CMUPowerLoaderBeaconTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestPowerLoaderPilot
  parent: CMMobHuman
  components:
  - type: Skills
    skills:
      RMCSkillPowerLoader: 1
""";

    [TestCase("RMCMechPowerLoader")]
    [TestCase("RMCMechPowerLoaderGreen")]
    [TestCase("RMCMechPowerLoaderBlue")]
    public async Task BeaconRunsOnlyWhilePiloted(string prototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid loader = default, pilot = default;
        PointLightComponent light = default!;
        RotatingLightComponent rotating = default!;
        await Server.WaitAssertion(() =>
        {
            loader = SEntMan.SpawnEntity(prototype, map.GridCoords);
            pilot = SEntMan.SpawnEntity("CMUTestPowerLoaderPilot", map.GridCoords);
            light = SEntMan.GetComponent<PointLightComponent>(loader);
            rotating = SEntMan.GetComponent<RotatingLightComponent>(loader);

            Assert.That(light.Enabled, Is.False);

            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(pilot, pilot, loader), Is.True);
            Assert.That(light.Enabled, Is.True);
            Assert.That(rotating.Enabled, Is.True);
        });

        // Unbuckling right after buckling is refused by the buckle delay.
        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryUnbuckle(pilot, pilot, popup: false), Is.True);
            Assert.That(light.Enabled, Is.False);
            Assert.That(rotating.Enabled, Is.False);
        });
    }

    [TestCase("RMCMechPowerLoader", "powerloader_open", "powerloader_overlay")]
    [TestCase("RMCMechPowerLoaderGreen", "powerloader_open_jd", "powerloader_overlay_jd")]
    [TestCase("RMCMechPowerLoaderBlue", "powerloader_open_ft", "powerloader_overlay_ft")]
    public async Task PilotedLoaderShowsOverlayAboveMobs(string prototype, string openState, string pilotedState)
    {
        var map = await Pair.CreateTestMap();
        EntityUid loader = default, pilot = default;
        NetEntity loaderNet = default;
        await Server.WaitAssertion(() =>
        {
            loader = SEntMan.SpawnEntity(prototype, map.GridCoords);
            pilot = SEntMan.SpawnEntity("CMUTestPowerLoaderPilot", map.GridCoords);
            loaderNet = SEntMan.GetNetEntity(loader);
        });

        // Freshly spawned it faces the camera, so mobs standing on the empty loader draw in front of it.
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClientLoader(loaderNet, openState, DrawDepth.BelowMobs));

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(pilot, pilot, loader), Is.True));
        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClientLoader(loaderNet, pilotedState, DrawDepth.OverMobs));

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryUnbuckle(pilot, pilot, popup: false), Is.True));
        await Pair.RunTicksSync(5);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClientLoader(loaderNet, openState, DrawDepth.BelowMobs));
    }

    [TestCase("RMCMechPowerLoaderWreckage")]
    [TestCase("RMCMechPowerLoaderWreckageGreen")]
    [TestCase("RMCMechPowerLoaderWreckageBlue")]
    public async Task WreckageDrawsBelowMobs(string prototype)
    {
        var map = await Pair.CreateTestMap();
        NetEntity wreckNet = default;
        await Server.WaitAssertion(() =>
            wreckNet = SEntMan.GetNetEntity(SEntMan.SpawnEntity(prototype, map.GridCoords)));

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(wreckNet));
            Assert.That(sprite.DrawDepth, Is.EqualTo((int) DrawDepth.BelowMobs));
        });
    }

    [Test]
    public async Task LoaderUsesPowerLoaderBuckleSounds()
    {
        await Server.WaitAssertion(() =>
        {
            var proto = SEntMan.ComponentFactory;
            Assert.That(Server.ProtoMan.Index<EntityPrototype>("RMCMechPowerLoader")
                .TryGetComponent<StrapComponent>(out var strap, proto), Is.True);
            Assert.That(((SoundPathSpecifier) strap!.BuckleSound).Path.ToString(),
                Is.EqualTo("/Audio/_RMC14/Mecha/powerloader_buckle.ogg"));
            Assert.That(((SoundPathSpecifier) strap.UnbuckleSound).Path.ToString(),
                Is.EqualTo("/Audio/_RMC14/Mecha/powerloader_unbuckle.ogg"));
        });
    }

    private void AssertClientLoader(NetEntity loaderNet, string state, DrawDepth depth)
    {
        var loader = CEntMan.GetEntity(loaderNet);
        var sprites = CEntMan.System<SpriteSystem>();
        var sprite = CEntMan.GetComponent<SpriteComponent>(loader);
        Assert.That(sprites.LayerMapTryGet((loader, sprite), "buckledLayer", out var index, false), Is.True);
        Assert.That(sprites.TryGetLayer((loader, sprite), index, out var layer, false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(layer!.State.Name, Is.EqualTo(state));
            Assert.That(sprite.DrawDepth, Is.EqualTo((int) depth));
        });
    }
}
