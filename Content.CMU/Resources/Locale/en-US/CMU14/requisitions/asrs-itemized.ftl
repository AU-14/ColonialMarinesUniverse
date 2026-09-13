cmu-asrs-window-title = Automated Storage and Retrieval System
cmu-asrs-tab-items = ITEMS
cmu-asrs-tab-bundles = LEGACY BUNDLES
cmu-asrs-return-items = ITEM CATALOG
cmu-asrs-mode-itemized = ITEMIZED PROCUREMENT
cmu-asrs-categories = CATEGORIES
cmu-asrs-search-items = Search individual items...
cmu-asrs-catalog-heading = AVAILABLE ITEMS
cmu-asrs-cart-heading = SHIPMENT CART
cmu-asrs-cart-empty = Cart is empty. Add items from the catalog.
cmu-asrs-checkout = PURCHASE SHIPMENT
cmu-asrs-category-all = ALL ITEMS
cmu-asrs-category-favorites = FAVORITES
cmu-asrs-category-recent = RECENT ORDERS
cmu-asrs-results = { $count } RESULTS
cmu-asrs-no-results = No items match the current filters.
cmu-asrs-budget = BUDGET: ${ $balance }
cmu-asrs-platform-status = PLATFORM: { $state } | { $slots } SLOTS FREE
cmu-asrs-platform-none = NOT CONNECTED
cmu-asrs-platform-busy = BUSY
cmu-asrs-platform-lowered = LOWERED
cmu-asrs-platform-raised = RAISED
cmu-asrs-platform-lowering = LOWERING
cmu-asrs-platform-raising = RAISING
cmu-asrs-stock-unlimited = UNLIMITED
cmu-asrs-stock-count = STOCK { $current }/{ $max }
cmu-asrs-stock-count-refill = STOCK { $current }/{ $max } | +{ $time }
cmu-asrs-cart-cost = TOTAL: ${ $cost }
cmu-asrs-cart-weight = CARGO: { $weight } WT
cmu-asrs-cart-crates = SHIPMENT: { $crates } PLATFORM SLOTS
cmu-asrs-cart-remove = Remove one
cmu-asrs-cart-add = Add one
cmu-asrs-favorite-toggle = Toggle favorite
cmu-asrs-projected-budget = AFTER ORDER: ${ $balance }
cmu-asrs-cart-capacity = ACTIVE CRATE: { $remaining } WT FREE
cmu-asrs-cart-state-idle = IDLE
cmu-asrs-cart-state-packing = PACKING PLAN
cmu-asrs-crate-title = CRATE { $number }
cmu-asrs-crate-packing = PACKING
cmu-asrs-crate-sealed = FULL
cmu-asrs-loose-title = LOOSE { $number }
cmu-asrs-loose-state = SHIPS ALONE
cmu-asrs-hint-empty = SELECT AN ITEM TO BEGIN A PACKING PLAN.
cmu-asrs-hint-fit = ACTIVE CRATE { $crate } HAS { $remaining } WT REMAINING.
cmu-asrs-hint-loose = { $amount } ITEM(S) MUST SHIP WITHOUT A CRATE.
cmu-asrs-hint-funds = ORDER EXCEEDS BUDGET BY ${ $amount }.
cmu-asrs-hint-slots = ORDER REQUIRES { $amount } MORE PLATFORM SLOT(S).
cmu-asrs-checkout-pending = Transmitting order...
cmu-asrs-checkout-success = Order accepted. Shipment queued.
cmu-asrs-checkout-invalid = Order rejected: invalid cart.
cmu-asrs-checkout-funds = Order rejected: insufficient budget.
cmu-asrs-checkout-stock = Order rejected: stock changed. Review the cart.
cmu-asrs-checkout-platform = Order rejected: no ASRS platform is connected.
cmu-asrs-checkout-full = Order rejected: the platform lacks enough free slots.
cmu-asrs-phase-verifying = VERIFYING INVENTORY...
cmu-asrs-phase-packing = PACKING MANIFEST...
cmu-asrs-phase-sealing = SEALING CRATES...
cmu-asrs-phase-dispatching = DISPATCHING TO PLATFORM...
cmu-asrs-phase-complete = SHIPMENT ACCEPTED
cmu-asrs-receipt-title = ASRS DISPATCH RECEIPT
cmu-asrs-receipt-summary = CHARGED: ${ $cost }
    CARGO: { $weight } WT
    SHIPMENTS: { $crates }
cmu-asrs-receipt-dismiss = RETURN TO CATALOG
cmu-asrs-preview-crate = ROUTE: CRATE { $crate } // { $weight }/{ $limit } WT
cmu-asrs-preview-loose = ROUTE: UNCRATED LOAD // { $weight } WT
cmu-asrs-slot-filled = Shipment occupies this platform slot
cmu-asrs-slot-free = Platform slot available
cmu-asrs-slot-overflow = Shipment exceeds available platform space
cmu-asrs-boot-bus = ASRS/88 LOAD CONTROL
    {"["}01] POLLING STORAGE BUS...
    {"["}02] WAITING FOR BAY TELEMETRY
cmu-asrs-boot-cranes = ASRS/88 LOAD CONTROL
    {"["}OK] STORAGE BUS
    {"["}03] HOMING CRANE SERVOS...
cmu-asrs-boot-scale = ASRS/88 LOAD CONTROL
    {"["}OK] CRANE DATUM
    {"["}04] ZEROING CARGO SCALES...
cmu-asrs-boot-manifest = ASRS/88 LOAD CONTROL
    {"["}OK] WEIGHT CELLS
    {"["}05] MOUNTING MANIFEST VOLUME...
cmu-asrs-boot-ready = ASRS/88 LOAD CONTROL
    ALL SYSTEMS NOMINAL // LOAD BAY READY
cmu-asrs-idle = ASRS LOAD BAY // STANDBY
    ═══════════════════════════════════
    CRANE POSITION { $position } // AWAITING MANIFEST
    STORAGE BUS QUIET // PLATFORM MONITOR ACTIVE
cmu-asrs-load-bay = LOAD BAY //
cmu-asrs-load-control = INDUSTRIAL LOAD CONTROL
cmu-asrs-catalog-index = CATALOG INDEX
cmu-asrs-holographic-inspection = HOLOGRAPHIC INSPECTION
cmu-asrs-feed-rack = FEED RACK
cmu-asrs-physical-manifest = PHYSICAL LOADING MANIFEST
cmu-asrs-platform-slots = PLATFORM SLOTS
cmu-asrs-line-printer = LINE PRINTER // DISPATCH RECEIPT
cmu-asrs-seal-dispatch = SEAL & DISPATCH
cmu-asrs-budget-prefix = BUDGET:
cmu-asrs-after-prefix = AFTER:
cmu-asrs-conveyor-crate = ▥  CRATE { $number }  //  SEALED
cmu-asrs-seal-stamp = { $code } // SEALED
