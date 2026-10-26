using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Weapons.SerialNumber;

/// <summary>
///     Stamps every handheld gun with a serial number, like "KR-482193", and adds a serial number examine window to
///     read it. Numbers aren't repeated within a round. A blade scratches the number off, or a lit blowtorch burns it
///     off faster. Each gun also gets a firing-pin signature that can't be scratched off: a forensic scanner reads it off
///     the gun and off every casing the gun ejects.
/// </summary>
public sealed partial class CMUGunSerialNumberSystem : EntitySystem
{
    private const string Letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
    private const string Hex = "0123456789ABCDEF";

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly string[] SharpDamage = ["Slash", "Piercing"];

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<string> _used = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _used.Clear());
        SubscribeLocalEvent<GunComponent, CMUGunMapInitEvent>(OnGunMapInit);
        SubscribeLocalEvent<CMUGunSerialNumberComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        SubscribeLocalEvent<CMUGunSerialNumberComponent, GetVerbsEvent<UtilityVerb>>(OnGetUtilityVerbs);
        SubscribeLocalEvent<CMUGunSerialNumberComponent, CMUScratchSerialDoAfterEvent>(OnScratchDoAfter);
        SubscribeLocalEvent<CMUGunSerialNumberComponent, GunShotEvent>(OnGunShot);
    }

    // Guns mounted on vehicles and emplacements, or used by mobs, aren't items and get no serial.
    private void OnGunMapInit(Entity<GunComponent> ent, ref CMUGunMapInitEvent args)
    {
        if (_net.IsClient || !HasComp<ItemComponent>(ent) || HasComp<CMUGunSerialNumberComponent>(ent))
            return;

        var serial = EnsureComp<CMUGunSerialNumberComponent>(ent);
        serial.Serial = NewSerial();
        serial.Signature = NewSignature();
        Dirty(ent, serial);
        AddResidue(ent, Loc.GetString("cmu-forensic-firing-pin", ("signature", serial.Signature)));
    }

    private string NewSignature()
    {
        string signature;
        do
        {
            var chars = new char[9];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = i == 4 ? '-' : Hex[_random.Next(Hex.Length)];
            signature = new string(chars);
        } while (!_used.Add(signature));

        return signature;
    }

    /// <summary>Every casing the gun ejects carries its firing-pin marks.</summary>
    private void OnGunShot(Entity<CMUGunSerialNumberComponent> ent, ref GunShotEvent args)
    {
        if (_net.IsClient || ent.Comp.Signature.Length == 0)
            return;

        var marks = Loc.GetString("cmu-forensic-casing-marks", ("signature", ent.Comp.Signature));
        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is not { } casing || !HasComp<CartridgeAmmoComponent>(casing))
                continue;

            AddResidue(casing, marks);
            EnsureComp<CMUBallisticMarksComponent>(casing).Signature = ent.Comp.Signature;
        }
    }

    private void AddResidue(EntityUid uid, string residue)
    {
        var forensics = EnsureComp<ForensicsComponent>(uid);
        if (forensics.Residues.Add(residue))
            Dirty(uid, forensics);
    }

    private string NewSerial()
    {
        string serial;
        do
        {
            serial = $"{Letters[_random.Next(Letters.Length)]}{Letters[_random.Next(Letters.Length)]}-{_random.Next(100000, 1000000)}";
        } while (!_used.Add(serial));

        return serial;
    }

    private void OnGetExamineVerbs(Entity<CMUGunSerialNumberComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || ent.Comp.Serial.Length == 0 && !ent.Comp.Scratched)
            return;

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(ent.Comp.Scratched
            ? Loc.GetString("cmu-gun-serial-examine-scratched")
            : Loc.GetString("cmu-gun-serial-examine", ("serial", ent.Comp.Serial)));
        _examine.AddDetailedExamineVerb(args, ent.Comp, message,
            Loc.GetString("cmu-gun-serial-verb-text"),
            "/Textures/Interface/VerbIcons/information.svg.192dpi.png",
            Loc.GetString("cmu-gun-serial-verb-message"));
    }

    private void OnGetUtilityVerbs(Entity<CMUGunSerialNumberComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || ent.Comp.Scratched || args.Using is not { } used)
            return;

        var welder = _tools.HasQuality(used, WeldingQuality);
        if (!welder && !IsSharp(used))
            return;

        var user = args.User;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("cmu-gun-serial-scratch-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/delete.svg.192dpi.png")),
            Act = () => StartScratch(ent, user, used, welder),
        });
    }

    /// <summary>A knife, bayonet, machete or anything else that cuts or stabs. Guns' own bash damage doesn't count.</summary>
    private bool IsSharp(EntityUid used)
    {
        if (HasComp<GunComponent>(used) || !TryComp(used, out MeleeWeaponComponent? melee))
            return false;

        foreach (var type in SharpDamage)
        {
            if (melee.Damage.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                return true;
        }

        return false;
    }

    private void StartScratch(Entity<CMUGunSerialNumberComponent> ent, EntityUid user, EntityUid used, bool welder)
    {
        if (welder)
        {
            // The tool system checks the torch is lit and burns its fuel.
            _tools.UseTool(used, user, ent, (float) ent.Comp.WeldDelay.TotalSeconds, WeldingQuality,
                new CMUScratchSerialDoAfterEvent(), fuel: 1);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.ScratchDelay, new CMUScratchSerialDoAfterEvent(), ent,
            ent, used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("cmu-gun-serial-scratch-start", ("gun", ent.Owner)), ent, user);
    }

    private void OnScratchDoAfter(Entity<CMUGunSerialNumberComponent> ent, ref CMUScratchSerialDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Scratched)
            return;

        args.Handled = true;
        ent.Comp.Scratched = true;
        ent.Comp.Serial = string.Empty;
        Dirty(ent);
        _popup.PopupClient(Loc.GetString("cmu-gun-serial-scratch-done", ("gun", ent.Owner)), ent, args.User);
    }
}
