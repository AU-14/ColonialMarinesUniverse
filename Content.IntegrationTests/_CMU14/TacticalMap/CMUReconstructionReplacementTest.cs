using Content.Client._RMC14.TacticalMap;
using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.TacticalMap;

public sealed partial class CMUReconstructionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UnassignedComputerUsesTheNormalFactionAuthorization(bool identified)
    {
        await Server.WaitAssertion(() =>
        {
            _ui.CloseUi(_console, Key, _actor);
            var computer = SComp<TacticalMapComputerComponent>(_console);
            SEntMan.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().SetComputerFaction((_console, computer), null);
            if (identified) SEntMan.EnsureComponent<Content.Shared._RMC14.Marines.MarineComponent>(_actor).Faction = "opfor";
            Assert.That(_ui.TryOpenUi(_console, Key, _actor), Is.True);
            Send(new CMUReconViewMessage(Vector2i.Zero));
            var scene = _recon.BuildSnapshot(_console, _actor)!;
            Assert.That(scene.CanOrder, Is.EqualTo(identified));
            Assert.That(computer.Faction, Is.EqualTo(identified ? "OPFOR" : null));
            Send(new CMUReconSendMessage(scene.Generation, 1, [new(0, [new(2, 3)], CMUReconInk.Green)], []));
            Assert.That(_recon.BuildSnapshot(_console, _actor)!.Orders.Length, Is.EqualTo(identified ? 1 : 0),
                "An unidentified user must not write to the default marine faction merely by having leadership.");
        });
    }

}
