using Content.Server.Body.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Humanoid;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Metabolism;
using Content.Shared.Tag;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Synth;

public sealed partial class SynthSystem : SharedSynthSystem
{
    private const string GrenadeTag = "Grenade";

    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private Content.Shared.Body.BodySystem _body = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TagComponent, UseInHandEvent>(OnTaggedUseInHand, before: [typeof(TriggerSystem)]);
    }

    protected override void MakeSynth(Entity<SynthComponent> ent)
    {
        base.MakeSynth(ent);

        if (TryComp<DamageableComponent>(ent.Owner, out var damageable))
            _damageable.SetDamageModifierSetId(ent.Owner, ent.Comp.NewDamageModifier, damageable);

        if (TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream)) // These TryComps are so tests don't fail
        {
            // This makes it so the synth doesn't take bloodloss damage.
            _bloodstream.SetBloodLossThreshold((ent, bloodstream), 0f);
            _bloodstream.ChangeBloodReagent((ent, bloodstream), ent.Comp.NewBloodReagent);
        }

        var repOverrideComp = EnsureComp<RMCHumanoidRepresentationOverrideComponent>(ent);
        repOverrideComp.Species = ent.Comp.SpeciesName;
        Dirty(ent, repOverrideComp);

        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return;

        if (_body.TryGetOrgansWithComponent<MetabolizerComponent>((ent.Owner, body), out var metabolizers))
        {
            foreach (var organ in metabolizers)
                Del(organ); // Synths do not metabolize chems or breathe
        }

        if (_body.TryGetOrgansWithComponent<BrainComponent>((ent.Owner, body), out var brains))
        {
            foreach (var brain in brains)
                Del(brain);
        }

        var newBrain = SpawnNextToOrDrop(ent.Comp.NewBrain, ent);
        var organContainer = _container.EnsureContainer<Container>(ent.Owner, BodyComponent.ContainerID);
        if (!_container.Insert(newBrain, organContainer))
            QueueDel(newBrain);
    }

    private void OnTaggedUseInHand(Entity<TagComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<SynthComponent>(args.User))
            return;

        if (!_tags.HasTag(ent.Owner, GrenadeTag))
            return;

        if (!HasComp<TriggerOnUseComponent>(ent.Owner) || !HasComp<TimerTriggerComponent>(ent.Owner))
            return;

        DoSynthUnableToUsePopup(args.User, ent.Owner);
        args.Handled = true;
    }
}
