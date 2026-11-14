using Content.Server._RMC14.Language.Systems;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Language;

[TestFixture]
public sealed class CMUXenoLanguageSystemTest
{
    private static readonly ProtoId<LanguagePrototype> XenoLanguage = "Xeno";

    // the overmind rules the pathogen hive, the queen-only check left it mute in a corrupted hive
    [Test]
    public async Task CorruptedPathogenOvermindSpeaksHumanLanguages()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid overmind = default;
        EntityUid corruptedHive = default;

        await server.WaitPost(() =>
        {
            overmind = server.EntMan.SpawnEntity("CMU14XenoOvermind", map.GridCoords);
            corruptedHive = server.EntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<LanguageSystem>().CanSpeak(overmind, SharedLanguageSystem.CommonLanguage), Is.False);
            server.EntMan.System<XenoHiveSystem>().SetHive(overmind, corruptedHive);
        });

        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<LanguageSystem>().CanSpeak(overmind, SharedLanguageSystem.CommonLanguage), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMXenoDrone", false)]
    [TestCase("CMXenoQueen", true)]
    public async Task CorruptedHiveOnlyQueenGainsHumanSpeechOnHiveAssignment(string prototype, bool canSpeakHuman)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid corruptedHive = default;

        await server.WaitPost(() =>
        {
            xeno = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            corruptedHive = server.EntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var language = server.EntMan.System<LanguageSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(language.CanSpeak(xeno, XenoLanguage), Is.True);
                Assert.That(language.CanUnderstand(xeno, XenoLanguage), Is.True);
                Assert.That(language.CanSpeak(xeno, SharedLanguageSystem.CommonLanguage), Is.False);
                Assert.That(language.CanUnderstand(xeno, SharedLanguageSystem.CommonLanguage), Is.False);
            });
        });

        await server.WaitPost(() =>
        {
            var hive = server.EntMan.System<XenoHiveSystem>();
            hive.SetHive(xeno, corruptedHive);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var language = server.EntMan.System<LanguageSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(language.CanSpeak(xeno, SharedLanguageSystem.CommonLanguage), Is.EqualTo(canSpeakHuman));
                Assert.That(language.CanSpeak(xeno, "Russian"), Is.EqualTo(canSpeakHuman));
                Assert.That(language.CanUnderstand(xeno, SharedLanguageSystem.CommonLanguage), Is.True);
                Assert.That(language.CanSpeak(xeno, XenoLanguage), Is.True);
                Assert.That(language.CanUnderstand(xeno, XenoLanguage), Is.True);
                Assert.That(language.GetCurrentLanguage(xeno), Is.EqualTo(canSpeakHuman ? SharedLanguageSystem.CommonLanguage : XenoLanguage));
            });
        });

        await server.WaitPost(() =>
        {
            var language = server.EntMan.System<LanguageSystem>();
            language.SetLanguage(xeno, XenoLanguage);
        });

        await server.WaitAssertion(() =>
        {
            var ev = new DetermineLanguageEvent(xeno, XenoLanguage);
            server.EntMan.EventBus.RaiseLocalEvent(xeno, ref ev);

            Assert.That(ev.Language, Is.EqualTo(XenoLanguage));
        });

        await server.WaitPost(() =>
        {
            var hive = server.EntMan.System<XenoHiveSystem>();
            hive.SetHive(xeno, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var language = server.EntMan.System<LanguageSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(language.CanSpeak(xeno, SharedLanguageSystem.CommonLanguage), Is.False);
                Assert.That(language.CanUnderstand(xeno, SharedLanguageSystem.CommonLanguage), Is.False);
                Assert.That(language.CanSpeak(xeno, XenoLanguage), Is.True);
                Assert.That(language.CanUnderstand(xeno, XenoLanguage), Is.True);
                Assert.That(language.GetCurrentLanguage(xeno), Is.EqualTo(XenoLanguage));
            });
        });

        await pair.CleanReturnAsync();
    }
}
