ent-RUMCArmorsmithBenchKit = armorer's bench kit
    .desc = A folded armorer's bench. Use it in hand to unfold it where you stand. Only someone trained as a sapper knows how.
ent-RUMCArmorsmithBench = armorer's bench
    .desc = A bench with an anvil, a press, shears and a dyeing vat. Feed it ingots and cloth, design an armor or a helmet, and then paint it any color you want.
ent-RUMCCraftedArmorBodyBase = handmade armor
ent-RUMCCraftedArmorHeadBase = handmade helmet

# ------------------------------------------------------------ the bench
armorsmith-unskilled = You do not know how to work this bench.
armorsmith-item-occupied = There is already an armor on the bench.
armorsmith-item-placed = You lay the armor on the bench.
armorsmith-stock-full = The bench cannot hold any more of that.
armorsmith-stock-taken = You load into the bench: { $count }.
armorsmith-recolored = You paint the armor.
armorsmith-working = You set to work.
armorsmith-invalid = That cannot be made: { $what }.
armorsmith-lacking = Not enough: { $what }.
armorsmith-built = The armor is ready.
armorsmith-item-name-body = handmade armor ({ $look })
armorsmith-item-name-head = handmade helmet ({ $look })
armorsmith-item-description = An armor made by hand. The better the sums, the better it takes a blow.
armorsmith-examine = A handmade armor. Plate: { $plate }, { $thickness } mm. Mass: { $mass } kg.

armorsmith-backing-cloth = cloth
armorsmith-backing-durathread = durathread

# ------------------------------------------------------------ the window
armorsmith-ui-title = Armorer's bench
armorsmith-ui-tab-armor = Armor
armorsmith-ui-tab-recolor = Painting
armorsmith-ui-tab-stock = Stock
armorsmith-ui-tab-handbook = Handbook
armorsmith-ui-tab-data = Data
armorsmith-ui-hint = Choose the look, the plate and the soft layer. The window says what the work will cost but not how well the armor protects: that is for you to work out with the formulas of the handbook.
armorsmith-ui-build = Make
armorsmith-ui-recolor = Paint anew
armorsmith-ui-take-item = Take the armor
armorsmith-ui-name = Name
armorsmith-ui-description = Description
armorsmith-ui-slot = Worn on
armorsmith-slot-body = the body (armor)
armorsmith-slot-head = the head (helmet)
armorsmith-ui-look = Look (a grey sprite of the game, painted any color)
armorsmith-ui-plate-material = Material of the plate
armorsmith-ui-thickness = Thickness of the plate
armorsmith-ui-coverage = Share of the area the plate covers
armorsmith-ui-backing = Soft layer
armorsmith-ui-backing-none = none
armorsmith-ui-layers = Number of layers
armorsmith-ui-color-hint = The color the grey armor is painted:
armorsmith-ui-section-look = Look
armorsmith-ui-section-layers = Layers
armorsmith-ui-section-color = Color
armorsmith-ui-section-naming = Name and description
armorsmith-ui-section-cost = What the work costs
armorsmith-ui-section-stock-list = What the bench holds
armorsmith-ui-recolor-hint = Lay a handmade armor on the bench (use it on the bench), choose a color and press "Paint anew". It can be painted as often as you like.
armorsmith-ui-stock-hint = The ingots, sheets and cloth you put on the bench are here.
armorsmith-ui-no-item = There is no handmade armor on the bench.
armorsmith-ui-item-on-bench = A handmade armor lies on the bench.

armorsmith-error-look = look
armorsmith-error-plate-material = material of the plate
armorsmith-error-plate-thickness = thickness of the plate, mm
armorsmith-error-coverage = share of the area, %
armorsmith-error-backing-layers = number of layers
armorsmith-error-backing-material = soft layer
armorsmith-error-name = name
armorsmith-error-description = description
armorsmith-error-mass = too heavy, kg

armorsmith-data-plates = Plates
armorsmith-data-backings = Soft layers
armorsmith-data-constants = Constants
armorsmith-data-name = Material
armorsmith-data-yield = σy, MPa
armorsmith-data-ultimate = σu, MPa
armorsmith-data-density = ρ, g/cm³
armorsmith-data-young = E, GPa
armorsmith-data-strain = ε
armorsmith-data-machin = Machin.
armorsmith-data-volume = Volume of one item, cm³
armorsmith-data-layer = Layer, kg/m²
armorsmith-data-area = m² per item

armorsmith-const-body-area = area of the plate on the body, m²
armorsmith-const-head-area = area of the plate on a helmet, m²
armorsmith-const-body-frame = mass of the frame of an armor, kg
armorsmith-const-head-frame = mass of the frame of a helmet, kg
armorsmith-const-bullet-mass = mass of the reference bullet, kg
armorsmith-const-bullet-area = area of the reference bullet, m²
armorsmith-const-shape = shape factor
armorsmith-const-velocity-scale = speed scale for bullets, m/s
armorsmith-const-bullet-cap = greatest protection from bullets
armorsmith-const-melee-cap = greatest protection from melee
armorsmith-const-explosion-cap = greatest protection from explosions
armorsmith-const-bio-cap = greatest protection from bio
armorsmith-const-melee-scale = scale of bending strength, MPa·mm²
armorsmith-const-explosion-scale = scale of mass for explosions, kg/m²

