using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Examine;
using Content.Server.Destructible;
using Content.Server.Power.Components;
using Content.Server.Construction.Components;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Power.Components;
using Content.Shared.UserInterface;
using Content.Shared.Wires;
using Content.Shared._RMC14.Xenonids.Acid;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaMachineSourceParityTest
{
    private static readonly ResPath YautjaMachinesRsi = new("/Textures/_CMU14/Yautja/Structures/yautja_machines.rsi");
    private static readonly ResPath HunterShipYautjaMachinesRsi = new("/Textures/_CMU14/HunterShip/obj/structures/machinery/yautja_machines.rsi");
    private static readonly IReadOnlyDictionary<ProtoId<MaterialPrototype>, int> NoMaterials =
        new Dictionary<ProtoId<MaterialPrototype>, int>();

    [Test]
    public async Task YautjaMachineStaticPrototypesMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in Cmss13YautjaMachineRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo(row.Name), $"{row.Id} CMSS13 source name");
                    Assert.That(prototype.Description, Is.EqualTo(row.Description), $"{row.Id} CMSS13 source description");
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, $"{row.Id} sprite");
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(row.RsiPath), $"{row.Id} CMSS13 yautja_machines.dmi import path");
                    Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo(row.IconState), $"{row.Id} CMSS13 icon_state");
                    Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, $"{row.Id} icon");
                    var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                    Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith(row.RsiPath.ToString().Replace("/Textures/", string.Empty)),
                        $"{row.Id} icon RSI");
                    Assert.That(rsiIcon.RsiState, Is.EqualTo(row.IconState), $"{row.Id} icon state");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var entMan = server.EntMan;
            var factory = server.EntMan.ComponentFactory;

            foreach (var row in Cmss13YautjaMachineRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);
                var entity = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);

                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.HasComponent<DestructibleComponent>(entity), Is.False, $"{row.Id} CMSS13 breakable = FALSE");

                        if (row.Dense)
                        {
                            Assert.That(prototype.TryGetComponent<PhysicsComponent>(out var physics, factory), Is.True, $"{row.Id} physics");
                            Assert.That(physics!.BodyType, Is.EqualTo(BodyType.Static), $"{row.Id} CMSS13 density static body");
                            Assert.That(prototype.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True, $"{row.Id} fixtures");
                            Assert.That(fixtures!.Fixtures.Values.Any(fixture => fixture.Hard), Is.True, $"{row.Id} blocking fixture");
                        }

                        if (row.FunctionalAutolathe)
                        {
                            Assert.That(entMan.HasComponent<LatheComponent>(entity), Is.True,
                                $"{row.Id} CMSS13 inherits /obj/structure/machinery/autolathe runtime.");
                            Assert.That(entMan.HasComponent<MaterialStorageComponent>(entity), Is.True,
                                $"{row.Id} CMSS13 stores source metal/glass materials.");
                            Assert.That(entMan.HasComponent<ActivatableUIComponent>(entity), Is.True,
                                $"{row.Id} CMSS13 attack_hand opens the autolathe TGUI.");
                            Assert.That(entMan.HasComponent<UserInterfaceComponent>(entity), Is.True,
                                $"{row.Id} local lathe UI surface.");
                            Assert.That(entMan.HasComponent<WiresPanelComponent>(entity), Is.True,
                                $"{row.Id} CMSS13 autolathe keeps its wire panel surface.");
                            Assert.That(entMan.HasComponent<MachineComponent>(entity), Is.True,
                                $"{row.Id} CMSS13 autolathe remains a machinery/lathe object.");
                            Assert.That(entMan.TryGetComponent(entity, out ApcPowerReceiverComponent? power), Is.True,
                                $"{row.Id} CMSS13 autolathe inherits powered machinery state.");
                            Assert.That(power!.NeedsPower, Is.False,
                                $"{row.Id} local RMC autolathe maps CMSS13 ship machinery power to always-powered access.");
                            Assert.That(entMan.HasComponent<ActivatableUIRequiresPowerComponent>(entity), Is.True,
                                $"{row.Id} local RMC autolathe keeps the powered-UI gate while NeedsPower=false.");
                        }
                    });
                }
                finally
                {
                    entMan.DeleteEntity(entity);
                }

                if (row.MaterialStorage.Count != 0)
                    AssertMaterialStorage(prototype, factory, row.Id, row.MaterialStorage);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterGlobeUsesCmss13AllMinimapFlagAndNoDrawing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid globe = default;
        EntityUid yautjaUser = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var tacticalMaps = entMan.System<TacticalMapSystem>();

                entMan.EnsureComponent<TacticalMapComponent>(map.Grid);
                globe = entMan.SpawnEntity("CMUYautjaStructureYautjaMachinesGlobe", map.GridCoords);

                yautjaUser = entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(1, 0)));
                tacticalMaps.SetYautjaUser(yautjaUser, true);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var tacticalMaps = entMan.System<TacticalMapSystem>();
                var tacticalMap = entMan.GetComponent<TacticalMapComponent>(map.Grid);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent(globe, out TacticalMapAlwaysVisibleComponent? alwaysVisible), Is.True,
                        "CMSS13 hunter globe sets minimap_flag = MINIMAP_FLAG_ALL.");
                    Assert.That(alwaysVisible!.VisibleToMarines, Is.True);
                    Assert.That(alwaysVisible.VisibleToXenos, Is.True);
                    Assert.That(alwaysVisible.VisibleToOpfor, Is.True);
                    Assert.That(alwaysVisible.VisibleToGovfor, Is.True);
                    Assert.That(alwaysVisible.VisibleToClf, Is.True);
                    Assert.That(alwaysVisible.VisibleToYautja, Is.True,
                        "MINIMAP_FLAG_ALL must include the local Yautja tactical-map bucket too.");

                    Assert.That(entMan.HasComponent<TacticalMapTrackedComponent>(globe), Is.True,
                        "CMSS13 minimap_flag requires local tactical-map tracking on the globe prototype.");
                    Assert.That(entMan.TryGetComponent(globe, out TacticalMapIconComponent? icon), Is.True);
                    Assert.That(icon!.Icon, Is.EqualTo(new SpriteSpecifier.Rsi(YautjaMachinesRsi, "globe")));
                    Assert.That(entMan.HasComponent<TacticalMapUserComponent>(globe), Is.False,
                        "CMSS13 hunter globe sets drawing = FALSE; it is a map marker, not a tactical-map drawing UI user.");
                    Assert.That(tacticalMaps.TryGetBlip(tacticalMap, "MARINES", globe.Id, out _), Is.True,
                        "The local tracked blip may live in one backing bucket; always-visible flags fan it out to user views.");
                });

                AssertUserSeesGlobe(entMan, yautjaUser, map.Grid, globe, user => user.Comp.YautjaBlips, "yautja");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { globe, yautjaUser })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaDoorControlVisualStatesMatchCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in Cmss13YautjaDoorControlVisualRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo("remote door-control"), $"{row.Id} CMSS13 source name");
                    Assert.That(prototype.Description, Is.EqualTo("A remote control-switch for a door."), $"{row.Id} CMSS13 source description");
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, $"{row.Id} sprite");
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(YautjaMachinesRsi), $"{row.Id} /door_control/yautja icon import path");
                    Assert.That(sprite.AllLayers.Single().RsiState.Name, Is.EqualTo(row.IconState), $"{row.Id} CMSS13 runtime icon_state");
                    Assert.That(sprite.DrawDepth, Is.EqualTo((int) DrawDepth.Objects), $"{row.Id} CMSS13 TILE_BOUND object sprite depth");

                    Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, $"{row.Id} icon");
                    var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                    Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith(YautjaMachinesRsi.ToString().Replace("/Textures/", string.Empty)),
                        $"{row.Id} icon RSI");
                    Assert.That(rsiIcon.RsiState, Is.EqualTo(row.IconState), $"{row.Id} icon state");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            foreach (var row in Cmss13YautjaDoorControlVisualRows())
            {
                var entity = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);

                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.HasComponent<DamageableComponent>(entity), Is.True,
                            $"{row.Id} keeps a local structural damage surface while source explo_proof prevents full destruction.");
                        Assert.That(entMan.HasComponent<DestructibleComponent>(entity), Is.False,
                            $"{row.Id} CMSS13 door_control has explo_proof = TRUE.");
                        Assert.That(entMan.TryGetComponent(entity, out CorrodibleComponent? corrodible), Is.True,
                            $"{row.Id} CMSS13 door_control has unacidable = TRUE.");
                        Assert.That(corrodible!.IsCorrodible, Is.False,
                            $"{row.Id} CMSS13 door_control has unacidable = TRUE.");
                    });
                }
                finally
                {
                    entMan.DeleteEntity(entity);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMonitorConsoleVisualStatesMatchCmss13AndDmmFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in Cmss13YautjaMonitorConsoleVisualRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.Multiple(() =>
                {
                    if (row.Name != null)
                        Assert.That(prototype.Name, Is.EqualTo(row.Name), $"{row.Id} {row.SourcePath} source name");

                    if (row.Description != null)
                        Assert.That(prototype.Description, Is.EqualTo(row.Description), $"{row.Id} {row.SourcePath} source description");

                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, $"{row.Id} sprite");
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(YautjaMachinesRsi), $"{row.Id} yautja_machines.dmi import path");
                    Assert.That(sprite.AllLayers.Single().RsiState.Name, Is.EqualTo(row.IconState), $"{row.Id} source/DMM icon_state");
                    Assert.That(sprite.DrawDepth, Is.EqualTo((int) DrawDepth.Objects), $"{row.Id} direct helper object sprite depth");

                    Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, $"{row.Id} icon");
                    var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                    Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith(YautjaMachinesRsi.ToString().Replace("/Textures/", string.Empty)),
                        $"{row.Id} icon RSI");
                    Assert.That(rsiIcon.RsiState, Is.EqualTo(row.IconState), $"{row.Id} icon state");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;

            foreach (var row in Cmss13YautjaMonitorConsoleVisualRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                if (row.SourceHardened)
                {
                    var entity = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);

                    try
                    {
                        Assert.Multiple(() =>
                        {
                            Assert.That(entMan.HasComponent<DamageableComponent>(entity), Is.True,
                                $"{row.Id} keeps a local structural damage surface while {row.SourcePath} prevents full destruction.");
                            Assert.That(entMan.HasComponent<DestructibleComponent>(entity), Is.False,
                                $"{row.Id} {row.SourcePath} is source-hardened with explo_proof = TRUE and, for hunt consoles, breakable = FALSE.");
                            Assert.That(entMan.TryGetComponent(entity, out CorrodibleComponent? corrodible), Is.True,
                                $"{row.Id} {row.SourcePath} has unacidable = TRUE.");
                            Assert.That(corrodible!.IsCorrodible, Is.False,
                                $"{row.Id} {row.SourcePath} has unacidable = TRUE.");

                            Assert.That(prototype.TryGetComponent<PhysicsComponent>(out var physics, factory), Is.True, $"{row.Id} physics");
                            Assert.That(physics!.BodyType, Is.EqualTo(BodyType.Static), $"{row.Id} dense console static body");
                            Assert.That(prototype.TryGetComponent<FixturesComponent>(out var fixtures, factory), Is.True, $"{row.Id} fixtures");
                            Assert.That(fixtures!.Fixtures.Values.Any(fixture => fixture.Hard), Is.True,
                                $"{row.Id} {row.SourcePath} dense console fixture");
                        });
                    }
                    finally
                    {
                        entMan.DeleteEntity(entity);
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaMachineHelperVisualStatesMatchDmmBackedWrapperFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var server = pair.Server;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;

            foreach (var row in DmmBackedYautjaMachineHelperRows())
            {
                var prototype = prototypes.Index<EntityPrototype>(row.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(prototype.Name, Is.EqualTo(row.Name), $"{row.Id} {row.SourcePath} wrapper-backed name");
                    Assert.That(prototype.Description, Is.EqualTo(row.Description), $"{row.Id} {row.SourcePath} wrapper-backed description");
                    Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True, $"{row.Id} sprite");
                    Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(YautjaMachinesRsi), $"{row.Id} yautja_machines.dmi import path");
                    Assert.That(sprite.AllLayers.Single().RsiState.Name, Is.EqualTo(row.IconState), $"{row.Id} DMM/conversion icon_state");
                    Assert.That(sprite.DrawDepth, Is.EqualTo((int) DrawDepth.Objects), $"{row.Id} direct helper object sprite depth");

                    Assert.That(prototype.TryGetComponent<IconComponent>(out var icon, factory), Is.True, $"{row.Id} icon");
                    var rsiIcon = (SpriteSpecifier.Rsi) icon!.Icon;
                    Assert.That(rsiIcon.RsiPath.ToString(), Does.EndWith(YautjaMachinesRsi.ToString().Replace("/Textures/", string.Empty)),
                        $"{row.Id} icon RSI");
                    Assert.That(rsiIcon.RsiState, Is.EqualTo(row.IconState), $"{row.Id} icon state");
                });
            }
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            foreach (var row in DmmBackedYautjaMachineHelperRows())
            {
                var entity = entMan.SpawnEntity(row.Id, MapCoordinates.Nullspace);

                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.HasComponent<DamageableComponent>(entity), Is.True,
                            $"{row.Id} keeps the local Yautja structural damage surface.");
                        Assert.That(entMan.HasComponent<DestructibleComponent>(entity), Is.False,
                            $"{row.Id} direct helper should not introduce a local destruction path for source-absent machinery.");
                        Assert.That(entMan.HasComponent<RMCMesonsNonviewableComponent>(entity), Is.True,
                            $"{row.Id} keeps the local Yautja mesons-hidden structure surface.");
                    });
                }
                finally
                {
                    entMan.DeleteEntity(entity);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaCauldronRuntimeMatchesCmss13BubblerAttackBy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid human = default;
        EntityUid cauldron = default;
        EntityUid crowbar = default;
        EntityUid rawLimb = default;
        EntityUid cancelLimb = default;
        EntityUid boiledLimb = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                cauldron = entMan.SpawnEntity("CMUYautjaStructureYautjaMachinesVat", map.GridCoords.Offset(new Vector2(2, 0)));
                crowbar = entMan.SpawnEntity("CMCrowbar", map.GridCoords);
                rawLimb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new Vector2(2, 1)));
                cancelLimb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new Vector2(2, 2)));
                boiledLimb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new Vector2(2, 3)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaFlayedComponent>(cancelLimb);
                entMan.EnsureComponent<YautjaFlayedComponent>(boiledLimb);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var examine = entMan.System<ExamineSystem>();

                Assert.That(entMan.HasComponent<YautjaCauldronComponent>(cauldron), Is.True,
                    "CMSS13 /obj/structure/machinery/prop/yautja/bubbler has custom get_examine_text() and attackby() behavior.");
                Assert.That(entMan.GetComponent<BodyPartComponent>(rawLimb).Body, Is.Null,
                    "The test fixture must model CMSS13 /obj/item/limb as a detached local body-part item.");

                var hunterText = examine.GetExamineText(cauldron, hunter).ToMarkup();
                var humanText = examine.GetExamineText(cauldron, human).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(hunterText, Does.Contain("You can use this machine to clean the skin off limbs, and turn them into bones for your armor."));
                    Assert.That(hunterText, Does.Contain("You first need to find a limb. Then you use a ceremonial dagger to prepare it."));
                    Assert.That(hunterText, Does.Contain("After preparing the limb, you put it into the cauldron, removing the flesh, leaving you with a bone."));
                    Assert.That(hunterText, Does.Contain("You will then clean and polish the resulting bones with a polishing rag, making it ready to be attached to your armor."));
                    Assert.That(humanText, Does.Not.Contain("clean the skin off limbs"),
                        "CMSS13 adds the cauldron instructions only when HAS_TRAIT(user, TRAIT_YAUTJA_TECH).");
                });

                var nonTech = InteractWithCauldron(entMan, human, rawLimb, cauldron);
                var nonLimb = InteractWithCauldron(entMan, hunter, crowbar, cauldron);
                var unflayed = InteractWithCauldron(entMan, hunter, rawLimb, cauldron);

                Assert.Multiple(() =>
                {
                    Assert.That(nonTech.Handled, Is.True,
                        "CMSS13 cauldron attackby() handles non-tech users with a denial notice.");
                    Assert.That(nonLimb.Handled, Is.True,
                        "CMSS13 cauldron attackby() handles non-limb items with a denial notice.");
                    Assert.That(unflayed.Handled, Is.True,
                        "CMSS13 cauldron attackby() handles unflayed limbs with a not-ready notice.");
                    Assert.That(ActiveCauldronDoAfters(entMan, human), Is.Zero);
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.Zero);
                    AssertCauldronVisual(entMan, cauldron, "vat");
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                var started = InteractWithCauldron(entMan, hunter, cancelLimb, cauldron);
                var busy = InteractWithCauldron(entMan, hunter, boiledLimb, cauldron);

                Assert.Multiple(() =>
                {
                    Assert.That(started.Handled, Is.True,
                        "CMSS13 sets icon_state = vat_boiling and starts do_after(user, 15 SECONDS, INTERRUPT_NONE, BUSY_ICON_HOSTILE, current_limb).");
                    Assert.That(busy.Handled, Is.True,
                        "CMSS13 user.action_busy returns after handling the cauldron attackby().");
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.EqualTo(1));
                    AssertActiveCauldronDoAfter(entMan, hunter, cancelLimb, cauldron);
                    AssertCauldronVisual(entMan, cauldron, "vat_boiling");
                });

                CancelActiveCauldronDoAfter(entMan, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.Zero);
                    Assert.That(entMan.Deleted(cancelLimb), Is.False,
                        "CMSS13 failed do_after pulls current_limb back out of the cauldron instead of deleting it.");
                    Assert.That(entMan.Deleted(boiledLimb), Is.False);
                    AssertCauldronVisual(entMan, cauldron, "vat");
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                var started = InteractWithCauldron(entMan, hunter, boiledLimb, cauldron);

                Assert.Multiple(() =>
                {
                    Assert.That(started.Handled, Is.True);
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.EqualTo(1));
                    AssertActiveCauldronDoAfter(entMan, hunter, boiledLimb, cauldron);
                    AssertCauldronVisual(entMan, cauldron, "vat_boiling");
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(15.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var trophies = entMan.EntityQuery<YautjaTrophyComponent, MetaDataComponent>()
                    .Where(trophy => trophy.Item1.Kind == YautjaTrophyKind.HumanLeftArmBone)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.Zero);
                    Assert.That(entMan.Deleted(boiledLimb) || entMan.IsQueuedForDeletion(boiledLimb), Is.True,
                        "CMSS13 qdel(current_limb) after creating current_limb.bone_type at get_turf(src).");
                    Assert.That(trophies, Has.Count.EqualTo(1),
                        "CMSS13 creates exactly one skeleton accessory from current_limb.bone_type.");
                    Assert.That(trophies.Single().Item2.EntityPrototype?.ID, Is.EqualTo("CMUYautjaHumanLeftArmBoneTrophy"));
                    AssertCauldronVisual(entMan, cauldron, "vat");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, human, cauldron, crowbar, rawLimb, cancelLimb, boiledLimb })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                var query = entMan.EntityQueryEnumerator<YautjaTrophyComponent>();
                while (query.MoveNext(out var uid, out var trophy))
                {
                    if (trophy.Kind == YautjaTrophyKind.HumanLeftArmBone && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void AssertUserSeesGlobe(
        IEntityManager entMan,
        EntityUid user,
        EntityUid map,
        EntityUid globe,
        Func<Entity<TacticalMapUserComponent>, Dictionary<int, TacticalMapBlip>> getBlips,
        string label)
    {
        var tacticalMaps = entMan.System<TacticalMapSystem>();
        var userComp = entMan.GetComponent<TacticalMapUserComponent>(user);
        tacticalMaps.UpdateUserData((user, userComp), entMan.GetComponent<TacticalMapComponent>(map));

        Assert.That(getBlips((user, userComp)).ContainsKey(globe.Id), Is.True,
            $"CMSS13 MINIMAP_FLAG_ALL makes hunter globe visible to {label} tactical-map users.");
    }

    private static void AssertMaterialStorage(
        EntityPrototype prototype,
        IComponentFactory factory,
        string id,
        IReadOnlyDictionary<ProtoId<MaterialPrototype>, int> expected)
    {
        Assert.That(prototype.TryGetComponent<MaterialStorageComponent>(out var storage, factory), Is.True, $"{id} MaterialStorage");
        Assert.Multiple(() =>
        {
            foreach (var (material, amount) in expected)
            {
                Assert.That(storage!.Storage.GetValueOrDefault(material), Is.EqualTo(amount),
                    $"{id} CMSS13 stored_material {material}");
            }
        });
    }

    private static IEnumerable<Cmss13YautjaMachineRow> Cmss13YautjaMachineRows()
    {
        yield return new Cmss13YautjaMachineRow(
            "CMUYautjaStructureYautjaMachinesGlobe",
            "hunter globe",
            "A globe designed by the hunters to show them the location of prey across the hunting grounds.",
            YautjaMachinesRsi,
            "globe",
            Dense: false,
            MaterialStorage: NoMaterials,
            FunctionalAutolathe: false);

        yield return new Cmss13YautjaMachineRow(
            "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesGlobeSouth",
            "hunter globe",
            "A globe designed by the hunters to show them the location of prey across the hunting grounds.",
            YautjaMachinesRsi,
            "globe",
            Dense: false,
            MaterialStorage: NoMaterials,
            FunctionalAutolathe: false);

        yield return new Cmss13YautjaMachineRow(
            "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesGlobeSouthOffset0x3",
            "hunter globe",
            "A globe designed by the hunters to show them the location of prey across the hunting grounds.",
            YautjaMachinesRsi,
            "globe",
            Dense: false,
            MaterialStorage: NoMaterials,
            FunctionalAutolathe: false);

        yield return new Cmss13YautjaMachineRow(
            "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesGlobeSouthOffset0xNeg10",
            "hunter globe",
            "A globe designed by the hunters to show them the location of prey across the hunting grounds.",
            YautjaMachinesRsi,
            "globe",
            Dense: false,
            MaterialStorage: NoMaterials,
            FunctionalAutolathe: false);

        var autolatheMaterials = new Dictionary<ProtoId<MaterialPrototype>, int>
        {
            ["CMSteel"] = 40000,
            ["CMGlass"] = 20000,
        };

        yield return new Cmss13YautjaMachineRow(
            "CMUYautjaStructureYautjaMachinesAutolathe",
            "yautja autolathe",
            "It produces items using metal and glass.",
            YautjaMachinesRsi,
            "autolathe",
            Dense: true,
            MaterialStorage: autolatheMaterials,
            FunctionalAutolathe: true);

        yield return new Cmss13YautjaMachineRow(
            "CMUHunterShipPlacedCMAutolatheAutolatheSouth",
            "yautja autolathe",
            "It produces items using metal and glass.",
            HunterShipYautjaMachinesRsi,
            "autolathe",
            Dense: true,
            MaterialStorage: autolatheMaterials,
            FunctionalAutolathe: true);

        yield return new Cmss13YautjaMachineRow(
            "CMUYautjaStructureYautjaMachinesVat",
            "yautja cauldron",
            "A large, black machine emitting an ominous hum with an attached pot of boiling fluid. Bits of what appears to be leftover lard and balls of hair can be seen floating inside of it.",
            YautjaMachinesRsi,
            "vat",
            Dense: true,
            MaterialStorage: NoMaterials,
            FunctionalAutolathe: false);

        yield return new Cmss13YautjaMachineRow(
            "CMUHunterShipPlacedCMUYautjaStructureYautjaMachinesVatSouth",
            "yautja cauldron",
            "A large, black machine emitting an ominous hum with an attached pot of boiling fluid. Bits of what appears to be leftover lard and balls of hair can be seen floating inside of it.",
            YautjaMachinesRsi,
            "vat",
            Dense: true,
            MaterialStorage: NoMaterials,
            FunctionalAutolathe: false);
    }

    private static IEnumerable<Cmss13YautjaDoorControlVisualRow> Cmss13YautjaDoorControlVisualRows()
    {
        yield return new Cmss13YautjaDoorControlVisualRow("CMUYautjaStructureYautjaMachinesDoorctrl", "doorctrl");
        yield return new Cmss13YautjaDoorControlVisualRow("CMUYautjaStructureYautjaMachinesDoorctrl0", "doorctrl0");
        yield return new Cmss13YautjaDoorControlVisualRow("CMUYautjaStructureYautjaMachinesDoorctrl1", "doorctrl1");
        yield return new Cmss13YautjaDoorControlVisualRow("CMUYautjaStructureYautjaMachinesDoorctrlDenied", "doorctrl-denied");
        yield return new Cmss13YautjaDoorControlVisualRow("CMUYautjaStructureYautjaMachinesDoorctrlP", "doorctrl-p");
    }

    private static IEnumerable<Cmss13YautjaMonitorConsoleVisualRow> Cmss13YautjaMonitorConsoleVisualRows()
    {
        yield return new Cmss13YautjaMonitorConsoleVisualRow(
            "CMUYautjaStructureYautjaMachinesCrew",
            "/obj/structure/machinery/hunt_ground_escape",
            "preserve shutter console",
            "A console for opening a shutter to another part of the reserve.",
            "crew",
            SourceHardened: true);

        yield return new Cmss13YautjaMonitorConsoleVisualRow(
            "CMUYautjaStructureYautjaMachinesTerminal",
            "/obj/structure/machinery/computer/cryopod/yautja",
            "hypersleep bay console",
            "A large console controlling the ship's hypersleep bay. Most of the options are disabled and locked, although it allows recovery of items from long-term hypersleeping crew.",
            "terminal",
            SourceHardened: true);

        yield return new Cmss13YautjaMonitorConsoleVisualRow(
            "CMUYautjaStructureYautjaMachinesCmonitor",
            "yautja_machines.dmi imported cmonitor state; no located source runtime path in the current CMSS13 checkout",
            Name: null,
            Description: null,
            IconState: "cmonitor",
            SourceHardened: false);

        yield return new Cmss13YautjaMonitorConsoleVisualRow(
            "CMUYautjaStructureYautjaMachinesCameras",
            "/obj/structure/machinery/blooding_spawner",
            "blooding console",
            "A console used by Yautja to awaken Youngbloods awaiting their Blooding Ritual.",
            "cameras",
            SourceHardened: true);

        yield return new Cmss13YautjaMonitorConsoleVisualRow(
            "CMUYautjaStructureYautjaMachinesOverwatch",
            "/obj/structure/machinery/hunting_ground_selection and /obj/structure/machinery/hunt_ground_spawner",
            Name: null,
            Description: null,
            IconState: "overwatch",
            SourceHardened: true);

        yield return new Cmss13YautjaMonitorConsoleVisualRow(
            "CMUYautjaStructureYautjaMachinesSmallmonitor",
            "Tools/_CMU14/HunterShipPort/Hunter_Ship.dmm /obj/structure/showcase smallmonitor placements",
            Name: null,
            Description: null,
            IconState: "smallmonitor",
            SourceHardened: false);
    }

    private static IEnumerable<DmmBackedYautjaMachineHelperRow> DmmBackedYautjaMachineHelperRows()
    {
        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesSmes",
            "/obj/structure/machinery/power/smes/magical/yautja",
            "Yautja Energy Core",
            "A highly advanced power source of Yautja design, utilizing unknown technology to generate and distribute energy efficiently throughout the vessel.",
            "smes");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesJuicer1",
            "/obj/structure/machinery/juicer/yautja",
            "Bone grinder",
            "A functional object aboard the Yautja Hunter Ship.",
            "juicer1");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesMw",
            "/obj/structure/machinery/microwave/yautja",
            "Alien microwave",
            "Dark alloy sinister machine that heats up cold food.",
            "mw");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesGrinder",
            "/obj/structure/machinery/gibber/yautja",
            "Gibber",
            "The name isn't descriptive enough?",
            "grinder");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesProcessor",
            "/obj/structure/machinery/processor/yautja",
            "Food grinder",
            "A functional object aboard the Yautja Hunter Ship.",
            "processor");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesDinnerware",
            "/obj/structure/machinery/vending/dinnerware/yautja",
            "dinnerplate dispenser",
            "A kitchen and restaurant equipment vendor.",
            "dinnerware");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesMixer0",
            "/obj/structure/machinery/chem_master/yautja",
            "Chemical distributor",
            "A functional object aboard the Yautja Hunter Ship.",
            "mixer0");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesDispenser",
            "/obj/structure/machinery/chem_dispenser/yauja",
            "Chemical dispenser",
            "A complex machine for mixing elements into chemicals. A Wey-Yu product.",
            "dispenser");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesSodaDispenser",
            "/obj/structure/machinery/chem_dispenser/soda/yautja",
            "Soda fountain",
            "A drink fabricating machine, capable of producing many sugary drinks with just one touch.",
            "soda_dispenser");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesBoozeDispenser",
            "/obj/structure/machinery/chem_dispenser/soda/beer/yautja",
            "Booze dispenser",
            "A technological marvel, supposedly able to mix just the mixture you'd like to drink the moment you ask for one.",
            "booze_dispenser");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesFreezer0",
            "/obj/structure/pipes/unary/freezer/yautja",
            "Gas cooling system",
            "Cools gas when connected to pipe network.",
            "freezer_0");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesWeldtank",
            "/obj/structure/reagent_dispensers/tank/fuel/yautja",
            "Fuel tank",
            "A tank filled with fuel.",
            "weldtank");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesWatertank",
            "/obj/structure/reagent_dispensers/tank/water/yautja",
            "Water tank",
            "A tank filled with water.",
            "watertank");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesBlue",
            "/obj/structure/machinery/portable_atmospherics/canister/oxygen/yautja",
            "Canister: \\[O2\\]",
            "A functional object aboard the Yautja Hunter Ship.",
            "blue");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesSeeds",
            "/obj/structure/machinery/vending/hydroseeds/yautja",
            "MegaSeed Servitor",
            "When you need seeds fast!",
            "seeds");

        yield return new DmmBackedYautjaMachineHelperRow(
            "CMUYautjaStructureYautjaMachinesNutri",
            "/obj/structure/machinery/vending/hydronutrients/yautja",
            "NutriMax",
            "A plant nutrients vendor.",
            "nutri");
    }

    private static InteractUsingEvent InteractWithCauldron(
        IEntityManager entMan,
        EntityUid user,
        EntityUid used,
        EntityUid cauldron)
    {
        var interact = new InteractUsingEvent(
            user,
            used,
            cauldron,
            entMan.GetComponent<TransformComponent>(cauldron).Coordinates);

        entMan.EventBus.RaiseLocalEvent(cauldron, interact);
        return interact;
    }

    private static int ActiveCauldronDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaCauldronBoilDoAfterEvent)
            : 0;
    }

    private static void AssertActiveCauldronDoAfter(
        IEntityManager entMan,
        EntityUid user,
        EntityUid limb,
        EntityUid cauldron)
    {
        var active = entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(doAfter =>
            !doAfter.Cancelled &&
            !doAfter.Completed &&
            doAfter.Args.Event is YautjaCauldronBoilDoAfterEvent);

        Assert.Multiple(() =>
        {
            Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(15)),
                "CMSS13 cauldron do_after waits exactly 15 seconds.");
            Assert.That(active.Args.EventTarget, Is.EqualTo(cauldron),
                "The local completion event must return to the cauldron machinery.");
            Assert.That(active.Args.Target, Is.EqualTo(limb),
                "CMSS13 passes current_limb as the do_after target.");
            Assert.That(active.Args.Used, Is.Null,
                "Local Used is reserved for hand-held tools; CMSS13 passes current_limb as the do_after target and uses INTERRUPT_NONE.");
        });
    }

    private static void CancelActiveCauldronDoAfter(IEntityManager entMan, EntityUid user)
    {
        var doAfterSystem = entMan.System<SharedDoAfterSystem>();
        var doAfterComp = entMan.GetComponent<DoAfterComponent>(user);
        var active = doAfterComp.DoAfters.Values.Single(doAfter =>
            !doAfter.Cancelled &&
            !doAfter.Completed &&
            doAfter.Args.Event is YautjaCauldronBoilDoAfterEvent);

        doAfterSystem.Cancel(active.Id, doAfterComp);
    }

    private static void AssertCauldronVisual(IEntityManager entMan, EntityUid cauldron, string state)
    {
        var appearance = entMan.System<SharedAppearanceSystem>();
        Assert.That(appearance.TryGetData<string>(cauldron, YautjaCauldronVisuals.State, out var actual), Is.True,
            "Yautja cauldron runtime state must be available to GenericVisualizer.");
        Assert.That(actual, Is.EqualTo(state));
    }

    private readonly record struct Cmss13YautjaMachineRow(
        string Id,
        string Name,
        string Description,
        ResPath RsiPath,
        string IconState,
        bool Dense,
        IReadOnlyDictionary<ProtoId<MaterialPrototype>, int> MaterialStorage,
        bool FunctionalAutolathe);

    private readonly record struct Cmss13YautjaDoorControlVisualRow(
        string Id,
        string IconState);

    private readonly record struct Cmss13YautjaMonitorConsoleVisualRow(
        string Id,
        string SourcePath,
        string? Name,
        string? Description,
        string IconState,
        bool SourceHardened);

    private readonly record struct DmmBackedYautjaMachineHelperRow(
        string Id,
        string SourcePath,
        string Name,
        string Description,
        string IconState);
}
