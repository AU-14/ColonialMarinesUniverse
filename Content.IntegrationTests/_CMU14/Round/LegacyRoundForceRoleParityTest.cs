#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class LegacyRoundForceRoleParityTest
{
    private const int ExpectedLegacyJobCount = 120;
    private const string ExpectedLegacyJobDigest =
        "656D16F9C816A1CEC18034D840FDA5B6F8360FB35632B359DC2FE2C3546BCFA8";

    private static readonly IReadOnlyDictionary<string, int> ExpectedResolvedForceCounts =
        new Dictionary<string, int>
        {
            ["Govfor/CMBCIU"] = 10,
            ["Govfor/GOVFOR"] = 31,
            ["Govfor/RMC"] = 18,
            ["Govfor/UPP"] = 19,
            ["Govfor/WYPMC"] = 17,
            ["Opfor/OPFOR"] = 25,
        };

    [Test]
    public async Task LegacyForceRoleMatrixRemainsStableDuringMigration()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var jobs = prototypes.EnumeratePrototypes<JobPrototype>()
                .Where(job => job.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
                .OrderBy(job => job.ID, StringComparer.Ordinal)
                .ToArray();
            var rows = jobs
                .Select(job =>
                    $"{job.ID}\t{job.RoundSide}\t{job.RoundForce ?? "<none>"}\t{job.RoundRole ?? "<none>"}")
                .ToArray();
            var digest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
            var forceCounts = jobs
                .GroupBy(job => $"{job.RoundSide}/{job.RoundForce ?? "<none>"}")
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var summary = string.Join(
                ", ",
                forceCounts.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => $"{entry.Key}={entry.Value}"));

            Assert.Multiple(() =>
            {
                Assert.That(
                    jobs,
                    Has.Length.EqualTo(ExpectedLegacyJobCount),
                    $"Legacy force-role count changed. Matrix: {summary}");
                Assert.That(
                    forceCounts,
                    Is.EquivalentTo(ExpectedResolvedForceCounts),
                    $"Legacy side/force counts changed. Matrix: {summary}");
                Assert.That(
                    digest,
                    Is.EqualTo(ExpectedLegacyJobDigest),
                    $"Legacy force-role digest changed. Matrix: {summary}");
            });
        });

        await pair.CleanReturnAsync();
    }
}
