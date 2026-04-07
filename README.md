# PowerUp+

Fork of [PowerUp](https://github.com/CSho27/PowerUp) by Christopher Shorter. PowerUp is a save editor for MLB Power Pros 2007 (Wii) — this fork adds a full MLB Stats API pipeline so you can generate complete rosters from real data instead of editing players by hand.

Manually building a roster in Power Pros is tedious as hell. Every player needs batting stance, hot zones, pitch arsenal, appearance, special abilities — hundreds of attributes each. This pulls real data from the MLB Stats API and writes it directly into your save file, for any season or the current 2026 roster.

---

## What's new vs the original

- Full MLB Stats API integration — stats, pitch arsenals, hot zones, fielding metrics, speed data, any player, any season
- Curated JSON overlays to fill gaps the API doesn't cover (appearance, batting stances, pitching forms, special abilities) so nothing defaults to generic placeholders
- Two pre-generated rosters you can just drop in and play:
  - 2026 MLB Season — all 30 teams + AL/NL All-Stars, 1,691 players
  - All-Time Franchise — 710 players across 32 teams (30 franchise + Japan + World)
- Parallel API calls to keep generation reasonably fast
- Everything from the original still works

---

## Just want to play?

1. Download `AllTime_Franchise_Rosters.dat` or `2026_MLB_Rosters.dat` from this repo
2. Find your MLB Power Pros 2007 save directory
3. Replace the existing .dat save file
4. Launch the game

---

## Generate your own rosters

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download) and MLB Power Pros 2007.

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

## Known issues / to do

- AI-assisted attribute generation is functional but not fully documented yet
- Some historical players with limited data have partially estimated appearances
- Region compatibility across different save states not fully tested — let me know if you hit issues

---

## Credits

Built on top of the original PowerUp by Christopher Shorter. The save format reverse engineering is entirely his work, none of this exists without it.

Data from the [MLB Stats API](https://statsapi.mlb.com).

---

## License

GPL v3 — see [LICENSE](LICENSE).
