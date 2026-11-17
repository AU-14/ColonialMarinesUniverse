using Content.Shared.CMU14.Yautja;
using NUnit.Framework;

namespace Content.Tests.Shared.CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminValidationTest
{

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
