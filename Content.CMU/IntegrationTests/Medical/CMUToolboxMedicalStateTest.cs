using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CMUToolboxMedicalStateTest : GameTest
{
    [Test]
    public async Task StumpCorrectionRestoresClampWithoutMutatingSavedState()
    {
        await Client.WaitAssertion(() =>
        {
            var map = Client.System<SharedMapSystem>().CreateMap(out var mapId);
            var part = CEntMan.SpawnEntity(null, new MapCoordinates(Vector2.Zero, mapId));
            var component = CEntMan.AddComponent<CMUOpenStumpComponent>(part);
            component.Stumps.Add(new CMUStump { Type = BodyPartType.Arm, Symmetry = BodyPartSymmetry.Left });
            var stumps = Client.System<SharedCMUOpenStumpSystem>();
            var unclamped = Capture();

            stumps.SetClamped(part, component.Stumps[0], true, "AU14Tourniquet");
            var clamped = Capture();
            Apply(unclamped);
            Assert.That(component.Stumps[0].Clamped, Is.False, "Correction must undo the predicted clamp.");
            Assert.That(component.Stumps[0].ClampRefund, Is.Null);

            Apply(clamped);
            stumps.SetClamped(part, component.Stumps[0], false, null);
            Apply(clamped);
            Assert.That(component.Stumps[0].Clamped, Is.True, "Replaying removal must not mutate the saved clamped state.");
            Assert.That(component.Stumps[0].ClampRefund?.Id, Is.EqualTo("AU14Tourniquet"));

            CEntMan.DeleteEntity(map);

            IComponentState Capture() => CEntMan.GetComponentState(CEntMan.EventBus, component, null, GameTick.Zero)!;
            void Apply(IComponentState state)
            {
                var change = new ComponentHandleState(state, null);
                CEntMan.EventBus.RaiseComponentEvent(part, component, ref change);
            }
        });
    }
}
