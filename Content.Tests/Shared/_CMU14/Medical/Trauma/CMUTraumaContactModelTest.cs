using Content.Shared._CMU14.Medical.Trauma;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Trauma;

[TestFixture, TestOf(typeof(CMUTraumaContactModel))]
public sealed class CMUTraumaContactModelTest
{
    [Test]
    public void DefaultBallisticBoneProfileFavorsHeadAndLimbs()
    {
        var settings = CMUTraumaContactSettings.Default;

        Assert.Multiple(() =>
        {
            Assert.That(settings.BallisticHighDamageThreshold, Is.EqualTo(FixedPoint2.New(45)));
            Assert.That(settings.BallisticHeadBoneChance, Is.EqualTo(0.65f));
            Assert.That(settings.BallisticTorsoBoneChance, Is.EqualTo(0.30f));
            Assert.That(settings.BallisticArmBoneChance, Is.EqualTo(0.60f));
            Assert.That(settings.BallisticLegBoneChance, Is.EqualTo(0.60f));
            Assert.That(settings.BallisticOtherBoneChance, Is.EqualTo(0.35f));
        });
    }

    [Test]
    public void DefaultOrganProfileUsesModerateContactRates()
    {
        var settings = CMUTraumaContactSettings.Default;

        Assert.Multiple(() =>
        {
            Assert.That(settings.BallisticHeadOrganChance, Is.EqualTo(0.55f));
            Assert.That(settings.BallisticTorsoOrganChance, Is.EqualTo(0.50f));
            Assert.That(settings.PierceOrganChance, Is.EqualTo(0.35f));
            Assert.That(settings.SlashOrganChance, Is.EqualTo(0.20f));
            Assert.That(settings.BluntOrganChance, Is.EqualTo(0.10f));
        });
    }

