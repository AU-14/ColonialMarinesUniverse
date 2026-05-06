using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Collections.Generic;
using System.Reflection;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class PainShockReworkTest
{
    [Test]
    public async Task ComminutedFractureAloneIsSeverePressureNotShock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var frac = entMan.EnsureComponent<FractureComponent>(part);
                fracture.SetSeverity((part, frac), FractureSeverity.Comminuted);

                var profile = pain.ComputePainSourceProfile(human);
                var rawTier = PainTierThresholds.Get(PainTier.None, profile.Target, 0f, pain.ShockThreshold);

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Target.Float(), Is.EqualTo(65f).Within(0.001f));
                    Assert.That(profile.RiseRate.Float(), Is.EqualTo(3.25f).Within(0.001f));
                    Assert.That(rawTier, Is.EqualTo(PainTier.Severe));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StackedSeriousSourcesCanReachShockPressure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var frac = entMan.EnsureComponent<FractureComponent>(part);
                fracture.SetSeverity((part, frac), FractureSeverity.Comminuted);
                AddWound(entMan, part, WoundSize.Massive, treated: false);
                entMan.EnsureComponent<CMUEscharComponent>(part);

                var profile = pain.ComputePainSourceProfile(human);
                var rawTier = PainTierThresholds.Get(PainTier.None, profile.Target, 0f, pain.ShockThreshold);

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Target.Float(), Is.EqualTo(95f).Within(0.001f));
                    Assert.That(profile.RiseRate.Float(), Is.EqualTo(4f).Within(0.001f));
                    Assert.That(rawTier, Is.EqualTo(PainTier.Shock));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatingWoundsRemovesTheirPainFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var wounds = AddWound(entMan, part, WoundSize.Massive, treated: false);

                Assert.That(pain.ComputePainSourceProfile(human).Target.Float(), Is.EqualTo(50f).Within(0.001f));

                var woundList = WoundsOf(wounds);
                woundList[0] = woundList[0] with { Treated = true };
                Assert.That(pain.ComputePainSourceProfile(human).Target, Is.EqualTo(FixedPoint2.Zero));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OxyMasksShockAndWeakerMedsDoNotInheritItsStrength()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();

            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var comp = entMan.EnsureComponent<PainShockComponent>(human);
            comp.Pain = 90;
            comp.PainTarget = 90;
            comp.CachedRiseRate = 0;

            pain.AddPainSuppressionProfile(human, 0.75f, 4, 1.25f, TimeSpan.FromSeconds(1));
            pain.AddPainSuppressionProfile(human, 0.50f, 2, 0.75f, TimeSpan.FromSeconds(30));
            pain.RefreshTier(human);

            Assert.Multiple(() =>
            {
                Assert.That(comp.RawTier, Is.EqualTo(PainTier.Shock));
                Assert.That(comp.Tier, Is.EqualTo(PainTier.None));
                Assert.That(pain.GetTierSuppression(human), Is.EqualTo(4));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var comp = entMan.GetComponent<PainShockComponent>(human);

            pain.RefreshTier(human);

            Assert.Multiple(() =>
            {
                Assert.That(comp.RawTier, Is.EqualTo(PainTier.Shock));
                Assert.That(comp.Tier, Is.EqualTo(PainTier.Moderate));
                Assert.That(pain.GetTierSuppression(human), Is.EqualTo(2));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetFirstPart(IEntityManager entMan, EntityUid bodyUid)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, _) in body.GetBodyChildren(bodyUid))
        {
            if (entMan.HasComponent<BodyPartComponent>(partUid))
                return partUid;
        }

        Assert.Fail("Expected CMU human to have at least one body part.");
        return EntityUid.Invalid;
    }

    private static BodyPartWoundComponent AddWound(IEntityManager entMan, EntityUid part, WoundSize size, bool treated)
    {
        var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
        WoundsOf(wounds).Add(new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, treated));
        SizesOf(wounds).Add(size);
        BandagesOf(wounds).Add(0);
        return wounds;
    }

    private static List<Wound> WoundsOf(BodyPartWoundComponent comp)
        => GetField<List<Wound>>(comp, "Wounds");

    private static List<WoundSize> SizesOf(BodyPartWoundComponent comp)
        => GetField<List<WoundSize>>(comp, "Sizes");

    private static List<int> BandagesOf(BodyPartWoundComponent comp)
        => GetField<List<int>>(comp, "Bandages");

    private static T GetField<T>(BodyPartWoundComponent comp, string name)
        => (T) typeof(BodyPartWoundComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(comp)!;
}
