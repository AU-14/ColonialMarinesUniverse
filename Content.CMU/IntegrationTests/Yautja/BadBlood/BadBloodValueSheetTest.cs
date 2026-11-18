#pragma warning disable RA0002 // tests read component data directly
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
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
    private const string Kennel = "CMUBadBloodCampKennel";

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

                // caster (COUPLING-602): stun 80 at 0.5/s, immobilizer 300 every 17 s, lethal 150 every 12 s,
                // eradicator 1500 every 14 s
                var modes = Comp<YautjaCasterComponent>(Caster).Modes;
                Assert.That(modes, Has.Count.EqualTo(4));
                Assert.That(modes[0].PowerCost, Is.EqualTo(FixedPoint2.New(80)));
                Assert.That(modes[0].FireRate, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(modes[1].PowerCost, Is.EqualTo(FixedPoint2.New(300)));
                Assert.That(1f / modes[1].FireRate, Is.EqualTo(17f).Within(0.01f));
                Assert.That(modes[BadBloodEradicatorSystem.LethalMode].PowerCost, Is.EqualTo(FixedPoint2.New(150)));
                Assert.That(modes[BadBloodEradicatorSystem.EradicatorMode].PowerCost, Is.EqualTo(FixedPoint2.New(1500)));
                Assert.That(1f / modes[BadBloodEradicatorSystem.LethalMode].FireRate, Is.EqualTo(12f).Within(0.01f));
                Assert.That(1f / modes[BadBloodEradicatorSystem.EradicatorMode].FireRate, Is.EqualTo(14f).Within(0.01f));
                Assert.That(new BadBloodEradicatorComponent().Cooldown, Is.EqualTo(TimeSpan.FromMinutes(10)));

                // its bolts: stun 3 s with no human bonus, immobilizer radius 4 for 3 s, lethal 68 Heat. both stuns leave a
                // 6 s window after the stun in which another only slows for 2 s (COUPLING-601)
                var stun = Comp<YautjaCasterStunProjectileComponent>(modes[0].Projectile);
                Assert.That(stun.StunTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(stun.HumanBonusStunTime, Is.EqualTo(TimeSpan.Zero));
                Assert.That(stun.ImmunityWindow, Is.EqualTo(TimeSpan.FromSeconds(6)));
                Assert.That(stun.ImmuneSlow, Is.EqualTo(TimeSpan.FromSeconds(2)));
                var immobilizer = Comp<YautjaCasterImmobilizerProjectileComponent>(modes[1].Projectile);
                Assert.That(immobilizer.StunRange, Is.EqualTo(4f));
                Assert.That(immobilizer.StunTime, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(immobilizer.ImmunityWindow, Is.EqualTo(TimeSpan.FromSeconds(6)));
                Assert.That(immobilizer.ImmuneSlow, Is.EqualTo(TimeSpan.FromSeconds(2)));
                var lethal = modes[BadBloodEradicatorSystem.LethalMode].Projectile;
                Assert.That(Comp<ProjectileComponent>(lethal).Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(68)));
                var blast = Comp<Content.Shared.Explosion.Components.ExplosiveComponent>(lethal);
                Assert.That(blast.MaxIntensity, Is.EqualTo(45f));
                Assert.That(blast.TotalIntensity, Is.EqualTo(45f));
                var eradicator = modes[BadBloodEradicatorSystem.EradicatorMode].Projectile;
                Assert.That(Comp<ProjectileComponent>(eradicator).Damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(50)));

                // range by mode, closer for control (COUPLING-602): stun 7, immobilizer 7, lethal 12, eradicator 12, each
                // short of the hunters' bolt for the same mode
                float[] ranges = [7f, 7f, 12f, 12f];
                var hunterModes = Comp<YautjaCasterComponent>("CMUYautjaPlasmaCaster").Modes;
                for (var i = 0; i < modes.Count; i++)
                {
                    var bolt = modes[i].Projectile;
                    Assert.That(bolt.Id, Does.StartWith("CMUYautjaBadBlood"), $"mode {i} fires a Bad Blood bolt");
                    Assert.That(Comp<ProjectileMaxRangeComponent>(bolt).Max, Is.EqualTo(ranges[i]), $"mode {i} range cap");

                    var hunterBolt = hunterModes.Find(m => m.Name == modes[i].Name)?.Projectile;
                    if (hunterBolt is { } hunter)
                        Assert.That(Comp<ProjectileMaxRangeComponent>(hunter).Max, Is.GreaterThan(ranges[i]),
                            $"mode {i}: the hunters' bolt keeps its longer range");
                }
                Assert.That(protos.HasIndex<EntityPrototype>("CMUYautjaBadBloodCasterLethalBolt"), Is.False,
                    "the bolt nothing fired is gone");

                // the hunters' caster and bolts are not touched
                Assert.That(Comp<YautjaCasterComponent>("CMUYautjaPlasmaCaster").Modes.TrueForAll(m => m.PowerCost == null),
                    Is.True);
                Assert.That(Comp<YautjaCasterStunProjectileComponent>("CMUYautjaCasterStunBolt").HumanBonusStunTime,
                    Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(Comp<YautjaCasterStunProjectileComponent>("CMUYautjaCasterStunBolt").ImmunityWindow,
                    Is.EqualTo(TimeSpan.Zero), "the hunters' stun bolt has no diminishing returns");
                Assert.That(Comp<YautjaCasterImmobilizerProjectileComponent>("CMUYautjaCasterImmobilizerBolt").StunRange,
                    Is.EqualTo(7f));
                Assert.That(Comp<YautjaCasterImmobilizerProjectileComponent>("CMUYautjaCasterImmobilizerBolt").ImmunityWindow,
                    Is.EqualTo(TimeSpan.Zero));
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

                // Blooded kill (STATE-603): +1 Honour when a worthy target dies within 5 s of a Bad Blood melee hit
                Assert.That(rule.BloodedKillWindow, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(rule.BloodedKillHonour, Is.EqualTo(FixedPoint4.New(1)));

                // the squad tiers fit the budget: Bad Bloods against round(players x fraction). ARCH-603: hounds are
                // free kennel filler outside the budget. 30 players land 5 and leave room for a squad of 3; 40 land 6
                // and leave room for 5
                int Room(int players, int landing) =>
                    (int) MathF.Round(players * rule.ThreatBudgetFraction, MidpointRounding.AwayFromZero) - landing;
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
                Assert.That(rule.TotemLimitHighPop, Is.EqualTo(3), "D6: orders + Totems never above 3");
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
                // FLOW-607: contested Totems
                Assert.That(totem.ContestAnnounceCooldown, Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(totem.LightEnergy, Is.EqualTo(1.5f));
                Assert.That(totem.ContestedLightEnergy, Is.EqualTo(0.4f));

                // Lodge: 1 HP/s after 60 s without damage
                var lodge = Comp<BadBloodCampComponent>(Lodge);
                Assert.That(lodge.Regen, Is.EqualTo(1f));
                Assert.That(lodge.RegenDelay, Is.EqualTo(TimeSpan.FromSeconds(60)));

                // hound: xeno armor 30, a bite that pierces 20
                Assert.That(Comp<CMArmorComponent>(Hound).XenoArmor, Is.EqualTo(30));
                Assert.That(Comp<CMArmorPiercingComponent>(Hound).Amount, Is.EqualTo(20));

                // ECS-606: the clan hound is grunt spam: crit 150 / dead 220, a 1 s leap knockdown, always gibbed. The
                // override replaces the whole thresholds map; the rest of the leap is the hellhound's
                var houndThresholds = Comp<MobThresholdsComponent>(Hound).Thresholds;
                Assert.That(houndThresholds, Has.Count.EqualTo(3));
                Assert.That(houndThresholds[FixedPoint2.New(150)], Is.EqualTo(MobState.Critical));
                Assert.That(houndThresholds[FixedPoint2.New(220)], Is.EqualTo(MobState.Dead));
                var houndLeap = Comp<XenoLeapComponent>(Hound);
                Assert.That(houndLeap.KnockdownTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(houndLeap.Delay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(houndLeap.CanBeShieldBlocked, Is.False);
                Assert.That(houndLeap.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(Comp<RMCGibOnDeathComponent>(Hound).GibChance, Is.EqualTo(1f));
                Assert.That(protos.Index<EntityPrototype>(Hound).TryComp<Content.Shared.Traits.Assorted.UnrevivableComponent>(out _, factory),
                    Is.True, "an unrevivable hound gibs when it dies from crit too");

                // the hunters' hellhound is untouched: 230 / 330, a 2 s leap knockdown, no gib
                var hellhoundThresholds = Comp<MobThresholdsComponent>("CMUMobYautjaHellhound").Thresholds;
                Assert.That(hellhoundThresholds[FixedPoint2.New(230)], Is.EqualTo(MobState.Critical));
                Assert.That(hellhoundThresholds[FixedPoint2.New(330)], Is.EqualTo(MobState.Dead));
                Assert.That(Comp<XenoLeapComponent>("CMUMobYautjaHellhound").KnockdownTime, Is.EqualTo(TimeSpan.FromSeconds(2)));
                Assert.That(protos.Index<EntityPrototype>("CMUMobYautjaHellhound").TryComp<RMCGibOnDeathComponent>(out _, factory),
                    Is.False);

                // ARCH-603, the kennel: 2 free hounds at pitch, one more every 2 minutes, one per 2 living Bad Bloods,
                // the reserve's first minute. FLOW-605: a minute between a player's hounds
                var kennel = Comp<BadBloodKennelSpawnerComponent>(Kennel);
                Assert.That(kennel.MinStock, Is.EqualTo(2));
                Assert.That(kennel.BadBloodsPerHound, Is.EqualTo(2));
                Assert.That(kennel.Interval, Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(kennel.ReservePriority, Is.EqualTo(TimeSpan.FromSeconds(60)));
                var kennelSpawner = Comp<GhostRoleMobSpawnerComponent>(Kennel);
                Assert.That(kennelSpawner.Prototype?.Id, Is.EqualTo(Hound));
                Assert.That(kennelSpawner.DeleteOnSpawn, Is.False);
                Assert.That(rule.HoundRespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(rule.LodgeRegionSize, Is.EqualTo(30), "FLOW-606: the marine map box around the Lodge");

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

                // health (ECS-608): Bad Bloods go critical at 280 and die at 380, the hunters stay at 240 / 340. the
                // override replaces the whole map, so no 240 / 340 entry is left over
                foreach (var mob in new[] { "CMUMobYautjaBadBloodGrunt", "CMUMobYautjaBadBloodLeader" })
                {
                    var thresholds = Comp<MobThresholdsComponent>(mob).Thresholds;
                    Assert.That(thresholds, Has.Count.EqualTo(3), mob);
                    Assert.That(thresholds[FixedPoint2.New(280)], Is.EqualTo(MobState.Critical), mob);
                    Assert.That(thresholds[FixedPoint2.New(380)], Is.EqualTo(MobState.Dead), mob);
                }

                var hunterThresholds = Comp<MobThresholdsComponent>("CMUMobYautja").Thresholds;
                Assert.That(hunterThresholds[FixedPoint2.New(240)], Is.EqualTo(MobState.Critical));
                Assert.That(hunterThresholds[FixedPoint2.New(340)], Is.EqualTo(MobState.Dead));

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

                // bullet armor (ARCH-03, ECS-608): patchwork 40 (melee stays 20), armored mask 30, full clan armor 50
                Assert.That(Comp<CMArmorComponent>("CMUYautjaBadBloodArmorPatchwork").Bullet, Is.EqualTo(40));
                Assert.That(Comp<CMArmorComponent>("CMUBadBloodArmoredMask").Bullet, Is.EqualTo(30));

                // the armored clan mask is the upgrade over the crimson mask a grunt lands in
                Assert.That(Comp<CMArmorComponent>("CMUYautjaMaskPred01Crimson").Melee, Is.EqualTo(20));
                Assert.That(Comp<CMArmorComponent>("CMUYautjaMaskPred20Crimson").Bio, Is.EqualTo(20));
                Assert.That(Comp<CMArmorComponent>("CMUBadBloodArmoredMask").Melee, Is.EqualTo(25));
                Assert.That(Comp<CMArmorComponent>("CMUBadBloodArmoredMask").Bio, Is.EqualTo(25));
                Assert.That(Comp<CMArmorComponent>("CMUYautjaMask").Melee, Is.EqualTo(25), "the honourable mask is untouched");

                // no Bad Blood mask is bare: the Bane grunt mask has grunt armor, the Leader's masks the best
                Assert.That(Comp<CMArmorComponent>("CMUYautjaMaskBadBloodBane").Melee, Is.EqualTo(20));
                foreach (var leaderMask in new[]
                         {
                             "CMUYautjaMaskBadBloodCommando", "CMUYautjaMaskBadBloodCommandoAlt",
                             "CMUYautjaMaskBadBloodLunatic", "CMUYautjaMaskBadBloodVenator",
                         })
                {
                    Assert.That(Comp<CMArmorComponent>(leaderMask).Melee, Is.EqualTo(30), leaderMask);
                    Assert.That(Comp<CMArmorComponent>(leaderMask).Bullet, Is.EqualTo(35), leaderMask);
                }
                Assert.That(Comp<CMArmorComponent>("CMUBadBloodClanArmor").Bullet, Is.EqualTo(50));
                Assert.That(Comp<CMArmorComponent>("CMUYautjaBadBloodGreavesPatchwork").Bullet, Is.EqualTo(50));

                // melee (ECS-607): the Bad Blood weapons hit 20-28% harder than the honourable ones and keep their attack
                // rate and sounds; the forge and the golden tier add on top. against marines all three pierce 50 / strip
                // 0.004, the honourable weapons 40 / 0.01. each staggers a marine (ECS-604) and pays 10 charge a hit
                // (ECS-609)
                foreach (var (badBlood, hunter, type, hit, hunterHit, slow, cooldown) in new[]
                         {
                             ("CMUYautjaBadBloodWristBlades", "CMUYautjaWristBlades", "Slash", 24, 20, 0.8, 2.0),
                             ("CMUYautjaBadBloodScimitar", "CMUYautjaScimitar", "Slash", 32, 25, 1.5, 2.5),
                             ("CMUYautjaBadBloodChainGauntlet", "CMUYautjaChainGauntlet", "Blunt", 24, 20, 1.2, 2.0),
                         })
                {
                    var badBloodMelee = Comp<MeleeWeaponComponent>(badBlood);
                    var hunterMelee = Comp<MeleeWeaponComponent>(hunter);
                    Assert.That(badBloodMelee.Damage.DamageDict[type], Is.EqualTo(FixedPoint2.New(hit)), badBlood);
                    Assert.That(badBloodMelee.Damage.DamageDict, Has.Count.EqualTo(1), badBlood);
                    Assert.That(hunterMelee.Damage.DamageDict[type], Is.EqualTo(FixedPoint2.New(hunterHit)), hunter);
                    Assert.That(badBloodMelee.AttackRate, Is.EqualTo(hunterMelee.AttackRate), $"{badBlood} keeps the attack rate");
                    Assert.That((badBloodMelee.HitSound as SoundCollectionSpecifier)?.Collection,
                        Is.EqualTo((hunterMelee.HitSound as SoundCollectionSpecifier)?.Collection).And.Not.Null,
                        $"{badBlood} keeps its hit sound");

                    var badBloodHuman = Comp<YautjaHumanMeleeComponent>(badBlood);
                    Assert.That(badBloodHuman.Penetration, Is.EqualTo(50), badBlood);
                    Assert.That(badBloodHuman.StripChancePerArmor, Is.EqualTo(0.004f), badBlood);
                    var hunterHuman = Comp<YautjaHumanMeleeComponent>(hunter);
                    Assert.That(hunterHuman.Penetration, Is.EqualTo(40), hunter);
                    Assert.That(hunterHuman.StripChancePerArmor, Is.EqualTo(0.01f), hunter);

                    var onHit = Comp<BadBloodMeleeComponent>(badBlood);
                    Assert.That(onHit.StaggerSlow, Is.EqualTo(TimeSpan.FromSeconds(slow)), badBlood);
                    Assert.That(onHit.StaggerCooldown, Is.EqualTo(TimeSpan.FromSeconds(cooldown)), badBlood);
                    Assert.That(onHit.ChargeRefund, Is.EqualTo(10f), badBlood);
                    Assert.That(protos.Index<EntityPrototype>(hunter).TryComp<BadBloodMeleeComponent>(out _, factory), Is.False,
                        $"{hunter} stays as it is");
                }

                Assert.That(Comp<StaminaDamageOnHitComponent>("CMUYautjaBadBloodChainGauntlet").Damage, Is.EqualTo(15f),
                    "the Bad Blood chain gauntlet keeps its stamina damage");

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

                // STATE-602: kennel breeding is tier I for 5 (halves the interval, twice), the pack tier II for 7
                // (two more hounds always ready, once)
                var vendor = Comp<CMAutomatedVendorComponent>(Armory);
                Assert.That(armory.Tiers["CMUBadBloodKennelRate"], Is.EqualTo(1));
                Assert.That(PriceOf(vendor, "CMUBadBloodKennelRate"), Is.EqualTo(5));
                var rate = Comp<BadBloodKennelUpgradeComponent>("CMUBadBloodKennelRate");
                Assert.That(rate.IntervalMultiplier, Is.EqualTo(0.5f));
                Assert.That(rate.MinStockBonus, Is.EqualTo(0));
                Assert.That(rate.MaxBuys, Is.EqualTo(2));
                Assert.That(armory.Tiers["CMUBadBloodKennelPack"], Is.EqualTo(2));
                Assert.That(PriceOf(vendor, "CMUBadBloodKennelPack"), Is.EqualTo(7));
                var pack = Comp<BadBloodKennelUpgradeComponent>("CMUBadBloodKennelPack");
                Assert.That(pack.IntervalMultiplier, Is.EqualTo(1f));
                Assert.That(pack.MinStockBonus, Is.EqualTo(2));
                Assert.That(pack.MaxBuys, Is.EqualTo(1));
                Assert.That(armory.TierFourPrices["CMUBadBloodLeaderArmor"], Is.EqualTo(5));

                // STATE-102: the old job-body gear and the unmapped vendor are retired
                Assert.That(protos.HasIndex<Content.Shared.Roles.StartingGearPrototype>("CMUYautjaBadBloodGear"), Is.False);
                Assert.That(protos.HasIndex<EntityPrototype>("CMUYautjaBadBloodLoadoutVendor"), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    // CMU14 ARCH-604 to ARCH-607: every caste is one per four living Bad Bloods and at least one, the perks hold the
    // sheet, and each perk component's code default is its YAML value
    [Test]
    public async Task CastesHoldTheValueSheet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ProtoMan;
            var factory = server.EntMan.ComponentFactory;
            ProtoId<BadBloodCastePrototype> engineer = "Engineer";
            ProtoId<BadBloodCastePrototype> medic = "Medic";
            ProtoId<BadBloodCastePrototype> berserker = "Berserker";
            ProtoId<BadBloodCastePrototype> tracker = "Tracker";
            ProtoId<BadBloodCastePrototype> beastMaster = "BeastMaster";

            T Perk<T>(ProtoId<BadBloodCastePrototype> caste) where T : IComponent, new()
            {
                var found = protos.Index(caste).AddComponents.TryGetValue(factory.GetComponentName<T>(), out var entry);
                Assert.That(found, Is.True, $"{caste.Id} adds no {typeof(T).Name}");
                return (T) entry.Component;
            }

            Assert.Multiple(() =>
            {
                foreach (var caste in new[] { engineer, medic, berserker, tracker, beastMaster })
                {
                    var proto = protos.Index(caste);
                    Assert.That(proto.CapDivisor, Is.EqualTo(4), $"{caste.Id}: one per four living Bad Bloods");
                    Assert.That(proto.MinCap, Is.EqualTo(1), $"{caste.Id}: at least one");
                }

                // Engineer: RMC engineering 2, builds anywhere, the honourable visor
                Assert.That(protos.Index(engineer).Skills["RMCSkillEngineer"], Is.EqualTo(2));
                Perk<BadBloodBuildAnywhereComponent>(engineer);
                Perk<BadBloodHunterVisorComponent>(engineer);

                // CMU14 ECS-701: Raise Bulwark (3 barricades, 2 bricks each), half-time reconsecration, 1.5 at a seize,
                // a 5 s repair of 100
                foreach (var e in new[] { Perk<BadBloodEngineerComponent>(engineer), new BadBloodEngineerComponent() })
                {
                    Assert.That(e.BulwarkWidth, Is.EqualTo(3));
                    Assert.That(e.BricksPerBarricade, Is.EqualTo(2));
                    Assert.That(e.ConsecrationTimeMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
                    Assert.That(e.ContestWeight, Is.EqualTo(1.5f).Within(0.0001f));
                    Assert.That(e.RepairTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
                    Assert.That(e.RepairAmount, Is.EqualTo(100f).Within(0.0001f));
                }

                // Medic: Yautja medicine in 60% of the time
                Assert.That(Perk<BadBloodMedicSpeedComponent>(medic).Multiplier, Is.EqualTo(0.6f).Within(0.0001f));
                Assert.That(new BadBloodMedicSpeedComponent().Multiplier, Is.EqualTo(0.6f).Within(0.0001f));

                // Berserker: +80 thresholds, 85% damage taken, 90% sprint, 1.5x stagger
                foreach (var b in new[] { Perk<BadBloodBerserkerComponent>(berserker), new BadBloodBerserkerComponent() })
                {
                    Assert.That(b.ThresholdBonus, Is.EqualTo(80));
                    Assert.That(b.DamageTakenMultiplier, Is.EqualTo(0.85f).Within(0.0001f));
                    Assert.That(b.SprintMultiplier, Is.EqualTo(0.9f).Within(0.0001f));
                    Assert.That(b.StaggerMultiplier, Is.EqualTo(1.5f).Within(0.0001f));

                    // CMU14 ECS-703: Blood Rage under half health, 10 s, +25% melee, counts 2 at a seize
                    Assert.That(b.RageHealthThreshold, Is.EqualTo(0.5f).Within(0.0001f));
                    Assert.That(b.RageDuration, Is.EqualTo(TimeSpan.FromSeconds(10)));
                    Assert.That(b.RageMeleeMultiplier, Is.EqualTo(1.25f).Within(0.0001f));
                    Assert.That(b.RageContestWeight, Is.EqualTo(2f).Within(0.0001f));
                }

                // CMU14 ECS-705: War Cry, 7 tiles, 8 s, x1.15 speed, half stagger
                var cry = new BadBloodWarCryComponent();
                Assert.That(cry.Radius, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(cry.Duration, Is.EqualTo(TimeSpan.FromSeconds(8)));
                Assert.That(cry.SpeedMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
                Assert.That(cry.StaggerMultiplier, Is.EqualTo(0.5f).Within(0.0001f));

                // Tracker: 85% armour, 75% cloak cooldown, marks show for 60 s
                foreach (var t in new[] { Perk<BadBloodTrackerComponent>(tracker), new BadBloodTrackerComponent() })
                {
                    Assert.That(t.ArmorMultiplier, Is.EqualTo(0.85f).Within(0.0001f));
                    Assert.That(t.CloakCooldownMultiplier, Is.EqualTo(0.75f).Within(0.0001f));
                    Assert.That(t.MarkDuration, Is.EqualTo(TimeSpan.FromSeconds(60)));

                    // CMU14 ECS-704: Blood Trail in 15 tiles; early warning 6 tiles out, once a minute per Totem
                    Assert.That(t.BloodTrailRange, Is.EqualTo(15f).Within(0.0001f));
                    Assert.That(t.WarningPadding, Is.EqualTo(6f).Within(0.0001f));
                    Assert.That(t.WarningCooldown, Is.EqualTo(TimeSpan.FromSeconds(60)));
                }

                // CMU14 ARCH-705: the Beast-Master replaces the Falconer and alone buys the falcon drone at the armory,
                // tier I for 4, one each
                Assert.That(protos.HasIndex<BadBloodCastePrototype>("Falconer"), Is.False, "the Falconer is retired");
                Perk<BadBloodBeastMasterComponent>(beastMaster);
                var armory = protos.Index<EntityPrototype>(Armory);
                Assert.That(armory.TryComp<BadBloodCampArmoryComponent>(out var armoryComp, factory), Is.True);
                Assert.That(armory.TryComp<CMAutomatedVendorComponent>(out var vendor, factory), Is.True);
                Assert.That(armoryComp!.Tiers["CMUYautjaFalconDroneBadBlood"], Is.EqualTo(1));
                Assert.That(armoryComp.CasteWares["CMUYautjaFalconDroneBadBlood"], Is.EqualTo(beastMaster));
                Assert.That(PriceOf(vendor!, "CMUYautjaFalconDroneBadBlood"), Is.EqualTo(4));
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
