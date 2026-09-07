using System.Globalization;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceLogFormattingTest
{
    [Test]
    public void MultiPartDiagnosticLinesUseInvariantNumbers()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            string result = CMUServerPerformanceDiagnosticsManager.Invariant(
                $"frameMs={12.5:F2} ",
                $"fps={29.25:F2}");

            Assert.That(result, Is.EqualTo("frameMs=12.50 fps=29.25"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
