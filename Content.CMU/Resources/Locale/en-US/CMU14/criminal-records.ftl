# Criminal records computer screen
cmu-criminal-records-heading = CRIMINAL RECORDS
cmu-criminal-records-subheading = { $count ->
    [one] SECURITY DATABASE // 1 RECORD LISTED
   *[other] SECURITY DATABASE // { $count } RECORDS LISTED
}
cmu-criminal-records-access-granted = ACCESS GRANTED
cmu-criminal-records-access-read-only = READ ONLY
cmu-criminal-records-search = SEARCH RECORDS
cmu-criminal-records-search-placeholder = Type to search...
cmu-criminal-records-show = SHOW
cmu-criminal-records-show-all = All statuses
cmu-criminal-records-none = NO RECORDS FOUND
cmu-criminal-records-select = SELECT A RECORD
cmu-criminal-records-particulars = Particulars
cmu-criminal-records-fingerprints = Fingerprints
cmu-criminal-records-dna = DNA
cmu-criminal-records-religion = Religion
cmu-criminal-records-squad = Squad
cmu-criminal-records-bounty = Bounty
cmu-criminal-records-set-by = SET BY { $name }
cmu-criminal-records-set-status = Set Status
cmu-criminal-records-no-access = Your ID doesn't have access to change records.
cmu-criminal-records-add-entry = ADD ENTRY
cmu-criminal-records-delete = DELETE
cmu-criminal-records-delete-confirm = CONFIRM
cmu-criminal-records-footer-edit = Wanted, suspect and hostile statuses ask for a reason. History entries are timestamped.
cmu-criminal-records-footer-read-only = Read-only: swipe an ID with security access to make changes.

## Firearm registry
cmu-criminal-records-tab-records = RECORDS
cmu-criminal-records-tab-firearms = FIREARMS
cmu-firearm-registry-subheading = { $count ->
    [one] 1 firearm bought through colony logistics this round
   *[other] { $count } firearms bought through colony logistics this round
}
cmu-firearm-registry-footer = Firearms bought through the colony ASRS, with who ordered them. Use a spent casing, a gun or a forensic scanner that just scanned one on this console to compare its firing-pin signature.
cmu-firearm-registry-search-placeholder = Search serial, signature, weapon or buyer...
cmu-firearm-registry-empty = No firearms have been bought through colony logistics yet.
cmu-firearm-registry-no-match = No firearms match that search.
cmu-firearm-registry-serial = Serial
cmu-firearm-registry-signature = Signature
cmu-firearm-registry-buyer = Ordered by
cmu-firearm-registry-order = Order
cmu-firearm-registry-time = Delivered
cmu-firearm-registry-ordered-by-them = Firearms ordered
cmu-firearm-registry-owned-line = { $name } ({ $serial })
cmu-firearm-registry-compare-match = Ballistics match: { $name } ({ $serial }), ordered by { $buyer }.
cmu-firearm-registry-compare-none = No registered firearm has signature { $signature }.

## Mugshot, description, warrants and arrests
cmu-records-photo-name = photo of {$name}
cmu-records-photo-description = Age {$age}, {$gender}, height {$height}.
cmu-records-photo-printed = The console prints a photo.
cmu-records-print-photo = PRINT PHOTO
cmu-records-no-photo = NO PHOTO ON FILE
cmu-records-age = Age
cmu-records-gender = Gender
cmu-records-height = Height
cmu-records-warrants = Warrants
cmu-records-warrants-none = No active warrants.
cmu-records-warrant-issue = ISSUE WARRANT
cmu-records-warrant-revoke = REVOKE
cmu-records-warrant-prompt = Charge
cmu-records-warrant-line = {$time} // ISSUED BY {$officer}
cmu-records-arrests = Arrest History
cmu-records-arrests-none = No arrests on record.
cmu-records-arrest-log = LOG ARREST
cmu-records-arrest-charges-prompt = Charges
cmu-records-arrest-sentence-prompt = Sentence (minutes)
cmu-records-arrest-line = {$time} // {$officer} // { $minutes ->
    [0] no sentence
    [one] 1 minute
   *[other] {$minutes} minutes
}
cmu-records-personnel = PERSONNEL FILE
cmu-records-personnel-none = No personnel file: they aren't anywhere the colony can find them.
cmu-records-gender-male = Male
cmu-records-gender-female = Female
cmu-records-gender-epicene = Unspecified
cmu-records-gender-neuter = Unspecified
cmu-records-print-record = PRINT RECORD
cmu-records-print-printed = The console prints the record.
cmu-records-print-name = criminal record ({$name})
cmu-records-print-warrant = {$time}: {$charge} (issued by {$officer})
cmu-records-print-arrest = {$time}: {$charges}, { $minutes ->
    [0] no sentence
    [one] 1 minute
   *[other] {$minutes} minutes
} (arrested by {$officer})
cmu-records-print-history = {$time}: {$crime}
cmu-records-print-content = [head=2]CRIMINAL RECORD[/head]
    {"["}bold]{$name}[/bold], {$job}

    Age: {$age}
    Gender: {$gender}
    Height: {$height}
    Fingerprints: {$prints}
    DNA: {$dna}

    {"["}bold]Status:[/bold] {$status}
    Reason: {$reason}

    {"["}bold]Warrants[/bold]
    {$warrants}

    {"["}bold]Arrest history[/bold]
    {$arrests}

    {"["}bold]Crime history[/bold]
    {$history}
