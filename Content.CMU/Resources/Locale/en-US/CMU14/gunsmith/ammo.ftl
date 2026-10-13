# The ammunition bench: messages, window, handbook. Based on the mod "CE Custom Ammo" (a cartridge is made of a core, a jacket, a tip and a casing).

ent-RUMCAmmoBench = ammunition bench
    .desc = A bench with a mold, a press, a scale and a tank. Feed it ingots and powder, and pour the fuel of a flamethrower or any other liquid into the tank, then load cartridges the way you want them.
ent-RUMCAmmoBenchKit = ammunition bench kit
    .desc = A folded ammunition bench. Use it in hand to unfold it where you stand. Only someone trained as a sapper knows how.

# ------------------------------------------------------------ messages of the bench
ammo-item-occupied = There is a sample on the bench already.
ammo-item-placed = You lay the sample on the bench.
ammo-tank-full = The tank is full.
ammo-poured = You pour into the tank: { $units }.
ammo-cartridge-name = handloaded cartridge ({ $caliber })
ammo-cartridge-description = A cartridge loaded on the ammunition bench. It fits only a weapon of its own caliber: { $key }. { $parts }.
ammo-examine = A handloaded cartridge. Caliber: { $key }.
ammo-examine-payload = The bullet has a hollow with a liquid in it.
ammo-part-core = core: { $material }
ammo-part-jacket = jacket: { $material }
ammo-part-tip = tip: { $material }
ammo-part-casing = casing: { $material }
ammo-part-powder = powder { $level }
ammo-part-payload = in the bullet: { $reagent }

# ------------------------------------------------------------ the window
ammo-ui-title = Ammunition bench
ammo-ui-tab-cartridge = Cartridge
ammo-ui-tab-tank = Tank
ammo-ui-tab-stock = Stock
ammo-ui-tab-handbook = Handbook
ammo-ui-tab-data = Data
ammo-ui-hint = Choose the caliber, the material of each part, the powder load, the pellets and the liquid for the bullet. The bench shows what it will cost, and what comes out: the damage and the armor penetration.
ammo-ui-section-caliber = Caliber
ammo-ui-section-parts = Parts of the cartridge
ammo-ui-section-cost = Batch
ammo-ui-section-tank = What is in the tank
ammo-ui-section-item = Sample on the bench
ammo-ui-nominal-mass = Nominal bullet mass (lead)
ammo-ui-nominal-charge = Nominal charge
ammo-ui-core = Core (the shot, for buckshot)
ammo-ui-jacket = Jacket
ammo-ui-tip = Tip
ammo-ui-casing = Casing
ammo-ui-powder-level = Load against the nominal charge
ammo-ui-payload = Liquid in the bullet
ammo-ui-none = none
ammo-ui-count = How many cartridges (1-20)
ammo-ui-build = Load cartridges
ammo-ui-drain = Drain the tank
ammo-ui-take-item = Take the sample
ammo-ui-caliber-from-item = Take the caliber of the sample on the bench
ammo-ui-no-item = There is no sample on the bench. Lay a weapon, a magazine or a cartridge on it to copy its caliber.
ammo-ui-item-on-bench = A sample is on the bench. Caliber: { $key }
ammo-ui-tank-hint = Liquid is poured into the tank by hand: from a container, a canister or a flamethrower tank. Only what is in the tank can go into a bullet. A bullet is small, so it takes little.
ammo-ui-stock-hint = Ingots, sheets and powder are put into the bench by hand. What your cartridge needs is written on the first tab.
ammo-ui-tank-volume = In the tank { $units } of { $capacity }
ammo-ui-tank-empty = The tank is empty.
ammo-ui-in-tank = in the tank { $units }

# ------------------------------------------------------------ design errors
ammo-error-core-material = no material chosen for the core
ammo-error-jacket-material = no material chosen for the jacket
ammo-error-tip-material = no material chosen for the tip
ammo-error-casing-material = no material chosen for the casing
ammo-error-powder-level = powder load, steps
ammo-error-payload = liquid in the bullet

# ------------------------------------------------------------ data
ammo-data-materials = Materials of the parts of a cartridge
ammo-data-material = Material
ammo-data-part = Part
ammo-data-damage = Damage x
ammo-data-pierce = Pierce +
ammo-data-seal = Seal x
ammo-data-jam = Jams
ammo-data-powder-levels = Powder load
ammo-data-level = Step
ammo-data-factor = Share of the nominal charge
ammo-data-liquids = Liquids in a bullet
ammo-data-reagent = Liquid
ammo-data-burns = Ignites
ammo-data-injected = Injected
ammo-data-inject-share = Share injected
ammo-data-damage-type = Own damage
ammo-data-damage-per-unit = Per unit
ammo-data-yes = yes
ammo-data-other-liquids = Any liquid that is not in the table is just carried in the bullet and injected into the target, like the contents of a syringe.
ammo-part-name-core = core
ammo-part-name-jacket = jacket
ammo-part-name-tip = tip
ammo-part-name-casing = casing
ammo-const-density = the density the nominal mass of a bullet is taken at, g/cm3
ammo-const-jacket-share = the share of the volume of the bullet that the jacket takes
ammo-const-core-share = the share of the volume of the bullet that the core takes without a tip
ammo-const-core-share-tip = the same with a tip
ammo-const-tip-share = the share of the volume of the bullet that the tip takes
ammo-const-cavity-share = the share of the volume of the core that the hollow for the liquid takes
ammo-const-units-per-cm3 = units of liquid per cm3 of the hollow
ammo-const-reference-barrel = the length of the barrel the armor penetration is rated in, in calibers
ammo-const-case-wall = wall of the casing, mm
ammo-const-case-head = head of the casing, mm

