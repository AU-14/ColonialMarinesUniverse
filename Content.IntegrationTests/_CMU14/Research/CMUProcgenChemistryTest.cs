#pragma warning disable RA0002 // The chain and damage components are driven directly to exercise the fire path.

using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Chemistry.Reagents;
using Content.Server.CMU14.Chemistry.Research;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests._CMU14.Research;

/// <summary>
/// Regression coverage for the procgen chemistry repair: tier/material ladder, rarity ceilings, recipe
/// collision guard, fire-stat emission, the simulator's separate currency, and "no generatable stubs".
/// </summary>
[TestFixture]
public sealed class CMUProcgenChemistryTest : GameTest
{
    // Generated prototypes survive entity cleanup and would change later tests' reactions.
    public override PoolSettings PoolSettings => new() { Connected = false, Destructive = true };

    private const int Samples = 40;

    private static GeneratedReagentData NewOffer(int tier, int index)
        => new()
        {
            ID = $"CMUProcgenTest-T{tier}-{index}",
            Name = $"procgen test {tier}-{index}",
            GenTier = tier,
            Class = ReagentClass.Ultra,
            Effects = [],
            Recipe = [],
        };

    /// <summary>
    /// Contracts must never ask for more than 3 parts (90u at 30u per contract) of any precious metal,
    /// whatever the first-slot multiplier, Hard bump, catalyst amount or simulator copy.
    /// </summary>
    [Test]
    public async Task PreciousMetalsNeverExceedThreeParts()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var seed = NewOffer(1, 0);
            gen.GenerateStats(ref seed); // forces Setup()
            var precious = gen.ChemicalGenClassesList[SharedReagentGeneratorSystem.PreciousMetalPool];
            Assert.That(precious, Does.Contain("RMCUranium"));
            const int max = ServerReagentGeneratorSystem.MaxPreciousMetalParts;

            // Direct requests above the cap are clamped, for reactants and catalysts alike.
            var direct = NewOffer(3, 970);
            gen.AddChemical(ref direct, "RMCUranium", 7, null);
            gen.AddChemical(ref direct, "RMCSilver", 5, null, catalyst: true);
            gen.AddChemical(ref direct, "Water", 6, null);
            Assert.Multiple(() =>
            {
                Assert.That(direct.Recipe["RMCUranium"].Item1, Is.EqualTo(max));
                Assert.That(direct.Recipe["RMCSilver"], Is.EqualTo((max, true)));
                Assert.That(direct.Recipe["Water"].Item1, Is.EqualTo(6), "non-precious amounts are untouched");
            });

            // Recipes assembled elsewhere (simulator copies) are clamped too.
            var copied = new Dictionary<string, (int, bool)> { ["RMCGold"] = (9, false), ["RMCCarbon"] = (4, false) };
            gen.ClampPreciousAmounts(copied);
            Assert.That(copied, Is.EqualTo(new Dictionary<string, (int, bool)> { ["RMCGold"] = (max, false), ["RMCCarbon"] = (4, false) }));

