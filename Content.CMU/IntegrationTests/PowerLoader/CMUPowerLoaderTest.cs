using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.PowerLoader;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Power;
using Content.Shared.Tools.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Utility;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using ClientPointLightComponent = Robust.Client.GameObjects.PointLightComponent;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.PowerLoader;

/// <summary>
/// The power loader as its pilot plays it: boarding, how they and the beacon are drawn, the clamps on doors,
/// damage and repair, and the variants' speeds.
/// </summary>
public sealed class CMUPowerLoaderTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    private EntityUid _loader;

    private async Task BoardLoader()
    {
        await Server.WaitAssertion(() =>
        {
            Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillPowerLoader", 1);
            _loader = SEntMan.SpawnEntity("RMCMechPowerLoader", SEntMan.GetCoordinates(PlayerCoords));
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, _loader), Is.True);
        });
        await RunTicks(5);
    }

    /// <summary>
    /// In game the pilot is the local player, so boarding is real client input predicted on their own client.
    /// </summary>
    [Test]
    public async Task PilotAndBeaconFollowTheLoader()
    {
        await Server.WaitPost(() => Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillPowerLoader", 1));
        await SpawnTarget("RMCMechPowerLoader");
        await RunTicks(5);
        var pilotOffset = Vector2.Zero;
        await Client.WaitPost(() => pilotOffset = CEntMan.GetComponent<SpriteComponent>(CPlayer).Offset);
        await PressKey(EngineKeyFunctions.Use);
        await RunTicks(10);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).BuckledTo, Is.EqualTo(STarget)));

        await Client.WaitAssertion(() =>
        {
            AssertClientVisuals("powerloader_overlay", DrawDepth.OverMobs, DrawDepth.Mobs);
            Assert.That(CountSounds(CEntMan, "/Audio/_RMC14/Mecha/powerloader_buckle.ogg"), Is.GreaterThan(0),
                "the pilot's own client plays the predicted buckle sound");
            var piloted = CEntMan.GetComponent<SpriteComponent>(CPlayer).Offset;
            Assert.That(piloted.Y, Is.EqualTo(pilotOffset.Y - 0.08f).Within(0.01f), "the pilot sits low in the cab");
            Assert.That(piloted.X, Is.EqualTo(pilotOffset.X).Within(0.01f));
        });

        // The pilot faces the way the loader is drawn, even if they turned on their own, and leans 7px into it
        // east or west, like CM13. Diagonals follow the loader's drawn frame, and so does the beacon.
        await Server.WaitPost(() =>
            Server.System<SharedTransformSystem>().SetLocalRotation(SPlayer, Angle.FromDegrees(180)));
        foreach (var degrees in new[] { 90.0, 270.0, 180.0, 45.0, 135.0, 225.0, 315.0 })
        {
            await Server.WaitPost(() =>
                Server.System<SharedTransformSystem>().SetLocalRotation(STarget!.Value, Angle.FromDegrees(degrees)));
            await RunTicks(5);
            await Client.WaitAssertion(() =>
            {
                var angle = Angle.FromDegrees(degrees);
                var facing = SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle).Convert();
                var lean = facing switch
                {
                    Direction.East => 0.21875f,
                    Direction.West => -0.21875f,
                    _ => 0f,
                };
                var pilot = CEntMan.GetComponent<SpriteComponent>(CPlayer);
                var loader = CEntMan.GetComponent<CMUPowerLoaderComponent>(CTarget!.Value);
                var beacon = (-angle).RotateVec(loader.BeaconOffsets[facing]);
                var light = CEntMan.GetComponent<ClientPointLightComponent>(CTarget.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(pilot.EnableDirectionOverride, Is.True);
                    Assert.That(pilot.DirectionOverride, Is.EqualTo(facing), $"facing {degrees} degrees");
                    Assert.That(pilot.Offset.X, Is.EqualTo(pilotOffset.X + lean).Within(0.01f), $"facing {degrees} degrees");
                    Assert.That(pilot.Offset.Y, Is.EqualTo(pilotOffset.Y - 0.08f).Within(0.01f), $"facing {degrees} degrees");
                    Assert.That(light.Offset.X, Is.EqualTo(beacon.X).Within(0.01f), $"beacon facing {degrees} degrees");
                    Assert.That(light.Offset.Y, Is.EqualTo(beacon.Y).Within(0.01f), $"beacon facing {degrees} degrees");
                });
            });
        }

        // Unbuckling right after buckling is refused by the buckle delay.
        await Server.WaitPost(() =>
            Server.System<SharedTransformSystem>().SetLocalRotation(STarget!.Value, Angle.FromDegrees(90)));
        await RunTicks(30);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedBuckleSystem>().TryUnbuckle(SPlayer, SPlayer, popup: false), Is.True));
        await RunTicks(10);
        // RMC draws the unbuckled local player below other mobs, and an empty loader facing east keeps the usual depth.
        await Client.WaitAssertion(() =>
        {
            AssertClientVisuals("powerloader_open", DrawDepth.Mobs, DrawDepth.BelowMobs);
            Assert.That(CEntMan.GetComponent<SpriteComponent>(CPlayer).Offset, Is.EqualTo(pilotOffset),
                "leaving the loader puts the pilot's sprite back");
            Assert.That(CEntMan.GetComponent<SpriteComponent>(CPlayer).EnableDirectionOverride, Is.False,
                "and lets them turn on their own again");
        });
    }

    /// <summary>
    /// Someone standing on an empty loader shows in front of it when it faces the camera, behind it when it faces away.
    /// </summary>
    [TestCase(0.0, DrawDepth.BelowMobs)]
    [TestCase(180.0, DrawDepth.OverMobs)]
    [TestCase(90.0, DrawDepth.Mobs)]
    [TestCase(270.0, DrawDepth.Mobs)]
    public async Task EmptyLoaderDrawsByItsFacing(double degrees, DrawDepth depth)
    {
        await SpawnTarget("RMCMechPowerLoader");
        await Server.WaitPost(() =>
            Server.System<SharedTransformSystem>().SetLocalRotation(STarget!.Value, Angle.FromDegrees(degrees)));
        await RunTicks(5);
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<SpriteComponent>(CTarget!.Value).DrawDepth, Is.EqualTo((int) depth)));

        // A pilot still draws under the loader's overlay, whichever way it faces.
        await Server.WaitAssertion(() =>
        {
            Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillPowerLoader", 1);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, STarget!.Value), Is.True);
        });
        await RunTicks(10);
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<SpriteComponent>(CTarget!.Value).DrawDepth,
                Is.EqualTo((int) DrawDepth.OverMobs)));
    }

    [Test]
    public async Task TurningPlaysTheHydraulics()
    {
        await SpawnTarget("RMCMechPowerLoader");
        await RunTicks(20);
        var before = 0;
        await Server.WaitPost(() =>
        {
            before = CountSounds("/Audio/CMU14/PowerLoader/powerloader_turn");
            Server.System<SharedTransformSystem>().SetLocalRotation(STarget!.Value, Angle.FromDegrees(90));
        });
        await RunTicks(1);
        await Server.WaitAssertion(() =>
            Assert.That(CountSounds("/Audio/CMU14/PowerLoader/powerloader_turn"), Is.GreaterThan(before)));
    }

    [TestCase("RMCMechPowerLoaderBlue", 1.12f)]
    [TestCase("RMCMechPowerLoaderGreen", 0.8f)]
    public async Task VariantsMoveAtTheirOwnSpeed(string variant, float relative)
    {
        var loaders = new EntityUid[2];
        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.GetCoordinates(PlayerCoords);
            var prototypes = new[] { "RMCMechPowerLoader", variant };
            for (var i = 0; i < loaders.Length; i++)
            {
                var pilot = i == 0 ? SPlayer : SEntMan.SpawnEntity("CMMobHuman", coords);
                Server.System<SkillsSystem>().SetSkill(pilot, "RMCSkillPowerLoader", 1);
                loaders[i] = SEntMan.SpawnEntity(prototypes[i], coords);
                Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(pilot, pilot, loaders[i]), Is.True);
            }
        });
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var baseline = SEntMan.GetComponent<MovementSpeedModifierComponent>(loaders[0]).WalkSpeedModifier;
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(loaders[1]).WalkSpeedModifier;
            Assert.That(baseline, Is.GreaterThan(0f));
            Assert.That(speed, Is.EqualTo(baseline * relative).Within(0.01f));
        });
    }

    [Test]
    public async Task ClampsForceAnUnpoweredDoor()
    {
        await BoardLoader();
        await SpawnTarget("CMAirlock");
        await Interact();
        // The pry runs on the loader, not the player, so wait it out.
        await RunTicks(150);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DoorComponent>(STarget!.Value).State,
                Is.AnyOf(DoorState.Opening, DoorState.Open)));
    }

    [Test]
    public async Task ClampsForceAPoweredAirlock()
    {
        await BoardLoader();
        await SpawnTarget("CMAirlock");
        await Server.WaitPost(() =>
        {
            var powered = new PowerChangedEvent(true, 0);
            SEntMan.EventBus.RaiseLocalEvent(STarget!.Value, ref powered);
            Assert.That(SEntMan.GetComponent<AirlockComponent>(STarget.Value).Powered, Is.True);
        });
        await Interact();
        // Powered airlocks take nine times longer to force, and close again on their own afterwards.
        await RunTicks(330);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DoorComponent>(STarget!.Value).State,
                Is.AnyOf(DoorState.Opening, DoorState.Open)));
    }

    [Test]
    public async Task ClampsForceAPodlockSlowly()
    {
        await BoardLoader();
        await SpawnTarget("RMCPodDoor");
        await Interact();
        await RunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(CountSounds("/Audio/Machines/airlock_creaking.ogg"), Is.GreaterThan(0),
                "the door creaks while the clamps work on it"));

        // An airlock gives way in a second; a podlock holds out for twelve.
        await RunTicks(300);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DoorComponent>(STarget!.Value).State, Is.EqualTo(DoorState.Closed)));

        await RunTicks(90);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DoorComponent>(STarget!.Value).State,
                Is.AnyOf(DoorState.Opening, DoorState.Open)));
    }

    [Test]
    public async Task ClampsCannotForceAWeldedDoor()
    {
        await BoardLoader();
        await SpawnTarget("CMAirlock");
        await Server.WaitPost(() => Server.System<WeldableSystem>().SetWeldedState(STarget!.Value, true));
        await Interact();
        await RunTicks(150);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<DoorComponent>(STarget!.Value).State, Is.EqualTo(DoorState.Welded)));
    }

    [Test]
    public async Task DamageSlowsAndSparksUntilWelded()
    {
        await BoardLoader();
        var undamagedSpeed = 0f;
        await Server.WaitPost(() => undamagedSpeed = Speed());

        await Hurt(210);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Loader().DamageState, Is.EqualTo(0));
            Assert.That(Speed(), Is.EqualTo(undamagedSpeed * 0.8f).Within(0.01f));
            Assert.That(CountSpawned("RMCBruteSparks"), Is.EqualTo(1), "a damaged loader sparks once");
        });

        await Hurt(100);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Loader().DamageState, Is.EqualTo(1));
            Assert.That(Speed(), Is.EqualTo(undamagedSpeed * 0.6f).Within(0.01f));
            Assert.That(CountSpawned("RMCBruteSparks"), Is.EqualTo(2), "and once more when badly damaged");
        });

        // Then it stays quiet rather than sparking constantly.
        await RunTicks(90);
        await Server.WaitAssertion(() => Assert.That(CountSpawned("RMCBruteSparks"), Is.AtMost(2)));

        // Someone outside the cab welds it back together.
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedBuckleSystem>().TryUnbuckle(SPlayer, SPlayer, popup: false), Is.True));
        // The loader's clamps take a moment to leave the pilot's hands.
        await RunTicks(10);
        Target = SEntMan.GetNetEntity(_loader);
        await PlaceInHands("CMWelder");
        for (var i = 0; i < 3; i++)
            await Interact();

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(_loader).Float(), Is.LessThan(200f));
            Assert.That(Loader().DamageState, Is.EqualTo(-1));
        });

        await DeleteHeldEntity();
        await RunTicks(30);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, _loader), Is.True));
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(Speed(), Is.EqualTo(undamagedSpeed).Within(0.01f)));
    }

    private void AssertClientVisuals(string state, DrawDepth loaderDepth, DrawDepth pilotDepth)
    {
        var loader = CTarget!.Value;
        var sprites = CEntMan.System<SpriteSystem>();
        var sprite = CEntMan.GetComponent<SpriteComponent>(loader);
        Assert.That(sprites.LayerMapTryGet((loader, sprite), "buckledLayer", out var index, false), Is.True);
        Assert.That(sprites.TryGetLayer((loader, sprite), index, out var layer, false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(layer!.State.Name, Is.EqualTo(state));
            Assert.That(sprite.DrawDepth, Is.EqualTo((int) loaderDepth));
            Assert.That(CEntMan.GetComponent<SpriteComponent>(CPlayer).DrawDepth, Is.EqualTo((int) pilotDepth));
        });
    }

    private int CountSounds(string prefix)
    {
        return CountSounds(SEntMan, prefix);
    }

    private static int CountSounds(IEntityManager entMan, string prefix)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var audio))
        {
            string file = audio.FileName;
            if (file.StartsWith(prefix))
                count++;
        }

        return count;
    }

    private async Task Hurt(float amount)
    {
        await Server.WaitPost(() =>
        {
            var blunt = Server.ProtoMan.Index<DamageTypePrototype>("Blunt");
            Server.System<DamageableSystem>().TryChangeDamage(_loader, new DamageSpecifier(blunt, amount));
        });
    }

    private CMUPowerLoaderComponent Loader()
    {
        return SEntMan.GetComponent<CMUPowerLoaderComponent>(_loader);
    }

    private float Speed()
    {
        return SEntMan.GetComponent<MovementSpeedModifierComponent>(_loader).WalkSpeedModifier;
    }

    private int CountSpawned(string prototype)
    {
        var count = 0;
        var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var meta))
        {
            if (meta.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }
}
