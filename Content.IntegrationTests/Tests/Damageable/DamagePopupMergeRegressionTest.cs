using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(DamagePopupSystem))]
public sealed class DamagePopupMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DamagePopupMergeTarget
  components:
  - type: DamagePopup
    allowTypeChange: true

- type: entity
  id: DamagePopupMergeUser
";

    [Test]
    public async Task HandledInteractionDoesNotCycleButUnhandledInteractionDoes()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var popup = SEntMan.Spawn("DamagePopupMergeTarget");
            var user = SEntMan.Spawn("DamagePopupMergeUser");
            try
            {
                var popupComp = SEntMan.GetComponent<DamagePopupComponent>(popup);
                var initial = popupComp.Type;

                var handled = new InteractHandEvent(user, popup) { Handled = true };
                SEntMan.EventBus.RaiseLocalEvent(popup, handled);
                Assert.That(popupComp.Type, Is.EqualTo(initial),
                    "an interaction already handled by another system must not cycle popup type");

                var unhandled = new InteractHandEvent(user, popup);
                SEntMan.EventBus.RaiseLocalEvent(popup, unhandled);
                Assert.That(popupComp.Type, Is.EqualTo(DamagePopupType.Total),
                    "an unhandled interaction retains upstream Combined-to-Total cycling");
            }
            finally
            {
                SEntMan.DeleteEntity(popup);
                SEntMan.DeleteEntity(user);
            }
        });
    }
}