    [Test]
    public void BallisticCanReachOrganWithoutBone()
    {
        var settings = TestSettings() with
        {
            BallisticTorsoBoneChance = 0.20f,
            BallisticTorsoOrganChance = 0.50f,
            BallisticVascularChance = 0.05f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            BodyPartType.Torso,
            FixedPoint2.New(40),
            hasOrgans: true,
            roll: 0.30f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.False);
            Assert.That(result.OrganContact, Is.True);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.Deep));
        });
    }

    [Test]
    public void BallisticCanMissDeepStructures()
    {
        var settings = TestSettings() with
        {
            BallisticTorsoBoneChance = 0.20f,
            BallisticTorsoOrganChance = 0.35f,
            BallisticVascularChance = 0.03f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            BodyPartType.Torso,
            FixedPoint2.New(40),
            hasOrgans: true,
            roll: 0.80f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.False);
            Assert.That(result.OrganContact, Is.False);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.SoftTissue));
        });
    }

    [Test]
    public void LowRollCanCauseDirectVascularContact()
    {
        var settings = TestSettings() with
        {
            BallisticTorsoBoneChance = 0.20f,
            BallisticTorsoOrganChance = 0.35f,
            BallisticVascularChance = 0.03f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            BodyPartType.Torso,
            FixedPoint2.New(40),
            hasOrgans: true,
            roll: 0.02f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.True);
            Assert.That(result.OrganContact, Is.True);
            Assert.That(result.VascularContact, Is.True);
            Assert.That(result.InternalBleedRate, Is.EqualTo(settings.BallisticInternalBleedRate));
        });
    }

    [Test]
    public void HighDamageForcesSevereBoneAndOrganContact()
    {
        var settings = TestSettings() with
        {
            BallisticHighDamageThreshold = FixedPoint2.New(60),
            BallisticTorsoBoneChance = 0f,
            BallisticTorsoOrganChance = 0f,
            BallisticVascularChance = 0f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            BodyPartType.Torso,
            FixedPoint2.New(75),
            hasOrgans: true,
            roll: 0.99f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.True);
            Assert.That(result.OrganContact, Is.True);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.Severe));
            Assert.That(result.HighEnergy, Is.True);
        });
    }

    [Test]
    public void SlashUsesSurfaceBiasedChances()
    {
        var settings = TestSettings() with
        {
            SlashBoneChance = 0.10f,
            SlashOrganChance = 0.25f,
            SlashVascularChance = 0.05f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Slash,
            BodyPartType.Torso,
            FixedPoint2.New(35),
            hasOrgans: true,
            roll: 0.20f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.False);
            Assert.That(result.OrganContact, Is.True);
            Assert.That(result.VascularContact, Is.False);
        });
    }

    [Test]
    public void SurfaceSlashImpactStronglyReducesOrganContact()
    {
        var settings = TestSettings() with
        {
            SlashOrganChance = 0.20f,
            SlashVascularChance = 0.05f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Slash,
            DamageImpact.MeleeSlash,
            BodyPartType.Torso,
            FixedPoint2.New(35),
            hasOrgans: true,
            roll: 0.10f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganContact, Is.False);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.SoftTissue));
        });
    }

    [Test]
    public void HighPenetrationSlashImpactCanReachOrgans()
    {
        var settings = TestSettings() with
        {
            SlashBoneChance = 0.10f,
            SlashOrganChance = 0.20f,
            SlashOrganPassThrough = 0.20f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Slash,
            DamageImpact.XenoRendingSlash(3),
            BodyPartType.Torso,
            FixedPoint2.New(35),
            hasOrgans: true,
            roll: 0.30f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.OrganContact, Is.True);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.Deep));
            Assert.That(result.OrganPassThrough, Is.GreaterThan(settings.SlashOrganPassThrough));
        });
    }

    [Test]
    public void MediumAndDeeperBallisticHitsIncreaseOrganPassThrough()
    {
        var settings = TestSettings() with
        {
            BallisticTorsoBoneChance = 0f,
            BallisticTorsoOrganChance = 1f,
            BallisticOrganPassThrough = 0.35f,
        };

        var shallow = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            new DamageImpact(DamageImpactDelivery.Projectile, DamageImpactContact.Stab, DamageImpactPenetration.Low, DamageImpactEnergy.Medium),
            BodyPartType.Torso,
            FixedPoint2.New(20),
            hasOrgans: true,
            roll: 0f,
            settings);

        var medium = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            new DamageImpact(DamageImpactDelivery.Projectile, DamageImpactContact.Stab, DamageImpactPenetration.Medium, DamageImpactEnergy.Medium),
            BodyPartType.Torso,
            FixedPoint2.New(20),
            hasOrgans: true,
            roll: 0f,
            settings);

        var deep = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Ballistic,
            DamageImpact.Projectile,
            BodyPartType.Torso,
            FixedPoint2.New(20),
            hasOrgans: true,
            roll: 0f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(shallow.OrganPassThrough, Is.EqualTo(settings.BallisticOrganPassThrough));
            Assert.That(medium.OrganPassThrough, Is.GreaterThan(shallow.OrganPassThrough));
            Assert.That(deep.OrganPassThrough, Is.GreaterThan(medium.OrganPassThrough));
        });
    }

    [Test]
    public void SnaggingContactImpactCannotReachOrgans()
    {
        var settings = TestSettings() with
        {
            SlashBoneChance = 0.10f,
            SlashOrganChance = 0.20f,
            SlashVascularChance = 0.05f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Slash,
            DamageImpact.SnaggingContact,
            BodyPartType.Torso,
            FixedPoint2.New(35),
            hasOrgans: true,
            roll: 0.0f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.False);
            Assert.That(result.OrganContact, Is.False);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.SoftTissue));
        });
    }

    [Test]
    public void ContactSlashInfersSnaggingSurfaceImpact()
    {
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Slash"] = 1,
            },
        };

        var impact = DamageImpact.ForContact(damage);

        Assert.Multiple(() =>
        {
            Assert.That(impact.Delivery, Is.EqualTo(DamageImpactDelivery.Contact));
            Assert.That(impact.Contact, Is.EqualTo(DamageImpactContact.Snag));
            Assert.That(impact.Penetration, Is.EqualTo(DamageImpactPenetration.None));
        });
    }

    [Test]
    public void EqualBruteGroupMeleeInfersSlashInsteadOfStab()
    {
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = 10,
                ["Slash"] = 10,
                ["Piercing"] = 10,
            },
        };

        var impact = DamageImpact.ForMelee(damage);

        Assert.Multiple(() =>
        {
            Assert.That(impact.Delivery, Is.EqualTo(DamageImpactDelivery.Melee));
            Assert.That(impact.Contact, Is.EqualTo(DamageImpactContact.Slash));
            Assert.That(impact.Penetration, Is.EqualTo(DamageImpactPenetration.Low));
        });
    }

    [Test]
    public void DamageImpactProfileCanMakeThrownBladeStab()
    {
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Slash"] = 25,
            },
        };

        var fallback = DamageImpact.ForThrown(damage);
        var profile = new DamageImpactProfile
        {
            Contact = DamageImpactContact.Stab,
            Penetration = DamageImpactPenetration.Medium,
        };

        var impact = profile.ApplyTo(fallback, DamageImpactDelivery.Thrown);

        Assert.Multiple(() =>
        {
            Assert.That(fallback.Contact, Is.EqualTo(DamageImpactContact.Fragment));
            Assert.That(impact.Delivery, Is.EqualTo(DamageImpactDelivery.Thrown));
            Assert.That(impact.Contact, Is.EqualTo(DamageImpactContact.Stab));
            Assert.That(impact.Penetration, Is.EqualTo(DamageImpactPenetration.Medium));
            Assert.That(impact.Energy, Is.EqualTo(DamageImpactEnergy.Medium));
        });
    }

    [Test]
    public void CrushImpactCanBreakBoneWithoutOrganOrVascularContact()
    {
        var settings = TestSettings() with
        {
            MeleeHighDamageThreshold = FixedPoint2.New(100),
            BluntBoneChance = 1f,
            BluntOrganChance = 1f,
            BluntVascularChance = 1f,
        };

        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Blunt,
            new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Crush, DamageImpactPenetration.None, DamageImpactEnergy.Medium),
            BodyPartType.Torso,
            FixedPoint2.New(10),
            hasOrgans: true,
            roll: 0.0f,
            settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.True);
            Assert.That(result.OrganContact, Is.False);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.Bone));
        });
    }

    [Test]
    public void ContactPierceInfersLowDepthStab()
    {
        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Piercing"] = 5,
            },
        };

        var impact = DamageImpact.ForContact(damage);

        Assert.Multiple(() =>
        {
            Assert.That(impact.Delivery, Is.EqualTo(DamageImpactDelivery.Contact));
            Assert.That(impact.Contact, Is.EqualTo(DamageImpactContact.Stab));
            Assert.That(impact.Penetration, Is.EqualTo(DamageImpactPenetration.Low));
        });
    }

    [Test]
    public void ExplosiveAlwaysCreatesSevereTrauma()
    {
        var result = CMUTraumaContactModel.Create(
            CMUTraumaMechanism.Explosive,
            BodyPartType.Torso,
            FixedPoint2.New(30),
            hasOrgans: true,
            roll: 0.99f,
            TestSettings());

        Assert.Multiple(() =>
        {
            Assert.That(result.BoneContact, Is.True);
            Assert.That(result.OrganContact, Is.True);
            Assert.That(result.VascularContact, Is.False);
            Assert.That(result.Depth, Is.EqualTo(CMUTraumaDepth.Severe));
            Assert.That(result.HighEnergy, Is.True);
        });
    }

    private static CMUTraumaContactSettings TestSettings()
        => CMUTraumaContactSettings.Default;
}
