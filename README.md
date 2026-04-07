# PowerUp+

A fork of [PowerUp](https://github.com/CSho27/PowerUp) by Christopher Shorter — the save editor for MLB Power Pros 2007 — rebuilt with a full MLB Stats API pipeline and proper roster generation.

Manually editing a full roster in Power Pros is a nightmare. Batting stances, hot zones, pitch arsenals, appearance, special abilities — hundreds of attributes per player. PowerUp+ pulls real data from the MLB Stats API and writes it all directly into your save file, for any season in history or the current 2026 roster. The whole thing takes about 35 seconds.

---

## What's new

- **Full MLB Stats API pipeline** — real stats, pitch arsenals, hot zones, fielding metrics, speed data, any player, any season
- **Curated data overlays** — JSON lookups fill the gaps the API doesn't cover (appearance, batting stances, pitching forms, special abilities) rather than leaving everything blank or generic
- **Pre-generated rosters included** — drop in and play:
  - ⚾ **2026 MLB Season** — all 30 teams + AL/NL All-Stars, 1,691 players with real stats
  - 🏛️ **All-Time Franchise** — 710 hand-picked legends across 32 teams (30 franchise + Japan + World)
- **Granular detail** — every attribute the game supports. Stats, hot/cold zones, pitch arsenal, fielding position, batting stance, and full appearance (facial hair, eye black, glasses, complexion)
- **Fast** — parallel API calls, ~35 seconds per full roster
- Everything from the original PowerUp still works

---

## Quick start

### Just want to play?

1. Download `AllTime_Franchise_Rosters.dat` or `2026_MLB_Rosters.dat` from this repo
2. Find your MLB Power Pros 2007 save directory
3. Replace the existing .dat save file
4. Launch the game

### Generate your own rosters

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download), MLB Power Pros 2007

```bash
git clone https://github.com/ogura-connection/PowerUpPlus
cd PowerUpPlus
dotnet build PowerUp/PowerUp.sln

# Generate all-time franchise rosters
printf "generate-franchise-rosters --roster-file ROSTER_all_30_franchises.md\nquit\n" | dotnet run --project PowerUp/PowerUp.CommandLine

# Generate 2026 MLB season
printf "generate-roster --year 2026\nquit\n" | dotnet run --project PowerUp/PowerUp.CommandLine

# Export to .dat
printf "write-game-save --roster-id 2 --out-file AllTime_Franchise_Rosters.dat\nquit\n" | dotnet run --project PowerUp/PowerUp.CommandLine
```

---

## Pre-generated rosters

| Roster | Players | Teams | Source |
|---|---|---|---|
| 2026 MLB Season | 1,691 | 32 (30 + 2 All-Star) | MLB Stats API + curated |
| All-Time Franchise | 710 | 32 (30 + Japan + World) | MLB Stats API + curated |

---

## Acknowledgements

Built on top of the original PowerUp by Christopher Shorter. None of this exists without their reverse engineering of the Power Pros save format.

Data sourced from the [MLB Stats API](https://statsapi.mlb.com).

---

## License

GPL v3 — see [LICENSE](LICENSE).
