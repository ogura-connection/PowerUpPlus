# PowerUp+

A fork of [PowerUp](https://github.com/CSho27/PowerUp) by Christopher Shorter — the save editor for MLB Power Pros 2007 — expanded with a full MLB Stats API pipeline and curated roster generation.

Instead of manually editing hundreds of player attributes one by one, PowerUp+ can generate complete, accurate rosters from any point in baseball history — or pull live 2025 data — and write them directly into your save file. Batting stances, hot zones, fielding positions, appearance, skills, the works.

---

## What's new vs the original PowerUp

- **Full MLB Stats API pipeline** — real stats, pitch arsenals, hot zones, fielding metrics, speed data pulled automatically for any MLB player, any season
- **Curated data overlays** (enhancedMode) — curated JSON lookups fill attributes the API doesn't provide (appearance, batting stances, pitching forms, special abilities) rather than leaving them blank or generic
- **Pre-generated rosters included** — just drop into your save directory and play:
  - ⚾ **2025 MLB Season** — all 30 teams + AL/NL All-Stars, 1,691 players with real stats
  - 🏛️ **All-Time Franchise** — 710 hand-picked legends across 32 teams (30 franchise + Japan + World)
- **Granular player detail** — every attribute the game supports: batting stance, hot/cold zones, pitch arsenal, fielding position, appearance (facial hair, eye black, glasses, complexion), special abilities
- **Fast** — parallel API calls, ~35 seconds per full roster generation
- Everything the original does, still works

---

## Quick start

### Just want to play? Use the pre-built rosters:
1. Download `AllTime_Franchise_Rosters.dat` or `2025_MLB_Rosters.dat` from this repo
2. Find your MLB Power Pros 2007 save directory
3. Replace the existing .dat game save file
4. Launch the game

### Generate your own rosters:

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download), MLB Power Pros 2007

```bash
git clone https://github.com/ogura-connection/PowerUpImproved
cd PowerUpImproved
dotnet build PowerUp/PowerUp.sln

# Generate all-time franchise rosters
printf "generate-franchise-rosters --roster-file ROSTER_all_30_franchises.md\nquit\n" | dotnet run --project PowerUp/PowerUp.CommandLine

# Generate 2025 MLB season
printf "generate-roster --year 2025\nquit\n" | dotnet run --project PowerUp/PowerUp.CommandLine

# Export to .dat
printf "write-game-save --roster-id 2 --out-file AllTime_Franchise_Rosters.dat\nquit\n" | dotnet run --project PowerUp/PowerUp.CommandLine
```

---

## Pre-generated rosters

| Roster | Players | Teams | Data Source |
|---|---|---|---|
| 2025 MLB Season | 1,691 | 32 (30 + 2 All-Star) | MLB Stats API + curated |
| All-Time Franchise | 710 | 32 (30 + Japan + World) | MLB Stats API + curated |

---

## Acknowledgements

Built on top of the original PowerUp by Christopher Shorter. None of this exists without their reverse engineering of the Power Pros save format.

Data sourced from the [MLB Stats API](https://statsapi.mlb.com).

---

## License

GPL v3 — see [LICENSE](LICENSE).
