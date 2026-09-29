using System.Collections.Generic;
using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

[TestFixture]
public sealed class ColonyAtmTest
{
    private const string IdCard = "AU14IDCardCLFCivilian";

    /// <summary>
    ///     A colonist's ID card has its account and PIN the moment the job spawn finishes, in the same
    ///     tick, without anyone opening character info or swiping at an ATM.
    /// </summary>
    [Test]
    public async Task JobSpawnedIdCardHasPinImmediately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var spawning = server.System<StationSpawningSystem>();
            var idCards = server.System<IdCardSystem>();
            ProtoId<JobPrototype> job = "AU14JobCivilianNurse";

            var first = spawning.SpawnPlayerMob(testMap.GridCoords, job, new HumanoidCharacterProfile(), station: null);
            var second = spawning.SpawnPlayerMob(testMap.GridCoords, job, new HumanoidCharacterProfile(), station: null);

            Assert.That(idCards.TryFindIdCard(first, out var firstCard), "Colonist spawned without an ID card");
            Assert.That(idCards.TryFindIdCard(second, out var secondCard), "Colonist spawned without an ID card");
            var (firstAccount, firstPin) = (firstCard.Comp.AccountNumber, firstCard.Comp.AtmPin);
            var (secondAccount, secondPin) = (secondCard.Comp.AccountNumber, secondCard.Comp.AtmPin);

            Assert.Multiple(() =>
            {
                Assert.That(firstAccount, Is.InRange(10000, 99999));
                Assert.That(firstPin, Is.InRange(1000, 9999));
                Assert.That(secondAccount, Is.InRange(10000, 99999));
                Assert.That(secondPin, Is.InRange(1000, 9999));
                Assert.That(secondAccount, Is.Not.EqualTo(firstAccount));
                Assert.That(secondPin, Is.Not.EqualTo(firstPin));
            });

            server.EntMan.DeleteEntity(first);
            server.EntMan.DeleteEntity(second);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryIdCardGetsItsOwnAccountAndPin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var cards = new List<IdCardComponent>();
            for (var i = 0; i < 200; i++)
            {
                var uid = entMan.SpawnEntity(IdCard, testMap.GridCoords);
                cards.Add(entMan.GetComponent<IdCardComponent>(uid));
            }

            Assert.Multiple(() =>
            {
                Assert.That(cards.All(c => c.AccountNumber is >= 10000 and <= 99999), "Account number out of range");
                Assert.That(cards.All(c => c.AtmPin is >= 1000 and <= 9999), "PIN out of range");
                Assert.That(cards.Select(c => c.AccountNumber).Distinct().Count(), Is.EqualTo(cards.Count), "Two cards share an account number");
                Assert.That(cards.Select(c => c.AtmPin).Distinct().Count(), Is.EqualTo(cards.Count), "Two cards share a PIN");
            });
        });

        await pair.CleanReturnAsync();
    }
}
