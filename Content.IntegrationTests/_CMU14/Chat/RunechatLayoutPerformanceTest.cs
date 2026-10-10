using System.Collections;
using System.Reflection;
using System.Text;
using Content.Client.Chat.UI;
using Content.IntegrationTests.Fixtures;
using Moq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.CMU14.Chat;

[TestFixture]
public sealed class RunechatLayoutPerformanceTest : GameTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type BubbleType = typeof(RunechatSpeechBubble);

    [TestCase("Обычный текст [bold]жирный[/bold] [italic]курсив[/italic] [color=#ff0000]цвет 😀[/color]")]
    [TestCase("[bolditalic]ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ[/bolditalic]")]
    public async Task DrawReusesGlyphMetricsUntilUiScaleChanges(string markup)
    {
        await Client.WaitAssertion(() =>
        {
            using var root = new ScaledRoot();
            var control = CreateControl(markup);
            root.AddChild(control);
            var regular = new CountingFont(4);
            var italic = new CountingFont(6);
            SetFont(control, "_regularFont", regular);
            SetFont(control, "_italicFont", italic);
            var handle = new Mock<DrawingHandleScreen>(MockBehavior.Loose, new TestTexture(Vector2i.One));

            Draw(control, handle.Object);
            Assert.That(regular.MetricCalls + italic.MetricCalls, Is.GreaterThan(0));
            var firstRegular = regular.Glyphs.ToArray();
            var firstItalic = italic.Glyphs.ToArray();
            var firstLayout = Layout(control);

            regular.Reset();
            italic.Reset();
            Draw(control, handle.Object);
            Assert.Multiple(() =>
            {
                Assert.That(regular.MetricCalls + italic.MetricCalls, Is.Zero,
                    "Halo, stroke and fill passes must reuse the layout's glyph bounds and advances.");
                Assert.That(regular.Glyphs, Is.EqualTo(firstRegular));
                Assert.That(italic.Glyphs, Is.EqualTo(firstItalic));
                Assert.That(Layout(control), Is.SameAs(firstLayout));
            });

            root.Scale = 2f;
            Method(control, "UIScaleChanged").Invoke(control, null);
            regular.Reset();
            italic.Reset();
            Draw(control, handle.Object);
            Assert.That(regular.MetricCalls + italic.MetricCalls, Is.GreaterThan(0),
                "A DPI change must rebuild glyph bounds and advances at the new scale.");
            Assert.That(Layout(control), Is.Not.SameAs(firstLayout));
            var scaledRegular = regular.Glyphs.ToArray();
            var scaledItalic = italic.Glyphs.ToArray();

            regular.Reset();
            italic.Reset();
            Draw(control, handle.Object);
            Assert.Multiple(() =>
            {
                Assert.That(regular.MetricCalls + italic.MetricCalls, Is.Zero);
                Assert.That(regular.Glyphs, Is.EqualTo(scaledRegular));
                Assert.That(italic.Glyphs, Is.EqualTo(scaledItalic));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EnteringAnotherRootRebuildsMetricsWithoutUiScaleEvent(bool measuredBeforeAttach)
    {
        await Client.WaitAssertion(() =>
        {
            using var firstRoot = new ScaledRoot();
            using var secondRoot = new ScaledRoot { Scale = 2f };
            var control = CreateControl("Обычный [bold]жирный[/bold] текст");
            var font = new CountingFont(4);
            SetFont(control, "_regularFont", font);
            var handle = new Mock<DrawingHandleScreen>(MockBehavior.Loose, new TestTexture(Vector2i.One));

            if (!measuredBeforeAttach)
                firstRoot.AddChild(control);
            Draw(control, handle.Object);
            var firstLayout = Layout(control);

            if (!measuredBeforeAttach)
                firstRoot.RemoveChild(control);
            secondRoot.AddChild(control);
            font.Reset();
            Draw(control, handle.Object);
            Assert.That(font.MetricCalls, Is.GreaterThan(0),
                "Entering a root changes UIScale without invoking UIScaleChanged.");
            Assert.That(Layout(control), Is.Not.SameAs(firstLayout));
            font.Reset();
            Draw(control, handle.Object);
            Assert.That(font.MetricCalls, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LongWordWrappingMeasuresEachRuneOnce(bool bold)
    {
        await Client.WaitAssertion(() =>
        {
            var text = new string('A', 79);
            using var control = CreateControl(bold ? $"[bold]{text}[/bold]" : text);
            var font = new CountingFont(40);
            SetFont(control, "_regularFont", font);
            var runs = BubbleType.GetMethod("ParseFormattingRuns", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [bold ? $"[bold]{text}[/bold]" : text]);
            var lines = (IList) Method(control, "BreakWordAcrossLines").Invoke(control, [runs])!;

            Assert.That(font.MetricCalls, Is.EqualTo(text.Length),
                "A long word must not remeasure and allocate every growing prefix.");
            var maxWidth = (float) Method(control, "GetMaxWidth").Invoke(control, null)!;
            var charsPerLine = (int) ((maxWidth - (bold ? control.UIScale : 0)) / 40);
            var wrapped = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = (IList) lines[i]!;
                Assert.That(line.Count, Is.EqualTo(1));
                var runText = (string) line[0]!.GetType().GetProperty("Text")!.GetValue(line[0])!;
                Assert.That(runText.Length, Is.EqualTo(Math.Min(charsPerLine, text.Length - wrapped.Length)));
                wrapped.Append(runText);
            }

            Assert.That(wrapped.ToString(), Is.EqualTo(text));
        });
    }

    private static Control CreateControl(string markup)
    {
        var runs = BubbleType.GetMethod("ParseFormattingRuns", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [markup]);
        var pages = BubbleType.GetMethod("GetRunPages", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [runs]);
        var styleType = BubbleType.GetNestedType("RunechatVisualStyle", BindingFlags.NonPublic)!;
        var style = styleType.GetField("Normal", BindingFlags.Static | BindingFlags.Public)!.GetValue(null);
        var controlType = BubbleType.GetNestedType("RunechatTextControl", BindingFlags.NonPublic)!;
        return (Control) Activator.CreateInstance(controlType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [pages, Color.White, style, null], null)!;
    }

    private static void SetFont(Control control, string name, Font font) =>
        control.GetType().GetField(name, PrivateInstance)!.SetValue(control, font);

    private static MethodInfo Method(Control control, string name) =>
        control.GetType().GetMethod(name, PrivateInstance | BindingFlags.DeclaredOnly)!;

    private static object Layout(Control control) =>
        ((IList) control.GetType().GetField("_layouts", PrivateInstance)!.GetValue(control)!)[0]!;

    private static void Draw(Control control, DrawingHandleScreen handle) =>
        Method(control, "Draw").Invoke(control, [handle]);

    private sealed class ScaledRoot : UIRoot
    {
        public float Scale = 1f;
        public override float UIScale => Scale;
    }

    private sealed class TestTexture(Vector2i size) : Texture(size)
    {
        public override Color GetPixel(int x, int y) => throw new NotSupportedException();
    }

    private sealed class CountingFont(int advance) : Font
    {
        public int MetricCalls;
        public readonly List<(Rune Rune, Vector2 Baseline, float Scale, Color Color)> Glyphs = new();

        public override int GetAscent(float scale) => (int) (8 * scale);
        public override int GetHeight(float scale) => (int) (8 * scale);
        public override int GetDescent(float scale) => 0;
        public override int GetLineHeight(float scale) => (int) (9 * scale);

        public override CharMetrics? GetCharMetrics(Rune rune, float scale, bool fallback = true)
        {
            MetricCalls++;
            return new CharMetrics((int) -scale, (int) (8 * scale), (int) (advance * scale),
                (int) ((advance - 1) * scale), (int) (7 * scale));
        }

        public override float DrawChar(DrawingHandleBase handle, Rune rune, Vector2 baseline, float scale,
            Color color, bool fallback = true)
        {
            Glyphs.Add((rune, baseline, scale, color));
            return advance * scale;
        }

        public override float DrawCharOutline(DrawingHandleBase handle, Rune rune, Vector2 baseline, float scale,
            TextOutline outline, bool fallback = true) => throw new NotSupportedException();

        public void Reset()
        {
            MetricCalls = 0;
            Glyphs.Clear();
        }
    }
}
