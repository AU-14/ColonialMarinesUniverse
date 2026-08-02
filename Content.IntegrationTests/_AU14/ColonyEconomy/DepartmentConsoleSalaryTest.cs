using Content.Server.Access.Systems;
using Content.Server.AU14.ColonyEconomy;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.ColonyEconomy;

[TestFixture]
public sealed class DepartmentConsoleSalaryTest
{
    [TestCase("AU14JobCivilianHeadPhysician", "AUDepartmentConsoleMedical")]
    [TestCase("AU14JobCivilianPhysician", "AUDepartmentConsoleMedical")]
    [TestCase("AU14JobCivilianNurse", "AUDepartmentConsoleMedical")]
    [TestCase("AU14JobCivilianEthicsAndWellnessAdvisor", "AUDepartmentConsoleMedical")]
    [TestCase("AU14JobCivilianEmergencyResponseOfficer", "AUDepartmentConsoleMedical")]
    [TestCase("AU14JobCivilianNurse", "AUDepartmentConsoleCivilian")]
    public async Task ColonyMedicalRoleReceivesSalaryAfterSpawnRegistration(string jobId, string consoleId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entityManager = server.EntMan;
            var adminConsoleSystem = server.System<AdminConsoleSystem>();
            var departmentSystem = server.System<DepartmentConsoleSystem>();
            var idCardSystem = server.System<IdCardSystem>();
            var stationSpawning = server.System<StationSpawningSystem>();
            var profile = new HumanoidCharacterProfile();
            EntProtoId consolePrototype = consoleId;
            var console = entityManager.SpawnEntity(consolePrototype, testMap.GridCoords);
            ProtoId<JobPrototype> job = jobId;
            var medicalWorker = stationSpawning.SpawnPlayerMob(testMap.GridCoords, job, profile, station: null);

            try
            {
                Assert.That(idCardSystem.TryFindIdCard(medicalWorker, out var idCard), Is.True);

                var department = entityManager.GetComponent<DepartmentConsoleComponent>(console);
                department.DepartmentBudget = department.DefaultSalary;
                var initialBalance = idCard.Comp.AccountBalance;
                Assert.That(adminConsoleSystem.GetIncomeTax(), Is.Zero, "The salary regression test requires zero income tax.");

                var spawned = new PlayerSpawnCompleteEvent(
                    medicalWorker,
                    pair.Player!,
                    job.Id,
                    lateJoin: false,
                    silent: true,
                    joinOrder: 1,
                    station: testMap.MapUid,
                    profile: profile);
                entityManager.EventBus.RaiseLocalEvent(medicalWorker, spawned, broadcast: true);

                Assert.That(
                    department.Members,
                    Does.Contain(idCard.Owner),
                    $"The payroll console did not register {job}'s ID card.");

                departmentSystem.DispenseSalaries();

                Assert.Multiple(() =>
                {
                    Assert.That(idCard.Comp.AccountBalance, Is.EqualTo(initialBalance + department.DefaultSalary));
                    Assert.That(idCard.Comp.AccountBalance, Is.GreaterThan(initialBalance));
                    Assert.That(department.DepartmentBudget, Is.Zero);
                });
            }
            finally
            {
                entityManager.DeleteEntity(medicalWorker);
                entityManager.DeleteEntity(console);
            }
        });

        await pair.CleanReturnAsync();
    }
}
