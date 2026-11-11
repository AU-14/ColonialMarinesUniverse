using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Round.Antags;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;

namespace Content.Server.CMU14.Round.Antags;

public sealed partial class SynthGunAccessSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private SharedSynthSystem _synth = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthGunAccessComponent, ComponentStartup>(OnGunAccessStartup);

        // any antagonist synth may fire guns, however it became one: antag selection, an admin, a
        // subversion (INSFOR) key; a reset key that clears the role takes the guns back
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleChanged);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleChanged);
        SubscribeLocalEvent<SynthComponent, MindAddedMessage>(OnSynthMindAdded);
        // the Early Warning Joe never fires guns, even if its role arrived before its component
        SubscribeLocalEvent<EWJoeComponent, ComponentStartup>(OnJoeStartup);
    }

    private void OnGunAccessStartup(Entity<SynthGunAccessComponent> ent, ref ComponentStartup args)
    {
        // the specifier's grant is permanent; it is no longer one this system may take back
        RemComp<SynthAntagGunsGrantedComponent>(ent);

        if (!TryComp<SynthComponent>(ent, out var synth) || synth.CanUseGuns)
            return;

        SetGuns((ent, synth), true);
    }

    private void OnRoleChanged(RoleEvent args)
    {
        if (args.Mind.OwnedEntity is { } body)
            Refresh(body);
    }

    private void OnSynthMindAdded(Entity<SynthComponent> ent, ref MindAddedMessage args)
    {
        Refresh(ent);
    }

    private void OnJoeStartup(Entity<EWJoeComponent> ent, ref ComponentStartup args)
    {
        Refresh(ent);
    }

    /// <summary>
    /// Lifts the gun restriction of a synth whose mind holds an antagonist role, except an Early Warning
    /// Joe's, and restores it once that is no longer so. Only a restriction this system lifted is restored.
    /// </summary>
    public void Refresh(EntityUid body)
    {
        if (!TryComp<SynthComponent>(body, out var synth))
            return;

        // a Joe that is standing down (its component going away) is a Joe no more
        var joe = TryComp(body, out EWJoeComponent? ewJoe) && ewJoe.LifeStage < ComponentLifeStage.Stopping;
        var antag = !joe
            && _mind.TryGetMind(body, out var mindId, out _)
            && !(ewJoe == null && _role.MindHasRole<EWJoeRoleComponent>(mindId))
            && _role.MindIsAntagonist(mindId);

        if (antag && !synth.CanUseGuns)
        {
            SetGuns((body, synth), true);
            EnsureComp<SynthAntagGunsGrantedComponent>(body);
        }
        else if (!antag && HasComp<SynthAntagGunsGrantedComponent>(body) && !HasComp<SynthGunAccessComponent>(body))
        {
            SetGuns((body, synth), false);
            RemComp<SynthAntagGunsGrantedComponent>(body);
        }
    }

    /// <summary>
    /// The shared setter only marks the entity dirty, which does not resend the synth component; the
    /// client predicts shots from it, so it is dirtied here.
    /// </summary>
    private void SetGuns(Entity<SynthComponent> synth, bool value)
    {
        _synth.SetGunRestriction(synth, value);
        Dirty(synth);
    }
}
