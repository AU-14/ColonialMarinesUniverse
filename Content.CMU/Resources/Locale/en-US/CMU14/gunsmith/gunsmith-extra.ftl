# The look, the name and the description of a weapon; the cartridges for the weapons of the game.

ammo-item-unknown = The bench cannot tell the caliber of this thing.
ammo-error-pellets = pellet count
gunsmith-ui-look = Look
gunsmith-ui-look-default = Made of parts
gunsmith-ui-section-naming = Name and description
gunsmith-ui-weapon-name = Name of the weapon (optional)
gunsmith-ui-weapon-description = Description of the weapon (optional)
gunsmith-ui-weapon-name-hint = What to call the weapon
gunsmith-ui-weapon-description-hint = What to write in the description
gunsmith-error-look = look
gunsmith-error-name = the name is too long
gunsmith-error-description = the description is too long

# ------------------------------------------------------------ uranium, shot, damage
gunsmith-material-uranium = uranium
ammo-note-uranium = for the tip only: part of the damage is radiation, as with the uranium cartridges
ammo-shell-name-slug = handmade shotgun shell ({ $caliber }, slug)
ammo-shell-name-shot = handmade shotgun shell ({ $caliber }, shot x{ $pellets })
ammo-cartridge-name-shot = handmade cartridge ({ $caliber }, shot x{ $pellets })
ammo-part-pellets = shot x{ $count }
ammo-ui-pellets = Load of the bullet: pellets (1 is one bullet)
ammo-ui-section-result = What it comes to
ammo-ui-result-damage = Damage of the bullet: { $damage }
ammo-ui-result-damage-shot = Damage of each of the { $pellets } pellets: { $damage }
ammo-ui-result-armor = Armor penetration: { $ap } (step { $level })
ammo-ui-result-fitted = The damage is fitted to the bullets of the game of this caliber: a handmade cartridge is stronger than a plain one, but not by much.
ammo-ui-result-fire = The target is set alight by the fire of the fuel (intensity { $intensity }, duration { $duration }).
ammo-ui-result-note = The damage and the armor penetration are for the barrel the caliber is rated in. In a weapon with another barrel they differ.
ammo-ui-result-custom = The caliber is not a preset: the game has no damage for it, so the damage follows the energy alone.
ammo-hb-damage = [bold]5. DAMAGE[/bold]
    The damage of a bullet is fitted to the bullets of the game. A plain cartridge of its caliber (lead, copper, brass, the nominal charge) does what a plain bullet of the game of that caliber does. The best materials and a hot charge add to it, but never past a quarter above the strongest bullet of the game.
    More energy, more damage: the damage grows as the root of the ratio of the energy of the bullet to the energy of the plain cartridge. The energy is shared among the pellets of a load of shot, and the damage is worked out for each.
    The damage never goes past 1.25 times that of the strongest bullet of the game of that caliber. The rarity of a material does not raise it.
    A uranium tip adds radiation: 40% of the damage of the bullet is dealt as radiation.
    A fuel in the bullet sets the target alight with the fire of that fuel: every fuel has its own intensity, duration and color of flame.
gunsmith-ui-preset-filter = Find a caliber
gunsmith-mismatch-magazine = It does not fit: the magazine is of caliber { $magazine }, the weapon of { $weapon }.
gunsmith-mismatch-cartridge = It does not fit: the cartridge is of caliber { $cartridge }, the magazine of { $magazine }.
gunsmith-mismatch-none = unknown
gunsmith-magazine-examine = A handmade magazine. Caliber: { $key }.