# ------------------------------------------------------------ the handbook
armorsmith-hb-intro = [bold]THE ARMORER'S HANDBOOK[/bold]
    An armor is a plate and a soft layer behind it. A grey look is painted any color, and the color changes nothing else.
    The window does not say how well the armor protects: work it out with the formulas below and the data of the "Data" tab. If the sums are wrong, the armor simply protects worse than you thought.
    Units: lengths in mm, areas in m², masses in kg, stresses in MPa, speeds in m/s.
armorsmith-hb-layers = [bold]1. THE LAYERS: THE PLATE AND THE SOFT LAYER[/bold]
    The mass of a plate per square metre, kg/m²: A = ρ · t, ρ the density (g/cm³), t the thickness (mm).
    The merit of a material against bullets (Cunniff): U* = σu · ε / (2ρ) · √(E / ρ) in SI (σu and E in Pa, ρ in kg/m³), ε the strain at the break. The stronger, the tougher and the lighter, the better. The speed of the material: V* = U*^(1/3).
    The speed of the reference bullet (7.62 mm, 8 g) that a layer just stops: V = k · V* · √(A · S / m), S and m the area and the mass of the reference bullet, k the shape factor.
    The energies of the layers add, so the squares of the speeds add. The plate covers only the share c of the area, the soft layer all of it: V_total² = c · V_plate² + V_layer². Then multiply by the workmanship w: w = 0.85 + 0.15 · Q (Q the quality, as for weapons; from 0.8 to 1.03).
    The soft layer is worked out by the same formulas: A_layer = (the mass of one layer) · (the number of layers).
armorsmith-hb-protection = [bold]2. THE PROTECTION OF THE GAME[/bold]
    All four numbers saturate: each grows slower and slower and never passes its cap.
    Bullets: Bullet = Bullet cap · (1 − e^(−(V_total / V₀)²)).
    Melee: Melee = Melee cap · (1 − e^(−(c · σy · t² / S₀ + A_layer / 12))), σy the yield of the plate (with the workmanship).
    Explosion: Expl = Expl cap · (1 − e^(−(c · A_plate + A_layer) / A₀)).
    Bio: Bio = Bio cap · (1 − e^(−(0.5 · c + 0.1 · n))), n the number of layers of the soft layer.
    All the numbers are rounded. The caps and the scales are on the "Data" tab.
armorsmith-hb-weight = [bold]3. WEIGHT AND SLOWDOWN[/bold]
    The mass of the piece, kg: m = m_frame + (c · A_plate + A_layer) · S, S the area of the plate for the body or for the helmet.
    The bench will not make a piece that is too heavy: more than 70 kg for the body, 25 kg for a helmet, 8 kg for a ghillie suit. The thickness of the plate: up to 44 mm for the body, 38 mm for a helmet, 4 mm for a ghillie suit.
    Armor slows the wearer. The share of the speed it takes: s = k_t · c · t + k_s · n, t the thickness of the plate (mm), c the share of the area the plate covers, n the number of layers of the soft layer. The wearer keeps the share 1 − s of the speed, but not less than v_min. The thicker the plate and the more layers, the slower the wearer. A helmet does not slow.
    The tier of the armor follows from the share of the speed: over 0.88 it is light, over 0.70 medium, under that heavy.
    A light material (titanium, plastitan) gives the same protection from bullets with less mass, but protects worse from explosions: it is the mass that stops a blast. The slowdown does not depend on the mass, only on the thickness and on the layers.
armorsmith-hb-extra = [bold]4. HELMETS AND GHILLIE SUITS[/bold]
    A helmet may be open or closed. A closed one covers the face too: the plate is 1.4 times larger (the helmet is 0.25 kg heavier), the speed the armor stops and the melee and explosion protection are 1.1 times greater, and 0.3 is added to the bio figure. In return a closed helmet darkens the sight, like a welding mask.
    A ghillie suit is light and is never heavy: its plate is up to 4 mm and its mass up to 8 kg. It is woven of plant fiber (for now a placeholder: cotton), 80 units to every square metre. A ghillie suit hides the wearer as the ghillie armor of the game does; snipers, spotters and the fighters of the CLF can wear it.
armorsmith-hb-build = [bold]5. COMPLEXITY AND MATERIALS[/bold]
    The volume of the plate, cm³: V = S · 1000 · c · t. An ingot gives as much metal as the table says; the finer the grade of work, the more is wasted.
    Cloth: layers · S / (the area one item of cloth makes).
    A material that is hard to machine (plastitan, titanium) gives a poorer quality in fine work, and so a poorer protection.
    Lay the materials on the bench by hand; the "Stock" tab lets you take them back.

# ------------------------------------------------------------ the slowdown, closed helmets, ghillie suits
armorsmith-slot-ghillie = ghillie suit (the body)
armorsmith-ui-closed = Closed helmet (protects better, darkens the sight)
armorsmith-item-name-ghillie = handmade ghillie suit ({ $look })
armorsmith-const-slowdown-mm = slowdown for a millimetre of plate
armorsmith-const-slowdown-layer = slowdown for a layer of the soft layer
armorsmith-const-speed-min = the least share of the speed of the wearer
armorsmith-const-closed-bonus = factor of protection of a closed helmet
armorsmith-const-closed-area = factor of the area of a closed helmet
armorsmith-const-ghillie-fiber = fibers per m² of a ghillie suit