            // And no generated contract ever exceeds it. Hard is where precious metals and the +1 bump live.
            for (var i = 0; i < 200; i++)
            {
                var data = NewOffer(3, 2000 + i);
                gen.GenerateStats(ref data);
                if (!gen.GenerateRecipe(ref data, []))
                    continue;
                foreach (var (reagent, (amount, _)) in data.Recipe)
                {
                    if (precious.Contains(reagent))
                        Assert.That(amount, Is.LessThanOrEqualTo(max), $"Hard contract asked for {amount} parts of {reagent}");
                }
            }
        });
    }

    [Test]
    public async Task MaterialDifficultyRisesWithTier()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var seed = NewOffer(1, 0);
            gen.GenerateStats(ref seed); // forces Setup()
            var pools = gen.ChemicalGenClassesList;
            var precious = pools[SharedReagentGeneratorSystem.PreciousMetalPool];
            Assert.That(precious, Is.Not.Empty, "precious-metal pool (CMUPreciousMetals) resolved to nothing");
            var ordinaryRare = pools["C4"].Except(precious).ToHashSet();
            var hardWithPremium = 0;
            var hardGenerated = 0;

            for (var tier = 1; tier <= 3; tier++)
            {
                var failures = 0;
                for (var i = 0; i < Samples; i++)
                {
                    var data = NewOffer(tier, i);
                    gen.GenerateStats(ref data);
                    if (!gen.GenerateRecipe(ref data, []))
                    {
                        // Collisions with hand-authored recipes are rejected and retried; a few give up.
                        failures++;
                        continue;
                    }
                    foreach (var reagent in data.Recipe.Keys)
                    {
                        switch (tier)
                        {
                            case 1:
                                Assert.That(pools["C1"].Contains(reagent) || pools["C2"].Contains(reagent), Is.True,
                                    $"Easy recipe used {reagent}, which is not Basic/Common");
                                break;
                            case 2:
                                Assert.That(pools["C4"].Contains(reagent) || pools["H1"].Contains(reagent), Is.False,
                                    $"Intermediate recipe used {reagent}, which is Rare/Hydro");
                                break;
                            default:
                                Assert.That(pools["C1"].Contains(reagent), Is.False, $"Hard recipe used element {reagent}");
                                Assert.That(ordinaryRare.Contains(reagent), Is.False,
                                    $"Hard recipe used {reagent}: Rare-class but not a precious metal");
                                break;
                        }
                    }
                    if (tier == 3)
                    {
                        Assert.That(data.Recipe.Count, Is.GreaterThanOrEqualTo(4), "Hard recipes ask for at least four reactants");
                        hardGenerated++;
                        if (data.Recipe.Keys.Any(r => precious.Contains(r) || pools["H1"].Contains(r)))
                            hardWithPremium++;
                    }
                }
                Assert.That(failures, Is.LessThanOrEqualTo(Samples / 10), $"tier {tier}: too many offers could not find a non-colliding recipe");
            }
            // Each Hard slot is 50% precious-or-botanical, and a Hard recipe has 4-5 slots.
            Assert.That(hardWithPremium, Is.GreaterThanOrEqualTo(hardGenerated * 3 / 4),
                "Hard recipes should almost always need a precious metal or a botanical");
        });
    }

    [Test]
    public async Task PropertyRarityAndLevelCeilingsFollowTier()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var props = SProtoMan.GetInstances<ReagentPropertyPrototype>();
            var hardWithRare = 0;
            for (var i = 0; i < Samples; i++)
            {
                var easy = NewOffer(1, i);
                gen.GenerateStats(ref easy);
                foreach (var (id, level) in easy.Effects)
                {
                    Assert.That(level, Is.LessThanOrEqualTo(3), $"Easy contract rolled {id} at level {level}");
                    Assert.That(props[id].Rarity, Is.Not.EqualTo(ReagentPropertyRarityEnum.Uncommon),
                        $"Easy contract rolled uncommon property {id}");
                    Assert.That(props[id].Rarity, Is.LessThan(ReagentPropertyRarityEnum.Rare),
                        $"Easy contract carries rare-or-better property {id}");
                }


                var hard = NewOffer(3, i);
                gen.GenerateStats(ref hard);
                if (hard.Effects.Keys.Any(id => props[id].Rarity >= ReagentPropertyRarityEnum.Rare))
                    hardWithRare++;
            }
            // Explosive is ~2% of a positive pick when allowed; 500 Intermediate offers make a regression near-certain to show.
            for (var i = 0; i < 500; i++)
            {
                var intermediate = NewOffer(2, 1000 + i);
                gen.GenerateStats(ref intermediate);
                Assert.That(intermediate.Effects.ContainsKey("Explosive"), Is.False, "Explosive must be Hard-only");
            }

            // Slot one of a Hard contract is always a Rare pick; conflicts can occasionally cancel it.
            Assert.That(hardWithRare, Is.GreaterThanOrEqualTo(Samples * 3 / 4), "Hard contracts should almost always carry a rare property");
        });
    }

    [Test]
    public async Task GeneratedRecipesDoNotShadowHandAuthoredReactions()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var seed = NewOffer(1, 0);
            gen.GenerateStats(ref seed); // forces Setup(): class pools and property buckets
            var existing = SProtoMan.EnumeratePrototypes<ReactionPrototype>()
                .First(r => r.Reactants.Count >= 3 && !r.ID.StartsWith("TAU-") && !r.ID.StartsWith("CMUProcgenTest"));
            var reactants = existing.Reactants.Keys.Select(k => k.Id).ToList();

            // A generated recipe that is a strict subset of an existing one would fire whenever the existing
            // recipe's ingredients are mixed; it must be detected.
            var subset = NewOffer(1, 900);
            subset.Recipe[reactants[0]] = (1, false);
            subset.Recipe[reactants[1]] = (1, false);
            Assert.That(gen.IsDuplicate(ref subset, out _), Is.True, "subset of an existing recipe was not detected");

            // A superset is detected too.
            var superset = NewOffer(1, 901);
            foreach (var r in reactants)
                superset.Recipe[r] = (1, false);
            superset.Recipe["CMUTearGas"] = (1, false);
            Assert.That(gen.IsDuplicate(ref superset, out _), Is.True, "superset of an existing recipe was not detected");

            // Whatever is loaded sits below hand-authored reactions, even a forced collision.
            var data = NewOffer(2, 902);
            data.Effects["Antitoxic"] = 1;
            data.Recipe["RMCGold"] = (1, false);
            data.Recipe["RMCPlatinum"] = (1, false);
            data.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));
            var reaction = SProtoMan.Index<ReactionPrototype>(data.ID);
            Assert.That(reaction.Priority, Is.EqualTo(SharedReagentGeneratorSystem.GeneratedRecipePriority));

            // The real contract path: a legalised offer pinned to a reactant of a hand-authored recipe must not
            // end up as a strict subset of any hand-authored recipe's reactant set.
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var offer = NewOffer(2, 904);
            offer.Effects["Antitoxic"] = 1;
            offer.RecipeHint = reactants[0];
            Assert.That(research.LegalizeChem(offer), Is.True, "offer could not be legalised");
            var loaded = SProtoMan.Index<ReactionPrototype>(offer.ID);
            Assert.That(loaded.Priority, Is.EqualTo(SharedReagentGeneratorSystem.GeneratedRecipePriority));
            var loadedSet = loaded.Reactants.Keys.Select(k => k.Id).ToHashSet();
            foreach (var other in SProtoMan.EnumeratePrototypes<ReactionPrototype>())
            {
                if (gen.GeneratedRecipes.Contains(other.ID) || other.Reactants.Count <= loadedSet.Count)
                    continue;
                var otherSet = other.Reactants.Keys.Select(k => k.Id).ToHashSet();
                Assert.That(loadedSet.IsProperSubsetOf(otherSet), Is.False,
                    $"legalised recipe {offer.ID} is a strict subset of hand-authored reaction {other.ID}");
            }
        });
    }

    [Test]
    public async Task FuelPropertiesMakeGeneratedReagentsBurn()
    {
        await Server.WaitAssertion(() =>
        {
            var data = NewOffer(2, 903);
            data.Effects["Fueling"] = 2;
            data.Effects["Oxidizing"] = 2;
            data.Recipe["RMCGold"] = (1, false);
            data.Recipe["RMCSilver"] = (1, false);
            data.Recipe["RMCUranium"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));

            var reagent = SProtoMan.Index<ReagentPrototype>(data.ID);
            // base 15/20/4, Fueling -3/+7 per level, Oxidizing +7/-4 per level
            Assert.Multiple(() =>
            {
                Assert.That(reagent.Intensity, Is.EqualTo(23));
                Assert.That(reagent.Duration, Is.EqualTo(26));
                Assert.That(reagent.Radius, Is.EqualTo(4));
            });

            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var pure = new Solution();
            pure.AddReagent(data.ID, FixedPoint2.New(40));
            Assert.That(fuel.TryCompute(pure, out var pureProfile), Is.True, "generated fuel alone is not a valid flamer fuel");
            // mods are a fractional multiplier: (-0.1*2 + 0.2*2) = +20%
            Assert.That(pureProfile.Intensity, Is.EqualTo(28), "per-unit intensity modifiers were not applied");

            var blend = new Solution();
            blend.AddReagent(data.ID, FixedPoint2.New(10));
            blend.AddReagent("RMCNapalmUT", FixedPoint2.New(30));
            Assert.That(fuel.TryCompute(blend, out var blendProfile), Is.True, "napalm blend is not a valid flamer fuel");
            Assert.Multiple(() =>
            {
                Assert.That(blendProfile.Intensity, Is.InRange(28, 32));
                Assert.That(blendProfile.Duration, Is.InRange(22, 25));
                Assert.That(blendProfile.Radius, Is.EqualTo(5));
                Assert.That(blendProfile.PrimaryReagent.Id, Is.EqualTo("RMCNapalmUT"));
            });

            var diluted = new Solution();
            diluted.AddReagent("RMCNapalmUT", FixedPoint2.New(20));
            diluted.AddReagent("Water", FixedPoint2.New(20));
            Assert.That(fuel.TryCompute(diluted, out var dilutedProfile), Is.True);
            Assert.That(dilutedProfile.Intensity, Is.LessThan(30), "water should dilute the blend");

            var inert = new Solution();
            inert.AddReagent("Water", FixedPoint2.New(40));
            Assert.That(fuel.TryCompute(inert, out _), Is.False, "water is not fuel");

            // A Fueling-only chemical is weak but must still be a usable fuel at tank volumes.
            var fuelingOnly = NewOffer(2, 905);
            fuelingOnly.Effects["Fueling"] = 2;
            fuelingOnly.Recipe["RMCSilver"] = (1, false);
            fuelingOnly.Recipe["RMCPlatinum"] = (1, false);
            fuelingOnly.Recipe["RMCUranium"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(fuelingOnly));
            var tank = new Solution();
            tank.AddReagent(fuelingOnly.ID, FixedPoint2.New(300));
            Assert.That(fuel.TryCompute(tank, out var fuelingProfile), Is.True, "a Fueling-only chemical should burn in a full tank");
            Assert.That(fuelingProfile.Duration, Is.GreaterThan(fuelingProfile.Intensity), "Fueling trades heat for burn time");

            // A non-fuel Flowing additive widens a napalm blend without extinguishing it.
            var flowing = NewOffer(2, 906);
            flowing.Effects["Flowing"] = 2;
            flowing.Recipe["RMCGold"] = (1, false);
            flowing.Recipe["RMCUranium"] = (1, false);
            flowing.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(flowing));
            var flowingReagent = SProtoMan.Index<ReagentPrototype>(flowing.ID);
            Assert.That(flowingReagent.Intensity, Is.Zero, "Flowing alone is not a fuel");
            var widened = new Solution();
            widened.AddReagent("RMCNapalmUT", FixedPoint2.New(200));
            widened.AddReagent(flowing.ID, FixedPoint2.New(100));
            Assert.That(fuel.TryCompute(widened, out var widenedProfile), Is.True, "napalm plus a Flowing additive must still burn");
            Assert.That(widenedProfile.Intensity, Is.InRange(15, 22));
            Assert.That(widenedProfile.PrimaryReagent.Id, Is.EqualTo("RMCNapalmUT"));
        });
    }

    [Test]
    public async Task SampleDataIsIndependentOfResearchCredits()
    {
        await Server.WaitAssertion(() =>
        {
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            const string faction = "opfor";
            var credits = research.GetCredits(faction);
            var clearance = research.GetClearance(faction);
            var sample = research.GetSampleData(faction);
            try
            {
                research.UpdateClearance(5, -1, faction, 9);
                Assert.Multiple(() =>
                {
                    Assert.That(research.GetCredits(faction), Is.EqualTo(5));
                    Assert.That(research.GetSampleData(faction), Is.EqualTo(9));
                });
                research.UpdateSampleData(3, faction);
                Assert.Multiple(() =>
                {
                    Assert.That(research.GetCredits(faction), Is.EqualTo(5), "spending sample data must not touch credits");
                    Assert.That(research.GetSampleData(faction), Is.EqualTo(3));
                });
                research.UpdateClearance(8, -1, faction);
                Assert.That(research.GetSampleData(faction), Is.EqualTo(3), "a credit-only update must leave sample data alone");
            }
            finally
            {
                research.UpdateClearance(credits, clearance, faction, sample);
            }
        });
    }

    [Test]
    public async Task HardContractRetryKeepsPremiumReactant()
    {
        await Server.WaitAssertion(() =>
        {
            var gen = SEntMan.System<ServerReagentGeneratorSystem>();
            var research = SEntMan.System<ServerResearchDataTerminalSystem>();
            var seed = NewOffer(1, 0);
            gen.GenerateStats(ref seed);
            var pools = gen.ChemicalGenClassesList;
            var premium = pools["H1"].Concat(pools[SharedReagentGeneratorSystem.PreciousMetalPool]).ToHashSet();

            // Pin the hint to a reagent that is the sole reactant of a hand-authored reaction, so every recipe
            // containing it collides and LegalizeChem must take its Hard retry path.
            var blocker = SProtoMan.EnumeratePrototypes<ReactionPrototype>()
                .Where(r => r.Reactants.Count == 1 && !gen.GeneratedRecipes.Contains(r.ID))
                .Select(r => r.Reactants.Keys.First().Id)
                .FirstOrDefault(id => !premium.Contains(id));
            Assume.That(blocker, Is.Not.Null, "no single-reactant hand-authored reaction to collide with");

            var offer = NewOffer(3, 950);
            offer.Effects["Antitoxic"] = 1;
            offer.RecipeHint = blocker!;
            Assert.That(research.LegalizeChem(offer), Is.True, "Hard offer should recover from a colliding hint");
            var data = gen.ProceduralReagentData[offer.ID];
            Assert.That(data.Recipe.Keys.Any(premium.Contains), Is.True, "Hard retry lost its precious/botanical reactant");
            Assert.That(data.Recipe.ContainsKey(data.RecipeHint), Is.True, "RecipeHint must name a reactant in the recipe");
        });
    }

    /// <summary>
    /// The screenshot scenario: three LC9 tanks holding UT, UT + Napalm X and UT + an Oxidizing research
    /// chemical must report (and fire) different values. Uses the same helper as the tank examine verb.
    /// </summary>
    [Test]
    public async Task BlendedTanksReportDifferentEffectiveFuel()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var flamer = SEntMan.System<SharedRMCFlamerSystem>();
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();

            (CMUFuelProfile Raw, CMUFuelProfile Effective) Fill(params (string Reagent, int Amount)[] contents)
            {
                var tank = SEntMan.SpawnEntity("RMCTankFlamer", map.GridCoords);
                var comp = SComp<RMCFlamerTankComponent>(tank);
                Assert.That(solutions.TryGetSolution(tank, comp.SolutionId, out var soln, out _), Is.True);
                solutions.RemoveAllSolution(soln!.Value);
                foreach (var (reagent, amount) in contents)
                    solutions.TryAddReagent(soln.Value, reagent, FixedPoint2.New(amount));
                Assert.That(flamer.TryGetEffectiveFuel((tank, comp), out var raw, out var effective), Is.True);
                return (raw, effective);
            }

            var ut = Fill(("RMCNapalmUT", 100));
            Assert.That((ut.Effective.Intensity, ut.Effective.Duration, ut.Effective.Radius), Is.EqualTo((30, 20, 5)));

            // Napalm X 40/40/6 averaged 1:1 with UT 30/20/5 → 35/30/6, range capped to the LC9's 5.
            var x = Fill(("RMCNapalmUT", 30), ("RMCNapalmX", 30));
            Assert.Multiple(() =>
            {
                Assert.That((x.Raw.Intensity, x.Raw.Duration), Is.EqualTo((35, 30)), "stock fuels must average");
                Assert.That(x.Effective.Radius, Is.EqualTo(5), "range is limited by the tank");
                Assert.That(x.Effective, Is.Not.EqualTo(ut.Effective), "a Napalm X blend must differ from plain UT");
            });

            var oxidizer = NewOffer(2, 960);
            oxidizer.Effects["Oxidizing"] = 3;
            oxidizer.Recipe["RMCGold"] = (1, false);
            oxidizer.Recipe["RMCSilver"] = (1, false);
            oxidizer.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(oxidizer));
            var ox = Fill(("RMCNapalmUT", 30), (oxidizer.ID, 30));
            Assert.Multiple(() =>
            {
                Assert.That(ox.Raw.Intensity, Is.GreaterThan(ut.Raw.Intensity), "Oxidizing must make the blend hotter");
                Assert.That(ox.Raw.Duration, Is.LessThan(ut.Raw.Duration), "Oxidizing must make the blend burn shorter");
                Assert.That(ox.Effective.Intensity, Is.EqualTo(40), "intensity is limited by the LC9 cap");
            });
        });
    }

    /// <summary>
    /// Hand-authored fuels' *Mod fields are molotov tuning; a pure tank of any of them must burn exactly at its
    /// own prototype stats, or loading stock napalm variants would silently change their strength.
    /// </summary>
    [Test]
    public async Task StockFuelsKeepTheirOwnStatsInFlamers()
    {
        await Server.WaitAssertion(() =>
        {
            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var checkedFuels = 0;
            foreach (var reagent in SProtoMan.EnumeratePrototypes<ReagentPrototype>())
            {
                if (reagent.Abstract || reagent.Generated || reagent.Intensity <= 0 || reagent.Duration <= 0 || reagent.Radius <= 0)
                    continue;
                var pure = new Solution();
                pure.AddReagent(reagent.ID, FixedPoint2.New(100));
                Assert.That(fuel.TryCompute(pure, out var profile), Is.True, $"{reagent.ID} is not a valid fuel on its own");
                Assert.That((profile.Intensity, profile.Duration, profile.Radius),
                    Is.EqualTo((reagent.Intensity, reagent.Duration, reagent.Radius)),
                    $"{reagent.ID}: a pure tank must burn at its prototype stats");
                checkedFuels++;
            }
            Assert.That(checkedFuels, Is.GreaterThanOrEqualTo(5), "expected the RMC napalm family to be checked");
        });
    }

    [Test]
    public async Task SimulatorLoopCannotFarmSampleData()
    {
        await Server.WaitAssertion(() =>
        {
            // Every simulation costs at least 1 sample data (the Rare-reagent discount is floored at 1); the first
            // scan of a simulator-made chemical must never pay more than that, whatever its tier.
            foreach (var tier in new[] { 1, 2, 3, 4 })
            {
                var proto = new ReagentPrototype { GenTier = tier, Class = ReagentClass.Rare, Generated = true };
                Assert.That(ServerResearchDataTerminalSystem.SampleDataForSimulated(proto), Is.LessThanOrEqualTo(1),
                    $"tier {tier} simulated scan pays more than the cheapest simulation costs");
            }
        });
    }

    /// <summary>
    /// Every property the generator can roll must have a real effect body. Marker properties that the
    /// generator itself interprets are listed explicitly so that a new stub cannot hide behind them.
    /// </summary>
    [Test]
    public async Task GenerationEligiblePropertiesAreImplemented()
    {
        var markerProperties = new HashSet<string>
        {
            "Regulating", // generator omits overdose thresholds
            "Optimizing", // generator raises recipe yield
            "Intravenous", "Hypermetabolic", "Hypometabolic", // generator scales metabolismRate
            "Fueling", "Oxidizing", "Flowing", "Viscous", "Firepenetrating", // generator emits fire fields
            "Explosive", // generator adds a reaction explosion effect
            "Electrogenetic", // event-driven via the defibrillator
        };
        var hooks = new[] { "Tick", "TickOverdose", "TickCriticalOverdose", "ReagentBoost", "TickHydroTray" };

        await Server.WaitAssertion(() =>
        {
            var reflection = Server.ResolveDependency<IReflectionManager>();
            var effectTypes = reflection.GetAllChildren<RMCChemicalEffect>().ToDictionary(t => t.Name, t => t);
            var missing = new List<string>();
            foreach (var prop in SProtoMan.EnumeratePrototypes<ReagentPropertyPrototype>())
            {
                if (prop.Abstract || markerProperties.Contains(prop.ID))
                    continue;
                if (prop.Rarity is ReagentPropertyRarityEnum.Disabled or ReagentPropertyRarityEnum.Admin)
                    continue;
                Assert.That(effectTypes.TryGetValue(prop.EffectName, out var type), Is.True, $"{prop.ID}: no effect class named {prop.EffectName}");
                var implemented = false;
                for (var t = type; t != null && t != typeof(RMCChemicalEffect); t = t.BaseType)
                {
                    if (hooks.Any(h => t.GetMethod(h, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null))
                    {
                        implemented = true;
                        break;
                    }
                }
                if (!implemented)
                    missing.Add(prop.ID);
            }
            Assert.That(missing, Is.Empty, "generation-eligible properties with no effect body");
        });
    }

    /// <summary>
    /// A flamer tank held in hand refills from lab glassware, not only from reagent tanks, and the pour is
    /// still gated on whether the resulting blend burns.
    /// </summary>
    [Test]
    public async Task FlamerTanksRefillFromBeakers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var tank = SEntMan.SpawnEntity("RMCTankFlamer", map.GridCoords);
            var tankComp = SComp<RMCFlamerTankComponent>(tank);
            Assert.That(SEntMan.HasComponent<NoMixingReagentsComponent>(tank), Is.False, "CMU tanks allow blends");
            Assert.That(solutions.TryGetSolution(tank, tankComp.SolutionId, out var tankSoln, out var tankSolution), Is.True);
            solutions.RemoveAllSolution(tankSoln!.Value);

            var beaker = SEntMan.SpawnEntity("Beaker", map.GridCoords);
            Assert.That(SEntMan.HasComponent<FitsInDispenserComponent>(beaker), Is.True);
            Assert.That(solutions.TryGetSolution(beaker, "beaker", out var beakerSoln, out var beakerSolution), Is.True);
            var user = SEntMan.SpawnEntity(null, map.GridCoords);

            // Water is not fuel: the potency gate refuses it and nothing moves.
            solutions.TryAddReagent(beakerSoln!.Value, "Water", FixedPoint2.New(30));
            SEntMan.EventBus.RaiseLocalEvent(tank, new BeforeRangedInteractEvent(user, tank, beaker, map.GridCoords, true));
            Assert.Multiple(() =>
            {
                Assert.That(tankSolution!.Volume, Is.EqualTo(FixedPoint2.Zero), "water must not enter a flamer tank");
                Assert.That(beakerSolution!.Volume, Is.EqualTo(FixedPoint2.New(30)));
            });

            // Napalm from a beaker refills the tank through the transfer whitelist.
            solutions.RemoveAllSolution(beakerSoln.Value);
            solutions.TryAddReagent(beakerSoln.Value, "RMCNapalmUT", FixedPoint2.New(40));
            SEntMan.EventBus.RaiseLocalEvent(tank, new BeforeRangedInteractEvent(user, tank, beaker, map.GridCoords, true));
            Assert.Multiple(() =>
            {
                Assert.That(tankSolution!.Volume, Is.EqualTo(FixedPoint2.New(40)), "napalm in a beaker should refill the tank");
                Assert.That(beakerSolution!.Volume, Is.EqualTo(FixedPoint2.Zero));
            });

            // A generated additive rides on the napalm already in the tank.
            var flowing = NewOffer(2, 908);
            flowing.Effects["Flowing"] = 1;
            flowing.Recipe["RMCGold"] = (1, false);
            flowing.Recipe["RMCSilver"] = (1, false);
            flowing.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(flowing));
            solutions.TryAddReagent(beakerSoln.Value, flowing.ID, FixedPoint2.New(10));
            SEntMan.EventBus.RaiseLocalEvent(tank, new BeforeRangedInteractEvent(user, tank, beaker, map.GridCoords, true));
            Assert.Multiple(() =>
            {
                Assert.That(tankSolution!.Volume, Is.EqualTo(FixedPoint2.New(50)), "a Flowing additive should pour onto napalm");
                Assert.That(tankSolution.GetTotalPrototypeQuantity(flowing.ID), Is.EqualTo(FixedPoint2.New(10)));
            });

            // The additive alone would not burn in an empty tank, so it is refused there.
            solutions.RemoveAllSolution(tankSoln.Value);
            solutions.TryAddReagent(beakerSoln.Value, flowing.ID, FixedPoint2.New(10));
            SEntMan.EventBus.RaiseLocalEvent(tank, new BeforeRangedInteractEvent(user, tank, beaker, map.GridCoords, true));
            Assert.That(tankSolution!.Volume, Is.EqualTo(FixedPoint2.Zero), "a non-fuel additive must not fill an empty tank");

            // The other direction: the beaker is poured into the tank. It must hit the same gate.
            var transfer = SEntMan.System<SolutionTransferSystem>();
            solutions.RemoveAllSolution(beakerSoln.Value);
            solutions.TryAddReagent(beakerSoln.Value, "Water", FixedPoint2.New(30));
            transfer.Transfer(user, beaker, beakerSoln.Value, tank, tankSoln.Value, FixedPoint2.New(30));
            Assert.That(tankSolution.Volume, Is.EqualTo(FixedPoint2.Zero), "pouring water from a beaker into a tank must be refused");

            solutions.RemoveAllSolution(beakerSoln.Value);
            solutions.TryAddReagent(beakerSoln.Value, "RMCNapalmUT", FixedPoint2.New(30));
            transfer.Transfer(user, beaker, beakerSoln.Value, tank, tankSoln.Value, FixedPoint2.New(30));
            Assert.That(tankSolution.Volume, Is.EqualTo(FixedPoint2.New(30)), "pouring napalm from a beaker into a tank should work");
        });
    }

    /// <summary>
    /// The chain sizes tile fires from the blended profile: the tile's lifetime scales with the blend duration
    /// and a penetrating blend marks the fire so that heat resistance is ignored.
    /// </summary>
    [Test]
    public async Task FlamerChainScalesTileLifetimeAndMarksPenetration()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var flamer = Server.System<SharedRMCFlamerSystem>();
            var fireProto = SProtoMan.Index<EntityPrototype>("RMCTileFire");
            Assert.That(fireProto.TryGetComponent<TileFireComponent>(out var fireTemplate, SEntMan.ComponentFactory), Is.True);
            var baseLifetime = fireTemplate!.Duration;

            Entity<TileFireComponent> LightTile(int duration, bool penetrating)
            {
                foreach (var old in SEntMan.QueryEntities<TileFireComponent>().ToArray())
                    SEntMan.DeleteEntity(old.Owner);

                var chain = SEntMan.SpawnEntity(null, map.GridCoords);
                var comp = SEntMan.EnsureComponent<RMCFlamerChainComponent>(chain);
                comp.Spawn = "RMCTileFire";
                comp.Origin = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
                comp.Tiles = [new LineTile(new MapCoordinates(0.5f, 0.5f, map.MapId), TimeSpan.Zero)];
                comp.Intensity = 20;
                comp.Duration = duration;
                comp.FirePenetrating = penetrating;
                flamer.Update(0);
                var fires = SEntMan.QueryEntities<TileFireComponent>().ToArray();
                Assert.That(fires, Has.Length.EqualTo(1), "the chain should light exactly one tile");
                return fires[0];
            }

            // Napalm UT's duration keeps the prototype lifetime.
            var reference = LightTile(CMUFuelProfileSystem.ReferenceDuration, false);
            Assert.That(reference.Comp.Duration, Is.EqualTo(baseLifetime));
            Assert.That(SComp<RMCIgniteOnCollideComponent>(reference).FirePenetrating, Is.False);

            // A blend above the tank cap is capped first, then scales the lifetime: 24 / 20 = 1.2.
            var longer = LightTile(30, true);
            var cappedDuration = new RMCFlamerChainComponent().MaxDuration;
            Assert.Multiple(() =>
            {
                Assert.That(longer.Comp.Duration, Is.EqualTo(baseLifetime * CMUFuelProfileSystem.TileLifetimeMultiplier(cappedDuration)));
                Assert.That(longer.Comp.Duration, Is.GreaterThan(baseLifetime));
                Assert.That(SComp<RMCIgniteOnCollideComponent>(longer).Duration, Is.EqualTo(cappedDuration));
                Assert.That(SComp<RMCIgniteOnCollideComponent>(longer).FirePenetrating, Is.True);
            });

            // A weak fuel burns out sooner, never below the clamp.
            var shorter = LightTile(5, false);
            Assert.That(shorter.Comp.Duration, Is.EqualTo(baseLifetime * CMUFuelProfileSystem.MinMultiplier));
            Assert.That(CMUFuelProfileSystem.TileLifetimeMultiplier(400), Is.EqualTo(CMUFuelProfileSystem.MaxMultiplier));
        });
    }

    /// <summary>
    /// Firepenetrating is a reagent flag, not an intensity bonus: the generator emits it, the blend carries it
    /// once a tenth of the tank is penetrating fuel, and penetrating fire damage ignores Yautja heat resistance.
    /// </summary>
    [Test]
    public async Task FirepenetratingBypassesHeatResistance()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var data = NewOffer(3, 909);
            data.Effects["Fueling"] = 1;
            data.Effects["Firepenetrating"] = 1;
            data.Recipe["RMCGold"] = (1, false);
            data.Recipe["RMCSilver"] = (1, false);
            data.Recipe["RMCPlatinum"] = (1, false);
            data.Recipe["RMCUranium"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(data));

            var reagent = SProtoMan.Index<ReagentPrototype>(data.ID);
            Assert.Multiple(() =>
            {
                Assert.That(reagent.FirePenetrating, Is.True, "the generator should emit the penetration flag");
                // base 15 with Fueling -3; no +10 stand-in any more
                Assert.That(reagent.Intensity, Is.EqualTo(12));
            });

            var fuel = SEntMan.System<CMUFuelProfileSystem>();
            var pure = new Solution();
            pure.AddReagent(data.ID, FixedPoint2.New(50));
            Assert.That(fuel.TryCompute(pure, out var pureProfile), Is.True);
            Assert.That(pureProfile.FirePenetrating, Is.True);

            var trace = new Solution();
            trace.AddReagent("RMCNapalmUT", FixedPoint2.New(95));
            trace.AddReagent(data.ID, FixedPoint2.New(5));
            Assert.That(fuel.TryCompute(trace, out var traceProfile), Is.True);
            Assert.That(traceProfile.FirePenetrating, Is.False, "a trace of penetrating fuel must not mark the whole tank");

            var blend = new Solution();
            blend.AddReagent("RMCNapalmUT", FixedPoint2.New(60));
            blend.AddReagent(data.ID, FixedPoint2.New(40));
            Assert.That(fuel.TryCompute(blend, out var blendProfile), Is.True);
            Assert.That(blendProfile.FirePenetrating, Is.True);

            // Damage: a Yautja keeps its modifier set against ordinary fire and loses it against penetrating fire.
            var yautja = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var damageable = SComp<DamageableComponent>(yautja);
            damageable.DamageModifierSetId = "RMCSynth"; // Heat 0.9
            var flammable = SEntMan.System<SharedRMCFlammableSystem>();
            var heat = new DamageSpecifier();
            heat.DamageDict["Heat"] = FixedPoint2.New(10);

            var before = damageable.TotalDamage;
            flammable.DamageFromFire(yautja, heat);
            var resisted = damageable.TotalDamage - before;
            flammable.DamageFromFire(yautja, heat, penetrating: true);
            var penetrated = damageable.TotalDamage - before - resisted;
            Assert.Multiple(() =>
            {
                Assert.That(penetrated, Is.EqualTo(FixedPoint2.New(10)), "penetrating fire ignores the modifier set");
                Assert.That(resisted, Is.LessThan(penetrated), "ordinary fire respects Yautja heat resistance");
            });
        });
    }

    /// <summary>
    /// Allergenic resolves the target's allergy by class and property, so a generated painkiller sets off a
    /// pre-rolled opioid allergy even though its ID never matches.
    /// </summary>
    [Test]
    public async Task AllergenicCrossReactsByProperty()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var painkiller = NewOffer(2, 910);
            painkiller.Effects["Painkilling"] = 2;
            painkiller.Effects["Allergenic"] = 1;
            painkiller.Recipe["RMCGold"] = (1, false);
            painkiller.Recipe["RMCPlatinum"] = (1, false);
            painkiller.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(painkiller));

            var unrelated = NewOffer(2, 911);
            unrelated.Effects["Antitoxic"] = 1;
            unrelated.Effects["Allergenic"] = 1;
            unrelated.Recipe["RMCSilver"] = (1, false);
            unrelated.Recipe["RMCUranium"] = (1, false);
            unrelated.Recipe["CMUTearGas"] = (1, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GenerateReagentEvent(unrelated));

            // The allergy trait spawns a dogtag at the owner's position, so the owner needs real coordinates.
            var allergic = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent(allergic, new DrugAllergyComponent { Allergen = "CMUOxycodone" });
            var allergies = SEntMan.System<AllergicReactionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(allergies.IsAllergen(allergic, SProtoMan.Index<ReagentPrototype>("CMUOxycodone")), Is.True, "the allergen itself");
                Assert.That(allergies.IsAllergen(allergic, SProtoMan.Index<ReagentPrototype>(painkiller.ID)), Is.True, "a generated painkiller shares the Painkilling property");
                Assert.That(allergies.IsAllergen(allergic, SProtoMan.Index<ReagentPrototype>(unrelated.ID)), Is.False, "an unrelated generated chemical");
                Assert.That(allergies.IsAllergen(allergic, SProtoMan.Index<ReagentPrototype>("CMUParacetamol")), Is.False, "another hand-authored painkiller still needs its own allergy");
            });
        });
    }
}
