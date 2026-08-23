using System;
using Content.Shared.Administration;
using NUnit.Framework;

namespace Content.Tests.Shared.Administration
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class AdminFlagsExtTest
    {
        [Test]
        [TestCase("ADMIN", AdminFlags.Admin)]
        [TestCase("ADMIN,DEBUG", AdminFlags.Admin | AdminFlags.Debug)]
        [TestCase("ADMIN,DEBUG,HOST", AdminFlags.Admin | AdminFlags.Debug | AdminFlags.Host)]
        [TestCase("", AdminFlags.None)]
        [TestCase("RMCMAINTAINER", AdminFlags.RMCMaintainer)]
        [TestCase("ADMINGHOST", AdminFlags.AdminGhost)]
        [TestCase("CLANS", AdminFlags.Clans)]
        [TestCase("ADMIN,CLANS", AdminFlags.Admin | AdminFlags.Clans)]
        [TestCase("HOST,ADMINGHOST", AdminFlags.Host | AdminFlags.AdminGhost)]
        public void TestNamesToFlags(string namesConcat, AdminFlags flags)
        {
            var names = namesConcat.Split(",", StringSplitOptions.RemoveEmptyEntries);

            Assert.That(AdminFlagsHelper.NamesToFlags(names), Is.EqualTo(flags));
        }

        [Test]
        [TestCase("ADMIN", AdminFlags.Admin)]
        [TestCase("ADMIN,DEBUG", AdminFlags.Admin | AdminFlags.Debug)]
        [TestCase("ADMIN,DEBUG,HOST", AdminFlags.Admin | AdminFlags.Debug | AdminFlags.Host)]
        [TestCase("", AdminFlags.None)]
        [TestCase("RMCMAINTAINER", AdminFlags.RMCMaintainer)]
        [TestCase("ADMINGHOST", AdminFlags.AdminGhost)]
        [TestCase("CLANS", AdminFlags.Clans)]
        [TestCase("ADMIN,CLANS", AdminFlags.Admin | AdminFlags.Clans)]
        [TestCase("ADMINGHOST,HOST", AdminFlags.AdminGhost | AdminFlags.Host)]
        public void TestFlagsToNames(string namesConcat, AdminFlags flags)
        {
            var names = namesConcat.Split(",", StringSplitOptions.RemoveEmptyEntries);

            Assert.That(AdminFlagsHelper.FlagsToNames(flags), Is.EquivalentTo(names));
        }

        [TestCase(AdminFlags.Host, AdminFlags.Clans, true)]
        [TestCase(AdminFlags.Host | AdminFlags.Permissions,
            AdminFlags.Clans | AdminFlags.Permissions, true)]
        [TestCase(AdminFlags.Host, AdminFlags.Clans | AdminFlags.Ban, false)]
        [TestCase(AdminFlags.Permissions, AdminFlags.Clans, false)]
        [TestCase(AdminFlags.Clans, AdminFlags.Clans, true)]
        public void HostCanGrantOnlyTheClansFlag(AdminFlags actorFlags, AdminFlags requestedFlags, bool expected)
        {
            Assert.That(AdminFlagsHelper.CanGrant(actorFlags, requestedFlags), Is.EqualTo(expected));
        }
    }
}
