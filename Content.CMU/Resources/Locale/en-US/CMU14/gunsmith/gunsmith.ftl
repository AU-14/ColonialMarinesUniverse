# Gunsmith's bench: messages, window, handbook.

# ------------------------------------------------------------ messages of the bench
gunsmith-unskilled = You do not know how to work at this bench.
gunsmith-weapon-occupied = There is a weapon on the bench already.
gunsmith-weapon-placed = You lay the weapon on the bench.
gunsmith-stock-full = The stock of the bench is full.
gunsmith-stock-taken = You load into the bench: { $count }.
gunsmith-invalid = It cannot be made like this: { $what }.
gunsmith-lacking = Not enough material: { $what }.
gunsmith-working = You set to work.
gunsmith-built = Done.
gunsmith-magazine-name = handmade magazine ({ $caliber }, { $capacity })
gunsmith-magazine-description = A magazine made at a gunsmith's bench. It only fits a weapon of its own caliber.
gunsmith-weapon-name = handmade weapon: { $action } ({ $caliber })
gunsmith-weapon-description = A weapon assembled at a gunsmith's bench. Caliber: { $key }.

# ------------------------------------------------------------ in use
gunsmith-broken = The weapon is broken.
gunsmith-jam = The mechanism jams!
gunsmith-burst-weak = The barrel or the bolt cannot take the pressure: the weapon bursts!
gunsmith-burst-hot = The barrel has overheated and lost its strength: the weapon bursts!
gunsmith-burst-fatigue = The metal gave out from fatigue: the weapon bursts!
gunsmith-stock-snapped = The stock cannot take the recoil and snaps!
gunsmith-examine = A handmade weapon. Caliber: { $key }. Shots fired: { $shots }.
gunsmith-examine-broken = The weapon is broken and no longer fires.

# ------------------------------------------------------------ materials, powders, actions, calibers
gunsmith-material-iron = iron
gunsmith-material-steel = steel
gunsmith-material-plasteel = plasteel
gunsmith-material-titanium = titanium
gunsmith-material-plastitan = plastitan
gunsmith-material-tungsten = tungsten
gunsmith-material-aluminum = aluminum
gunsmith-material-brass = brass
gunsmith-material-copper = copper
gunsmith-material-wood = wood
gunsmith-material-plastic = plastic
gunsmith-material-lead = lead

gunsmith-powder-black = black powder
gunsmith-powder-smokeless = smokeless powder
gunsmith-powder-high = high-energy powder

gunsmith-action-bolt = bolt-action
gunsmith-action-pump = pump-action
gunsmith-action-lever = lever-action
gunsmith-action-semi = semi-automatic
gunsmith-action-burst = burst-fire
gunsmith-action-auto = automatic

gunsmith-caliber-9mm = 9 mm pistol
gunsmith-caliber-10mm = 10 mm
gunsmith-caliber-45 = .45
gunsmith-caliber-556 = 5.56 mm rifle
gunsmith-caliber-762 = 7.62 mm intermediate
gunsmith-caliber-308 = 7.82 mm (.308) battle rifle
gunsmith-caliber-22 = 5.6 mm small bore

gunsmith-grade-rough = rough
gunsmith-grade-standard = standard
gunsmith-grade-fine = fine
gunsmith-grade-match = match

gunsmith-stock-none = no stock
gunsmith-stock-fixed = fixed
gunsmith-stock-folding = folding

