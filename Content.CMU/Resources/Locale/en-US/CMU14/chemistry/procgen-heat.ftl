cmu-entity-effect-guidebook-solution-tile-fire =
    { $chance ->
        [1] Sets
        *[other] set
    } the surrounding floor on fire { $range ->
        [1] (1 tile)
        *[other] ({ $range } tiles)
    }
