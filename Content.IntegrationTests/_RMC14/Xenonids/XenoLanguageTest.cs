using Content.Server._RMC14.Language.Systems;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Systems;

namespace Content.IntegrationTests._RMC14.Xenonids;

[TestFixture, TestOf(typeof(LanguageSystem))]
public sealed class XenoLanguageTest
{
    [Test]
    public async Task NormalXenoDoesNotUnderstandEnglish()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var language = entMan.System<LanguageSystem>();

            Assert.That(language.CanUnderstand(xeno, SharedLanguageSystem.CommonLanguage), Is.False);

            entMan.RemoveComponent<LanguageComponent>(xeno);
            Assert.That(language.CanUnderstand(xeno, SharedLanguageSystem.CommonLanguage), Is.False);

            entMan.DeleteEntity(xeno);
        });

        await pair.CleanReturnAsync();
    }
}
