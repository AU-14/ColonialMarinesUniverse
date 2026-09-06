using System.Linq;
using System.Net;
using Content.Server._CMU14.BalanceRating;
using Content.Shared._CMU14.BalanceRating;

namespace Content.IntegrationTests.Tests.Preferences
{
    public sealed partial class ServerDbSqliteTests
    {
        [TestCase(2)]
        [TestCase(6)]
        public void CMUBalanceRatingScheduleBecomesDueAfterExactRoundInterval(int interval)
        {
            var schedule = new CMUBalanceRatingSchedule(interval);

            for (var roundId = 1; roundId < interval; roundId++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(schedule.CountRound(roundId), Is.False);
                    Assert.That(schedule.RoundsRemaining, Is.EqualTo(interval - roundId));
                    Assert.That(schedule.Due, Is.False);
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(schedule.CountRound(interval), Is.True);
                Assert.That(schedule.RoundsRemaining, Is.Zero);
                Assert.That(schedule.Due, Is.True);
            });
        }

        [Test]
        public void CMUBalanceRatingScheduleIgnoresInvalidDuplicateAndPostDueRounds()
        {
            var schedule = new CMUBalanceRatingSchedule(2);

            Assert.Multiple(() =>
            {
                Assert.That(schedule.CountRound(-1), Is.False);
                Assert.That(schedule.CountRound(0), Is.False);
                Assert.That(schedule.RoundsRemaining, Is.EqualTo(2));
            });

            Assert.That(schedule.CountRound(10), Is.False);
            Assert.That(schedule.RoundsRemaining, Is.EqualTo(1));

            Assert.Multiple(() =>
            {
                Assert.That(schedule.CountRound(9), Is.False);
                Assert.That(schedule.CountRound(10), Is.False);
                Assert.That(schedule.RoundsRemaining, Is.EqualTo(1));
            });

            Assert.That(schedule.CountRound(11), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(schedule.CountRound(12), Is.False);
                Assert.That(schedule.RoundsRemaining, Is.Zero);
                Assert.That(schedule.Due, Is.True);
            });
        }

        [Test]
        public void CMUBalanceRatingScheduleResetStartsFreshIntervalWithoutRecountingRound()
        {
            var schedule = new CMUBalanceRatingSchedule(1);

            Assert.That(schedule.CountRound(20), Is.True);
            schedule.Reset(2);

            Assert.Multiple(() =>
            {
                Assert.That(schedule.CountRound(20), Is.False);
                Assert.That(schedule.RoundsRemaining, Is.EqualTo(2));
                Assert.That(schedule.Due, Is.False);
            });

            Assert.That(schedule.CountRound(21), Is.False);
            Assert.That(schedule.RoundsRemaining, Is.EqualTo(1));

            Assert.Multiple(() =>
            {
                Assert.That(schedule.CountRound(22), Is.True);
                Assert.That(schedule.RoundsRemaining, Is.Zero);
                Assert.That(schedule.Due, Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CMUBalanceRatingAutomaticMetricForMapIsAlwaysFun(bool chooseFun)
        {
            var metric = CMUBalanceRatingSystem.SelectAutomaticMetric(CMUBalanceRatingTarget.Map, chooseFun);

            Assert.That(metric, Is.EqualTo(CMUBalanceRatingMetric.Fun));
        }

        [TestCase(CMUBalanceRatingTarget.Weapon, false, CMUBalanceRatingMetric.Power)]
        [TestCase(CMUBalanceRatingTarget.Weapon, true, CMUBalanceRatingMetric.Fun)]
        [TestCase(CMUBalanceRatingTarget.Xeno, false, CMUBalanceRatingMetric.Power)]
        [TestCase(CMUBalanceRatingTarget.Xeno, true, CMUBalanceRatingMetric.Fun)]
        public void CMUBalanceRatingAutomaticMetricForEntityUsesSelectedBranch(
            CMUBalanceRatingTarget target,
            bool chooseFun,
            CMUBalanceRatingMetric expected)
        {
            var metric = CMUBalanceRatingSystem.SelectAutomaticMetric(target, chooseFun);

            Assert.That(metric, Is.EqualTo(expected));
        }

        [Test]
        public async Task CMUBalanceRatingDashboardAggregatesPollsAndResponses()
        {
            var pair = await PoolManager.GetServerClient();

            try
            {
                await pair.Server.WaitAssertion(() =>
                {
                    var ratingSystem = pair.Server.System<CMUBalanceRatingSystem>();
                    var targets = ratingSystem.GetTargets().ToList();
                    var weapons = targets
                        .Where(target => target.Target == CMUBalanceRatingTarget.Weapon)
                        .Select(target => target.Id)
                        .ToList();
                    var xenos = targets
                        .Where(target => target.Target == CMUBalanceRatingTarget.Xeno)
                        .Select(target => target.Id)
                        .ToList();
                    var maps = targets
                        .Where(target => target.Target == CMUBalanceRatingTarget.Map)
                        .ToList();
                    var foundWeapon = ratingSystem.TryGetTarget("weaponlauncherm83", out var resolvedWeapon);
                    var foundReaper = ratingSystem.TryGetTarget("cmxenoreaper", out var resolvedReaper);

                    Assert.Multiple(() =>
                    {
                        Assert.That(weapons, Is.EquivalentTo(new[]
                        {
                            "WeaponLauncherM83",
                            "RMCWeaponLauncherM5ATL",
                            "RMCWeaponRifleSharp",
                            "WeaponRifleM4SPRCustom",
                            "CMM96SSniperRifle",
                            "RMCXM43E1AntiMaterielRifle",
                            "RMCWeaponSniperM707Vulture",
                            "RMCWeaponShotgunMK481",
                            "RMCWeaponFlamerSpec",
                            "AU14WeaponXM99A",
                            "WeaponRifleM5SPR2",
                            "RMCWeaponFL3Flamer",
                            "AU14WeaponShotgunSPAS12HAZOPS",
                            "AU14WeaponFlamerM240A2HAZOPS",
                            "AU14L64A3SniperRifle",
                            "RMCWeaponRifleL24B",
                            "AU14WeaponLauncherL164A3",
                            "RMCWeaponShotgunType23",
                            "RMCWeaponLauncherHJRA12",
                            "RMCType88SniperRifle",
                            "RMCWeaponFlamerSpecUPP",
                        }));
                        Assert.That(weapons, Does.Not.Contain("RMCWeaponRifleM54C"));
                        Assert.That(foundWeapon, Is.True);
                        Assert.That(resolvedWeapon.Id, Is.EqualTo("WeaponLauncherM83"));
                        Assert.That(resolvedWeapon.Name, Is.Not.Empty);
                        Assert.That(xenos, Does.Contain("CMXenoReaper"));
                        Assert.That(foundReaper, Is.True);
                        Assert.That(resolvedReaper.Target, Is.EqualTo(CMUBalanceRatingTarget.Xeno));
                        Assert.That(resolvedReaper.Id, Is.EqualTo("CMXenoReaper"));
                        Assert.That(maps, Has.Some.Matches<CMUBalanceRatingTargetOption>(target =>
                            target.Id == "CMUPlanetLament/ColonyFall" &&
                            target.Name.Contains("Barker's Lament", StringComparison.Ordinal) &&
                            target.AllowsMetric(CMUBalanceRatingMetric.Fun) &&
                            !target.AllowsMetric(CMUBalanceRatingMetric.Power)));
                        // don't pin specific presets
                        // Assert.That(maps, Has.Some.Matches<CMUBalanceRatingTargetOption>(target =>
                        //     target.Id.EndsWith("/Insurgency", StringComparison.Ordinal)));
                        Assert.That(maps.All(target =>
                            target.AllowsMetric(CMUBalanceRatingMetric.Fun) &&
                            !target.AllowsMetric(CMUBalanceRatingMetric.Power)), Is.True);
                        Assert.That(maps.Select(target => target.Id), Is.Unique);
                    });
                });

                var db = GetDb(pair.Server);
                var firstPlayer = NewUserId();
                var secondPlayer = NewUserId();
                await db.UpdatePlayerRecord(firstPlayer, "BalanceRaterOne", IPAddress.Loopback, null);
                await db.UpdatePlayerRecord(secondPlayer, "BalanceRaterTwo", IPAddress.Loopback, null);

                var (server, _) = await db.AddOrGetServer("BalanceRatingTestServer");
                var roundId = await db.AddNewRound(server, firstPlayer.UserId, secondPlayer.UserId);
                var openedAt = DateTime.UtcNow;

                Assert.That(
                    async () => await db.CreateCMUBalanceRatingPoll(
                        roundId,
                        CMUBalanceRatingTarget.Map,
                        "CMUPlanetLament/ColonyFall",
                        CMUBalanceRatingMetric.Power,
                        firstPlayer.UserId,
                        openedAt),
                    Throws.ArgumentException);

                var abortedPoll = await db.CreateCMUBalanceRatingPoll(
                    roundId,
                    CMUBalanceRatingTarget.Weapon,
                    "AbortedWeapon",
                    CMUBalanceRatingMetric.Fun,
                    firstPlayer.UserId,
                    openedAt.AddMinutes(-1));
                await db.DeleteCMUBalanceRatingPoll(abortedPoll);

                await db.CreateCMUBalanceRatingPoll(
                    roundId,
                    CMUBalanceRatingTarget.Xeno,
                    "CMXenoRavager",
                    CMUBalanceRatingMetric.Fun,
                    firstPlayer.UserId,
                    openedAt);

                var mapPoll = await db.CreateCMUBalanceRatingPoll(
                    roundId,
                    CMUBalanceRatingTarget.Map,
                    "CMUPlanetLament/ColonyFall",
                    CMUBalanceRatingMetric.Fun,
                    firstPlayer.UserId,
                    openedAt.AddSeconds(30));
                await db.AddCMUBalanceRatingResponse(mapPoll, secondPlayer.UserId, 4, openedAt.AddSeconds(30));

                var firstWeaponPoll = await db.CreateCMUBalanceRatingPoll(
                    roundId,
                    CMUBalanceRatingTarget.Weapon,
                    "RMCXM43E1AntiMaterielRifle",
                    CMUBalanceRatingMetric.Power,
                    firstPlayer.UserId,
                    openedAt.AddMinutes(1));
                await db.AddCMUBalanceRatingResponse(firstWeaponPoll, firstPlayer.UserId, 1, openedAt.AddMinutes(1));
                await db.AddCMUBalanceRatingResponse(firstWeaponPoll, firstPlayer.UserId, 5, openedAt.AddMinutes(1));

                var secondWeaponPoll = await db.CreateCMUBalanceRatingPoll(
                    roundId,
                    CMUBalanceRatingTarget.Weapon,
                    "RMCXM43E1AntiMaterielRifle",
                    CMUBalanceRatingMetric.Power,
                    secondPlayer.UserId,
                    openedAt.AddMinutes(2));
                await db.AddCMUBalanceRatingResponse(secondWeaponPoll, firstPlayer.UserId, 5, openedAt.AddMinutes(2));
                await db.AddCMUBalanceRatingResponse(secondWeaponPoll, secondPlayer.UserId, 5, openedAt.AddMinutes(2));

                var dashboard = await db.GetCMUBalanceRatingDashboard();
                var xeno = dashboard.Entries.Single(entry =>
                    entry.Target == CMUBalanceRatingTarget.Xeno &&
                    entry.TargetId == "CMXenoRavager" &&
                    entry.Metric == CMUBalanceRatingMetric.Fun);
                var map = dashboard.Entries.Single(entry =>
                    entry.Target == CMUBalanceRatingTarget.Map &&
                    entry.TargetId == "CMUPlanetLament/ColonyFall" &&
                    entry.Metric == CMUBalanceRatingMetric.Fun);
                var weapon = dashboard.Entries.Single(entry =>
                    entry.Target == CMUBalanceRatingTarget.Weapon &&
                    entry.TargetId == "RMCXM43E1AntiMaterielRifle" &&
                    entry.Metric == CMUBalanceRatingMetric.Power);

                Assert.Multiple(() =>
                {
                    Assert.That(dashboard.Entries, Has.Count.EqualTo(3));
                    Assert.That(dashboard.Entries, Has.None.Matches<CMUBalanceRatingStatisticsEntry>(
                        entry => entry.TargetId == "AbortedWeapon"));
                    Assert.That(dashboard.TotalPolls, Is.EqualTo(4));
                    Assert.That(dashboard.TotalResponses, Is.EqualTo(4));

                    Assert.That(xeno.Polls, Is.EqualTo(1));
                    Assert.That(xeno.Rating1, Is.Zero);
                    Assert.That(xeno.Rating2, Is.Zero);
                    Assert.That(xeno.Rating3, Is.Zero);
                    Assert.That(xeno.Rating4, Is.Zero);
                    Assert.That(xeno.Rating5, Is.Zero);
                    Assert.That(xeno.Responses, Is.Zero);
                    Assert.That(xeno.Average, Is.Zero);

                    Assert.That(map.Polls, Is.EqualTo(1));
                    Assert.That(map.Rating4, Is.EqualTo(1));
                    Assert.That(map.Responses, Is.EqualTo(1));
                    Assert.That(map.Average, Is.EqualTo(4));

                    Assert.That(weapon.Polls, Is.EqualTo(2));
                    Assert.That(weapon.Rating1, Is.EqualTo(1));
                    Assert.That(weapon.Rating2, Is.Zero);
                    Assert.That(weapon.Rating3, Is.Zero);
                    Assert.That(weapon.Rating4, Is.Zero);
                    Assert.That(weapon.Rating5, Is.EqualTo(2));
                    Assert.That(weapon.Responses, Is.EqualTo(3));
                    Assert.That(weapon.Average, Is.EqualTo(11d / 3d).Within(0.0001d));
                });
            }
            finally
            {
                await pair.CleanReturnAsync();
            }
        }
    }
}
