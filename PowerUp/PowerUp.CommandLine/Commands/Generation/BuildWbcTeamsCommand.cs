using Microsoft.Extensions.Logging;
using PowerUp.Databases;
using PowerUp.Entities;
using PowerUp.Entities.Players;
using PowerUp.Entities.Rosters;
using PowerUp.Entities.Teams;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Globalization;
using System.Text;

namespace PowerUp.CommandLine.Commands.Generation
{
  public class BuildWbcTeamsCommand(
    ILogger<BuildWbcTeamsCommand> logger
  ) : ICommand
  {
    public Command Build()
    {
      var command = new Command("build-wbc-teams")
      {
        new Option<int>("--roster-id", "Roster ID to modify") { IsRequired = true },
      };
      command.Handler = GetHandler();
      return command;
    }

    // Team USA (AL All-Stars slot = RED) — 22 players
    private static readonly (string Name, string Position, string Role)[] UsaRoster = new[]
    {
      // Starters
      ("Paul Skenes", "SP", "Starter"),
      ("Logan Webb", "SP", "Starter"),
      ("Nolan McLean", "SP", "Starter"),
      ("Jeff Hoffman", "SP", "Starter"),
      ("Manny Barreda", "SP", "Starter"),
      // Closer
      ("Mason Miller", "RP", "Closer"),
      // Relievers
      ("David Bednar", "RP", "MiddleReliever"),
      ("Garrett Whitlock", "RP", "MiddleReliever"),
      // Position players
      ("Bobby Witt Jr.", "SS", "Position"),
      ("Bryce Harper", "1B", "Position"),
      ("Aaron Judge", "RF", "Position"),
      ("Kyle Schwarber", "DH", "Position"),
      ("Alex Bregman", "3B", "Position"),
      ("Roman Anthony", "LF", "Position"),
      ("Will Smith", "C", "Position"),
      ("Brice Turang", "2B", "Position"),
      ("Byron Buxton", "CF", "Position"),
      // Bench
      ("Cal Raleigh", "C", "Bench"),
      ("Gunnar Henderson", "IF", "Bench"),
      ("Pete Crow-Armstrong", "OF", "Bench"),
      ("Paul Goldschmidt", "1B", "Bench"),
      ("Ernie Clement", "IF", "Bench"),
    };

    // Team Venezuela (NL All-Stars slot = BLUE) — 22 players
    private static readonly (string Name, string Position, string Role)[] VenezuelaRoster = new[]
    {
      // Starters
      ("Eduardo Rodriguez", "SP", "Starter"),
      ("Ranger Suárez", "SP", "Starter"),
      ("Keider Montero", "SP", "Starter"),
      ("Germán Márquez", "SP", "Starter"),
      ("Antonio Senzatela", "SP", "Starter"),
      // Closer
      ("Daniel Palencia", "RP", "Closer"),
      // Relievers
      ("Andrés Machado", "RP", "MiddleReliever"),
      ("José Butto", "RP", "MiddleReliever"),
      // Position players
      ("Ronald Acuña Jr.", "RF", "Position"),
      ("Maikel Garcia", "3B", "Position"),
      ("Luis Arraez", "1B", "Position"),
      ("Eugenio Suárez", "DH", "Position"),
      ("Gleyber Torres", "2B", "Position"),
      ("Ezequiel Tovar", "SS", "Position"),
      ("Wilyer Abreu", "LF", "Position"),
      ("Salvador Pérez", "C", "Position"),
      ("Jackson Chourio", "CF", "Position"),
      // Bench
      ("William Contreras", "C", "Bench"),
      ("Willson Contreras", "IF", "Bench"),
      ("Andrés Giménez", "IF", "Bench"),
      ("Anthony Santander", "OF", "Bench"),
      ("Javier Sanoja", "IF", "Bench"),
    };

    private ICommandHandler GetHandler()
    {
      return CommandHandler.Create((int rosterId) =>
      {
        var roster = DatabaseConfig.Database.Load<Roster>(rosterId);
        if (roster is null)
        {
          logger.LogError($"Roster {rosterId} not found");
          return;
        }

        logger.LogInformation($"Building WBC teams for roster '{roster.Name}' (id {rosterId})");

        // Collect all players from all teams in the roster + free agents
        // Use accent-normalized keys so "Suárez" matches "Suarez"
        var allPlayers = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
        void AddPlayer(Player p)
        {
          var fullName = $"{p.FirstName} {p.LastName}".Trim();
          var savedName = p.SavedName ?? "";
          foreach (var name in new[] { fullName, savedName, NormalizeAccents(fullName), NormalizeAccents(savedName) })
          {
            if (!string.IsNullOrEmpty(name) && !allPlayers.ContainsKey(name))
              allPlayers[name] = p;
          }
        }

        foreach (var kvp in roster.TeamIdsByPPTeam)
        {
          var team = DatabaseConfig.Database.Load<Team>(kvp.Value);
          if (team == null) continue;
          foreach (var p in team.GetPlayers())
            AddPlayer(p);
        }
        // Also include free agents
        foreach (var p in roster.GetFreeAgentPlayers())
          AddPlayer(p);
        // Also scan ALL recently generated Player entities (in case some teams use base roster IDs)
        var allPlayerEntities = DatabaseConfig.Database.Query<Player>()
          .Where(p => p.SourceType == EntitySourceType.Generated)
          .ToList();
        foreach (var p in allPlayerEntities)
          AddPlayer(p);

        logger.LogInformation($"Found {allPlayers.Count} unique player name mappings across all teams");

        // Build USA team
        var usaTeam = BuildWbcTeam("Team USA", UsaRoster, allPlayers, MLBPPTeam.AmericanLeagueAllStars, roster.Year ?? 2025);
        // Build Venezuela team
        var venTeam = BuildWbcTeam("Team Venezuela", VenezuelaRoster, allPlayers, MLBPPTeam.NationalLeagueAllStars, roster.Year ?? 2025);

        if (usaTeam == null || venTeam == null)
        {
          logger.LogError("Failed to build WBC teams — too many missing players");
          return;
        }

        // Save teams
        DatabaseConfig.Database.Save(usaTeam);
        DatabaseConfig.Database.Save(venTeam);

        // Replace All-Star team mappings
        roster.TeamIdsByPPTeam[MLBPPTeam.AmericanLeagueAllStars] = usaTeam.Id!.Value;
        roster.TeamIdsByPPTeam[MLBPPTeam.NationalLeagueAllStars] = venTeam.Id!.Value;
        DatabaseConfig.Database.Save(roster);

        logger.LogInformation($"Team USA (RED/AL slot) saved with id {usaTeam.Id}");
        logger.LogInformation($"Team Venezuela (BLUE/NL slot) saved with id {venTeam.Id}");
        logger.LogInformation("WBC team replacement complete");
      });
    }

    private Team? BuildWbcTeam(
      string teamName,
      (string Name, string Position, string Role)[] rosterSpec,
      Dictionary<string, Player> allPlayers,
      MLBPPTeam ppTeam,
      int year)
    {
      logger.LogInformation($"\n=== {teamName} ===");

      var playerDefs = new List<PlayerRoleDefinition>();
      var starters = new List<Player>();
      var foundPlayers = new List<(Player Player, string Position, string Role)>();
      var missing = new List<string>();

      foreach (var (name, pos, role) in rosterSpec)
      {
        // Try exact name, then accent-normalized
        if (allPlayers.TryGetValue(name, out var player) ||
            allPlayers.TryGetValue(NormalizeAccents(name), out player))
        {
          logger.LogInformation($"  OK  {pos,-4} {name}");
          foundPlayers.Add((player, pos, role));
        }
        else
        {
          logger.LogWarning($"  MISS {pos,-4} {name} — not found in generated roster");
          missing.Add(name);
        }
      }

      if (missing.Count > 5)
      {
        logger.LogError($"Too many missing players ({missing.Count}) for {teamName}");
        return null;
      }

      // Build player definitions
      foreach (var (player, pos, role) in foundPlayers)
      {
        var pitcherRole = role switch
        {
          "Starter" => PitcherRole.Starter,
          "Closer" => PitcherRole.Closer,
          "MiddleReliever" => PitcherRole.MiddleReliever,
          _ => PitcherRole.MiddleReliever
        };

        var def = new PlayerRoleDefinition(player.Id!.Value)
        {
          PitcherRole = pitcherRole,
          IsAAA = role == "Bench",
          IsPinchRunner = pos == "IF" && role == "Bench" && player.HitterAbilities.RunSpeed >= 10
        };
        playerDefs.Add(def);
      }

      // Build lineup (DH lineup for WBC)
      var positionMap = new Dictionary<string, Position>
      {
        { "C", Position.Catcher }, { "1B", Position.FirstBase }, { "2B", Position.SecondBase },
        { "3B", Position.ThirdBase }, { "SS", Position.Shortstop },
        { "LF", Position.LeftField }, { "CF", Position.CenterField }, { "RF", Position.RightField },
        { "DH", Position.DesignatedHitter }
      };

      var lineupOrder = foundPlayers
        .Where(f => f.Role == "Position" && positionMap.ContainsKey(f.Position))
        .Select(f => new LineupSlot { PlayerId = f.Player.Id, Position = positionMap[f.Position] })
        .ToList();

      // NoDH lineup: same but DH plays a position or is omitted
      var noDhLineup = lineupOrder
        .Where(s => s.Position != Position.DesignatedHitter)
        .ToList();

      return new Team
      {
        Name = teamName,
        SourceType = EntitySourceType.Generated,
        GeneratedTeam_LSTeamId = ppTeam.GetLSTeamId(),
        Year = year,
        PlayerDefinitions = playerDefs,
        DHLineup = lineupOrder,
        NoDHLineup = noDhLineup
      };
    }

    private static string NormalizeAccents(string text)
    {
      var normalized = text.Normalize(NormalizationForm.FormD);
      var sb = new StringBuilder();
      foreach (var c in normalized)
      {
        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
          sb.Append(c);
      }
      return sb.ToString().Normalize(NormalizationForm.FormC);
    }
  }
}