# ------------------------------------------------------------ the window
gunsmith-ui-title = Gunsmith's bench
gunsmith-ui-tab-weapon = Weapon
gunsmith-ui-tab-caliber = Caliber
gunsmith-ui-tab-magazine = Magazine
gunsmith-ui-tab-stock = Stock
gunsmith-ui-tab-handbook = Handbook
gunsmith-ui-tab-data = Data
gunsmith-ui-build-weapon = Build the weapon
gunsmith-ui-build-magazine = Build the magazine
gunsmith-ui-caliber-from-weapon = Take the caliber of the weapon on the bench
gunsmith-ui-take-weapon = Take the weapon off the bench
gunsmith-ui-in-stock = { $count } in stock
gunsmith-ui-grade = Grade of work
gunsmith-ui-action = Action
gunsmith-ui-optic = Optic (complexity +6)
gunsmith-ui-suppressor = Suppressor (complexity +8)
gunsmith-ui-material = Material
gunsmith-ui-barrel-length = Barrel length
gunsmith-ui-barrel-outer = Outer diameter of the barrel D
gunsmith-ui-bolt-diameter = Diameter of the bolt
gunsmith-ui-pin-hole = Hole for the firing pin
gunsmith-ui-fire-rate = Rate of fire
gunsmith-ui-fire-rate-unit = shots/s
gunsmith-ui-fire-rate-hint = Rate of fire: 0 is what the mechanism gives by itself. A rate that heats the barrel faster than it cools bursts it once it has got hot.
gunsmith-ui-lugs = Number of locking lugs
gunsmith-ui-lug-width = Width of a lug w
gunsmith-ui-lug-length = Length of a lug l
gunsmith-ui-lug-depth = Depth of engagement t
gunsmith-ui-stock-kind = Stock
gunsmith-ui-stock-width = Width of the stock b
gunsmith-ui-stock-height = Height of the stock h
gunsmith-ui-stock-length = Length of the stock L
gunsmith-ui-hinge-pin = Diameter of the hinge pin
gunsmith-ui-section-general = General
gunsmith-ui-section-barrel = Barrel
gunsmith-ui-section-bolt = Bolt and locking lugs
gunsmith-ui-section-receiver = Receiver (seats of the lugs)
gunsmith-ui-section-stock = Stock
gunsmith-ui-section-cost = Cost and time
gunsmith-ui-section-caliber = Caliber
gunsmith-ui-section-magazine = Magazine
gunsmith-ui-section-stock-list = What the bench holds
gunsmith-ui-section-bench-weapon = The weapon on the bench
gunsmith-ui-preset = Start from the cartridge
gunsmith-ui-bore = Caliber (diameter of the bore) d
gunsmith-ui-case-diameter = Inner diameter of the case
gunsmith-ui-case-length = Length of the case
gunsmith-ui-seat-depth = Depth the bullet sits at
gunsmith-ui-bullet-mass = Mass of the bullet m
gunsmith-ui-powder = Powder
gunsmith-ui-charge = Mass of the charge ω
gunsmith-ui-caliber-hint = The bench will not tell the pressure and the speed of a cartridge: work them out (handbook, section 1). The barrel and the bolt depend on the pressure.
gunsmith-ui-capacity = Capacity
gunsmith-ui-body-material = Material of the body
gunsmith-ui-wall = Thickness of the wall and the lips
gunsmith-ui-spring-material = Material of the spring
gunsmith-ui-wire = Diameter of the wire of the spring
gunsmith-ui-coil = Mean diameter of the coil
gunsmith-ui-turns = Number of working turns
gunsmith-ui-free-length = Free length of the spring
gunsmith-ui-magazine-hint = A magazine is made for the caliber of the weapon. The spring must feed the last round and not break, and the lips must not bend (handbook, section 6).
gunsmith-ui-stock-hint = Ingots, sheets and powder are put into the bench by hand. What the design needs is on the other tabs.
gunsmith-ui-stock-empty = The bench is empty.
gunsmith-ui-eject = Give out
gunsmith-ui-no-weapon = There is no weapon on the bench.
gunsmith-ui-weapon-on-bench = A weapon of caliber { $key } is on the bench.
gunsmith-ui-caliber-key = Designation of the caliber: { $key }
gunsmith-ui-weapon-caliber = Caliber: { $key }
gunsmith-ui-complexity = Complexity: { $points } pts
gunsmith-ui-time = Time of work: { $seconds } s
gunsmith-ui-error = { $what }

