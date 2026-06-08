using System;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared.Database;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Tests.Client.Administration.UI.Logs;

[TestFixture]
[TestOf(typeof(AdminLogMessageFormatter))]
public sealed class AdminLogMessageFormatterTest
{
    [Test]
    public void FormatHighlightsEntityLogWithoutChangingText()
    {
        const string message =
            "Projectile BaseBullet (15284/n15284, BulletRifle10x24mm) shot by Nolan Seidner (14944/n14944, AU14JobFORECONFirstReconSmartGunOperator, localhost@TheHellFire) hit reinforced hull (1809/n1809, CMWallReinforcedAlmayer) and dealt 2 damage";

        var formatted = AdminLogMessageFormatter.Format(LogType.BulletHit, message);

        Assert.Multiple(() =>
        {
            Assert.That(formatted.ToString(), Is.EqualTo(message));
            Assert.That(ContainsColoredText(formatted, "Projectile BaseBullet", Color.FromHex("#A9D18E")), Is.True);
            Assert.That(ContainsColoredText(formatted, "Nolan Seidner", Color.FromHex("#F3D38A")), Is.True);
            Assert.That(ContainsColoredText(formatted, "BulletRifle10x24mm", Color.FromHex("#8DB4E2")), Is.True);
            Assert.That(ContainsColoredText(formatted, "2 damage", Color.FromHex("#FF8E7A")), Is.True);
        });
    }

    [Test]
    public void FormatHighlightsChatTextAfterColon()
    {
        const string message = "Mira Santos said over squad radio: Need corpsman at FOB, north barricade is down";

        var formatted = AdminLogMessageFormatter.Format(LogType.Chat, message);

        Assert.Multiple(() =>
        {
            Assert.That(formatted.ToString(), Is.EqualTo(message));
            Assert.That(ContainsColoredText(formatted, "Need corpsman at FOB, north barricade is down", Color.FromHex("#D6B8FF")), Is.True);
        });
    }

    [Test]
    public void GetDetailsExtractsShortScanChips()
    {
        const string message =
            "Projectile BaseBullet (15284/n15284, BulletRifle10x24mm) shot by Nolan Seidner (14944/n14944, AU14JobFORECONFirstReconSmartGunOperator, localhost@TheHellFire) hit reinforced hull (1809/n1809, CMWallReinforcedAlmayer) and dealt 2 damage";

        var details = AdminLogMessageFormatter.GetDetails(LogType.BulletHit, message);

        Assert.Multiple(() =>
        {
            Assert.That(details, Does.Contain(new AdminLogMessageDetail("proto", "BulletRifle10x24mm")));
            Assert.That(details, Does.Contain(new AdminLogMessageDetail("damage", "2")));
            Assert.That(details.Count, Is.LessThanOrEqualTo(3));
        });
    }

    private static bool ContainsColoredText(FormattedMessage message, string expected, Color color)
    {
        var colorDepth = 0;

        foreach (var node in message.Nodes)
        {
            if (node.Name == "color")
            {
                if (node.Closing)
                {
                    colorDepth = Math.Max(0, colorDepth - 1);
                    continue;
                }

                if (node.Value.TryGetColor(out var nodeColor) &&
                    nodeColor == color)
                {
                    colorDepth++;
                }

                continue;
            }

            if (colorDepth > 0 &&
                node.Value.StringValue?.Contains(expected, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }
}
