using Content.Shared._CMU14.Yautja;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminValidationTest
{
    [Test]
    public void EmptyColorUsesWhiteAndTextIsTrimmed()
    {
        var valid = YautjaClanAdminValidation.TryNormalize(
            "  Clan  ",
            "  Description  ",
            "  ",
            out var fields,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(error, Is.EqualTo(YautjaClanAdminValidationError.None));
            Assert.That(fields, Is.EqualTo(new YautjaClanAdminFields("Clan", "Description", "#ffffff")));
        });
    }

    [Test]
    public void UppercaseHexColorIsPreserved()
    {
        var valid = YautjaClanAdminValidation.TryNormalize(
            "Clan",
            "Description",
            "#A1B2C3",
            out var fields,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(error, Is.EqualTo(YautjaClanAdminValidationError.None));
            Assert.That(fields, Is.EqualTo(new YautjaClanAdminFields("Clan", "Description", "#A1B2C3")));
        });
    }

    [TestCase("", "Description", "#ffffff", YautjaClanAdminValidationError.MissingNameOrDescription)]
    [TestCase("Clan", "", "#ffffff", YautjaClanAdminValidationError.MissingNameOrDescription)]
    [TestCase("Clan", "Description", "red", YautjaClanAdminValidationError.InvalidColor)]
    [TestCase("Clan", "Description", "#12345G", YautjaClanAdminValidationError.InvalidColor)]
    public void InvalidFieldsAreRejected(
        string name,
        string description,
        string color,
        YautjaClanAdminValidationError expected)
    {
        Assert.That(
            YautjaClanAdminValidation.TryNormalize(name, description, color, out _, out var error),
            Is.False);
        Assert.That(error, Is.EqualTo(expected));
    }
}
