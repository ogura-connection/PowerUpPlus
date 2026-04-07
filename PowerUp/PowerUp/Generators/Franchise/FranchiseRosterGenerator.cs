using Microsoft.Extensions.Logging;
using PowerUp.Databases;
using PowerUp.Entities;
using PowerUp.Entities.Players;
using PowerUp.Entities.Rosters;
using PowerUp.Entities.Teams;
using PowerUp.Fetchers.MLBStatsApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PowerUp.Generators.Franchise
{
  public class FranchisePlayerResult
  {
    public RosterFileEntry Entry { get; set; }
    public long? ResolvedPlayerId { get; set; }
    public Player? Player { get; set; }
    public string? SkipReason { get; set; }

    public FranchisePlayerResult(RosterFileEntry entry)
    {
      Entry = entry;
    }
  }

  public interface IFranchiseRosterGenerator
  {
    FranchiseGenerationResult GenerateRoster(
      string rosterFilePath,
      PlayerGenerationAlgorithm algorithm,
      Action<string>? onLog = null,
      HashSet<string>? teamFilter = null
    );
  }

  public class FranchiseGenerationResult
  {
    public Roster? Roster { get; set; }
    public List<FranchisePlayerResult> PlayerResults { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
  }

  public class FranchiseRosterGenerator : IFranchiseRosterGenerator
  {
    private readonly IMLBStatsApiClient _mlbApi;
    private readonly IPlayerGenerator _playerGenerator;
    private readonly ISpecialAbilitiesLookup _specialAbilitiesLookup;
    private readonly IPlayerFormLookup _playerFormLookup;
    private readonly IHotZoneLookup _hotZoneLookup;
    private readonly IAppearanceLookup _appearanceLookup;
    private readonly ManualPlayerBuilder _manualBuilder;
    private readonly ILogger<FranchiseRosterGenerator> _logger;

    public FranchiseRosterGenerator(
      IMLBStatsApiClient mlbApi,
      IPlayerGenerator playerGenerator,
      ISpecialAbilitiesLookup specialAbilitiesLookup,
      IPlayerFormLookup playerFormLookup,
      IHotZoneLookup hotZoneLookup,
      IAppearanceLookup appearanceLookup,
      ManualPlayerBuilder manualBuilder,
      ILogger<FranchiseRosterGenerator> logger
    )
    {
      _mlbApi = mlbApi;
      _playerGenerator = playerGenerator;
      _specialAbilitiesLookup = specialAbilitiesLookup;
      _playerFormLookup = playerFormLookup;
      _hotZoneLookup = hotZoneLookup;
      _appearanceLookup = appearanceLookup;
      _manualBuilder = manualBuilder;
      _logger = logger;
    }

    private const int MaxConcurrency = 12;

    public FranchiseGenerationResult GenerateRoster(
      string rosterFilePath,
      PlayerGenerationAlgorithm algorithm,
      Action<string>? onLog = null,
      HashSet<string>? teamFilter = null
    )
    {
      var stopwatch = Stopwatch.StartNew();

      void Log(string msg)
      {
        if (onLog != null)
          onLog(msg);
        else
          _logger.LogInformation(msg);
      }

      var result = new FranchiseGenerationResult();

      // Step 1: Parse roster file
      var allEntries = RosterFileParser.Parse(rosterFilePath);
      var byTeam = RosterFileParser.GroupByTeam(allEntries);
      Log($"Parsed {allEntries.Count} players across {byTeam.Count} teams");

      // Step 2: Collect work items and process manual players (fast, no API calls)
      var apiWorkItems = new List<(RosterFileEntry entry, string teamName, MLBPPTeam ppTeam)>();
      var manualPlayersByTeam = new Dictionary<MLBPPTeam, List<Player>>();
      var filteredTeams = new List<(MLBPPTeam ppTeam, List<RosterFileEntry> entries)>();

      foreach (var (ppTeam, entries) in byTeam)
      {
        var teamName = entries.First().TeamName;
        if (teamFilter != null && !teamFilter.Any(f =>
          teamName.Contains(f, StringComparison.OrdinalIgnoreCase)))
          continue;

        filteredTeams.Add((ppTeam, entries));
        manualPlayersByTeam[ppTeam] = new List<Player>();

        Log($"\n=== {teamName} ({entries.Count} players) ===");

        foreach (var entry in entries)
        {
          if (_manualBuilder.ShouldUseManual(entry.PlayerName, entry.PeakSeason!.Value))
          {
            var playerResult = new FranchisePlayerResult(entry);
            try
            {
              var player = _manualBuilder.Build(entry.PlayerName);
              var lookupName = entry.PlayerName.RemoveAccents();
              _specialAbilitiesLookup.ApplySpecialAbilities(lookupName, player.SpecialAbilities);
              _playerFormLookup.ApplyForms(lookupName, player);
              _hotZoneLookup.ApplyHotZones(lookupName, player);
              _appearanceLookup.ApplyAppearance(lookupName, entry.PeakSeason!.Value, player);
              player.GeneratedPlayer_PitchArsenalSource = "manual";
              playerResult.Player = player;
              manualPlayersByTeam[ppTeam].Add(player);
              Log($"  OK {entry.Slot,-4} {entry.PlayerName} ({entry.PeakSeason}) [manual]");
            }
            catch (Exception ex)
            {
              playerResult.SkipReason = $"Manual build failed: {ex.Message}";
              result.Warnings.Add($"FAILED: {entry.PlayerName} ({entry.PeakSeason}) - {ex.Message}");
              Log($"  FAIL {entry.PlayerName} ({entry.PeakSeason}) - {ex.Message}");
            }
            result.PlayerResults.Add(playerResult);
          }
          else
          {
            apiWorkItems.Add((entry, teamName, ppTeam));
          }
        }
      }

      // Step 3: Parallel resolve + generate for all API players
      Log($"\nGenerating {apiWorkItems.Count} API players (concurrency {MaxConcurrency})...");
      var apiResults = new ConcurrentBag<(MLBPPTeam ppTeam, RosterFileEntry entry, FranchisePlayerResult playerResult, Player? player, long? resolvedId)>();

      Parallel.ForEach(apiWorkItems, new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency }, item =>
      {
        var playerResult = new FranchisePlayerResult(item.entry);

        // Resolve MLB player ID
        long? playerId = ResolvePlayerId(item.entry, _ => { }); // suppress per-player logging during parallel
        if (playerId == null)
        {
          playerResult.SkipReason = "Could not resolve MLB player ID";
          apiResults.Add((item.ppTeam, item.entry, playerResult, null, null));
          return;
        }

        playerResult.ResolvedPlayerId = playerId;

        // Generate player at their peak season
        try
        {
          var genResult = _playerGenerator.GeneratePlayer(
            playerId.Value, item.entry.PeakSeason!.Value, algorithm,
            rosterDisplayName: item.entry.PlayerName);

          playerResult.Player = genResult.Player;
          apiResults.Add((item.ppTeam, item.entry, playerResult, genResult.Player, playerId));
        }
        catch (Exception ex)
        {
          playerResult.SkipReason = $"Generation failed: {ex.Message}";
          apiResults.Add((item.ppTeam, item.entry, playerResult, null, playerId));
        }
      });

      // Step 4: Batch save to DB and build teams (sequential — LiteDB is not thread-safe)
      var teamsByPPTeam = new Dictionary<MLBPPTeam, int>();
      var apiResultsByTeam = apiResults.GroupBy(r => r.ppTeam).ToDictionary(g => g.Key, g => g.ToList());

      foreach (var (ppTeam, entries) in filteredTeams)
      {
        var teamName = entries.First().TeamName;
        var generatedPlayers = new List<Player>();
        var generatedPlayerIds = new HashSet<long>();

        // Save manual players to DB
        foreach (var player in manualPlayersByTeam[ppTeam])
        {
          DatabaseConfig.Database.Save(player);
          generatedPlayers.Add(player);
        }

        // Save API players to DB and log results
        if (apiResultsByTeam.TryGetValue(ppTeam, out var teamApiResults))
        {
          foreach (var (_, entry, playerResult, player, resolvedId) in teamApiResults)
          {
            result.PlayerResults.Add(playerResult);

            if (playerResult.SkipReason != null && playerResult.SkipReason.Contains("resolve"))
            {
              result.Warnings.Add($"UNRESOLVED: {entry.PlayerName} ({entry.PeakSeason}) - {teamName}");
              Log($"  SKIP {entry.PlayerName} ({entry.PeakSeason}) - unresolved");
              continue;
            }

            if (player == null)
            {
              result.Warnings.Add($"FAILED: {entry.PlayerName} ({entry.PeakSeason}) - {playerResult.SkipReason}");
              Log($"  FAIL {entry.PlayerName} ({entry.PeakSeason}) - {playerResult.SkipReason}");
              continue;
            }

            if (resolvedId.HasValue && generatedPlayerIds.Contains(resolvedId.Value))
            {
              Log($"  DUP  {entry.Slot,-4} {entry.PlayerName} ({entry.PeakSeason}) - already generated");
              continue;
            }

            DatabaseConfig.Database.Save(player);
            generatedPlayers.Add(player);
            if (resolvedId.HasValue) generatedPlayerIds.Add(resolvedId.Value);

            var tags = new List<string>();
            if (player.GeneratedPlayer_PitchArsenalSource == "api") tags.Add("arsenal:api");
            if (player.HitterAbilities.HotZones.UpAndIn != HotZonePreference.Neutral ||
                player.HitterAbilities.HotZones.Middle != HotZonePreference.Neutral) tags.Add("hz");
            var tagStr = tags.Count > 0 ? $" [{string.Join(",", tags)}]" : "";
            Log($"  OK {entry.Slot,-4} {entry.PlayerName} ({entry.PeakSeason}){tagStr}");
          }
        }

        if (generatedPlayers.Count < 9)
        {
          Log($"  Only {generatedPlayers.Count} players - skipping team assembly");
          continue;
        }

        // Build team with lineup
        try
        {
          var team = BuildTeam(teamName, ppTeam, entries, generatedPlayers);
          DatabaseConfig.Database.Save(team);
          teamsByPPTeam[ppTeam] = team.Id!.Value;
          Log($"  Team saved: {teamName} with {generatedPlayers.Count} players (id {team.Id})");
        }
        catch (Exception ex)
        {
          result.Warnings.Add($"TEAM ASSEMBLY FAILED: {teamName} - {ex.Message}");
          Log($"  TEAM ASSEMBLY FAILED: {teamName} - {ex.Message}");
        }
      }

      // Step 5: Build roster
      if (teamsByPPTeam.Count > 0)
      {
        var baseRoster = DatabaseConfig.Database
          .LoadAll<Roster>()
          .SingleOrDefault(r => r.SourceType == EntitySourceType.Base);

        var allPPTeams = Enum.GetValues<MLBPPTeam>();
        foreach (var ppTeam in allPPTeams)
        {
          if (!teamsByPPTeam.ContainsKey(ppTeam) && baseRoster != null)
            teamsByPPTeam[ppTeam] = baseRoster.TeamIdsByPPTeam[ppTeam];
        }

        var roster = new Roster
        {
          SourceType = EntitySourceType.Generated,
          Name = "All-Time Franchise Rosters",
          Year = 2000,
          TeamIdsByPPTeam = teamsByPPTeam,
          FreeAgentPlayerIds = Enumerable.Empty<int>(),
        };
        DatabaseConfig.Database.Save(roster);
        result.Roster = roster;
        Log($"\nRoster saved: \"{roster.Name}\" id={roster.Id}");
      }

      // Summary
      stopwatch.Stop();
      var resolved = result.PlayerResults.Count(r => r.Player != null);
      var unresolved = result.PlayerResults.Count(r => r.SkipReason != null && r.SkipReason.Contains("resolve"));
      var failed = result.PlayerResults.Count(r => r.SkipReason != null && !r.SkipReason.Contains("resolve"));
      Log($"\nSummary: {resolved} generated, {unresolved} unresolved, {failed} failed — elapsed {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s");

      return result;
    }

    // Players whose roster file names don't match the MLB Stats API search.
    // Maps nickname/common name → API-searchable name.
    private static readonly Dictionary<string, string> NameAliases = new(StringComparer.OrdinalIgnoreCase)
    {
      ["Goose Gossage"] = "Rich Gossage",
      ["Shoeless Joe Jackson"] = "Joe Jackson",
      ["Boog Powell"] = "John Powell",
      ["Catfish Hunter"] = "Jim Hunter",
      ["Whitey Herzog"] = "Dorrel Herzog",
      ["Mudcat Grant"] = "Jim Grant",
      ["Minnie Miñoso"] = "Minnie Minoso",
      ["Bo Jackson"] = "Vincent Jackson",
      ["Nap Lajoie"] = "Napoleon Lajoie",
      ["Tris Speaker"] = "Tristram Speaker",
      ["Cy Young"] = "Denton Young",
      ["Satchel Paige"] = "Leroy Paige",
      ["Dizzy Dean"] = "Jay Hanna Dean",
      ["Yogi Berra"] = "Lawrence Berra",
      ["Lefty Gomez"] = "Vernon Gomez",
      ["Lefty Grove"] = "Robert Grove",
      ["Pie Traynor"] = "Harold Traynor",
      ["Pee Wee Reese"] = "Harold Reese",
      ["Home Run Baker"] = "Frank Baker",
      ["Preacher Roe"] = "Elwin Roe",
      ["Dummy Hoy"] = "William Hoy",
      ["Kiki Cuyler"] = "Hazen Cuyler",
      ["Duke Snider"] = "Edwin Snider",
      ["Nellie Fox"] = "Jacob Fox",
      ["Chief Bender"] = "Charles Bender",
      ["Kid Nichols"] = "Charles Nichols",
      ["Mel Ott"] = "Melvin Ott",
      ["Christy Mathewson"] = "Christopher Mathewson",
      ["Hack Wilson"] = "Lewis Wilson",
      ["Mordecai Brown"] = "Mordecai Brown",
      ["Three Finger Brown"] = "Mordecai Brown",
      ["Jim Wynn"] = "Jimmy Wynn",
      ["Ferguson Jenkins"] = "Fergie Jenkins",
      ["A.J. Ramos"] = "AJ Ramos",
      ["King Kelly"] = "Michael Kelly",
      ["Cap Anson"] = "Adrian Anson",
      ["Arky Vaughan"] = "Joseph Vaughan",
      ["Dazzy Vance"] = "Charles Vance",
      ["Rube Waddell"] = "George Waddell",
      ["Zack Wheat"] = "Zachariah Wheat",
      ["Freddie Patek"] = "Freddie Patek",
      ["Hal McRae"] = "Harold McRae",
      ["Willie McCovey"] = "Willie McCovey",
      ["Kirby Puckett"] = "Kirby Puckett",
      ["Ichiro Suzuki"] = "Ichiro Suzuki",
      ["Ozzie Guillén"] = "Ozzie Guillen",
      ["José Ramírez"] = "Jose Ramirez",
      ["Carlos Beltrán"] = "Carlos Beltran",
      ["José Reyes"] = "Jose Reyes",
      ["Salvador Pérez"] = "Salvador Perez",
      ["José Rosado"] = "Jose Rosado",
      ["Adrián Beltré"] = "Adrian Beltre",
      ["Iván Rodríguez"] = "Ivan Rodriguez",
      ["Roberto Clemente"] = "Roberto Clemente",
      ["José Cruz"] = "Jose Cruz",
      ["Yadier Molina"] = "Yadier Molina",
      ["Ozzie Smith"] = "Ozzie Smith",
      ["Tripp Cromer III"] = "Tripp Cromer",
      ["Pedro Martínez"] = "Pedro Martinez",
      ["Vladimir Guerrero"] = "Vladimir Guerrero",
      ["Grover Cleveland Alexander"] = "Grover Alexander",
      ["A.J. Pollock"] = "AJ Pollock",
      ["Garry Templeton"] = "Garry Templeton",
    };

    private long? ResolvePlayerId(RosterFileEntry entry, Action<string> log)
    {
      try
      {
        var searchName = NameAliases.GetValueOrDefault(entry.PlayerName, entry.PlayerName);
        var searchResult = Task.Run(() => _mlbApi.SearchPlayers(searchName))
          .WaitAsync(TimeSpan.FromSeconds(10))
          .GetAwaiter()
          .GetResult();

        // If alias search returned nothing, try original name
        if (searchResult.People.Length == 0 && searchName != entry.PlayerName)
        {
          searchResult = Task.Run(() => _mlbApi.SearchPlayers(entry.PlayerName))
            .WaitAsync(TimeSpan.FromSeconds(10))
            .GetAwaiter()
            .GetResult();
        }

        if (searchResult.People.Length == 0)
          return null;

        // If single result, use it
        if (searchResult.People.Length == 1)
          return searchResult.People[0].Id;

        // Multiple results — disambiguate by era
        // Prefer a player whose career overlaps with the peak season
        var peakYear = entry.PeakSeason!.Value;
        var candidates = searchResult.People
          .Where(p => p.IsPlayer)
          .Where(p =>
          {
            var debut = p.MlbDebutDate?.Year ?? 9999;
            var lastPlayed = p.LastPlayedDate?.Year ?? (p.Active ? DateTime.Now.Year : debut + 20);
            return debut <= peakYear + 1 && lastPlayed >= peakYear - 1;
          })
          .ToList();

        if (candidates.Count == 1)
          return candidates[0].Id;

        if (candidates.Count > 1)
        {
          // Further disambiguate by position if the slot tells us something
          var expectedPos = entry.GetPosition();
          var posMatch = candidates.FirstOrDefault(c =>
            c.PrimaryPosition?.Code != null &&
            PositionMatches(c.PrimaryPosition.Code, expectedPos));
          if (posMatch != null)
            return posMatch.Id;

          // Fall back to first candidate
          return candidates[0].Id;
        }

        // No era match — try the first player result
        return searchResult.People.FirstOrDefault(p => p.IsPlayer)?.Id;
      }
      catch (Exception)
      {
        return null;
      }
    }

    private static bool PositionMatches(string apiPositionCode, Position expected)
    {
      return expected switch
      {
        Position.Pitcher => apiPositionCode == "1",
        Position.Catcher => apiPositionCode == "2",
        Position.FirstBase => apiPositionCode == "3",
        Position.SecondBase => apiPositionCode == "4",
        Position.ThirdBase => apiPositionCode == "5",
        Position.Shortstop => apiPositionCode == "6",
        Position.LeftField => apiPositionCode == "7",
        Position.CenterField => apiPositionCode == "8",
        Position.RightField => apiPositionCode == "9",
        Position.DesignatedHitter => apiPositionCode == "10",
        _ => false
      };
    }

    private Team BuildTeam(
      string teamName,
      MLBPPTeam ppTeam,
      List<RosterFileEntry> entries,
      List<Player> players
    )
    {
      // Use RosterCreator for lineup generation — keyed by DB Id (not MLB Id)
      // to handle manual players (no MLB Id) and duplicates (Ernie Banks)
      var rosterParams = players.Select(p => new RosterParams(
        playerId: p.Id!.Value,
        hitterRating: p.HitterAbilities.GetHitterRating(),
        pitcherRating: p.PitcherAbilities.GetPitcherRating(),
        contact: p.HitterAbilities.Contact,
        power: p.HitterAbilities.Power,
        runSpeed: p.HitterAbilities.RunSpeed,
        primaryPosition: p.PrimaryPosition,
        pitcherType: p.PitcherType,
        positionCapabilityDictionary: p.PositionCapabilities.GetDictionary()
      ));

      var rosterResults = RosterCreator.CreateRosters(rosterParams);

      // Map slot info to pitcher roles by DB Id
      var entryByDbId = new Dictionary<int, RosterFileEntry>();
      foreach (var entry in entries)
      {
        var player = players.FirstOrDefault(p =>
          p.FirstName + " " + p.LastName == entry.PlayerName ||
          entry.PlayerName.Contains(p.LastName));
        if (player?.Id != null && !entryByDbId.ContainsKey(player.Id.Value))
          entryByDbId[player.Id.Value] = entry;
      }

      return new Team
      {
        Name = teamName,
        SourceType = EntitySourceType.Generated,
        GeneratedTeam_LSTeamId = ppTeam.GetLSTeamId(),
        Year = 2000,
        PlayerDefinitions = players.Select(p =>
        {
          entryByDbId.TryGetValue(p.Id!.Value, out var entry);

          return new PlayerRoleDefinition(p.Id!.Value)
          {
            IsAAA = false,
            PitcherRole = entry?.IsStarter == true ? PitcherRole.Starter
              : entry?.IsCloser == true ? PitcherRole.Closer
              : PitcherRole.MiddleReliever
          };
        }),
        NoDHLineup = rosterResults.NoDHLineup.Select(s => new LineupSlot
        {
          PlayerId = (int?)s.PlayerId, // Already using DB Id
          Position = s.Position
        }),
        DHLineup = rosterResults.DHLineup.Select(s => new LineupSlot
        {
          PlayerId = (int?)s.PlayerId, // Already using DB Id
          Position = s.Position
        })
      };
    }
  }
}
