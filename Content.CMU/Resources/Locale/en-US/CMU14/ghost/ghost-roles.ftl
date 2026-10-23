cmu-ghost-role-category-xenomorph = XENOMORPH
cmu-ghost-role-category-govfor = GOVFOR
cmu-ghost-role-category-opfor = OPFOR
cmu-ghost-role-category-corporate = CORPORATE
cmu-ghost-role-category-survivor = SURVIVORS
cmu-ghost-role-category-other = OTHER

cmu-ghost-roles-all-category = ALL ROLES
cmu-ghost-roles-open-label = OPEN
cmu-ghost-roles-raffle-label = RAFFLE
cmu-ghost-roles-unknown-location = LOCATION UNKNOWN
cmu-ghost-roles-raffle-entrants = { $players ->
    [one] 1 in raffle
   *[other] { $players } in raffle
}
cmu-ghost-roles-summary = { $roles } AVAILABLE · { $categories ->
    [one] 1 CATEGORY
   *[other] { $categories } CATEGORIES
}
