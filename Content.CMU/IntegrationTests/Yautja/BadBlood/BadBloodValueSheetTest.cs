#pragma warning disable RA0002 // tests read component data directly
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-107: the non-combat numbers of the Bad Blood master value sheet (docs/GraphAudit-BadBlood-Balance.md §12),
// read from the prototypes. If one of these fails, a number was retuned: change the sheet and this test together.
// The body's damage set and the armor values are not pinned here, they wait for the maintainer's combat sign-off.
[TestFixture]
public sealed class BadBloodValueSheetTest
{
    private const string Bracer = "CMUYautjaBadBloodBracer";
    private const string Caster = "CMUYautjaBadBloodPlasmaCaster";
    private const string Rule = "CMUBadBloodCampRule";
    private const string Totem = "CMUBadBloodTotem";
    private const string Lodge = "CMUBadBloodCamp";
    private const string Hound = "CMUMobYautjaBadBloodHound";
    private const string Armory = "CMUBadBloodCampArmory";

    [Test]
    public async Task PrototypesHoldTheValueSheet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ProtoMan;
            var factory = server.EntMan.ComponentFactory;

            T Comp<T>(string id) where T : IComponent, new()
            {
                Assert.That(protos.Index<EntityPrototype>(id).TryComp<T>(out var comp, factory), Is.True,
                    $"{id} has no {typeof(T).Name}");
                return comp!;
            }

