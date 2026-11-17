/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using System;
using System.Text.RegularExpressions;
using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU14.Speech;

/// <summary>
/// Unit tests for the CMU accent rules. The rules are plain string functions, so no game, prototype or entity
/// setup is needed. The language checks (French and Irish speakers skip their accent) live in the systems and are
/// not covered here, because they need a running entity system.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUAccentText))]
public sealed class CMUAccentTextTest
{
    [TestCase("hello", "allo", ExpectedResult = "allo")]
    [TestCase("Hello", "allo", ExpectedResult = "Allo")]
    [TestCase("HELLO", "allo", ExpectedResult = "ALLO")]
    [TestCase("I", "allo", ExpectedResult = "Allo")]
    public string MatchCase(string source, string replacement)
        => CMUAccentText.MatchCase(source, replacement);

    [Test]
    public void ApplyWordsReplacesWholeWordsOnly()
    {
        var words = CMUAccentText.BuildWords(("cat", "dog"));
        Assert.That(CMUAccentText.ApplyWords(words, "cat concat Cat CAT"), Is.EqualTo("dog concat Dog DOG"));
    }

    [Test]
    public void ApplyWordsHandlesRegexCharactersInWords()
    {
        var words = CMUAccentText.BuildWords(("ma'am", "madame"), ("a.b", "x"));
        Assert.That(CMUAccentText.ApplyWords(words, "Yes ma'am, a.b and axb"), Is.EqualTo("Yes madame, x and axb"));
    }

    [TestCase("running", ExpectedResult = "runnin'")]
    [TestCase("Running", ExpectedResult = "Runnin'")]
    [TestCase("RUNNING", ExpectedResult = "RUNNIN'")]
    [TestCase("GETTING out of here", ExpectedResult = "GETTIN' out of here")]
    [TestCase("something", ExpectedResult = "somethin'")]
    [TestCase("SOMETHING", ExpectedResult = "SOMETHIN'")]
    [TestCase("thing", ExpectedResult = "thing")]
    [TestCase("THING", ExpectedResult = "THING")]
    [TestCase("king", ExpectedResult = "king")]
    [TestCase("bring", ExpectedResult = "bring")]
    [TestCase("I am getting something", ExpectedResult = "I am gettin' somethin'")]
    public string DropG(string message)
        => CMUAccentText.DropG(message);

    [Test]
    public void DropGDoesNothingTwice()
    {
        var once = CMUAccentText.DropG("RUNNING and getting");
        Assert.That(CMUAccentText.DropG(once), Is.EqualTo(once));
    }

    [TestCase("Sure.", "partner", ExpectedResult = "Sure, partner.")]
    [TestCase("Sure", "partner", ExpectedResult = "Sure, partner")]
    [TestCase("Sure!!", "partner", ExpectedResult = "Sure, partner!!")]
    [TestCase("Sure.  ", "partner", ExpectedResult = "Sure, partner.")]
    [TestCase("Sure, partner.", "partner", ExpectedResult = "Sure, partner.")]
    [TestCase("", "partner", ExpectedResult = "")]
    [TestCase("   ", "partner", ExpectedResult = "   ")]
    public string AppendTag(string message, string tag)
        => CMUAccentText.AppendTag(message, tag);
}

