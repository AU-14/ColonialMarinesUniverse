using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Components;
using Robust.Shared.Input;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.PowerLoader;

/// <summary>
/// In game the pilot is the local player, so boarding is real client input predicted on their own client.
/// </summary>
public sealed class CMUPowerLoaderPilotVisualsTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    [Test]
    public async Task OverlayDrawsOverItsPilot()
    {
        await Server.WaitPost(() => Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillPowerLoader", 1));
        await SpawnTarget("RMCMechPowerLoader");
        await RunTicks(5);
        await PressKey(EngineKeyFunctions.Use);
        await RunTicks(10);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).BuckledTo, Is.EqualTo(STarget)));

        await Client.WaitAssertion(() =>
        {
            AssertClientVisuals("powerloader_overlay", DrawDepth.OverMobs, DrawDepth.Mobs);
            Assert.That(ClientSounds(), Does.Contain("/Audio/_RMC14/Mecha/powerloader_buckle.ogg"),
                "the pilot's own client plays the predicted buckle sound");
        });

        // Unbuckling right after buckling is refused by the buckle delay.
        await RunTicks(30);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedBuckleSystem>().TryUnbuckle(SPlayer, SPlayer, popup: false), Is.True));
        await RunTicks(10);
        // RMC draws the unbuckled local player below other mobs.
        await Client.WaitAssertion(() => AssertClientVisuals("powerloader_open", DrawDepth.Mobs, DrawDepth.BelowMobs));
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

    private List<string> ClientSounds()
    {
        var sounds = new List<string>();
        var query = CEntMan.AllEntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var audio))
            sounds.Add(audio.FileName);
        return sounds;
    }
}
