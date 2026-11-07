using System.Linq;
using System.Text;
using Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Speech;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoSpeechTest
{
    private const string Human = "CMMobHuman";


    [Test]
    public async Task StabilizedMutagenDamagesTheBrain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CMUMedicalCCVars.Enabled, true);
            server.CfgMan.SetCVar(CMUMedicalCCVars.OrganEnabled, true);

            var entMan = server.EntMan;
            var subject = entMan.SpawnEntity(Human, map.GridCoords);
            Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<CMUBrainComponent>(subject, out var brain), Is.True);
            Assert.That(entMan.GetComponent<OrganHealthComponent>(brain).Stage, Is.EqualTo(OrganDamageStage.Healthy));

            var comp = entMan.AddComponent<CMUWendigoSubjectComponent>(subject);
            comp.Stage = CMUWendigoSubjectStage.Primed;
            comp.StageEndsAt = TimeSpan.Zero;

            var reagent = CMUWendigoTransformationSystem.StabilizedMutagen;
            var proto = server.ProtoMan.Index<ReagentPrototype>(reagent);
            var ev = new ReactionEntityEvent(ReactionMethod.Injection,
                new ReagentQuantity(reagent, CMUWendigoTransformationSystem.RequiredDose), proto, null);
            entMan.EventBus.RaiseLocalEvent(subject, ref ev);

            Assert.That(comp.Stage, Is.EqualTo(CMUWendigoSubjectStage.Mutagen));
            Assert.That(entMan.GetComponent<OrganHealthComponent>(brain).Stage, Is.EqualTo(OrganDamageStage.Damaged),
                "Stabilized Mutagen damages the brain to Damaged, not Failing.");
            Assert.That(entMan.HasComponent<CMUBrainSpeechImpairmentComponent>(subject), Is.True,
                "The brain system applies its own speech impairment.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Lower-cased letters and digits with repeated letters collapsed.
    /// </summary>

}
