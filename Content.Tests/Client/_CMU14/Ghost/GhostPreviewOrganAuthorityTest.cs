using System.Collections.Generic;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Tests.Client.CMU14.Ghost;

[TestFixture]
[TestOf(typeof(GhostPreviewHelper))]
public sealed class GhostPreviewOrganAuthorityTest
{
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly ProtoId<OrganCategoryPrototype> Alpha = "Alpha";
    private static readonly ProtoId<OrganCategoryPrototype> Zeta = "Zeta";

    [Test]
    public void ProfileSourceUsesHeadThenTorsoThenOrdinalCategory()
    {
        var head = Profile(Color.Red);
        var torso = Profile(Color.Green);
        var alpha = Profile(Color.Blue);
        var zeta = Profile(Color.Yellow);

        Assert.Multiple(() =>
        {
            Assert.That(GhostPreviewHelper.SelectOrganProfile(new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>
            {
                [Zeta] = zeta,
                [Torso] = torso,
                [Head] = head,
                [Alpha] = alpha,
            }), Is.EqualTo(head));
            Assert.That(GhostPreviewHelper.SelectOrganProfile(new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>
            {
                [Zeta] = zeta,
                [Torso] = torso,
                [Alpha] = alpha,
            }), Is.EqualTo(torso));
            Assert.That(GhostPreviewHelper.SelectOrganProfile(new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>
            {
                [Zeta] = zeta,
                [Alpha] = alpha,
            }), Is.EqualTo(alpha));
        });
    }

    private static OrganProfileData Profile(Color eyeColor)
    {
        return new OrganProfileData
        {
            Sex = Sex.Unsexed,
            EyeColor = eyeColor,
            SkinColor = Color.White,
        };
    }
}
