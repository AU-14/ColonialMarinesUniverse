/*
using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Toolshed.Commands.Math;

namespace Content.Shared._CMU14.Chemistry.Reagent;


public sealed partial class EvilTricordGeneratorSystem : EntitySystem
{
    [Dependency] private ILogManager _logMan = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    private ISawmill _sawmill = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReagentGeneratorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _sawmill = _logMan.GetSawmill("reagent");
    }
    public void OnMapInit(Entity<ReagentGeneratorComponent> ent, ref MapInitEvent args)
    {
        _sawmill.Debug("ATTEMPTING TO CREATE EVIL TRICORDRAZINE");
        SequenceDataNode parents = [];
        parents.Add(new ValueDataNode("CMReagentMedicine"));
        parents.Add(new ValueDataNode("Tricordrazine"));
        MappingDataNode neogenetic = new(),
            antitoxic = new(),
            anticorrosive = new(),
            oxygenating = new();
        neogenetic.Tag = "!type:Neogenetic";
        neogenetic.Add("potency", new ValueDataNode("1"));
        antitoxic.Tag = "!type:Antitoxic";
        antitoxic.Add("potency", new ValueDataNode("1"));
        anticorrosive.Tag = "!type:Anticorrosive";
        anticorrosive.Add("potency", new ValueDataNode("1"));
        oxygenating.Tag = "!type:Oxygenating";
        oxygenating.Add("potency", new ValueDataNode("1"));
        SequenceDataNode effects = [];
        effects.Add(neogenetic);
        effects.Add(antitoxic);
        effects.Add(anticorrosive);
        effects.Add(oxygenating);
        MappingDataNode pr = new();
        pr.Add("id", new ValueDataNode("Evilcordrazine"));
        pr.Add("name", new ValueDataNode("Evil Tricordrazine"));
        pr.Add("desc", new ValueDataNode("This is like tricordrazine, but it is evil."));
        pr.Add("color", new ValueDataNode("#AA0000"));
        pr.Add("overdose", new ValueDataNode("30"));
        pr.Add("criticalOverdose", new ValueDataNode("50"));
        pr.Add("group", new ValueDataNode("Medicine"));
        pr.Add("isCM", new ValueDataNode("true"));
        pr.Add("physicalDesc", new ValueDataNode("reagent-physical-desc-opaque"));
        pr.Add("flavor", new ValueDataNode("medicine"));
        pr.Add("type", new ValueDataNode("reagent"));

        MappingDataNode medicine = [];
        medicine.Add("metabolismRate", new ValueDataNode("0.1"));
        medicine.Add("effects", effects);
        MappingDataNode metabs = [];
        metabs.Add("Medicine", medicine);
        pr.Add("metabolisms", metabs);
        pr.Add("parent", parents);
        if (_protoMan.TryLoadDynamic(pr))
        {
            _sawmill.Debug("EVIL TRICORDRAZINE SUCCESSFULLY CREATED!");
        }
        else
        {
            _sawmill.Debug("EVIL TRICORDRAZINE WAS NOT CREATED!");
        }
    }

    public void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _sawmill.Debug("ATTEMPTING TO DELETE EVIL TRICORDAZINE!");
        string literal = "Evilcordrazine";
        if (_protoMan.TryDelete<ReagentPrototype>(literal))
        {
            _sawmill.Debug("EVIL TRICORD DELETED! MAYBE?");
        }
        else
        {
            _sawmill.Debug("EVIL TRICORD DEFINITELY NOT DELETED!");
        }
    }
}
*/