gunsmith-error-action = no action chosen
gunsmith-error-barrel-material = no material of the barrel
gunsmith-error-barrel-outer = outer diameter of the barrel (above the diameter of the case + 2), mm
gunsmith-error-barrel-length = length of the barrel, mm
gunsmith-error-bolt-material = no material of the bolt
gunsmith-error-receiver-material = no material of the receiver
gunsmith-error-stock-material = no material of the stock
gunsmith-error-bolt-diameter = diameter of the bolt (not below that of the case), mm
gunsmith-error-pin-hole = hole for the firing pin, mm
gunsmith-error-fire-rate = rate of fire, shots/s (0 for the mechanism's own)
gunsmith-error-lugs = number of lugs
gunsmith-error-lug-width = width of a lug, mm
gunsmith-error-lug-length = length of a lug, mm
gunsmith-error-lug-depth = depth of engagement, mm
gunsmith-error-stock-width = width of the stock, mm
gunsmith-error-stock-height = height of the stock, mm
gunsmith-error-stock-length = length of the stock, mm
gunsmith-error-hinge-pin = hinge pin, mm
gunsmith-error-bore = caliber, mm
gunsmith-error-case-diameter = diameter of the case, mm
gunsmith-error-case-length = length of the case, mm
gunsmith-error-seat = depth of the seat of the bullet, mm
gunsmith-error-bullet-mass = mass of the bullet, g
gunsmith-error-charge = mass of the charge, g
gunsmith-error-powder = no powder chosen
gunsmith-error-capacity = capacity
gunsmith-error-wall = thickness of the wall, mm
gunsmith-error-wire = diameter of the wire, mm
gunsmith-error-coil = diameter of the coil, mm
gunsmith-error-turns = number of turns
gunsmith-error-free-length = free length of the spring, mm
gunsmith-error-body-material = no material of the body
gunsmith-error-spring-material = no material of the spring

# ------------------------------------------------------------ data
gunsmith-data-materials = Materials
gunsmith-data-powders = Powders
gunsmith-data-actions = Actions
gunsmith-data-constants = Constants
gunsmith-data-name = Name
gunsmith-data-yield = σy, MPa
gunsmith-data-ultimate = σu, MPa
gunsmith-data-endurance = σ-1, MPa
gunsmith-data-young = E, GPa
gunsmith-data-shear = G, GPa
gunsmith-data-density = ρ, g/cm³
gunsmith-data-heat = c, J/(g·K)
gunsmith-data-limit = Tlim, °C
gunsmith-data-machin = Machin.
gunsmith-data-volume = Unit vol., cm³
gunsmith-data-force = f, MPa·cm³/g
gunsmith-data-covolume = α, cm³/g
gunsmith-data-energy = Q, J/g
gunsmith-data-efficiency = η
gunsmith-data-cost = saltpeter/g
gunsmith-data-complexity = Complexity
gunsmith-data-cycle = Cycle, s
gunsmith-data-bolt-length = Bolt length, mm

gunsmith-const-peak = share of the ideal pressure at the peak (pressure formula)
gunsmith-const-gas = speed of the powder gases in the speeds of the bullet
gunsmith-const-burn = barrel length at which the powder has done most of its work, in bores
gunsmith-const-heat-share = share of the energy of the powder that heats the barrel
gunsmith-const-shear-ratio = shear yield to tensile yield
gunsmith-const-stock-pulse = time the recoil takes to pass through the stock into the shoulder
gunsmith-const-stock-ecc = arm of the bending moment of the stock, in its lengths
gunsmith-const-ambient = ambient temperature
gunsmith-const-half-life = seconds in which the barrel loses half of its excess heat
gunsmith-const-lip-length = length of the feed lip of the magazine
gunsmith-const-lip-width = width of the feed lip of the magazine
gunsmith-const-friction = friction of the rounds in the magazine
gunsmith-const-feed-accel = acceleration the spring must give the last round, in g
gunsmith-const-spring-allow = allowed stress of the spring, in the strength of the wire
gunsmith-const-spring-wire = how many times drawn spring wire is stronger than a bar of the same metal
gunsmith-const-gravity = acceleration of gravity

# ------------------------------------------------------------ handbook
gunsmith-hb-intro = [bold]GUNSMITH'S HANDBOOK[/bold]
    The bench will not tell you whether the weapon holds. It tells what you may set and what it costs. The rest is calculation, and it is yours to do.
    Units: lengths in mm, masses in g, forces in N, stresses and pressures in MPa (N/mm²), energy in J, temperature in °C, speed in m/s.
    The properties of the materials and the powders and all the constants are on the Data tab.
    If you are wrong: with a safety factor below 1 the barrel, the bolt or the lugs fail at the very first shot, the weapon bursts and the shooter is hurt. From 1 to about 1.6 the weapon will not last (fatigue). A weak stock snaps and the weapon stops firing. A hot barrel loses strength. A stronger weapon means a higher pressure, and so better materials.

gunsmith-hb-ballistics = [bold]1. THE CARTRIDGE: pressure, speed, recoil[/bold]
    Volume of the powder space of the case, cm³: V = π/4 · dc² · (Lc − hs) / 1000 (dc inner diameter of the case, Lc its length, hs depth of the seat of the bullet, all in mm).
    Loading density, g/cm³: Δ = ω / V, ω is the mass of the charge in grams.
    Peak pressure, MPa: P = ηp · f · Δ / (1 − α · Δ). f is the force of the powder, α its covolume (from the table), ηp a constant. As α·Δ nears one the pressure soars: the powder does not fit in the case.
    Efficiency of the barrel: ε = η · (1 − e^(−L / L0)), L0 = 8 · d, L the length of the barrel, d the caliber, η the efficiency of the powder.
    Speed of the bullet, m/s: v = √( 2000 · ε · ω · Q / (m + ω / 3) ), Q the heat of combustion (J/g), m the mass of the bullet (g). A suppressor takes about 6% of it.
    Energy of the bullet, J: E = m · v² / 2000. The damage depends on it.
    Impulse of the recoil, N·s: J = (m + kg · ω) · v / 1000.
    Time of the bullet in the bore, s: t = 2 · L / v, with L in metres.
    Peak force of the recoil on the barrel, N: F = π · J / (2 · t).
    Speed of the weapon from the recoil, m/s: u = J / M, M the mass of the weapon in kg. Energy of the recoil: J² / (2·M).

gunsmith-hb-barrel = [bold]2. THE BARREL: a thick-walled tube (Lamé)[/bold]
    The pressure acts in the chamber. The inner diameter there is that of the case dc, the outer one is D (yours to set).
    Hoop stress at the inner wall, MPa: σθ = P · (D² + dc²) / (D² − dc²).
    Radial: σr = −P.
    Axial (the bolt closes the end): σz = P · dc² / (D² − dc²).
    Equivalent stress (von Mises): σe = √( ½ · [ (σθ − σr)² + (σr − σz)² + (σz − σθ)² ] ). On paper Tresca will do: σe = σθ − σr (a little stricter).
    Factor of safety: n = σy / σe. Below 1 the barrel bursts.
    Fatigue: every shot takes the stress from 0 to σe, so σa = σe/2 and σm = σe/2. By Goodman: 1/nf = σa / σ-1 + σm / σu. If nf is 1 or more, the life is unlimited. Below 1 the life falls about as nf^8.
    Mass of the barrel, g: M = ρ · π/4 · (D² − d²) · L / 1000, d the caliber, L the length.

gunsmith-hb-bolt = [bold]3. THE BOLT AND THE LOCKING LUGS[/bold]
    The gas presses on the head of the case and drives the bolt back. Force, N: F = P · π/4 · dc².
    Shear of the lugs: τ = F / (n · w · l), n the number of lugs, w the width, l the length. Allowed: 0.577 · σy. Factor: ns = 0.577 · σy / τ.
    Bearing under the lugs: σb = F / (n · w · t), t the depth of engagement. Factor: nb = σy / σb.
    Tension of the bolt at the hole of the firing pin: σ = F / (π/4 · (db² − h²)), db the diameter of the bolt, h the diameter of the hole. Factor: σy / σ.
    The lugs load both the bolt and the receiver: in the seats the weaker of the two materials counts.
    Fatigue of the lugs is worked out with σe = √3 · τ the same as for the barrel (section 2).
    The bolt must not be narrower than the case: db is not less than dc.

gunsmith-hb-stock = [bold]4. THE STOCK: a cantilever[/bold]
    The recoil goes through the stock more slowly than through the barrel: F = π · J / (2 · tst), tst the time of the transfer (see Data).
    Moment: M = F · ke · L, L the length of the stock, ke the relative arm.
    Bending stress of a rectangular beam, MPa: σ = M · c / I = 6 · F · ke · L / (b · h²), b the width, h the height (in the plane of the bending).
    Factor: n = σy / σ. A stock below 1 snaps at the first shot and the weapon stops firing.
    A folding stock: the hinge pin is in double shear: τ = F / (2 · π/4 · d²). Factor: 0.577 · σy / τ, the pin is of the material of the receiver.
    Mass of the stock, g: M = ρ · b · h · L / 1000.

gunsmith-hb-heat = [bold]5. HEATING OF THE BARREL[/bold]
    Every shot heats the barrel. Rise of temperature, K: ΔT = χ · ω · Q / (M · c), M the mass of the barrel (g), c the specific heat (J/(g·K)), χ the share of the energy of the powder that goes into the barrel.
    The barrel cools by Newton: the excess over the ambient temperature halves every T½ seconds.
    Hot metal is weaker: σy(T) = σy · (1 − (T − 20) / (Tlim − 20)), Tlim the temperature at which the material loses its strength.
    If σy(T) falls below σe of the barrel, the barrel bursts. Long bursts need a heavy barrel and a material with a high Tlim.
    Rate of fire: r is shots a second. Between the shots the barrel cools by the factor q = 0.5^(1/(r · T½)). Fired like that without a stop, the temperature just after a shot comes to T = 20 + ΔT / (1 − q). If σy(T) at that temperature is below σe of the barrel, it bursts once it has got that hot. A light barrel and a low Tlim cannot stand a high rate of fire. A rate of 0 is the one the mechanism gives: the cycle grows longer with the mass of the bolt and shorter with the quality.

gunsmith-hb-magazine = [bold]6. THE MAGAZINE AND ITS SPRING[/bold]
    Pitch of the rounds in the stack, mm: p = 1.05 · dc. The body is longer than the stack by the solid length of the spring.
    Spring index: C = Dc / dw, Dc the mean diameter of the coil, dw the diameter of the wire (4 to 12 is good).
    Wahl factor: K = (4C − 1) / (4C − 4) + 0.615 / C.
    Rate of the spring, N/mm: k = G · dw⁴ / (8 · Dc³ · n), G the shear modulus (in MPa: GPa × 1000), n the number of working turns.
    Solid length: Ls = (n + 2) · dw. A full magazine squeezes the spring to exactly Ls + 1 mm, so the free length L0 must be well above Ls + 1.
    Force of the spring at length L, N: F = k · (L0 − L). With s rounds left the length is L = Ls + 1 + (N − s) · p.
    Force with a full magazine: Ff = k · (L0 − Ls − 1). Stress in the wire then, MPa: τ = 8 · Ff · Dc · K / (π · dw³). Allowed: τa = 0.5 · kw · σu (kw = 2.5: drawn wire is stronger than bar stock), σu of the material of the spring.
    Force needed to feed with s rounds left, N: Fn = mr · (a · g + μ · s · g) / 1000, mr the mass of a round in grams (bullet + powder + about 3 g of case), a the feed acceleration in g, μ the friction.
    The spring must give at least Fn for every s from 1 to N, or the weapon will sometimes jam on the feed.
    The lips of the magazine are a cantilever of length ll and width bl (see Data) loaded by the force of the spring: σ = 6 · Ff · ll / (bl · t²), t the thickness of the wall. Factor: σy of the body / σ.

gunsmith-hb-build = [bold]7. COMPLEXITY, QUALITY, MATERIALS[/bold]
    Complexity is made of the action, the grade of work, the options (optic, suppressor, folding stock) and extra locking lugs. The more complex the thing, the longer the work.
    The grade asks of the material its machinability: rough 0.3, standard 0.5, fine 0.7, match 0.9. If the material falls short, the quality falls. Good quality gives accuracy, rate of fire and reliability of the mechanism.
    Hard materials (plasteel, titanium, tungsten) are stronger but hard to work: fine work will not do on them for now.
    Raw material: each part takes its volume plus waste (more for finer work). The volume of a unit of a material is in Data.
    A powerful weapon is strong materials, a complex design and a careful calculation; none of them does without the others.
    Cartridges are made for the caliber of the weapon: each takes brass for the case, lead for the bullet and powder.
