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
    private const string Sentence = "They're in the upper window, get the rifles and hold the stairs now!";

    [Test]
    public async Task BrokenSpeechKeepsMostWords()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var accent = entMan.System<CMUWendigoBrokenSpeechAccentSystem>();
            var speaker = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entMan.AddComponent<CMUWendigoBrokenSpeechAccentComponent>(speaker);

            var words = Sentence.Split(' ').Select(Squeeze).Where(w => w.Length > 0).ToList();
            Assert.That(words, Has.Count.GreaterThanOrEqualTo(10));

            var changed = false;
            for (var i = 0; i < 5; i++)
            {
                // Without the entity the accent rolls the shared random, so each try differs.
                var result = i == 0
                    ? accent.Accentuate(Sentence, new Entity<CMUWendigoBrokenSpeechAccentComponent>(speaker, comp))
                    : accent.Accentuate(Sentence);
                changed |= result != Sentence;

                // Stretched and doubled letters collapse back, so "Uuupperrr" still counts as "upper".
                var squeezed = Squeeze(result);
                var survived = words.Count(w => squeezed.Contains(w));
                Assert.That(survived, Is.GreaterThanOrEqualTo(words.Count / 2),
                    $"Too many words lost: \"{result}\"");
                Assert.That(result.TrimEnd().EndsWith('!'), Is.True, $"End punctuation lost: \"{result}\"");
            }

            Assert.That(changed, Is.True, "The accent must break the sentence up.");
            entMan.DeleteEntity(speaker);
        });

        await pair.CleanReturnAsync();
    }

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
            comp.Stage = CMUWendigoSubjectStage.Fed2;
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
    private static string Squeeze(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (!char.IsLetterOrDigit(c) && c != ' ')
                continue;

            if (builder.Length > 0 && builder[builder.Length - 1] == c)
                continue;

            builder.Append(c);
        }

        return builder.ToString();
    }
}
