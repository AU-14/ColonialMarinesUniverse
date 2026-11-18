using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUTexasAccentTest
{
    [TestCase("God damn it, goddamn bugs", ExpectedResult = "Got damn it, got damn bugs")]
    [TestCase("I swear to God", ExpectedResult = "I swear to God")]
    [TestCase("GETTING out of here", ExpectedResult = "GETTIN' out of here")]
    [TestCase("My friend, my friends and a pal", ExpectedResult = "Ol' pal, ol' pals and a pardner")]
    public string Apply(string message)
        => CMUTexasAccentRules.Apply(message);

    // an accent can be applied twice (directly and through an inventory relay), so a second pass must change nothing
    [Test]
    public void SecondPassChangesNothing()
    {
        foreach (var sample in new[] { "Hello friend, thanks for the help!", "What the hell is this?", "I was running to the colony", "He is getting something", "Hi, I think that mother is nothing", "Um, uh, er, em", "God damn it, goddamn bugs", "My friend, my friends and a pal" })
        {
            var once = CMUTexasAccentRules.Apply(sample);
            Assert.That(CMUTexasAccentRules.Apply(once), Is.EqualTo(once), sample);
        }
    }
}
