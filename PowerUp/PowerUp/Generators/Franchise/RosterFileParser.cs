using PowerUp.Entities;
using PowerUp.Entities.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PowerUp.Generators.Franchise
{
  public class RosterFileEntry
  {
    public string TeamName { get; set; } = "";
    public MLBPPTeam? PPTeam { get; set; }
    public string Slot { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int? PeakSeason { get; set; }
    public string Notes { get; set; } = "";

    /// <summary>Map slot string to a Position + pitcher role hint.</summary>
    public Position GetPosition() => Slot switch
    {
      "SP1" or "SP2" or "SP3" or "SP4" or "SP5" => Position.Pitcher,
      "CL" or "RP" => Position.Pitcher,
      "C" => Position.Catcher,
      "1B" => Position.FirstBase,
      "2B" => Position.SecondBase,
      "3B" => Position.ThirdBase,
      "SS" => Position.Shortstop,
      "LF" => Position.LeftField,
      "CF" => Position.CenterField,
      "RF" => Position.RightField,
      "DH" => Position.DesignatedHitter,
      "BN" => Position.DesignatedHitter, // bench players get DH as default
      _ => Position.DesignatedHitter
    };

    public bool IsStarter => Slot.StartsWith("SP");
    public bool IsCloser => Slot == "CL";
    public bool IsReliever => Slot == "RP";
    public bool IsBench => Slot == "BN";
  }

  public static class RosterFileParser
  {
    // Maps franchise header names to MLBPPTeam enum values
    private static readonly Dictionary<string, MLBPPTeam> TeamNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
      ["NEW YORK YANKEES"] = MLBPPTeam.Yankees,
      ["BOSTON RED SOX"] = MLBPPTeam.RedSox,
      ["BALTIMORE ORIOLES"] = MLBPPTeam.Orioles,
      ["TORONTO BLUE JAYS"] = MLBPPTeam.BlueJays,
      ["TAMPA BAY RAYS"] = MLBPPTeam.Rays,
      ["DETROIT TIGERS"] = MLBPPTeam.Tigers,
      ["CLEVELAND GUARDIANS (INDIANS)"] = MLBPPTeam.Indians,
      ["CHICAGO WHITE SOX"] = MLBPPTeam.WhiteSox,
      ["KANSAS CITY ROYALS"] = MLBPPTeam.Royals,
      ["MINNESOTA TWINS"] = MLBPPTeam.Twins,
      ["LOS ANGELES ANGELS"] = MLBPPTeam.Angels,
      ["OAKLAND ATHLETICS"] = MLBPPTeam.Athletics,
      ["SEATTLE MARINERS"] = MLBPPTeam.Mariners,
      ["TEXAS RANGERS"] = MLBPPTeam.Rangers,
      ["ATLANTA BRAVES"] = MLBPPTeam.Braves,
      ["MIAMI MARLINS"] = MLBPPTeam.Marlins,
      ["NEW YORK METS"] = MLBPPTeam.Mets,
      ["PHILADELPHIA PHILLIES"] = MLBPPTeam.Phillies,
      ["WASHINGTON NATIONALS"] = MLBPPTeam.Nationals,
      ["WASHINGTON NATIONALS / MONTREAL EXPOS"] = MLBPPTeam.Nationals,
      ["CHICAGO CUBS"] = MLBPPTeam.Cubs,
      ["CINCINNATI REDS"] = MLBPPTeam.Reds,
      ["HOUSTON ASTROS"] = MLBPPTeam.Astros,
      ["MILWAUKEE BREWERS"] = MLBPPTeam.Brewers,
      ["PITTSBURGH PIRATES"] = MLBPPTeam.Pirates,
      ["ST. LOUIS CARDINALS"] = MLBPPTeam.Cardinals,
      ["ARIZONA DIAMONDBACKS"] = MLBPPTeam.DBacks,
      ["COLORADO ROCKIES"] = MLBPPTeam.Rockies,
      ["LOS ANGELES DODGERS"] = MLBPPTeam.Dodgers,
      ["SAN DIEGO PADRES"] = MLBPPTeam.Padres,
      ["SAN FRANCISCO GIANTS"] = MLBPPTeam.Giants,
      // Expansion teams → All-Star slots
      ["ALL-TIME JAPAN (NPB)"] = MLBPPTeam.AmericanLeagueAllStars,
      ["ALL-TIME WORLD"] = MLBPPTeam.NationalLeagueAllStars,
    };

    /// <summary>
    /// Parse the roster markdown file into structured entries.
    /// Skips entries with no peak season (managers, etc.).
    /// </summary>
    public static List<RosterFileEntry> Parse(string filePath)
    {
      var lines = File.ReadAllLines(filePath);
      var entries = new List<RosterFileEntry>();
      string currentTeam = "";
      MLBPPTeam? currentPPTeam = null;

      foreach (var line in lines)
      {
        // Detect team headers: ### TEAM NAME
        if (line.StartsWith("### "))
        {
          currentTeam = line.Substring(4).Trim();
          currentPPTeam = TeamNameMap.TryGetValue(currentTeam, out var mapped) ? mapped : null;
          continue;
        }

        // Parse table rows: | Slot | Player | Peak Season | Notes |
        if (!line.StartsWith("| ") || line.StartsWith("| Slot") || line.StartsWith("|---"))
          continue;

        var columns = line.Split('|', StringSplitOptions.None)
          .Select(c => c.Trim())
          .Where(c => c.Length > 0)
          .ToArray();

        if (columns.Length < 3)
          continue;

        var slot = columns[0].Trim();
        var rawName = columns[1].Trim();
        var seasonStr = columns[2].Trim();
        var notes = columns.Length > 3 ? columns[3].Trim() : "";

        // Clean player name: remove annotations like "*(pitcher)*", "*(hitter)*"
        var playerName = Regex.Replace(rawName, @"\s*\*.*?\*\s*", "").Trim();

        // Handle swap entries like "Tim Raines → swap: Juan Soto"
        var swapMatch = Regex.Match(playerName, @"→\s*swap:\s*(.+)$");
        if (swapMatch.Success)
          playerName = swapMatch.Groups[1].Value.Trim();

        // Parse season — skip entries with no valid year (e.g. "—" for managers)
        if (!int.TryParse(seasonStr, out var peakSeason))
          continue;

        entries.Add(new RosterFileEntry
        {
          TeamName = currentTeam,
          PPTeam = currentPPTeam,
          Slot = slot,
          PlayerName = playerName,
          PeakSeason = peakSeason,
          Notes = notes,
        });
      }

      return entries;
    }

    /// <summary>Group parsed entries by team.</summary>
    public static Dictionary<MLBPPTeam, List<RosterFileEntry>> GroupByTeam(List<RosterFileEntry> entries)
    {
      return entries
        .Where(e => e.PPTeam.HasValue)
        .GroupBy(e => e.PPTeam!.Value)
        .ToDictionary(g => g.Key, g => g.ToList());
    }
  }
}
