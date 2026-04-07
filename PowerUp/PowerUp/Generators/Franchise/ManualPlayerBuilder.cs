using PowerUp.Entities;
using PowerUp.Entities.Players;
using PowerUp.Entities.Players.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerUp.Generators.Franchise
{
  public class ManualPlayerDefinition
  {
    [JsonPropertyName("first_name")] public string FirstName { get; set; } = "";
    [JsonPropertyName("last_name")] public string LastName { get; set; } = "";
    [JsonPropertyName("position")] public string Position { get; set; } = "DH";
    [JsonPropertyName("pitcher_type")] public string? PitcherType { get; set; }
    [JsonPropertyName("peak_season")] public int PeakSeason { get; set; }
    [JsonPropertyName("bats")] public string Bats { get; set; } = "R";
    [JsonPropertyName("throws")] public string Throws { get; set; } = "R";
    [JsonPropertyName("age")] public int Age { get; set; } = 28;
    [JsonPropertyName("birth_month")] public int BirthMonth { get; set; } = 1;
    [JsonPropertyName("birth_day")] public int BirthDay { get; set; } = 1;
    [JsonPropertyName("uniform_number")] public string UniformNumber { get; set; } = "000";
    [JsonPropertyName("skin_color")] public int SkinColor { get; set; } = 1;

    // Display stats
    [JsonPropertyName("batting_average")] public double? BattingAverage { get; set; }
    [JsonPropertyName("home_runs")] public int? HomeRuns { get; set; }
    [JsonPropertyName("rbi")] public int? RBI { get; set; }
    [JsonPropertyName("era")] public double? ERA { get; set; }

    // Hitter abilities
    [JsonPropertyName("trajectory")] public int Trajectory { get; set; } = 2;
    [JsonPropertyName("contact")] public int Contact { get; set; } = 7;
    [JsonPropertyName("power")] public int Power { get; set; } = 100;
    [JsonPropertyName("run_speed")] public int RunSpeed { get; set; } = 7;
    [JsonPropertyName("arm_strength")] public int ArmStrength { get; set; } = 7;
    [JsonPropertyName("fielding")] public int Fielding { get; set; } = 7;
    [JsonPropertyName("error_resistance")] public int ErrorResistance { get; set; } = 7;

    // Pitcher abilities
    [JsonPropertyName("stamina")] public int Stamina { get; set; } = 0;
    [JsonPropertyName("control")] public int Control { get; set; } = 0;
    [JsonPropertyName("top_speed_mph")] public double TopSpeedMph { get; set; } = 74;

    // Pitch arsenal (list of {type, movement} objects)
    [JsonPropertyName("pitches")] public List<ManualPitch>? Pitches { get; set; }

    // Position capabilities (list of positions with grade A-G)
    [JsonPropertyName("position_grades")] public Dictionary<string, string>? PositionGrades { get; set; }
  }

  public class ManualPitch
  {
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("movement")] public int Movement { get; set; } = 3;
  }

  /// <summary>
  /// Builds Player entities from a JSON definition file for players not in statsapi.
  /// </summary>
  public class ManualPlayerBuilder
  {
    private readonly Dictionary<string, ManualPlayerDefinition> _definitions;

    public ManualPlayerBuilder(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        _definitions = JsonSerializer.Deserialize<Dictionary<string, ManualPlayerDefinition>>(json)
          ?? new();
      }
      else
      {
        _definitions = new();
      }
    }

    public bool HasPlayer(string name) => _definitions.ContainsKey(name);

    /// <summary>
    /// Check if a player should use the manual builder for a specific peak season.
    /// Returns true only if the manual definition's peak_season matches.
    /// This prevents MLB players from being manually built when they appear
    /// on a different team at a different peak season (e.g. Ichiro 2004 MLB vs 1994 NPB).
    /// </summary>
    public bool ShouldUseManual(string name, int peakSeason)
    {
      if (!_definitions.TryGetValue(name, out var def))
        return false;
      return def.PeakSeason == peakSeason;
    }

    public Player Build(string name)
    {
      if (!_definitions.TryGetValue(name, out var def))
        throw new InvalidOperationException($"No manual definition for '{name}'");

      var isPitcher = def.Position == "P" || def.Position == "SP" || def.Position == "RP" || def.Position == "CL";
      var player = new PlayerApi().CreateDefaultPlayer(EntitySourceType.Generated, isPitcher);

      player.FirstName = def.FirstName;
      player.LastName = def.LastName;
      player.SavedName = NameUtils.GetSavedName(def.FirstName, def.LastName);
      player.GeneratedPlayer_FullFirstName = def.FirstName;
      player.GeneratedPlayer_FullLastName = def.LastName;
      player.GeneratedPlayer_IsUnedited = true;
      player.Year = def.PeakSeason;
      player.PrimaryPosition = ParsePosition(def.Position);
      player.PitcherType = ParsePitcherType(def.PitcherType, isPitcher);
      player.BattingSide = def.Bats == "L" ? BattingSide.Left : def.Bats == "S" ? BattingSide.Switch : BattingSide.Right;
      player.ThrowingArm = def.Throws == "L" ? ThrowingArm.Left : ThrowingArm.Right;
      player.Age = def.Age;
      player.BirthMonth = def.BirthMonth;
      player.BirthDay = def.BirthDay;
      player.UniformNumber = def.UniformNumber;
      player.Appearance.SkinColor = (SkinColor)(def.SkinColor - 1);

      if (def.BattingAverage.HasValue) player.BattingAverage = def.BattingAverage;
      if (def.HomeRuns.HasValue) player.HomeRuns = def.HomeRuns;
      if (def.RBI.HasValue) player.RunsBattedIn = def.RBI;
      if (def.ERA.HasValue) player.EarnedRunAverage = def.ERA;

      // Position capabilities
      if (def.PositionGrades != null)
      {
        foreach (var (pos, grade) in def.PositionGrades)
        {
          var position = ParsePosition(pos);
          var gradeVal = Enum.Parse<Grade>(grade, ignoreCase: true);
          player.PositionCapabilities.SetGrade(position, gradeVal);
        }
      }
      else
      {
        // Default: A at primary position
        player.PositionCapabilities.SetGrade(player.PrimaryPosition, Grade.A);
      }

      // Hitter abilities
      player.HitterAbilities.Trajectory = def.Trajectory;
      player.HitterAbilities.Contact = def.Contact;
      player.HitterAbilities.Power = def.Power;
      player.HitterAbilities.RunSpeed = def.RunSpeed;
      player.HitterAbilities.ArmStrength = def.ArmStrength;
      player.HitterAbilities.Fielding = def.Fielding;
      player.HitterAbilities.ErrorResistance = def.ErrorResistance;

      // Pitcher abilities
      player.PitcherAbilities.TopSpeedMph = def.TopSpeedMph;
      player.PitcherAbilities.Control = def.Control;
      player.PitcherAbilities.Stamina = def.Stamina;

      // Pitch arsenal
      if (def.Pitches != null)
      {
        foreach (var pitch in def.Pitches)
          ApplyPitch(player.PitcherAbilities, pitch.Type, pitch.Movement);
      }

      return player;
    }

    private static Position ParsePosition(string pos) => pos.ToUpperInvariant() switch
    {
      "P" or "SP" or "RP" or "CL" => Entities.Players.Position.Pitcher,
      "C" => Entities.Players.Position.Catcher,
      "1B" => Entities.Players.Position.FirstBase,
      "2B" => Entities.Players.Position.SecondBase,
      "3B" => Entities.Players.Position.ThirdBase,
      "SS" => Entities.Players.Position.Shortstop,
      "LF" => Entities.Players.Position.LeftField,
      "CF" => Entities.Players.Position.CenterField,
      "RF" => Entities.Players.Position.RightField,
      "DH" => Entities.Players.Position.DesignatedHitter,
      _ => Entities.Players.Position.DesignatedHitter
    };

    private static PitcherType ParsePitcherType(string? type, bool isPitcher) => type?.ToUpperInvariant() switch
    {
      "STARTER" => Entities.Players.PitcherType.Starter,
      "RELIEVER" => Entities.Players.PitcherType.Reliever,
      "CLOSER" => Entities.Players.PitcherType.Closer,
      _ => isPitcher ? Entities.Players.PitcherType.Starter : Entities.Players.PitcherType.SwingMan
    };

    private static void ApplyPitch(PitcherAbilities pa, string code, int movement)
    {
      switch (code.ToUpperInvariant())
      {
        case "2S": pa.HasTwoSeam = true; pa.TwoSeamMovement = movement; break;
        case "SL": pa.Slider1Type ??= SliderType.Slider; pa.Slider1Movement ??= movement; break;
        case "CU": pa.Slider1Type ??= SliderType.Cutter; pa.Slider1Movement ??= movement; break;
        case "CB": pa.Curve1Type ??= CurveType.Curve; pa.Curve1Movement ??= movement; break;
        case "S-CB": pa.Curve1Type ??= CurveType.SlowCurve; pa.Curve1Movement ??= movement; break;
        case "D-CB": pa.Curve1Type ??= CurveType.DropCurve; pa.Curve1Movement ??= movement; break;
        case "SLV": pa.Curve1Type ??= CurveType.Slurve; pa.Curve1Movement ??= movement; break;
        case "CH": pa.Fork1Type ??= ForkType.ChangeUp; pa.Fork1Movement ??= movement; break;
        case "FK": pa.Fork1Type ??= ForkType.Forkball; pa.Fork1Movement ??= movement; break;
        case "SPL": pa.Fork1Type ??= ForkType.Splitter; pa.Fork1Movement ??= movement; break;
        case "KN": pa.Fork1Type ??= ForkType.Knuckleball; pa.Fork1Movement ??= movement; break;
        case "SC": pa.Sinker1Type ??= SinkerType.Screwball; pa.Sinker1Movement ??= movement; break;
        case "SNK": pa.Sinker1Type ??= SinkerType.Sinker; pa.Sinker1Movement ??= movement; break;
        case "SHU": pa.SinkingFastball1Type ??= SinkingFastballType.Shuuto; pa.SinkingFastball1Movement ??= movement; break;
        case "SIFB": pa.SinkingFastball1Type ??= SinkingFastballType.SinkingFastball; pa.SinkingFastball1Movement ??= movement; break;
      }
    }
  }

  public static class PositionCapabilitiesExtensions
  {
    public static void SetGrade(this PositionCapabilities caps, Position pos, Grade grade)
    {
      switch (pos)
      {
        case Entities.Players.Position.Pitcher: caps.Pitcher = grade; break;
        case Entities.Players.Position.Catcher: caps.Catcher = grade; break;
        case Entities.Players.Position.FirstBase: caps.FirstBase = grade; break;
        case Entities.Players.Position.SecondBase: caps.SecondBase = grade; break;
        case Entities.Players.Position.ThirdBase: caps.ThirdBase = grade; break;
        case Entities.Players.Position.Shortstop: caps.Shortstop = grade; break;
        case Entities.Players.Position.LeftField: caps.LeftField = grade; break;
        case Entities.Players.Position.CenterField: caps.CenterField = grade; break;
        case Entities.Players.Position.RightField: caps.RightField = grade; break;
      }
    }
  }
}
