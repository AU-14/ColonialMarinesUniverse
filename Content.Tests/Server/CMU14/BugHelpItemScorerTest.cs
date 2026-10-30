using System;
using Content.Server.CMU14.BugHelp;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14
{
    public sealed class BugHelpItemScorerTest
    {
        [Test]
        public void ExactIdBeatsExactNameBeatsContains()
        {
            Assert.That(BugHelpSystem.ScoreItem("cmweaponm34", "cmweaponm34", "m34 incinerator"), Is.EqualTo(1.0));
            Assert.That(BugHelpSystem.ScoreItem("m34 incinerator", "cmweaponm34", "m34 incinerator"), Is.EqualTo(0.95));
            // Contains + starts-with on the id.
            Assert.That(BugHelpSystem.ScoreItem("cmw", "cmweaponm34", "m34 incinerator"), Is.EqualTo(0.7));
            // Plain contains below the start.
            Assert.That(BugHelpSystem.ScoreItem("m34", "cmweaponm34", "m34 incinerator"), Is.EqualTo(0.6));
        }

        [Test]
        public void StartsWithBonusOnIdAndName()
        {
            Assert.That(BugHelpSystem.ScoreItem("m34", "weaponm34x", "gun"), Is.EqualTo(0.6));
            Assert.That(BugHelpSystem.ScoreItem("34x", "weaponm34x", "gun"), Is.EqualTo(0.6));
            Assert.That(BugHelpSystem.ScoreItem("m34", "gun", "m34 incinerator"), Is.EqualTo(0.65));
        }

        [Test]
        public void WordPrefixIsWeakestMatch()
        {
            // Contains beats word-prefix by construction: any word starting with the query makes the
            // surface contain it, so the 0.3 branch in the design spec is unreachable in practice.
            Assert.That(BugHelpSystem.ScoreItem("fla", "cmuweaponarsonistflamer", "arsonist flamer"), Is.EqualTo(0.6));
            Assert.That(BugHelpSystem.ScoreItem("inci", "cmweaponm34", "m34 incinerator"), Is.EqualTo(0.55));
        }

        [Test]
        public void NoMatchScoresZero()
        {
            Assert.That(BugHelpSystem.ScoreItem("banana", "cmweaponm34", "m34 incinerator"), Is.EqualTo(0.0));
        }
    }
}
