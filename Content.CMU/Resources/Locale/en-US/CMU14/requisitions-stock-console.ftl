cmu-req-stock-console-window-title = Requisitions Stock
cmu-req-stock-console-refresh-button = Refresh
cmu-req-stock-console-summary = {$vendors} {$vendors ->
    [one] vendor
   *[other] vendors
}, {$items} items, {$empty} out of stock
cmu-req-stock-console-no-vendors = No automated requisitions vendors found on this ship.
cmu-req-stock-console-unlimited = unlimited
cmu-req-stock-console-amount = {$amount} left
cmu-req-stock-console-amount-max = {$amount} / {$max}
cmu-req-stock-console-search = Search
cmu-req-stock-console-of-max = of {$max}

## CRT refurb
cmu-req-stock-console-heading = REQUISITIONS STOCK
cmu-req-stock-console-search-placeholder = Search items...
cmu-req-stock-console-low-only = LOW STOCK ONLY
cmu-req-stock-console-footer = Live stock of this ship's requisitions vendors. Items at a quarter or less of their starting stock show as low.
cmu-req-stock-console-tag-out = { $count } OUT
cmu-req-stock-console-tag-low = { $count } LOW
cmu-req-stock-console-tag-ok = ALL STOCKED
cmu-req-stock-console-status-ok = IN STOCK
cmu-req-stock-console-status-low = LOW
cmu-req-stock-console-status-out = OUT

cmu-req-stock-console-order-add = +
cmu-req-stock-console-order-remove = -
cmu-req-stock-console-order-heading = ORDER SLIP
cmu-req-stock-console-order-summary = {$count}/{$max}
cmu-req-stock-console-order-empty = Use + on free items to add them to a slip.
cmu-req-stock-console-order-hint = Print it and get it stamped APPROVED by requisitions.
cmu-req-stock-console-order-full = The slip is full.
cmu-req-stock-console-order-add-tooltip = Add one to the order slip
cmu-req-stock-console-order-remove-tooltip = Take one off the order slip
cmu-req-stock-console-order-print = Print slip
cmu-req-stock-console-order-clear = Clear
cmu-req-slip-line = {$count}x {$item}
cmu-req-slip-content = [head=2]REQUISITION ORDER[/head]
    Requested by: {$requester}

    {$items}
    Total items: {$total}

    {"["}italic]Requires an APPROVED stamp from requisitions. Use the stamped slip on the requisitions vendor.[/italic]
cmu-req-slip-printed = {CAPITALIZE(THE($console))} chatters and feeds out a fresh order slip.
cmu-req-slip-not-approved = The vendor won't fill this slip until it's stamped APPROVED.
cmu-req-slip-no-access = You don't have access to this vendor.
cmu-req-slip-already-filled = This order has already been filled.
cmu-req-slip-none-in-stock = The vendor has none of this order in stock.
cmu-req-slip-filled = {CAPITALIZE(THE($vendor))} reads the slip, hands out {$count} {$count ->
    [one] item
   *[other] items
} and stamps the slip filled.
cmu-req-slip-partly-filled = The vendor hands out {$count} {$count ->
    [one] item
   *[other] items
}; {$left} {$left ->
    [one] is
   *[other] are
} still out of stock.
cmu-req-slip-completed-content = [head=2]REQUISITION ORDER - FILLED[/head]
    Requested by: {$requester}
    Filled by: {$filler}

    {$items}
    Total items: {$total}

    {$pays ->
        [yes] {"["}italic]Send this slip down the ASRS lift to close out the order.[/italic]
       *[other] {"["}italic]Record copy only. {$requester} has already been paid for an order this shift.[/italic]
    }