            Assert.Multiple(() =>
            {
                // cloak: 25 s on / 30 s cooldown / 10 s forced cooldown / +30% ambush / 4 s lockout
                var bracer = Comp<YautjaBracerComponent>(Bracer);
                Assert.That(bracer.CloakDuration, Is.EqualTo(TimeSpan.FromSeconds(25)));
                Assert.That(bracer.CloakCooldown, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(bracer.CloakForcedDecloakCooldown, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(bracer.CloakBadBloodAmbushBonus, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(bracer.CloakBadBloodCombatLockout, Is.EqualTo(TimeSpan.FromSeconds(4)));
                Assert.That(bracer.BulletDecloakAbsorbs, Is.False, "a bullet that drops the cloak still lands");

                // caster: stun 60 at 0.8/s, immobilizer 300 every 15 s, lethal 150, eradicator 1500
                var modes = Comp<YautjaCasterComponent>(Caster).Modes;
                Assert.That(modes, Has.Count.EqualTo(4));
                Assert.That(modes[0].PowerCost, Is.EqualTo(FixedPoint2.New(60)));
                Assert.That(modes[0].FireRate, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(modes[1].PowerCost, Is.EqualTo(FixedPoint2.New(300)));
                Assert.That(1f / modes[1].FireRate, Is.EqualTo(15f).Within(0.01f));
                Assert.That(modes[BadBloodEradicatorSystem.LethalMode].PowerCost, Is.EqualTo(FixedPoint2.New(150)));
                Assert.That(modes[BadBloodEradicatorSystem.EradicatorMode].PowerCost, Is.EqualTo(FixedPoint2.New(1500)));
                Assert.That(new BadBloodEradicatorComponent().Cooldown, Is.EqualTo(TimeSpan.FromMinutes(10)));

                // its bolts: stun 3 s with no human bonus, immobilizer radius 4 for 3 s, lethal 75 raw out to 16 tiles
                var stun = Comp<YautjaCasterStunProjectileComponent>(modes[0].Projectile);
                Assert.That(stun.StunTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(stun.HumanBonusStunTime, Is.EqualTo(TimeSpan.Zero));
                var immobilizer = Comp<YautjaCasterImmobilizerProjectileComponent>(modes[1].Projectile);
                Assert.That(immobilizer.StunRange, Is.EqualTo(4f));
                Assert.That(immobilizer.StunTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                var lethal = modes[BadBloodEradicatorSystem.LethalMode].Projectile;
                Assert.That(Comp<ProjectileMaxRangeComponent>(lethal).Max, Is.EqualTo(16f));
                Assert.That(protos.HasIndex<EntityPrototype>("CMUYautjaBadBloodCasterLethalBolt"), Is.False,
                    "the bolt nothing fired is gone");

                // the hunters' caster and bolts are not touched
                Assert.That(Comp<YautjaCasterComponent>("CMUYautjaPlasmaCaster").Modes.TrueForAll(m => m.PowerCost == null),
                    Is.True);
                Assert.That(Comp<YautjaCasterStunProjectileComponent>("CMUYautjaCasterStunBolt").HumanBonusStunTime,
                    Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(Comp<YautjaCasterImmobilizerProjectileComponent>("CMUYautjaCasterImmobilizerBolt").StunRange,
                    Is.EqualTo(7f));
                Assert.That(Comp<ProjectileMaxRangeComponent>("CMUYautjaCasterLethalBolt").Max, Is.EqualTo(22f));
                Assert.That(Comp<YautjaBracerComponent>("CMUYautjaBracer").BulletDecloakAbsorbs, Is.True);

                // Superiority Points, tiers, vote, end and population
                var rule = Comp<BadBloodCampRuleComponent>(Rule);
                Assert.That(rule.OfferingSp, Is.EqualTo(FixedPoint4.New(1)));
                Assert.That(rule.MaxOfferingsPerBody, Is.EqualTo(2));
                Assert.That(rule.CorpseCookSp, Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rule.MeatCookSp, Is.EqualTo(FixedPoint4.New(0.5)));
                Assert.That(rule.CookSpCapPerBody, Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rule.SquadOfTwoCost, Is.EqualTo(FixedPoint4.New(12)));
                Assert.That(rule.SquadOfThreeCost, Is.EqualTo(FixedPoint4.New(16)));
                Assert.That(rule.HoundCost, Is.EqualTo(FixedPoint4.New(6)));
                Assert.That(rule.TotemCost, Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rule.TierTwoLifetimeSp, Is.EqualTo(FixedPoint4.New(12)));
                Assert.That(rule.TierThreeLifetimeSp, Is.EqualTo(FixedPoint4.New(30)));
                Assert.That(rule.TotemTierCredit, Is.EqualTo(0.5f));
                Assert.That(rule.VoteDuration, Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(rule.VotePerCandidate, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(rule.VoteMaxDuration, Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(rule.LeaderSsdTimeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(rule.ClanWipeDelay, Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(rule.FoundingGrace, Is.EqualTo(TimeSpan.FromMinutes(15)));
                Assert.That(rule.CampRebuildCooldown, Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(rule.RipeTotemsForCall, Is.EqualTo(3));
                Assert.That(rule.RipeHoldForCall, Is.EqualTo(TimeSpan.FromMinutes(10)));
                Assert.That(rule.ShipEarliestCall, Is.EqualTo(TimeSpan.FromMinutes(45)));
                Assert.That(rule.ShipInboundTime, Is.EqualTo(TimeSpan.FromMinutes(20)));
                Assert.That(rule.SummonInterval, Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(rule.DepartureTime, Is.EqualTo(TimeSpan.FromMinutes(20)));
                Assert.That(rule.ReserveChoiceTime, Is.EqualTo(TimeSpan.FromSeconds(20)));
                Assert.That(rule.ThreatBudgetFraction, Is.EqualTo(0.25f));
                Assert.That(rule.LivingCapBonus, Is.EqualTo(2));
                Assert.That(rule.HoundsPerMaster, Is.EqualTo(3));
                Assert.That(rule.TotemLimit, Is.EqualTo(3));
                Assert.That(rule.TotemLimitHighPop, Is.EqualTo(4));
                Assert.That(rule.TotemHighPopPlayers, Is.EqualTo(80));

                var threat = protos.Index<ThreatPrototype>("BadBloodClanThreat");
                Assert.That(threat.MinPlayers, Is.EqualTo(30));
                Assert.That(threat.ReserveLeftoverVoters, Is.True);
                var landing = protos.Index<PartySpawnPrototype>("badBloodClanParty");
                Assert.That(landing.LeadersToSpawn["CMUMobYautjaBadBloodLeader"], Is.EqualTo(1));
                Assert.That(landing.GruntsToSpawn["CMUMobYautjaBadBloodGrunt"], Is.EqualTo(3));

                // Clan Totem: 0.2 / 0.4 / 0.7 a minute, ripening at 10 and 20 min, radius 7
                var totem = Comp<BadBloodTotemComponent>(Totem);
                Assert.That(totem.YoungIncome, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(totem.GrowingIncome, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(totem.RipeIncome, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(totem.GrowingAfter, Is.EqualTo(TimeSpan.FromMinutes(10)));
                Assert.That(totem.RipeAfter, Is.EqualTo(TimeSpan.FromMinutes(20)));
                Assert.That(totem.AuraRadius, Is.EqualTo(7f));
                Assert.That(totem.ContestRadius, Is.EqualTo(7f));

                // Lodge: 1 HP/s after 60 s without damage
                var lodge = Comp<BadBloodCampComponent>(Lodge);
                Assert.That(lodge.Regen, Is.EqualTo(1f));
                Assert.That(lodge.RegenDelay, Is.EqualTo(TimeSpan.FromSeconds(60)));

                // hound: xeno armor 30, a bite that pierces 20
                Assert.That(Comp<CMArmorComponent>(Hound).XenoArmor, Is.EqualTo(30));
                Assert.That(Comp<CMArmorPiercingComponent>(Hound).Amount, Is.EqualTo(20));

                // survival: hunger 8 and thirst 6 a minute, starting at 300 to 380
                var hunger = protos.Index<SatiationPrototype>("CMUYautjaBadBloodHunger");
                Assert.That(hunger.BaseChangeRate, Is.EqualTo(-0.2f).Within(0.0001f), "-0.2 / 1.5 a second is 8 a minute");
                Assert.That(hunger.StartingValueMinimum, Is.EqualTo(300));
                Assert.That(hunger.StartingValueMaximum, Is.EqualTo(380));
                var thirst = protos.Index<SatiationPrototype>("CMUYautjaBadBloodThirst");
                Assert.That(thirst.BaseChangeRate, Is.EqualTo(-0.1f).Within(0.0001f), "-0.1 a second is 6 a minute");
                Assert.That(thirst.StartingValueMinimum, Is.EqualTo(300));

                // body: Bad Bloods take a little more than honourable hunters, who are untouched
                foreach (var mob in new[] { "CMUMobYautjaBadBloodGrunt", "CMUMobYautjaBadBloodLeader" })
                {
                    Assert.That(Comp<DamageableComponent>(mob).DamageModifierSetId?.Id, Is.EqualTo("CMUYautjaBadBlood"), mob);
                }

                Assert.That(Comp<DamageableComponent>("CMUMobYautja").DamageModifierSetId?.Id, Is.EqualTo("CMUYautja"));
                var badBloodSet = protos.Index<DamageModifierSetPrototype>("CMUYautjaBadBlood").Coefficients;
                Assert.That(badBloodSet["Blunt"], Is.EqualTo(0.30f));
                Assert.That(badBloodSet["Slash"], Is.EqualTo(0.30f));
                Assert.That(badBloodSet["Piercing"], Is.EqualTo(0.30f));
                Assert.That(badBloodSet["Heat"], Is.EqualTo(0.75f));
                Assert.That(badBloodSet["Poison"], Is.EqualTo(0.5f));
                Assert.That(badBloodSet.ContainsKey("Caustic"), Is.False, "caustic lands in full");
                var hunterSet = protos.Index<DamageModifierSetPrototype>("CMUYautja").Coefficients;
                Assert.That(hunterSet["Slash"], Is.EqualTo(0.28f));
                Assert.That(hunterSet["Heat"], Is.EqualTo(0.65f));

                // blades against marines: Bad Blood wrist blades penetration 50 / strip 0.006, honourable 40 / 0.01
                var badBloodBlades = Comp<YautjaHumanMeleeComponent>("CMUYautjaBadBloodWristBlades");
                Assert.That(badBloodBlades.Penetration, Is.EqualTo(50));
                Assert.That(badBloodBlades.StripChancePerArmor, Is.EqualTo(0.006f));
                var hunterBlades = Comp<YautjaHumanMeleeComponent>("CMUYautjaWristBlades");
                Assert.That(hunterBlades.Penetration, Is.EqualTo(40));
                Assert.That(hunterBlades.StripChancePerArmor, Is.EqualTo(0.01f));

                // scuttle: 10 s fuse, max 12 / total 120 / slope 6
                var scuttle = new BadBloodScuttleComponent();
                Assert.That(scuttle.Fuse, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(scuttle.MaxIntensity, Is.EqualTo(12f));
                Assert.That(scuttle.TotalIntensity, Is.EqualTo(120f));
                Assert.That(scuttle.IntensitySlope, Is.EqualTo(6f));

                // armory: the Hivebreaker and the eradicator are tier III for 11 SP, the lethal bolt is tier II for 7
                var armory = Comp<BadBloodCampArmoryComponent>(Armory);
                Assert.That(armory.Tiers["CMUYautjaHivebreaker"], Is.EqualTo(3));
                Assert.That(armory.Tiers["CMUBadBloodEradicatorUnlock"], Is.EqualTo(3));
                Assert.That(armory.LeaderOnly, Does.Contain(new EntProtoId("CMUBadBloodEradicatorUnlock")));
                Assert.That(PriceOf(Comp<CMAutomatedVendorComponent>(Armory), "CMUYautjaHivebreaker"), Is.EqualTo(11));
                Assert.That(PriceOf(Comp<CMAutomatedVendorComponent>(Armory), "CMUBadBloodEradicatorUnlock"), Is.EqualTo(11));
                Assert.That(armory.Tiers["CMUBadBloodLethalUnlock"], Is.EqualTo(2));
                Assert.That(armory.LeaderOnly, Does.Contain(new EntProtoId("CMUBadBloodLethalUnlock")));
                Assert.That(PriceOf(Comp<CMAutomatedVendorComponent>(Armory), "CMUBadBloodLethalUnlock"), Is.EqualTo(7));
                Assert.That(armory.TierFourPrices["CMUYautjaWarGlaive"], Is.EqualTo(4));
                Assert.That(armory.TierFourPrices["CMUBadBloodLeaderArmor"], Is.EqualTo(5));

                // STATE-102: the old job-body gear and the unmapped vendor are retired
                Assert.That(protos.HasIndex<Content.Shared.Roles.StartingGearPrototype>("CMUYautjaBadBloodGear"), Is.False);
                Assert.That(protos.HasIndex<EntityPrototype>("CMUYautjaBadBloodLoadoutVendor"), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static int? PriceOf(CMAutomatedVendorComponent vendor, string id)
    {
        foreach (var section in vendor.Sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Id.Id == id)
                    return entry.Points;
            }
        }

        return null;
    }
}
