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
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// STATE-107: the non-combat numbers of the Bad Blood master value sheet (docs/GraphAudit-BadBlood-Balance.md §12),
// read from the prototypes. If one of these fails, a number was retuned: change the sheet and this test together.
// The body's damage set and the melee armor pin the signed-off fire/melee Option 2 (docs/GraphAudit-BadBlood-Objectives.md §4.3).
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

    // the clan's Leader wears the Ancient's council symbol, which YautjaHudSystem shows to Bad Bloods only
    [Test]
    public async Task TheLeaderIconIsTheAncientSymbol()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.That(protos.TryIndex<Content.Shared.StatusIcon.HealthIconPrototype>("CMUBadBloodLeaderIcon", out var leader), Is.True);
        var ancient = protos.Index<Content.Shared.StatusIcon.HealthIconPrototype>("CMUYautjaRankIconAncient");
        Assert.That(leader!.Icon.ToString(), Is.EqualTo(ancient.Icon.ToString()), "the Leader's icon is the Ancient's");

        await pair.CleanReturnAsync();
    }

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

                // caster: stun 60 at 0.7/s, immobilizer 300 every 17 s, lethal 150 every 12 s, eradicator 1500 every 14 s
                var modes = Comp<YautjaCasterComponent>(Caster).Modes;
                Assert.That(modes, Has.Count.EqualTo(4));
                Assert.That(modes[0].PowerCost, Is.EqualTo(FixedPoint2.New(60)));
                Assert.That(modes[0].FireRate, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(modes[1].PowerCost, Is.EqualTo(FixedPoint2.New(300)));
                Assert.That(1f / modes[1].FireRate, Is.EqualTo(17f).Within(0.01f));
                Assert.That(modes[BadBloodEradicatorSystem.LethalMode].PowerCost, Is.EqualTo(FixedPoint2.New(150)));
                Assert.That(modes[BadBloodEradicatorSystem.EradicatorMode].PowerCost, Is.EqualTo(FixedPoint2.New(1500)));
                Assert.That(1f / modes[BadBloodEradicatorSystem.LethalMode].FireRate, Is.EqualTo(12f).Within(0.01f));
                Assert.That(1f / modes[BadBloodEradicatorSystem.EradicatorMode].FireRate, Is.EqualTo(14f).Within(0.01f));
                Assert.That(new BadBloodEradicatorComponent().Cooldown, Is.EqualTo(TimeSpan.FromMinutes(10)));

                // its bolts: stun 3 s with no human bonus, immobilizer radius 4 for 3 s, lethal 68 Heat out to 16 tiles
                var stun = Comp<YautjaCasterStunProjectileComponent>(modes[0].Projectile);
                Assert.That(stun.StunTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(stun.HumanBonusStunTime, Is.EqualTo(TimeSpan.Zero));
                var immobilizer = Comp<YautjaCasterImmobilizerProjectileComponent>(modes[1].Projectile);
                Assert.That(immobilizer.StunRange, Is.EqualTo(4f));
                Assert.That(immobilizer.StunTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                var lethal = modes[BadBloodEradicatorSystem.LethalMode].Projectile;
                Assert.That(Comp<ProjectileMaxRangeComponent>(lethal).Max, Is.EqualTo(16f));
                Assert.That(Comp<ProjectileComponent>(lethal).Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(68)));
                var blast = Comp<Content.Shared.Explosion.Components.ExplosiveComponent>(lethal);
                Assert.That(blast.MaxIntensity, Is.EqualTo(45f));
                Assert.That(blast.TotalIntensity, Is.EqualTo(45f));
                var eradicator = modes[BadBloodEradicatorSystem.EradicatorMode].Projectile;
                Assert.That(Comp<ProjectileComponent>(eradicator).Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(50)));

                // range cap: every Bad Blood caster mode has its own bolt capped at 16 tiles, short of the hunters' bolt
                var hunterModes = Comp<YautjaCasterComponent>("CMUYautjaPlasmaCaster").Modes;
                for (var i = 0; i < modes.Count; i++)
                {
                    var bolt = modes[i].Projectile;
                    Assert.That(bolt.Id, Does.StartWith("CMUYautjaBadBlood"), $"mode {i} fires a Bad Blood bolt");
                    Assert.That(Comp<ProjectileMaxRangeComponent>(bolt).Max, Is.EqualTo(16f), $"mode {i} range cap");

                    var hunterBolt = hunterModes.Find(m => m.Name == modes[i].Name)?.Projectile;
                    if (hunterBolt is { } hunter)
                        Assert.That(Comp<ProjectileMaxRangeComponent>(hunter).Max, Is.GreaterThan(16f),
                            $"mode {i}: the hunters' bolt keeps its longer range");
                }
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
                Assert.That(Comp<ProjectileComponent>("CMUYautjaCasterLethalBolt").Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(75)));
                Assert.That(Comp<ProjectileComponent>("CMUYautjaCasterEradicatorBolt").Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(55)));
                Assert.That(Comp<YautjaBracerComponent>("CMUYautjaBracer").BulletDecloakAbsorbs, Is.True);

                // Superiority Points, tiers, vote, end and population
                var rule = Comp<BadBloodCampRuleComponent>(Rule);
                Assert.That(rule.OfferingHonour, Is.EqualTo(FixedPoint4.New(2)));
                Assert.That(rule.MaxOfferingsPerBody, Is.EqualTo(2));
                Assert.That(rule.CorpseCookSp, Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rule.MeatCookSp, Is.EqualTo(FixedPoint4.New(0.5)));
                Assert.That(rule.CookSpCapPerBody, Is.EqualTo(FixedPoint4.New(3)));
                Assert.That(rule.SquadOfOneCost, Is.EqualTo(FixedPoint4.New(7)));
                Assert.That(rule.SquadOfThreeCost, Is.EqualTo(FixedPoint4.New(16)));
                Assert.That(rule.SquadOfFiveCost, Is.EqualTo(FixedPoint4.New(24)));
                Assert.That(rule.LastStandCost, Is.EqualTo(FixedPoint4.New(12)));
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
                Assert.That(rule.ThreatBudgetFraction, Is.EqualTo(0.35f));
                Assert.That(rule.LivingCapBonus, Is.EqualTo(5));

                // the squad tiers fit the budget: Bad Bloods + landing hounds against round(players x fraction).
                // 30 players land 5 (+3 hounds) and leave room for a squad of 3; 40 land 6 (+3) and leave room for 5
                int Room(int players, int landing) =>
                    (int) MathF.Round(players * rule.ThreatBudgetFraction, MidpointRounding.AwayFromZero)
                    - landing - (landing + rule.LandingHoundDivisor - 1) / rule.LandingHoundDivisor;
                Assert.That(Room(30, 5), Is.GreaterThanOrEqualTo(3), "a squad of 3 fits at 30 players");
                Assert.That(Room(40, 6), Is.GreaterThanOrEqualTo(5), "a squad of 5 fits at 40 players");
                Assert.That(rule.LivingCapBonus, Is.GreaterThanOrEqualTo(5), "the living cap admits a squad of 5");
                Assert.That(rule.LandingHoundDivisor, Is.EqualTo(2));
                Assert.That(rule.HoundCapDivisor, Is.EqualTo(2));
                Assert.That(rule.HoundCapBonus, Is.EqualTo(1));
                Assert.That(rule.LeaderlessVoteDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(rule.FoundingSquadBase, Is.EqualTo(1));
                Assert.That(rule.FoundingSquadDivisor, Is.EqualTo(2));
                Assert.That(rule.LastStandSeats, Is.EqualTo(2));
                Assert.That(rule.TotemLimit, Is.EqualTo(3));
                Assert.That(rule.TotemLimitHighPop, Is.EqualTo(4));
                Assert.That(rule.TotemHighPopPlayers, Is.EqualTo(80));

                var threat = protos.Index<ThreatPrototype>("BadBloodClanThreat");
                Assert.That(threat.MinPlayers, Is.EqualTo(30));
                Assert.That(threat.ReserveLeftoverVoters, Is.True);
                var landing = protos.Index<PartySpawnPrototype>("badBloodClanParty");
                Assert.That(landing.LeadersToSpawn["CMUMobYautjaBadBloodLeader"], Is.EqualTo(1));
                Assert.That(landing.GruntsToSpawn["CMUMobYautjaBadBloodGrunt"], Is.EqualTo(4));
                // one extra grunt per ~12-13 players above 30
                var gruntScaling = landing.Scaling["CMUMobYautjaBadBloodGrunt"];
                Assert.That(gruntScaling.Scale, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(gruntScaling.WhenToBeginScaling, Is.EqualTo(30));

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

                // body (fire/melee Option 2): Bad Bloods take more melee and less fire than honourable hunters, who
                // are untouched
                foreach (var mob in new[] { "CMUMobYautjaBadBloodGrunt", "CMUMobYautjaBadBloodLeader" })
                {
                    Assert.That(Comp<DamageableComponent>(mob).DamageModifierSetId?.Id, Is.EqualTo("CMUYautjaBadBlood"), mob);
                }

                Assert.That(Comp<DamageableComponent>("CMUMobYautja").DamageModifierSetId?.Id, Is.EqualTo("CMUYautja"));
                var badBloodSet = protos.Index<DamageModifierSetPrototype>("CMUYautjaBadBlood").Coefficients;
                Assert.That(badBloodSet["Blunt"], Is.EqualTo(0.40f));
                Assert.That(badBloodSet["Slash"], Is.EqualTo(0.45f));
                Assert.That(badBloodSet["Piercing"], Is.EqualTo(0.27f));
                Assert.That(badBloodSet["Heat"], Is.EqualTo(0.50f));
                Assert.That(badBloodSet["Poison"], Is.EqualTo(0.5f));
                Assert.That(badBloodSet.ContainsKey("Caustic"), Is.False, "caustic lands in full");
                var hunterSet = protos.Index<DamageModifierSetPrototype>("CMUYautja").Coefficients;
                Assert.That(hunterSet["Slash"], Is.EqualTo(0.28f));
                Assert.That(hunterSet["Heat"], Is.EqualTo(0.65f));

                // armor (Option 2): patchwork melee 20, Leader plate melee 30 on every Leader piece and the armory's
                Assert.That(Comp<CMArmorComponent>("CMUYautjaBadBloodArmorPatchwork").Melee, Is.EqualTo(20));
                foreach (var plate in new[]
                         {
                             "CMUBadBloodLeaderArmorCommandoAlt", "CMUBadBloodLeaderArmorBane",
                             "CMUBadBloodLeaderArmorLunatic", "CMUBadBloodLeaderArmorScavengerAlt",
                             "CMUBadBloodLeaderArmor",
                         })
                {
                    Assert.That(Comp<CMArmorComponent>(plate).Melee, Is.EqualTo(30), plate);
                    Assert.That(Comp<CMArmorComponent>(plate).Bullet, Is.EqualTo(55), plate);
                }

                // bullet armor (ARCH-03): patchwork 25, armored mask 30, full clan armor 50
                Assert.That(Comp<CMArmorComponent>("CMUYautjaBadBloodArmorPatchwork").Bullet, Is.EqualTo(25));
                Assert.That(Comp<CMArmorComponent>("CMUBadBloodArmoredMask").Bullet, Is.EqualTo(30));
                Assert.That(Comp<CMArmorComponent>("CMUBadBloodClanArmor").Bullet, Is.EqualTo(50));
                Assert.That(Comp<CMArmorComponent>("CMUYautjaBadBloodGreavesPatchwork").Bullet, Is.EqualTo(50));

                // melee (ARCH-02): the starter weapons hit like the honourable ones; the forge and the golden tier add
                foreach (var (badBlood, hunter, type, hit, hunterHit) in new[]
                         {
                             ("CMUYautjaBadBloodWristBlades", "CMUYautjaWristBlades", "Slash", 20, 20),
                             ("CMUYautjaBadBloodScimitar", "CMUYautjaScimitar", "Slash", 25, 25),
                             ("CMUYautjaBadBloodChainGauntlet", "CMUYautjaChainGauntlet", "Blunt", 20, 20),
                         })
                {
                    Assert.That(Comp<MeleeWeaponComponent>(badBlood).Damage.DamageDict[type], Is.EqualTo(FixedPoint2.New(hit)), badBlood);
                    Assert.That(Comp<MeleeWeaponComponent>(hunter).Damage.DamageDict[type], Is.EqualTo(FixedPoint2.New(hunterHit)), hunter);
                }

                // blades against marines: Bad Blood wrist blades penetration 50 / strip 0.006, honourable 40 / 0.01
                var badBloodBlades = Comp<YautjaHumanMeleeComponent>("CMUYautjaBadBloodWristBlades");
                Assert.That(badBloodBlades.Penetration, Is.EqualTo(50));
                Assert.That(badBloodBlades.StripChancePerArmor, Is.EqualTo(0.006f));
                var hunterBlades = Comp<YautjaHumanMeleeComponent>("CMUYautjaWristBlades");
                Assert.That(hunterBlades.Penetration, Is.EqualTo(40));
                Assert.That(hunterBlades.StripChancePerArmor, Is.EqualTo(0.01f));

                // weapon tiers (ARCH-02): the forge adds a fifth, the blessed weapons are the golden tier
                Assert.That(Comp<BadBloodForgeUpgradeComponent>("CMUBadBloodForgeUpgrade").Multiplier, Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(Comp<MeleeWeaponComponent>("CMUBadBloodBlessedWarGlaive").Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(Comp<MeleeWeaponComponent>("CMUYautjaWarGlaive").Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(15)));
                foreach (var (blessed, hunter, type) in new[]
                         {
                             ("CMUBadBloodBlessedChainwhip", "CMUYautjaChainwhip", "Slash"),
                             ("CMUBadBloodBlessedSeveringSword", "CMUYautjaSeveringSword", "Slash"),
                             ("CMUBadBloodBlessedCruelStaff", "CMUYautjaCruelStaff", "Slash"),
                             ("CMUBadBloodBlessedDualWarScythe", "CMUYautjaDualWarScythe", "Slash"),
                             ("CMUBadBloodBlessedWarAxe", "CMUYautjaWarAxe", "Slash"),
                             ("CMUBadBloodBlessedHunterSpear", "CMUYautjaHunterSpear", "Piercing"),
                         })
                {
                    var ratio = Comp<MeleeWeaponComponent>(blessed).Damage.DamageDict[type].Float() /
                                Comp<MeleeWeaponComponent>(hunter).Damage.DamageDict[type].Float();
                    Assert.That(ratio, Is.InRange(1.2f, 1.35f), $"{blessed} hits about a quarter harder than {hunter}");
                }

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
