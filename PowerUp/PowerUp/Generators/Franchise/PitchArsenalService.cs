using PowerUp.Entities.Players;
using PowerUp.Fetchers.MLBStatsApi;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PowerUp.Generators.Franchise
{
  /// <summary>
  /// Fetches real pitch arsenal data from the MLB Stats API (2008+) and maps it
  /// to game pitch types, replacing the random-by-era heuristic with real repertoire.
  /// </summary>
  public class PitchArsenalService
  {
    private readonly IMLBStatsApiClient _mlbApi;
    private readonly ILogger<PitchArsenalService> _logger;

    public PitchArsenalService(IMLBStatsApiClient mlbApi, ILogger<PitchArsenalService> logger)
    {
      _mlbApi = mlbApi;
      _logger = logger;
    }

    /// <summary>
    /// Attempts to fetch real pitch arsenal data and apply it to the player.
    /// Returns the data source tag: "api", "fallback", or "skip".
    /// </summary>
    public string TryApplyRealArsenal(long mlbPlayerId, int year, Player player)
    {
      // Only pitchers get arsenal overrides
      if (player.PrimaryPosition != Position.Pitcher)
        return "skip";

      // pitchArsenal data starts ~2008 (PITCHf/x era)
      if (year < 2008)
        return "fallback";

      try
      {
        var response = Task.Run(() => _mlbApi.GetPitchArsenal(mlbPlayerId, year))
          .WaitAsync(TimeSpan.FromSeconds(10))
          .ConfigureAwait(false)
          .GetAwaiter()
          .GetResult();

        var splits = response.Stats
          ?.FirstOrDefault()
          ?.Splits
          ?.Where(s => s.Stat?.Percentage > 0 && s.Stat?.Type?.Code != null)
          .OrderByDescending(s => s.Stat!.Percentage)
          .ToList();

        if (splits == null || splits.Count == 0)
          return "fallback";

        ApplyArsenal(splits, player);
        return "api";
      }
      catch (Exception ex)
      {
        _logger.LogWarning("Pitch arsenal fetch failed for {PlayerId}/{Year}: {Message}",
          mlbPlayerId, year, ex.Message);
        return "fallback";
      }
    }

    private void ApplyArsenal(List<PitchArsenalSplit> splits, Player player)
    {
      // Clear existing pitch assignments (set by the random heuristic)
      ClearPitchSlots(player);

      // Derive base movement from ERA (already on the player entity)
      var baseMovement = GetBaseMovement(player.EarnedRunAverage ?? 4.00);
      var movementTier = 0;

      // Track which families have been filled (slot 1 vs slot 2)
      var sliderCount = 0;
      var curveCount = 0;
      var forkCount = 0;
      var sinkerCount = 0;
      var sinkingFbCount = 0;

      foreach (var split in splits)
      {
        var code = split.Stat!.Type!.Code;
        var speed = split.Stat?.AverageSpeed;

        // Fastball variants → TopSpeedMph
        if (code == "FF" || code == "FA")
        {
          if (speed.HasValue)
            player.PitcherAbilities.TopSpeedMph = Math.Min(105, speed.Value + 2); // avg → top
          continue;
        }

        if (code == "FT") // Two-seam fastball
        {
          player.PitcherAbilities.HasTwoSeam = true;
          player.PitcherAbilities.TwoSeamMovement = ClampMovement(baseMovement - 1);
          continue;
        }

        // Off-speed pitches get movement values by priority order
        var movement = ClampMovement(baseMovement - movementTier);
        movementTier++; // Each subsequent pitch gets less movement

        switch (code)
        {
          case "SL": // Slider
          case "ST": // Sweeper (modern slider variant)
            if (sliderCount == 0)
            {
              player.PitcherAbilities.Slider1Type = code == "ST" ? SliderType.Slider : SliderType.Slider;
              player.PitcherAbilities.Slider1Movement = movement;
              sliderCount++;
            }
            else if (sliderCount == 1)
            {
              player.PitcherAbilities.Slider2Type = SliderType.Slider;
              player.PitcherAbilities.Slider2Movement = movement;
              sliderCount++;
            }
            break;

          case "FC": // Cutter
            if (sliderCount == 0)
            {
              player.PitcherAbilities.Slider1Type = SliderType.Cutter;
              player.PitcherAbilities.Slider1Movement = movement;
              sliderCount++;
            }
            else if (sliderCount == 1)
            {
              player.PitcherAbilities.Slider2Type = SliderType.Cutter;
              player.PitcherAbilities.Slider2Movement = movement;
              sliderCount++;
            }
            break;

          case "CU": // Curveball
          case "CB":
            if (curveCount == 0)
            {
              player.PitcherAbilities.Curve1Type = CurveType.Curve;
              player.PitcherAbilities.Curve1Movement = movement;
              curveCount++;
            }
            else if (curveCount == 1)
            {
              player.PitcherAbilities.Curve2Type = CurveType.Curve;
              player.PitcherAbilities.Curve2Movement = movement;
              curveCount++;
            }
            break;

          case "KC": // Knuckle curve
            if (curveCount == 0)
            {
              player.PitcherAbilities.Curve1Type = CurveType.KnuckleCurve;
              player.PitcherAbilities.Curve1Movement = movement;
              curveCount++;
            }
            else if (curveCount == 1)
            {
              player.PitcherAbilities.Curve2Type = CurveType.KnuckleCurve;
              player.PitcherAbilities.Curve2Movement = movement;
              curveCount++;
            }
            break;

          case "SV": // Slurve
            if (curveCount == 0)
            {
              player.PitcherAbilities.Curve1Type = CurveType.Slurve;
              player.PitcherAbilities.Curve1Movement = movement;
              curveCount++;
            }
            else if (curveCount == 1)
            {
              player.PitcherAbilities.Curve2Type = CurveType.Slurve;
              player.PitcherAbilities.Curve2Movement = movement;
              curveCount++;
            }
            break;

          case "EP": // Eephus
            if (curveCount == 0)
            {
              player.PitcherAbilities.Curve1Type = CurveType.SlowCurve;
              player.PitcherAbilities.Curve1Movement = movement;
              curveCount++;
            }
            break;

          case "CH": // Changeup
            if (forkCount == 0)
            {
              player.PitcherAbilities.Fork1Type = ForkType.ChangeUp;
              player.PitcherAbilities.Fork1Movement = movement;
              forkCount++;
            }
            else if (forkCount == 1)
            {
              player.PitcherAbilities.Fork2Type = ForkType.ChangeUp;
              player.PitcherAbilities.Fork2Movement = movement;
              forkCount++;
            }
            break;

          case "FS": // Splitter
            if (forkCount == 0)
            {
              player.PitcherAbilities.Fork1Type = ForkType.Splitter;
              player.PitcherAbilities.Fork1Movement = movement;
              forkCount++;
            }
            else if (forkCount == 1)
            {
              player.PitcherAbilities.Fork2Type = ForkType.Splitter;
              player.PitcherAbilities.Fork2Movement = movement;
              forkCount++;
            }
            break;

          case "KN": // Knuckleball
            if (forkCount == 0)
            {
              player.PitcherAbilities.Fork1Type = ForkType.Knuckleball;
              player.PitcherAbilities.Fork1Movement = movement;
              forkCount++;
            }
            break;

          case "SI": // Sinker
            if (sinkerCount == 0)
            {
              player.PitcherAbilities.Sinker1Type = SinkerType.Sinker;
              player.PitcherAbilities.Sinker1Movement = movement;
              sinkerCount++;
            }
            else if (sinkerCount == 1)
            {
              player.PitcherAbilities.Sinker2Type = SinkerType.Sinker;
              player.PitcherAbilities.Sinker2Movement = movement;
              sinkerCount++;
            }
            break;

          case "SC": // Screwball
            if (sinkerCount == 0)
            {
              player.PitcherAbilities.Sinker1Type = SinkerType.Screwball;
              player.PitcherAbilities.Sinker1Movement = movement;
              sinkerCount++;
            }
            break;

          default:
            // Unknown pitch code — skip
            movementTier--; // Don't count it
            break;
        }
      }
    }

    private static void ClearPitchSlots(Player player)
    {
      var p = player.PitcherAbilities;
      p.HasTwoSeam = false;
      p.TwoSeamMovement = null;
      p.Slider1Type = null; p.Slider1Movement = null;
      p.Slider2Type = null; p.Slider2Movement = null;
      p.Curve1Type = null; p.Curve1Movement = null;
      p.Curve2Type = null; p.Curve2Movement = null;
      p.Fork1Type = null; p.Fork1Movement = null;
      p.Fork2Type = null; p.Fork2Movement = null;
      p.Sinker1Type = null; p.Sinker1Movement = null;
      p.Sinker2Type = null; p.Sinker2Movement = null;
      p.SinkingFastball1Type = null; p.SinkingFastball1Movement = null;
      p.SinkingFastball2Type = null; p.SinkingFastball2Movement = null;
    }

    private static int GetBaseMovement(double era)
    {
      if (era <= 0) return 4; // fallback
      if (era < 2.50) return 6;
      if (era < 3.25) return 5;
      if (era < 4.00) return 4;
      if (era < 5.00) return 3;
      return 2;
    }

    private static int ClampMovement(int value)
    {
      return Math.Max(1, Math.Min(7, value));
    }
  }
}
