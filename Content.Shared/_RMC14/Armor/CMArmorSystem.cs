using System.Linq;
using System.Text;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Slowing;
using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Body.Part;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Preferences;
using Content.Shared.Rounding;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Armor;

public sealed partial class CMArmorSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISerializationManager _serializationManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private static readonly ProtoId<DamageGroupPrototype> ArmorGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BioGroup = "Burn";
    private static readonly int MaxXenoArmor = 55;

    private EntityQuery<RMCAllowSuitStorageUserWhitelistComponent> _rmcAllowSuitStorageUserWhitelistQuery;

    public override void Initialize()
    {
        _rmcAllowSuitStorageUserWhitelistQuery = GetEntityQuery<RMCAllowSuitStorageUserWhitelistComponent>();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        SubscribeLocalEvent<CMArmorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMArmorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMArmorComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<CMArmorComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<CMArmorComponent, InventoryRelayedEvent<CMGetArmorEvent>>(OnGetArmorRelayed);
        SubscribeLocalEvent<CMArmorComponent, InventoryRelayedEvent<GetExplosionResistanceEvent>>(OnGetExplosionResistanceRelayed);
        SubscribeLocalEvent<CMArmorComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
        SubscribeLocalEvent<CMArmorComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<CMArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
        SubscribeLocalEvent<CMArmorComponent, ExaminedEvent>(OnArmorExamined);

        SubscribeLocalEvent<CMHardArmorComponent, InventoryRelayedEvent<HitBySlowingSpitEvent>>(OnArmorHitBySlowingSpit);
        SubscribeLocalEvent<CMHardArmorComponent, InventoryRelayedEvent<CMSurgeryCanPerformStepEvent>>(OnArmorCanPerformStep);

        SubscribeLocalEvent<InventoryComponent, CMSurgeryCanPerformStepEvent>(_inventory.RelayEvent);

        SubscribeLocalEvent<CMArmorUserComponent, DamageModifyEvent>(OnUserDamageModify);

        SubscribeLocalEvent<CMArmorPiercingComponent, CMGetArmorPiercingEvent>(OnPiercingGetArmor);

        SubscribeLocalEvent<InventoryComponent, CMGetArmorEvent>(_inventory.RelayEvent);

        SubscribeLocalEvent<ClothingBlockBackpackComponent, BeingEquippedAttemptEvent>(OnBlockBackpackEquippedAttempt);
        SubscribeLocalEvent<ClothingBlockBackpackComponent, InventoryRelayedEvent<RMCEquipAttemptEvent>>(OnBlockBackpackEquipAttempt);

        SubscribeLocalEvent<ClothingComponent, BeingEquippedAttemptEvent>(OnClothingEquippedAttempt);

        SubscribeLocalEvent<RMCArmorSpeedTierComponent, GotEquippedEvent>(OnArmorSpeedTierGotEquipped);
        SubscribeLocalEvent<RMCArmorSpeedTierComponent, GotUnequippedEvent>(OnArmorSpeedTierGotUnequipped);
        SubscribeLocalEvent<RMCArmorSpeedTierComponent, InventoryRelayedEvent<RefreshArmorSpeedTierEvent>>(OnRefreshArmorSpeedTier);

        SubscribeLocalEvent<InventoryComponent, RMCEquipAttemptEvent>(_inventory.RelayEvent);
        SubscribeLocalEvent<InventoryComponent, RefreshArmorSpeedTierEvent>(_inventory.RelayEvent);

        SubscribeLocalEvent<RMCAllowSuitStorageUserWhitelistComponent, GotEquippedEvent>(OnAllowSuitStorageUserWhitelistGotEquipped);
        SubscribeLocalEvent<RMCAllowSuitStorageUserWhitelistComponent, GotUnequippedEvent>(OnAllowSuitStorageUserWhitelistGotUnequipped);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!TryComp(ev.Mob, out InventoryComponent? inventory))
            return;

        var slots = _inventory.GetSlotEnumerator((ev.Mob, inventory));
        while (slots.MoveNext(out var slot))
        {
            if (!_rmcAllowSuitStorageUserWhitelistQuery.TryComp(slot.ContainedEntity, out var whitelist))
                continue;

            OnAllowSuitStorageWhitelistEquipped((slot.ContainedEntity.Value, whitelist), ev.Mob);
        }
    }

    private void OnMapInit(Entity<CMArmorComponent> armored, ref MapInitEvent args)
    {
        UpdateArmorValue((armored, armored.Comp));
    }

    public void UpdateArmorValue(Entity<CMArmorComponent?> armored)
    {
        if (!Resolve(armored, ref armored.Comp, false))
            return;

        if (!TryComp<XenoComponent>(armored, out var xeno))
            return;

        var ev = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
        RaiseLocalEvent(armored, ref ev);
        var armorMessage = ev.FrontalArmor == 0 &&
                           ev.SideArmor == 0 &&
                           armored.Comp.FrontalArmor == 0 &&
                           armored.Comp.SideArmor == 0
            ? $"{FixedPoint2.New(ev.XenoArmor * ev.ArmorModifier)} / {armored.Comp.XenoArmor}"
            : $"Overall: {FixedPoint2.New(ev.XenoArmor * ev.ArmorModifier)} / {armored.Comp.XenoArmor}";

        if (armored.Comp.FrontalArmor != 0 || ev.FrontalArmor != 0)
            armorMessage = $"{armorMessage}\nFrontal: {FixedPoint2.New((ev.XenoArmor + ev.FrontalArmor) * ev.ArmorModifier)} / {armored.Comp.XenoArmor + armored.Comp.FrontalArmor}";

        if (armored.Comp.SideArmor != 0 || ev.SideArmor != 0)
            armorMessage = $"{armorMessage}\nSide: {FixedPoint2.New((ev.XenoArmor + ev.SideArmor) * ev.ArmorModifier)} / {armored.Comp.XenoArmor + armored.Comp.SideArmor}";

        var max = _alerts.GetMaxSeverity(xeno.ArmorAlert);

        var severity = max - ContentHelpers.RoundToLevels(ev.XenoArmor * ev.ArmorModifier, MaxXenoArmor, max + 1);
        _alerts.ShowAlert(armored, xeno.ArmorAlert, (short)severity, dynamicMessage: armorMessage);
    }

    private void OnRemove(Entity<CMArmorComponent> armored, ref ComponentRemove args)
    {
        if (TryComp(armored, out XenoComponent? xeno))
            _alerts.ClearAlert(armored, xeno.ArmorAlert);
    }

    private void OnDamageModify(Entity<CMArmorComponent> armored, ref DamageModifyEvent args)
    {
        ModifyDamage(armored, ref args);
    }

    private void OnGetArmor(Entity<CMArmorComponent> armored, ref CMGetArmorEvent args)
    {
        if (!ShouldApplyArmor(armored.Comp, args.TargetPart, args.TargetZone))
            return;

        args.ExplosionArmor += armored.Comp.ExplosionArmor;
        args.FrontalArmor += armored.Comp.FrontalArmor;
        args.SideArmor += armored.Comp.SideArmor;

        if (HasComp<XenoComponent>(armored))
        {
            args.XenoArmor += armored.Comp.XenoArmor;
        }
        else
        {
            args.Melee += armored.Comp.Melee;
            args.Bullet += armored.Comp.Bullet;
            args.Bio += armored.Comp.Bio;
        }
    }

    private void OnGetArmorRelayed(Entity<CMArmorComponent> armored, ref InventoryRelayedEvent<CMGetArmorEvent> args)
    {
        if (!ShouldApplyArmor(armored.Comp, args.Args.TargetPart, args.Args.TargetZone))
            return;

        args.Args.ExplosionArmor += armored.Comp.ExplosionArmor;
        args.Args.FrontalArmor += armored.Comp.FrontalArmor;
        args.Args.SideArmor += armored.Comp.SideArmor;

        if (HasComp<XenoComponent>(armored))
        {
            args.Args.XenoArmor += armored.Comp.XenoArmor;
        }
        else
        {
            args.Args.Melee += armored.Comp.Melee;
            args.Args.Bullet += armored.Comp.Bullet;
            args.Args.Bio += armored.Comp.Bio;
        }
    }

    private void OnGetExplosionResistanceRelayed(Entity<CMArmorComponent> ent, ref InventoryRelayedEvent<GetExplosionResistanceEvent> args)
    {
        var armor = ent.Comp.ExplosionArmor;
        if (armor <= 0)
            return;

        var resist = (float) Math.Pow(1.1, armor / 5.0);
        args.Args.DamageCoefficient /= resist;
    }

    private void OnGetExplosionResistance(Entity<CMArmorComponent> armored, ref GetExplosionResistanceEvent args)
    {
        var armor = armored.Comp.ExplosionArmor;
        if (armor <= 0)
            return;

        var resist = (float) Math.Pow(1.1, armor / 5.0);
        args.DamageCoefficient /= resist;
    }

    private void OnGotEquipped(Entity<CMArmorComponent> armored, ref GotEquippedEvent args)
    {
        EnsureComp<CMArmorUserComponent>(args.Equipee);
    }

    private void OnArmorVerbExamine(EntityUid uid, CMArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || HasComp<XenoComponent>(uid))
            return;

        var examineMarkup = GetArmorExamine((uid, component));

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/Actions/actions_fakemindshield.rsi/icon-on.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private void OnArmorExamined(Entity<CMArmorComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Examined))
            return;

        using (args.PushGroup(nameof(CMArmorSystem), -10))
        {
            var armorRatings = new[]
            {
                ("rmc-examine-armor-xeno", ent.Comp.XenoArmor),
                ("rmc-examine-armor-xeno-frontal", ent.Comp.FrontalArmor),
                ("rmc-examine-armor-xeno-side", ent.Comp.SideArmor),
                ("rmc-examine-armor-xeno-explosion", ent.Comp.ExplosionArmor),
            };

            var examine = new StringBuilder();
            foreach (var (locId, value) in armorRatings)
            {
                if (value == 0)
                    continue;

                examine.AppendLine(Loc.GetString(locId, ("armor", value)));
            }

            if (examine.Length == 0)
                return;

            // Last 1 is for sandboxing, see https://github.com/space-wizards/RobustToolbox/pull/5955
            examine.Insert(0, $"{Loc.GetString("rmc-examine-armor-xeno-header", ("xeno", ent))}\n", 1);
            args.AddMarkup(examine.ToString());
        }
    }

    private void OnArmorHitBySlowingSpit(Entity<CMHardArmorComponent> ent, ref InventoryRelayedEvent<HitBySlowingSpitEvent> args)
    {
        args.Args.Cancelled = true;
    }

    private void OnArmorCanPerformStep(Entity<CMHardArmorComponent> ent, ref InventoryRelayedEvent<CMSurgeryCanPerformStepEvent> args)
    {
        if (args.Args.Invalid == StepInvalidReason.None)
            args.Args.Invalid = StepInvalidReason.Armor;
    }

    private void OnUserDamageModify(Entity<CMArmorUserComponent> ent, ref DamageModifyEvent args)
    {
        ModifyDamage(ent, ref args);
    }

    private void OnPiercingGetArmor(Entity<CMArmorPiercingComponent> piercing, ref CMGetArmorPiercingEvent args)
    {
        args.Piercing += piercing.Comp.Amount;
    }

    private void OnBlockBackpackEquippedAttempt(Entity<ClothingBlockBackpackComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var slots = _inventory.GetSlotEnumerator(args.EquipTarget, SlotFlags.BACK);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity == null)
                continue;

            if (HasComp<ClothingIgnoreBlockBackpackComponent>(slot.ContainedEntity))
                return;

            args.Cancel();
            args.Reason = "rmc-block-backpack-cant-other";
            break;
        }
    }

    private void OnBlockBackpackEquipAttempt(Entity<ClothingBlockBackpackComponent> ent, ref InventoryRelayedEvent<RMCEquipAttemptEvent> args)
    {
        ref readonly var ev = ref args.Args.Event;
        if (ev.Cancelled)
            return;

        if (HasComp<ClothingIgnoreBlockBackpackComponent>(args.Args.Event.Equipment))
            return;

        if ((ev.SlotFlags & SlotFlags.BACK) == 0)
            return;

        ev.Cancel();
        ev.Reason = "rmc-block-backpack-cant-backpack";
    }

    private void OnClothingEquippedAttempt(Entity<ClothingComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var ev = new RMCEquipAttemptEvent(args, SlotFlags.All);
        RaiseLocalEvent(args.EquipTarget, ref ev);
    }

    private void ModifyDamage(EntityUid ent, ref DamageModifyEvent args)
    {
        var ev = new CMGetArmorEvent(args.TargetSlots, TargetPart: args.TargetPart, TargetZone: args.TargetZone);
        RaiseLocalEvent(ent, ref ev);

        var armorPiercing = args.ArmorPiercing;
        if (args.Tool is { } tool && Exists(tool))
        {
            var piercingEv = new CMGetArmorPiercingEvent(ent);
            RaiseLocalEvent(tool, ref piercingEv);
            armorPiercing += piercingEv.Piercing;
        }

        var immuneToAP = TryComp<CMArmorComponent>(ent, out var armorComp) && armorComp.ImmuneToAP;
        if (HasComp<XenoComponent>(ent))
        {
            ev.XenoArmor = (int)(ev.XenoArmor * ev.ArmorModifier);
            if (!immuneToAP)
                ev.XenoArmor -= armorPiercing;
        }
        else
        {
            ev.Melee = (int)(ev.Melee * ev.ArmorModifier);
            ev.Bullet = (int)(ev.Bullet * ev.ArmorModifier);

            if (!immuneToAP)
            {
                ev.Melee -= armorPiercing;
                ev.Bullet -= armorPiercing;
                ev.Bio -= armorPiercing;
            }
        }

        if (args.Origin is { } origin && Exists(origin))
        {
            var originCoords = _transform.GetMapCoordinates(origin);
            var armorCoords = _transform.GetMapCoordinates(ent);

            if (originCoords.MapId == armorCoords.MapId)
            {
                var diff = (originCoords.Position - armorCoords.Position).ToWorldAngle().GetCardinalDir();
                var dir = _transform.GetWorldRotation(ent).GetCardinalDir();
                if (dir == diff)
                {
                    ev.XenoArmor += ev.FrontalArmor;
                }
                else
                {
                    var perpendiculars = diff.GetPerpendiculars();
                    if (dir == perpendiculars.First || dir == perpendiculars.Second)
                        ev.XenoArmor += ev.SideArmor;
                }
            }
        }

        //Default modifier
        var mod = EnsureComp<RMCArmorModifierComponent>(ent);

        args.Damage = new DamageSpecifier(args.Damage);
        if (!HasComp<XenoComponent>(ent))
        {
            if (HasComp<RMCBulletComponent>(args.Tool))
            {
                Resist(args.Damage, ev.Bullet, ArmorGroup, mod.RangedArmorModifier);
            }
            else if (HasComp<MeleeWeaponComponent>(args.Tool))
            {
                Resist(args.Damage, ev.Melee, ArmorGroup, mod.MeleeArmorModifier);
            }
            Resist(args.Damage, ev.Bio, BioGroup, mod.RangedArmorModifier);
        }
        else
        {
            Resist(args.Damage, ev.XenoArmor, ArmorGroup, mod.RangedArmorModifier);
        }
    }

    private static bool ShouldApplyArmor(CMArmorComponent armor, BodyPartType? targetPart, TargetBodyZone? targetZone)
    {
        if (armor.CoveredZones.Count == 0)
            return true;

        if (targetZone is { } zone)
            return armor.CoveredZones.Contains(zone);

        if (targetPart is not { } part)
            return true;

        foreach (var covered in armor.CoveredZones)
        {
            if (SharedBodyZoneTargetingSystem.ToBodyPart(covered).Type == part)
                return true;
        }

        return false;
    }

    private void Resist(DamageSpecifier damage, int armor, ProtoId<DamageGroupPrototype> group, int mult)
    {
        armor = Math.Max(armor, 0);
        if (armor <= 0)
            return;

        var resist = Math.Pow(1.1, armor / 5.0);
        var types = _prototypes.Index(group).DamageTypes;

        foreach (var type in types)
        {
            if (damage.DamageDict.TryGetValue(type, out var amount) &&
                amount > FixedPoint2.Zero)
            {
                damage.DamageDict[type] = amount / resist;
            }
        }

        var newDamage = damage.GetTotal();
        if (newDamage != FixedPoint2.Zero && newDamage < armor * 2)
        {

            var damageWithArmor = FixedPoint2.Max(0, newDamage * mult - armor);

            foreach (var type in types)
            {
                if (damage.DamageDict.TryGetValue(type, out var amount) &&
                    amount > FixedPoint2.Zero)
                {
                    damage.DamageDict[type] = amount * damageWithArmor / (newDamage * mult);
                }
            }
        }
    }

    public void SetArmorPiercing(Entity<CMArmorPiercingComponent> ent, int amount)
    {
        ent.Comp.Amount = amount;
        Dirty(ent);
    }

    public EntProtoId GetArmorVariant(Entity<RMCArmorVariantComponent> ent, ArmorPreference preference)
    {
        var comp = ent.Comp;
        var equipmentEntityID = comp.DefaultType;

        if (comp.Types.TryGetValue(preference.ToString(), out var equipment))
            equipmentEntityID = equipment;

        if (preference == ArmorPreference.Random)
        {
            var random = new System.Random();
            var randomType = comp.Types.ElementAt(random.Next(0, comp.Types.Count)).Value;
            equipmentEntityID = randomType;
        }

        return equipmentEntityID;
    }

    private void OnArmorSpeedTierGotEquipped(Entity<RMCArmorSpeedTierComponent> armour, ref GotEquippedEvent args)
    {
        EnsureComp(args.Equipee, out RMCArmorSpeedTierUserComponent comp);

        RefreshArmorSpeedTier((args.Equipee, comp));
    }

    private void OnArmorSpeedTierGotUnequipped(Entity<RMCArmorSpeedTierComponent> armour, ref GotUnequippedEvent args)
    {
        EnsureComp(args.Equipee, out RMCArmorSpeedTierUserComponent comp);

        RefreshArmorSpeedTier((args.Equipee, comp));
    }

    private void RefreshArmorSpeedTier(Entity<RMCArmorSpeedTierUserComponent> user)
    {
        var ev = new RefreshArmorSpeedTierEvent(~SlotFlags.POCKET);
        RaiseLocalEvent(user.Owner, ref ev);

        user.Comp.SpeedTier = ev.SpeedTier;

        var speed = user.Comp.SpeedTier switch
        {
            "light" => 0.483f,
            "medium" => 0.526f,
            "heavy" => 0.565f,
            _ => 0.35f,
        };

        if (!TryComp(user, out MobCollisionComponent? mobCollision))
            return;

        mobCollision.MinimumSpeedModifier = speed;
        Dirty(user, mobCollision);
    }

    private void OnRefreshArmorSpeedTier(Entity<RMCArmorSpeedTierComponent> armor, ref InventoryRelayedEvent<RefreshArmorSpeedTierEvent> args)
    {
        args.Args.SpeedTier = armor.Comp.SpeedTier;
    }

    private void OnAllowSuitStorageUserWhitelistGotEquipped(Entity<RMCAllowSuitStorageUserWhitelistComponent> ent, ref GotEquippedEvent args)
    {
        OnAllowSuitStorageWhitelistEquipped(ent, args.Equipee);
    }

    private void OnAllowSuitStorageUserWhitelistGotUnequipped(Entity<RMCAllowSuitStorageUserWhitelistComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var comp = EnsureComp<AllowSuitStorageComponent>(ent);
        comp.Whitelist = _serializationManager.CreateCopy(ent.Comp.DefaultWhitelist, notNullableOverride: true);
        Dirty(ent, comp);
    }

    private void OnAllowSuitStorageWhitelistEquipped(Entity<RMCAllowSuitStorageUserWhitelistComponent> ent, EntityUid user)
    {
        if (_timing.ApplyingState)
            return;

        if (!_entityWhitelist.IsWhitelistPass(ent.Comp.User, user))
        {
            var comp = EnsureComp<AllowSuitStorageComponent>(ent);
            comp.Whitelist = _serializationManager.CreateCopy(ent.Comp.DefaultWhitelist, notNullableOverride: true);
            Dirty(ent, comp);
            return;
        }

        if (!_prototypes.TryIndex(ent.Comp.AllowedWhitelist, out var allowed))
            return;

        EntityManager.AddComponents(ent, allowed);
    }

    private FormattedMessage GetArmorExamine(Entity<CMArmorComponent> armored)
    {
        var armorComponent = armored.Comp;
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        var armorRatings = new[]
        {
            (Loc.GetString("rmc-armor-melee"), armorComponent.Melee),
            (Loc.GetString("rmc-armor-bullet"), armorComponent.Bullet),
            (Loc.GetString("rmc-armor-bio"), armorComponent.Bio),
            (Loc.GetString("rmc-armor-explosion-armor"), armorComponent.ExplosionArmor),
        };

        foreach (var (text, value) in armorRatings)
        {
            if (value == 0)
                continue;
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString(
                "rmc-examine-armor",
                ("text", text),
                ("value", value)
            ));
        }

        if (armorComponent.ImmuneToAP)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("rmc-examine-armor-piercing-immune"));
        }

        var coveredZones = GetArmorCoveredZones(armored);
        if (coveredZones.Count > 0)
        {
            var parts = string.Join(", ", coveredZones.Select(GetArmorZoneName));
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("rmc-examine-armor-covers", ("parts", parts)));
        }

        return msg;
    }

    private List<TargetBodyZone> GetArmorCoveredZones(Entity<CMArmorComponent> armored)
    {
        var zones = new List<TargetBodyZone>();
        if (armored.Comp.CoveredZones.Count > 0)
        {
            AddCoveredZones(zones, armored.Comp.CoveredZones);
            return zones;
        }

        if (!TryComp<ClothingComponent>(armored, out var clothing))
            return zones;

        AddSlotCoveredZones(zones, clothing.Slots);
        return zones;
    }

    private static void AddSlotCoveredZones(List<TargetBodyZone> zones, SlotFlags slots)
    {
        if ((slots & SlotFlags.HEAD) != 0)
            AddCoveredZone(zones, TargetBodyZone.Head);

        if ((slots & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING)) != 0)
            AddCoveredZones(zones, TargetBodyZone.Chest, TargetBodyZone.GroinPelvis);

        if ((slots & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING)) != 0)
            AddCoveredZones(zones, TargetBodyZone.LeftArm, TargetBodyZone.RightArm);

        if ((slots & SlotFlags.GLOVES) != 0)
            AddCoveredZones(zones, TargetBodyZone.LeftHand, TargetBodyZone.RightHand);

        if ((slots & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING | SlotFlags.LEGS)) != 0)
            AddCoveredZones(zones, TargetBodyZone.LeftLeg, TargetBodyZone.RightLeg);

        if ((slots & SlotFlags.FEET) != 0)
            AddCoveredZones(zones, TargetBodyZone.LeftFoot, TargetBodyZone.RightFoot);
    }

    private static void AddCoveredZones(List<TargetBodyZone> zones, params TargetBodyZone[] additions)
    {
        foreach (var zone in additions)
        {
            AddCoveredZone(zones, zone);
        }
    }

    private static void AddCoveredZones(List<TargetBodyZone> zones, IEnumerable<TargetBodyZone> additions)
    {
        foreach (var zone in additions)
        {
            AddCoveredZone(zones, zone);
        }
    }

    private static void AddCoveredZone(List<TargetBodyZone> zones, TargetBodyZone zone)
    {
        if (!zones.Contains(zone))
            zones.Add(zone);
    }

    private string GetArmorZoneName(TargetBodyZone zone) => zone switch
    {
        TargetBodyZone.Head => Loc.GetString("rmc-armor-zone-head"),
        TargetBodyZone.Chest => Loc.GetString("rmc-armor-zone-chest"),
        TargetBodyZone.GroinPelvis => Loc.GetString("rmc-armor-zone-groin-pelvis"),
        TargetBodyZone.LeftArm => Loc.GetString("rmc-armor-zone-left-arm"),
        TargetBodyZone.RightArm => Loc.GetString("rmc-armor-zone-right-arm"),
        TargetBodyZone.LeftHand => Loc.GetString("rmc-armor-zone-left-hand"),
        TargetBodyZone.RightHand => Loc.GetString("rmc-armor-zone-right-hand"),
        TargetBodyZone.LeftLeg => Loc.GetString("rmc-armor-zone-left-leg"),
        TargetBodyZone.RightLeg => Loc.GetString("rmc-armor-zone-right-leg"),
        TargetBodyZone.LeftFoot => Loc.GetString("rmc-armor-zone-left-foot"),
        TargetBodyZone.RightFoot => Loc.GetString("rmc-armor-zone-right-foot"),
        _ => Loc.GetString("rmc-armor-zone-chest"),
    };
}
