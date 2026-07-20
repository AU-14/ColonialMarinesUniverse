using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Shared.Physics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._CMU14.Chemistry;

public sealed partial class CipherHintPaperSystem : EntitySystem
{
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedReagentGeneratorSystem _gen = default!;
    [Dependency] private EntityLookupSystem _look = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private bool hasSpawnedXenoBox = false;
    public override void Initialize()
    {
        SubscribeLocalEvent<CipherHintPaperComponent, MapInitEvent>(OnMapInit, after: [
            typeof(SharedReagentGeneratorSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        hasSpawnedXenoBox = false;
    }

    private void OnMapInit(Entity<CipherHintPaperComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<PaperComponent>(ent.Owner, out var paper) && _gen.UnfoldedCombinations.ContainsKey("Ciphering"))
        {
            var ciph = _gen.UnfoldedCombinations["Ciphering"];
            string content = string.Empty;
            content += Loc.GetString("cmu-paper-ciph-hint-header") + '\n';
            content += Loc.GetString("cmu-paper-ciph-hint-subheader") + '\n';
            content += Loc.GetString("cmu-paper-ciph-hint", ("CIPH", "Ciphering"), ("A", ciph[0]), ("B", ciph[1]), ("C", ciph[2])) + '\n';
            content += Loc.GetString("cmu-paper-xeno-knowledge") + '\n';
            if (ent.Comp.SpawnCrate && !hasSpawnedXenoBox)
            {
                content += Loc.GetString("cmu-paper-xeno-sample-deliv") + '\n';
                if (!hasSpawnedXenoBox)
                {
                    var elevators = new List<Entity<RequisitionsElevatorComponent, TransformComponent>>();
                    var query = EntityQueryEnumerator<RequisitionsElevatorComponent, TransformComponent>();
                    while (query.MoveNext(out var uid, out var elevator, out var xform))
                    {
                        elevators.Add((uid, elevator, xform));
                    }
                    if (elevators.Count != 0)
                    {
                        var order = new RequisitionsEntry();
                        order.Cost = 0;
                        order.Crate = "CMUCrateSecureCipheringExperiment";
                        if (elevators.Count == 1)
                        {
                            elevators[0].Comp1.Orders.Add(order);
                        }
                        else
                        {
                            var dtcoords = _xform.GetMapCoordinates(ent.Owner);

                            Entity<RequisitionsElevatorComponent>? closest = null;
                            float closestDist = float.MaxValue;
                            foreach (var (uid, elev, xform) in elevators)
                            {
                                var elevatorCoords = _xform.GetMapCoordinates(uid, xform);
                                if (dtcoords.MapId != elevatorCoords.MapId)
                                    continue;
                                float dist = (elevatorCoords.Position - dtcoords.Position).LengthSquared();
                                if (closestDist > dist)
                                {
                                    closestDist = dist;
                                    closest = (uid, elev);
                                }
                            }
                            if (closest == null)
                            {
                                closest = elevators[0];
                            }
                            closest.Value.Comp.Orders.Add(order);
                        }
                    }
                }
            }
            content += Loc.GetString("cmu-paper-ciph-hint-footer");
            _paper.SetContent(ent.Owner, content);
        }
    }
}
