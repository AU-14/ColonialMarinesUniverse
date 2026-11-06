using Content.Client._RMC14.Medical.Scanner;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical;

public sealed class CMUHealthScannerRefreshTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    [TestCase(false)]
    [TestCase(true)]
    public async Task OpenAnalyzerRefreshesBloodDuringInternalBleeding(bool putDown)
    {
        EntityUid scanner = default;
        EntityUid patient = default;
        HealthScannerWindow window = default!;
        string initial = string.Empty;
        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnEntity("CMMobHuman", MapData.GridCoords);
            scanner = SEntMan.SpawnEntity("CMHealthAnalyzer", MapData.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(SPlayer, scanner), Is.True);
            SEntMan.GetComponent<HealthScannerComponent>(scanner).Target = patient;
            SUiSys.OpenUi(scanner, HealthScannerUIKey.Key, SPlayer);
        });
        await RunTicks(15);
        await Client.WaitAssertion(() =>
        {
            window = Windows(UiMan.WindowRoot).Single();
            initial = window.BloodAmountLabel.Text;
            Assert.That(initial, Is.Not.Empty);
        });
        await Server.WaitAssertion(() =>
        {
            if (putDown)
                Assert.That(Server.System<SharedHandsSystem>().TryDrop(SPlayer, scanner), Is.True);
            var index = Server.System<CMUMedicalBodyIndexSystem>();
            foreach (var type in new[] { BodyPartType.Head, BodyPartType.Torso })
            {
                Assert.That(index.TryGetBodyPart(patient, new(type, BodyPartSymmetry.None), out var part), Is.True);
                Server.System<SharedCMUWoundsSystem>().SeedInternalBleed(part, "vascular:test", 0.5f);
            }
        });
        await RunSeconds(5);
        await Server.WaitAssertion(() => Assert.That(SUiSys.IsUiOpen(scanner, HealthScannerUIKey.Key, SPlayer), Is.True));
        await Client.WaitAssertion(() =>
            Assert.That(window.BloodAmountLabel.Text, Is.Not.EqualTo(initial),
                "An open analyzer must display the patient's falling blood volume without rescanning."));
    }

    private static IEnumerable<HealthScannerWindow> Windows(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is HealthScannerWindow window)
                yield return window;
            foreach (var descendant in Windows(child))
                yield return descendant;
        }
    }
}
