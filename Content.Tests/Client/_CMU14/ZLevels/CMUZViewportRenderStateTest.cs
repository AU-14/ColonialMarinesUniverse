using System;
using Content.Client.CMU14.ZLevels.Core;
using Moq;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZViewportRenderStateTest
{
    [TestCase(false)]
    [TestCase(true)]
    public void CompositionRestoresCallerStateEvenWhenRenderFails(bool fail)
    {
        var eye = new Eye();
        var viewport = new Mock<IClydeViewport>();
        viewport.SetupProperty(v => v.Eye, eye);
        viewport.SetupProperty(v => v.ClearColor, Color.CornflowerBlue);
        if (fail)
            viewport.Setup(v => v.Render()).Throws<InvalidOperationException>();

        void Compose()
        {
            using var state = new CMUZViewportRenderState(viewport.Object);
            viewport.Object.Eye = new Eye();
            viewport.Object.ClearColor = null;
            viewport.Object.Render();
        }

        if (fail)
            Assert.Throws<InvalidOperationException>(Compose);
        else
            Compose();

        Assert.Multiple(() =>
        {
            Assert.That(viewport.Object.Eye, Is.SameAs(eye));
            Assert.That(viewport.Object.ClearColor, Is.EqualTo(Color.CornflowerBlue));
        });
    }

    [Test]
    public void IndependentViewportScopesPreserveNullEyeAndClearColor()
    {
        var first = new Mock<IClydeViewport>();
        var second = new Mock<IClydeViewport>();
        first.SetupAllProperties();
        second.SetupAllProperties();
        second.Object.Eye = new Eye();
        second.Object.ClearColor = Color.Black;

        using (new CMUZViewportRenderState(first.Object))
        {
            first.Object.Eye = new Eye();
            first.Object.ClearColor = Color.Transparent;
            using (new CMUZViewportRenderState(second.Object))
            {
                second.Object.ClearColor = null;
            }

            Assert.That(second.Object.ClearColor, Is.EqualTo(Color.Black));
            Assert.That(first.Object.ClearColor, Is.EqualTo(Color.Transparent));
        }

        Assert.Multiple(() =>
        {
            Assert.That(first.Object.Eye, Is.Null);
            Assert.That(first.Object.ClearColor, Is.Null);
            Assert.That(second.Object.Eye, Is.Not.Null);
        });
    }
}