[TestFixture]
public sealed class CMUAccentRulesTest
{
    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "Allo cher, merci for ze 'elp !")]
    [TestCase("What the hell is this?", ExpectedResult = "Zut alors is zis ?")]
    [TestCase("Hi, I think that mother is nothing", ExpectedResult = "Allo, I tink zat mother is noting")]
    [TestCase("The doctor said yes.", ExpectedResult = "Ze docteur said oui.")]
    [TestCase("Um, uh, er, em", ExpectedResult = "Euh, euh, euh, euh")]
    [TestCase("I was running to the colony", ExpectedResult = "I was running to ze colonie")]
    public string French(string message)
        => CMUFrenchAccentRules.Apply(message);

    [TestCase("What the hell is this?", ExpectedResult = "What in tarnation is this?")]
    [TestCase("God damn it, goddamn bugs", ExpectedResult = "Got damn it, got damn bugs")]
    [TestCase("GODDAMN", ExpectedResult = "GOT DAMN")]
    [TestCase("damn it", ExpectedResult = "dadgummit")]
    [TestCase("Damn it all, damn", ExpectedResult = "Dadgummit all, dadgum")]
    [TestCase("My friend, my friends and a pal", ExpectedResult = "Ol' pal, ol' pals and a pardner")]
    [TestCase("God is here", ExpectedResult = "God is here")]
    [TestCase("I swear to God", ExpectedResult = "I swear to God")]
    [TestCase("GETTING out of here", ExpectedResult = "GETTIN' out of here")]
    [TestCase("I was running to the colony", ExpectedResult = "I was runnin' to the settlement")]
    [TestCase("Please, my friend, bring a beer to the mess hall", ExpectedResult = "Please, ol' pal, bring a cold one to the mess hall")]
    public string Texas(string message)
        => CMUTexasAccentRules.Apply(message);

    [TestCase("Hello, he's running", ExpectedResult = "Howdy, he's a-runnin'")]
    [TestCase("I was running to the colony", ExpectedResult = "I was a-runnin' to the colony")]
    [TestCase("SHE WAS RUNNING", ExpectedResult = "SHE WAS A-RUNNIN'")]
    [TestCase("He is nothing", ExpectedResult = "He is nothin'")]
    [TestCase("GETTING out of here", ExpectedResult = "GETTIN' out of here")]
    public string Appalachian(string message)
        => CMUAppalachianAccentRules.Apply(message);

    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "G'day mate, ta for the help!")]
    [TestCase("The doctor said yes.", ExpectedResult = "The doc said yes.")]
    public string Australian(string message)
        => CMUAustralianAccentRules.Apply(message);

    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "Hello buddy, thanks for the help!")]
    [TestCase("What the hell is this?", ExpectedResult = "What the heck is this?")]
    [TestCase("The doctor said yes.", ExpectedResult = "The doc said yup.")]
    public string Canadian(string message)
        => CMUCanadianAccentRules.Apply(message);

    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "Hello pal, thanks for the help!")]
    [TestCase("Hi, I think that mother is nothing", ExpectedResult = "Howya, I think that mother is nothing")]
    [TestCase("Please, my friend, bring a beer to the mess hall", ExpectedResult = "Please, me pal, bring a pint to the canteen")]
    public string Irish(string message)
        => CMUIrishAccentRules.Apply(message);

    [TestCase("That's great.", ExpectedResult = "That's great, mate.")]
    [TestCase("Sure", ExpectedResult = "Sure, mate")]
    [TestCase("Sure, mate", ExpectedResult = "Sure, mate")]
    [TestCase("", ExpectedResult = "")]
    public string AustralianMate(string message)
        => CMUAustralianAccentRules.AddMate(message);

    [TestCase("That's great.", ExpectedResult = "That's great, eh.")]
    [TestCase("Sure", ExpectedResult = "Sure, eh")]
    [TestCase("Sure, eh?", ExpectedResult = "Sure, eh?")]
    [TestCase("", ExpectedResult = "")]
    public string CanadianEh(string message)
        => CMUCanadianAccentRules.AddEh(message);

    private static readonly string[] Samples =
    {
        "Hello friend, thanks for the help!",
        "What the hell is this?",
        "GETTING out of here",
        "I was running to the colony",
        "God damn it, goddamn bugs",
        "He is getting something",
        "Hi, I think that mother is nothing",
        "The doctor said yes.",
        "Please, my friend, bring a beer to the mess hall",
        "Um, uh, er, em",
        "",
    };

    private static readonly (string Name, Func<string, string> Apply)[] Accents =
    {
        ("French", CMUFrenchAccentRules.Apply),
        ("Texas", CMUTexasAccentRules.Apply),
        ("Appalachian", CMUAppalachianAccentRules.Apply),
        ("Australian", CMUAustralianAccentRules.Apply),
        ("Canadian", CMUCanadianAccentRules.Apply),
        ("Irish", CMUIrishAccentRules.Apply),
    };

    /// <summary>
    /// An accent can be applied twice to the same message (directly and through an inventory relay), so the
    /// second pass must change nothing.
    /// </summary>
    [Test]
    public void EveryAccentIsIdempotent()
    {
        foreach (var (name, apply) in Accents)
        {
            foreach (var sample in Samples)
            {
                var once = apply(sample);
                Assert.That(apply(once), Is.EqualTo(once), $"{name} changed \"{once}\" on a second pass (from \"{sample}\")");
            }
        }
    }

    [Test]
    public void EveryAccentLeavesEmptyMessagesAlone()
    {
        foreach (var (name, apply) in Accents)
        {
            Assert.That(apply(""), Is.EqualTo(""), name);
        }
    }

    // Skeleton for every accent: running it on text that no rule matches must not change it.
    [Test]
    public void EveryAccentLeavesUnmatchedTextAlone()
    {
        foreach (var (name, apply) in Accents)
        {
            Assert.That(apply("Zxqv 123"), Is.EqualTo("Zxqv 123"), name);
        }
    }
}