ammo-note-plasteel = stronger than steel, but heavier and dearer to work
ammo-note-titanium = light and strong
ammo-note-tungsten = very dense, pierces armor, but seals the casing badly
ammo-note-wood = a training bullet only
ammo-note-fuel = fuel: the bullet sets the target on fire
ammo-note-acid = acid: it eats, and part of it is injected
ammo-note-poison = poison: injected whole

# ------------------------------------------------------------ handbook
ammo-hb-intro = [bold]AMMUNITION HANDBOOK[/bold]
    A cartridge is made of parts: a core, a jacket, an optional tip and a casing. Every material has its own effect in every part (the "Data" tab). The bench does not say what the cartridge comes to, only what it costs.
    Units: masses in g, lengths in mm, pressure in MPa, energy in J, speed in m/s.
    A cartridge fits a weapon when the caliber designations match: the designation is made of the bore, the dimensions of the casing and the seating depth of the bullet. Bullets of different materials fit the same weapon if the caliber is the same.

ammo-hb-parts = [bold]1. THE BULLET[/bold]
    Volume of the bullet, cm3: V = m_nom / rho_nom. The jacket takes 0.28 of it; the core 0.72, or 0.62 with a tip (which takes 0.10).
    Real mass of the bullet, g: m = sum of V_i * rho_i, rho_i the density of the material of the part. A light material makes a light bullet, a heavy one (tungsten) a heavy one, so more energy leaves the barrel, but less speed.
    The core acts on damage and penetration, the jacket on damage and jams, the tip on penetration, the casing on how well the gases are kept and on jams.
    The casing: volume of metal, cm3 = (pi * d * 0.7 * L + pi/4 * d^2 * 1.5) / 1000, d the diameter of the casing, L its length.

ammo-hb-powder = [bold]2. THE POWDER LOAD[/bold]
    The step of the load gives the share of the nominal charge: -3 is 0.55; -2 is 0.70; -1 is 0.85; 0 is 1.00; +1 is 1.12; +2 is 1.25; +3 is 1.40; +4 is 1.55.
    The pressure, MPa, comes out of the same formulas as in the gunsmith's handbook: P = eta_p * f * D / (1 - a * D), D = w / V (w the mass of the charge, V the powder space of the casing).
    A hot load gives more energy but presses harder. A weapon is made for a pressure. If the cartridge presses harder, the barrel, bolt and lugs are stressed that many times more. The safety factor is divided by the ratio of the pressures and the wear grows as its fourth power. A weak weapon with a hot cartridge bursts.
    Speed and energy: v = sqrt( 2000 * e * w * Q / (m + w / 3) ), E = m * v^2 / 2000, e the efficiency of the barrel, which depends on its length, Q the heat of the powder; e is also multiplied by the seal of the casing.
    The damage of the bullet is in section 5. Penetration: AP = 0.45 * (E / A)^0.9, A = pi/4 * d^2 the area of the bore in mm2, E rated for a barrel of 20 calibers. The corrections of the materials are added. The bullet gets the nearest step of penetration there is.

ammo-hb-liquid = [bold]3. THE LIQUID IN THE BULLET[/bold]
    Part of the core (8%) is a hollow. It takes 5 units of liquid for every cm3 of it. The bigger the bullet, the more fits.
    The fuel of the flamethrowers (napalms, gel, chlorine trifluoride, welding fuel): when it hits, the bullet sets the target on fire and burns by itself, and nothing is injected into the body.
    Acids: they eat by themselves and half of them is injected into the target.
    Poisons: injected whole.
    Any other liquid from the tank is simply carried and injected, like from a syringe.
    The liquid does not burn in the casing, so the powder does not touch it. A hot load is no more dangerous for the shooter for having a liquid in it: the danger is the pressure.

ammo-hb-weapon = [bold]4. THE WEAPON AND THE CARTRIDGE[/bold]
    The damage comes out of the energy in the barrel of the weapon that fired: the same cartridge hits harder from a long barrel than from a short one. A suppressor takes about 12% of the energy.
    Every part of the cartridge gives a damage multiplier and an addition to penetration; the casing also gives a chance to jam. These add to the jams of the mechanism.
    A bullet with a liquid adds its damage to the hit, if the liquid does damage by itself.
