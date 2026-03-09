game-ticker-get-info-text-stalker = Hi and welcome to [color=white]Stalker14![/color]
                            It's currently week [color=white]#{$roundId}[/color]
                            The current player count is: [color=white]{$playerCount}[/color]
                            The current map is: [color=white]{$mapName}[/color]
                            The current game mode is: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]

game-ticker-get-info-preround-text = Hi and welcome to [color=white]Stalker14![/color]
                            It's currently week [color=white]#{$roundId}[/color]
                            The current player count is: [color=white]{$playerCount}[/color] ([color=white]{$readyCount}[/color] {$readyCount ->
                                [one] is
                                *[other] are
                            } ready)
                            The current map is: [color=white]{$mapName}[/color]
                            The current game mode is: [color=white]{$gmTitle}[/color]
                            >[color=yellow]{$desc}[/color]
lobby-state-player-status-round-time-stalker =
    The current time is: {$hours} {$hours ->
    [1]hour
    *[other]hours
    } and {$minutes} {$minutes ->
    [1]minute
    *[other]minutes
    }
