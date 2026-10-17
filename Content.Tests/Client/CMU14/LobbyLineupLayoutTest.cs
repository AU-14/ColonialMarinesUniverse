using System;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14;

[TestFixture]
public sealed class LobbyLineupLayoutTest
{
    [TestCase(480, 220)]
    [TestCase(1050, 500)]
    [TestCase(320, 600)]
    public void EveryFactionAndMemberFitsWithoutOverlap(int width, int height)
    {
        foreach (var counts in new[] { new[] { 2, 2, 4, 3, 3, 2, 2, 2 }, new[] { 6, 6, 12, 9, 9, 6, 6, 6 }, new[] { 128, 32, 32, 24, 16, 12, 8, 4 } })
        {
            var size = new Vector2(width, height);
            var sections = LobbyLineupLayout.Fit(counts.Length, size, counts);
            AssertFits(sections, size);
            for (var i = 0; i < sections.Length; i++)
            {
                var formation = LobbyLineupLayout.FormationSize(sections[i].Size);
                var members = LobbyLineupLayout.Fit(counts[i], formation);
                Assert.That(members, Has.Length.EqualTo(counts[i]));
                AssertFits(members, formation);
            }
        }
    }

    private static void AssertFits(UIBox2[] cells, Vector2 available)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            Assert.Multiple(() =>
            {
                Assert.That(cell.Left, Is.GreaterThanOrEqualTo(-0.001f));
                Assert.That(cell.Top, Is.GreaterThanOrEqualTo(-0.001f));
                Assert.That(cell.Right, Is.LessThanOrEqualTo(available.X + 0.001f));
                Assert.That(cell.Bottom, Is.LessThanOrEqualTo(available.Y + 0.001f));
                Assert.That(cell.Width, Is.GreaterThan(0));
                Assert.That(cell.Height, Is.GreaterThan(0));
            });
            for (var j = 0; j < i; j++)
            {
                var overlapWidth = Math.Min(cell.Right, cells[j].Right) - Math.Max(cell.Left, cells[j].Left);
                var overlapHeight = Math.Min(cell.Bottom, cells[j].Bottom) - Math.Max(cell.Top, cells[j].Top);
                Assert.That(overlapWidth <= 0.001f || overlapHeight <= 0.001f, Is.True, "Characters must not cover each other.");
            }
        }
    }
}