# ------------------------------------------------------------ kinds of bullets
ammo-ui-kind = Kind of the bullet
ammo-kind-bullet = plain bullet (core, jacket, tip, liquid, powder load)
ammo-kind-buckshot = buckshot (the shot, the casing, the pellets, the powder load and a fuel)
ammo-kind-heavybuckshot = heavy buckshot (only for the heavy shotgun caliber)
ammo-error-kind = heavy buckshot is made only for the heavy shotgun caliber
ammo-part-shot = shot: { $material }
ammo-shell-name-heavy-slug = handmade heavy shotgun shell ({ $caliber }, slug)
ammo-shell-name-heavy-shot = handmade heavy shotgun shell ({ $caliber }, shot x{ $pellets })
ammo-hb-kinds = [bold]6. KINDS OF BULLETS[/bold]
    A plain bullet is made of a core, a jacket, a tip, powder and a liquid.
    Buckshot is a load of many equal pellets of one metal. It has no jacket, no tip, no liquid and no powder load to choose: only the metal of the shot, the casing and the number of the pellets. The more pellets, the less damage each does: the energy of the load is shared among them. One pellet is a shotgun bullet, a slug, like those of the shotguns of the game. The buckshot is the RMC buckshot, not the plain SS14 one.
    Heavy buckshot is the same for the shells of the heavy shotguns (the Type 23 and others). It can be made only for the heavy shotgun caliber.
    Cartridges made the same lie in one stack; cartridges made differently do not.

# ------------------------------------------------------------ syringe bullets, liquids
ammo-kind-syringe = syringe bullet (a hollow body and any liquid, up to 20 units)
ammo-ui-payload-units = How many units of the liquid (1-20)
ammo-error-payload-fuel = only the fuels of the flamethrowers go into a plain bullet and buckshot
ammo-error-payload-units = units of the liquid, 1 to 20
ammo-part-body = body: { $material }
ammo-part-syringe = inside: { $reagent } ({ $units } u.)
ammo-cartridge-name-syringe = handmade syringe cartridge ({ $caliber })
ammo-hb-syringe = [bold]7. LIQUIDS IN BULLETS[/bold]
    Only the fuels of the flamethrowers go into a plain bullet and into buckshot: the target is set alight with the fire of that fuel.
    A syringe bullet is a hollow body of any metal or plastic with a needle. Any liquid from the tank (any reagent of the game) goes into it, up to 20 units, and all of it is injected into whoever it hits. By itself a syringe bullet hits weakly: about a third of the damage of a plain bullet of the same caliber.
    The powder load can be chosen for any bullet, buckshot and the syringe bullet too.

# ------------------------------------------------------------ classes of weapons, the feed, the limits
gunsmith-class-pistol = pistol
gunsmith-class-smg = submachine gun
gunsmith-class-rifle = rifle
gunsmith-class-lmg = machine gun
gunsmith-class-sniper = sniper rifle
gunsmith-class-shotgun = shotgun
gunsmith-class-heavy = heavy weapon
gunsmith-ui-class = Class of the weapon
gunsmith-ui-feed = Feed of the cartridges
gunsmith-ui-feed-capacity = Capacity of the cylinder or of the inner magazine
gunsmith-feed-magazine = a magazine that is put in
gunsmith-feed-revolver = a cylinder, like a revolver's
gunsmith-feed-internal = an inner magazine, the cartridges are put inside, like a shotgun's
gunsmith-error-class = no class of the weapon chosen
gunsmith-error-look-class = the look is of another class of weapon
gunsmith-error-class-bore = the caliber does not suit this class of weapon, bore, mm
gunsmith-error-class-charge = too much powder in the cartridge for this class of weapon, g
gunsmith-error-class-case = the case is too long for this class of weapon, mm
gunsmith-error-class-barrel = a barrel of this length does not suit this class of weapon, mm
gunsmith-error-class-stock = this class of weapon cannot have such a stock
gunsmith-error-feed-capacity = capacity of the feed
gunsmith-ui-class-hint = The class says what caliber and what barrel are allowed: a huge cartridge cannot go into a pistol. For a weapon with a look the look says the class.
gunsmith-ui-attachments = The weapon has four sockets for attachments (the barrel, the rail, the stock, the underbarrel). What fits into them is set by the look of the weapon.
gunsmith-mismatch-cartridge-weapon = It does not fit: the cartridge is of caliber { $cartridge }, the weapon of { $weapon }.
