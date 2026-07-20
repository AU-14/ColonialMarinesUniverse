using System.Linq;
using Content.Shared.Chemistry;
using Content.Shared._RMC14.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem : EntitySystem
{
    public sealed partial class DamageableSystem : EntitySystem
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private SharedAppearanceSystem _appearance = default!;
        [Dependency] private INetManager _netMan = default!;
        [Dependency] private MobThresholdSystem _mobThreshold = default!;
        [Dependency] private IConfigurationManager _config = default!;
        [Dependency] private SharedChemistryGuideDataSystem _chemistryGuideData = default!;

        if (damageDelta != null && _appearanceQuery.TryGetComponent(ent, out var appearance))
        {
            _appearance.SetData(
                ent,
                DamageVisualizerKeys.DamageUpdateGroups,
                new DamageVisualizerGroupData(ent.Comp.DamagePerGroup.Keys.ToList()),
                appearance
            );

            if (ent.Comp.Displacement != null)
            {
                _appearance.SetData(
                    ent,
                    DamageVisualizerKeys.Displacement,
                    ent.Comp.Displacement.Value.Id,
                    appearance
                );
            }
            else
            {
                // No DamageContainerPrototype was given. So we will allow the container to support all damage types
                foreach (var type in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
                {
                    component.Damage.DamageDict.TryAdd(type.ID, FixedPoint2.Zero);
                }
            }

            component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
            component.TotalDamage = component.Damage.GetTotal();
        }

        /// <summary>
        ///     Directly sets the damage specifier of a damageable component.
        /// </summary>
        /// <remarks>
        ///     Useful for some unfriendly folk. Also ensures that cached values are updated and that a damage changed
        ///     event is raised.
        /// </remarks>
        public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
        {
            damageable.Damage = damage;
            DamageChanged(uid, damageable);
        }

        /// <summary>
        ///     Directly adds a damage specifier to a damageable component. Should rarely be used over TryChangeDamage.
        /// </summary>
        public void AddDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
        {
            damageable.Damage += damage;
            DamageChanged(uid, damageable, interruptsDoAfters: false);
        }

        /// <summary>
        ///     If the damage in a DamageableComponent was changed, this function should be called.
        /// </summary>
        /// <remarks>
        ///     This updates cached damage information, flags the component as dirty, and raises a damage changed event.
        ///     The damage changed event is used by other systems, such as damage thresholds.
        /// </remarks>
        public void DamageChanged(EntityUid uid, DamageableComponent component, DamageSpecifier? damageDelta = null,
            bool interruptsDoAfters = true, EntityUid? origin = null, EntityUid? tool = null)
        {
            component.Damage.GetDamagePerGroup(_prototypeManager, component.DamagePerGroup);
            component.TotalDamage = component.Damage.GetTotal();
            Dirty(uid, component);

            if (_appearanceQuery.TryGetComponent(uid, out var appearance) && damageDelta != null)
            {
                var data = new DamageVisualizerGroupData(component.DamagePerGroup.Keys.ToList());
                _appearance.SetData(uid, DamageVisualizerKeys.DamageUpdateGroups, data, appearance);
            }
            RaiseLocalEvent(uid, new DamageChangedEvent(component, damageDelta, interruptsDoAfters, origin, tool));
        }

        /// <summary>
        ///     Applies damage specified via a <see cref="DamageSpecifier"/>.
        /// </summary>
        /// <remarks>
        ///     <see cref="DamageSpecifier"/> is effectively just a dictionary of damage types and damage values. This
        ///     function just applies the container's resistances (unless otherwise specified) and then changes the
        ///     stored damage data. Division of group damage into types is managed by <see cref="DamageSpecifier"/>.
        /// </remarks>
        /// <returns>
        ///     Returns a <see cref="DamageSpecifier"/> with information about the actual damage changes. This will be
        ///     null if the user had no applicable components that can take damage.
        /// </returns>
        public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
            bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, EntityUid? tool = null, int armorPiercing = 0, bool shouldIgnoreClawLogic = false)
        {
            if (!uid.HasValue || !_damageableQuery.Resolve(uid.Value, ref damageable, false))
            {
                // TODO BODY SYSTEM pass damage onto body system
                return null;
            }

            if (damage.Empty)
            {
                return damage;
            }

            var before = new BeforeDamageChangedEvent(damage, origin, tool); //RMC14, added a parameter
            RaiseLocalEvent(uid.Value, ref before);

            if (before.Cancelled)
                return null;

            // Apply resistances
            if (!ignoreResistances)
            {
                if (damageable.DamageModifierSetId != null &&
                    _prototypeManager.TryIndex<DamageModifierSetPrototype>(damageable.DamageModifierSetId, out var modifierSet))
                {
                    // TODO DAMAGE PERFORMANCE
                    // use a local private field instead of creating a new dictionary here..
                    damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
                }

                var ev = new DamageModifyEvent(damage, origin, tool, armorPiercing, shouldIgnoreClawLogic);
                RaiseLocalEvent(uid.Value, ev);
                damage = ev.Damage;

                if (damage.Empty)
                {
                    return damage;
                }
            }

            var evd = new DamageModifyAfterResistEvent(damage, origin, tool);
            RaiseLocalEvent(uid.Value, evd);
            damage = evd.Damage;

            if (damage.Empty)
            {
                return damage;
            }

            damage = ApplyUniversalAllModifiers(damage);

            // TODO DAMAGE PERFORMANCE
            // Consider using a local private field instead of creating a new dictionary here.
            // Would need to check that nothing ever tries to cache the delta.
            var delta = new DamageSpecifier();
            delta.DamageDict.EnsureCapacity(damage.DamageDict.Count);

            var dict = damageable.Damage.DamageDict;
            foreach (var (type, value) in damage.DamageDict)
            {
                // CollectionsMarshal my beloved.
                if (!dict.TryGetValue(type, out var oldValue))
                    continue;

                var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
                if (newValue == oldValue)
                    continue;

                dict[type] = newValue;
                delta.DamageDict[type] = newValue - oldValue;
            }

            if (delta.DamageDict.Count > 0)
                DamageChanged(uid.Value, damageable, delta, interruptsDoAfters, origin, tool);

            return delta;
        }

        /// <summary>
        /// Applies the two univeral "All" modifiers, if set.
        /// Individual damage source modifiers are set in their respective code.
        /// </summary>
        /// <param name="damage">The damage to be changed.</param>
        public DamageSpecifier ApplyUniversalAllModifiers(DamageSpecifier damage)
        {
            // Checks for changes first since they're unlikely in normal play.
            if (UniversalAllDamageModifier == 1f && UniversalAllHealModifier == 1f)
                return damage;

            foreach (var (key, value) in damage.DamageDict)
            {
                if (value == 0)
                    continue;

                if (value > 0)
                {
                    damage.DamageDict[key] *= UniversalAllDamageModifier;
                    continue;
                }

                if (value < 0)
                {
                    damage.DamageDict[key] *= UniversalAllHealModifier;
                }
            }

            return damage;
        }

        /// <summary>
        ///     Sets all damage types supported by a <see cref="DamageableComponent"/> to the specified value.
        /// </summary>
        /// <remakrs>
        ///     Does nothing If the given damage value is negative.
        /// </remakrs>
        public void SetAllDamage(EntityUid uid, DamageableComponent component, FixedPoint2 newValue)
        {
            if (newValue < 0)
            {
                // invalid value
                return;
            }

            foreach (var type in component.Damage.DamageDict.Keys)
            {
                component.Damage.DamageDict[type] = newValue;
            }

            // Setting damage does not count as 'dealing' damage, even if it is set to a larger value, so we pass an
            // empty damage delta.
            DamageChanged(uid, component, new DamageSpecifier());
        }

        public void SetDamageModifierSetId(EntityUid uid, string? damageModifierSetId, DamageableComponent? comp = null)
        {
            if (!_damageableQuery.Resolve(uid, ref comp))
                return;

            comp.DamageModifierSetId = damageModifierSetId;
            Dirty(uid, comp);
        }

        private void DamageableGetState(EntityUid uid, DamageableComponent component, ref ComponentGetState args)
        {
            if (_netMan.IsServer)
            {
                args.State = new DamageableComponentState(component.Damage.DamageDict, component.DamageContainerID, component.DamageModifierSetId, component.HealthBarThreshold);
            }
            else
            {
                // avoid mispredicting damage on newly spawned entities.
                args.State = new DamageableComponentState(component.Damage.DamageDict.ShallowClone(), component.DamageContainerID, component.DamageModifierSetId, component.HealthBarThreshold);
            }
        }

        // TODO DAMAGE
        // byref struct event.
        RaiseLocalEvent(ent, new DamageChangedEvent(ent.Comp, damageDelta, interruptsDoAfters, origin));
    }

    /// <summary>
    /// Goes through an entity damage's and saves them inside a dictionary if the value is higher than 0
    /// The dictionary is structured with a string for the name of the damage type, and a FixedPoint2 for the numeric damage value
    /// </summary>
    [ByRefEvent]
    public record struct BeforeDamageChangedEvent(DamageSpecifier Damage, EntityUid? Origin = null, EntityUid? Source = null, bool Cancelled = false); //RMC14

    /// <summary>
    ///     Raised on an entity when damage is about to be dealt,
    ///     in case anything else needs to modify it other than the base
    ///     damageable component.
    ///
    ///     For example, armor.
    /// </summary>
    public sealed class DamageModifyEvent : EntityEventArgs, IInventoryRelayEvent
    {
        var damageTypes = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>();

        public readonly DamageSpecifier OriginalDamage;
        public DamageSpecifier Damage;
        public EntityUid? Origin;
        public EntityUid? Tool;
        public int ArmorPiercing;
        public bool ShouldIgnoreClawLogic;

        public DamageModifyEvent(DamageSpecifier damage, EntityUid? origin = null, EntityUid? tool = null, int armorPiercing = 0, bool shouldIgnoreClawLogic = false)
        {
            OriginalDamage = damage;
            Damage = damage;
            Origin = origin;
            Tool = tool;
            ArmorPiercing = armorPiercing;
            ShouldIgnoreClawLogic = shouldIgnoreClawLogic;
        }
    }

    public sealed class DamageChangedEvent : EntityEventArgs
    {
        /// <summary>
        ///     This is the component whose damage was changed.
        /// </summary>
        /// <remarks>
        ///     Given that nearly every component that cares about a change in the damage, needs to know the
        ///     current damage values, directly passing this information prevents a lot of duplicate
        ///     Owner.TryGetComponent() calls.
        /// </remarks>
        public readonly DamageableComponent Damageable;

        /// <summary>
        ///     The amount by which the damage has changed. If the damage was set directly to some number, this will be
        ///     null.
        /// </summary>
        public readonly DamageSpecifier? DamageDelta;

        /// <summary>
        ///     Was any of the damage change dealing damage, or was it all healing?
        /// </summary>
        public readonly bool DamageIncreased;

        /// <summary>
        ///     Does this event interrupt DoAfters?
        ///     Note: As provided in the constructor, this *does not* account for DamageIncreased.
        ///     As written into the event, this *does* account for DamageIncreased.
        /// </summary>
        public readonly bool InterruptsDoAfters;

        /// <summary>
        ///     Contains the entity which caused the change in damage, if any was responsible.
        /// </summary>
        public readonly EntityUid? Origin;

        public readonly EntityUid? Tool;

        public DamageChangedEvent(DamageableComponent damageable, DamageSpecifier? damageDelta, bool interruptsDoAfters, EntityUid? origin, EntityUid? tool)
        {
            Damageable = damageable;
            DamageDelta = damageDelta;
            Origin = origin;
            Tool = tool;

            if (DamageDelta == null)
                return;

            foreach (var damageChange in DamageDelta.DamageDict.Values)
            {
                if (!damage.DamageDict.TryGetValue(type, out var damageValue) || damageValue == 0) //get value and make sure it isn't 0
                    continue;

                damageTypes.Add(type, damageValue);
            }
        }
        return damageTypes;
    }
}
