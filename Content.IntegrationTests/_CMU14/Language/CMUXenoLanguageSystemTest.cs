using Content.Server._RMC14.Language.Systems;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Language;

[TestFixture]
public sealed class CMUXenoLanguageSystemTest
{
    private static readonly ProtoId<LanguagePrototype> XenoLanguage = "Xeno";

    [Test]
    public async Task CorruptedHiveXenoKeepsXenoAndGainsEnglishOnHiveAssignment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid corruptedHive = default;

        await server.WaitPost(() =>
        {
            xeno = server.EntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
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
                Assert.That(language.CanSpeak(xeno, SharedLanguageSystem.CommonLanguage), Is.True);
                Assert.That(language.CanUnderstand(xeno, SharedLanguageSystem.CommonLanguage), Is.True);
                Assert.That(language.CanSpeak(xeno, XenoLanguage), Is.True);
                Assert.That(language.CanUnderstand(xeno, XenoLanguage), Is.True);
                Assert.That(language.GetCurrentLanguage(xeno), Is.EqualTo(SharedLanguageSystem.CommonLanguage));
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
