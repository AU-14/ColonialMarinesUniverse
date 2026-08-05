using System;
using System.Numerics;
using Content.Client.Stylesheets;
using NUnit.Framework;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;

namespace Content.Tests.Client._CMU14.Stylesheets;

[TestFixture]
[TestOf(typeof(CrtStyleBox))]
public sealed class CrtStyleBoxTest
{
    [Test]
    public void CornerTicksDrawWithoutInvalidBoxes()
    {
        var texture = new TestTexture(new Vector2i(1, 1));
        var handle = new TestDrawingHandle(texture);
        var styleBox = new CrtStyleBox
        {
            DrawScanlines = false,
            DrawCornerTicks = true,
        };
        var crtUiEnabled = StyleNano.CrtUiEnabled;

        try
        {
            StyleNano.CrtUiEnabled = true;

            Assert.DoesNotThrow(() => styleBox.Draw(handle, new UIBox2(0, 0, 100, 100), 1f));
        }
        finally
        {
            StyleNano.CrtUiEnabled = crtUiEnabled;
        }
    }

    private sealed class TestTexture(Vector2i size) : Texture(size)
    {
        public override Color GetPixel(int x, int y) => default;
    }

    private sealed class TestDrawingHandle(Texture white) : DrawingHandleScreen(white)
    {
        public override void SetTransform(in Matrix3x2 matrix)
        {
        }

        public override Matrix3x2 GetTransform() => Matrix3x2.Identity;

        public override void UseShader(ShaderInstance shader)
        {
        }

        public override ShaderInstance GetShader() => null;

        public override void DrawPrimitives(
            DrawPrimitiveTopology primitiveTopology,
            Texture texture,
            ReadOnlySpan<DrawVertexUV2DColor> vertices)
        {
        }

        public override void DrawPrimitives(
            DrawPrimitiveTopology primitiveTopology,
            Texture texture,
            ReadOnlySpan<ushort> indices,
            ReadOnlySpan<DrawVertexUV2DColor> vertices)
        {
        }

        public override void DrawCircle(Vector2 position, float radius, Color color, bool filled = true)
        {
        }

        public override void DrawLine(Vector2 from, Vector2 to, Color color)
        {
        }

        public override void RenderInRenderTarget(IRenderTarget target, Action a, Color? clearColor)
        {
        }

        public override void DrawTexture(Texture texture, Vector2 position, Color? modulate = null)
        {
        }

        public override void DrawRect(UIBox2 rect, Color color, bool filled = true)
        {
        }

        public override void DrawTextureRectRegion(
            Texture texture,
            UIBox2 rect,
            UIBox2? subRegion = null,
            Color? modulate = null)
        {
        }

        public override void DrawEntity(
            EntityUid entity,
            Vector2 position,
            Vector2 scale,
            Angle? worldRot,
            Angle eyeRotation = default,
            Direction? overrideDirection = null,
            SpriteComponent sprite = null,
            TransformComponent xform = null,
            SharedTransformSystem xformSystem = null)
        {
        }
    }
}
