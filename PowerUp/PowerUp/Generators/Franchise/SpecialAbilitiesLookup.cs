using PowerUp.Entities.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerUp.Generators.Franchise
{
  public interface ISpecialAbilitiesLookup
  {
    void ApplySpecialAbilities(string playerName, SpecialAbilities abilities);
  }

  public class JsonSpecialAbilitiesLookup : ISpecialAbilitiesLookup
  {
    private readonly Dictionary<string, Dictionary<string, JsonElement>> _lookup;

    public JsonSpecialAbilitiesLookup(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json)
          ?? new();
        _lookup = new Dictionary<string, Dictionary<string, JsonElement>>();
        foreach (var kvp in raw)
          _lookup[kvp.Key.RemoveAccents()] = kvp.Value;
      }
      else
      {
        _lookup = new();
      }
    }

    // JSON uses display values (2/3/4 for Special2_4, 1-5 for Special1_5, -1/0/1 for PN).
    // Convert display values to enum backing values.
    private static Special2_4 ToSpecial2_4(int display) => display switch
    {
      2 => Special2_4.Two,   // -1
      3 => Special2_4.Three, //  0
      4 => Special2_4.Four,  //  1
      _ => Special2_4.Three
    };

    private static Special1_5 ToSpecial1_5(int display) => display switch
    {
      1 => Special1_5.One,
      2 => Special1_5.Two,
      3 => Special1_5.Three,
      4 => Special1_5.Four,
      5 => Special1_5.Five,
      _ => Special1_5.Three
    };

    private static SpecialPositive_Negative ToPN(int display) => display switch
    {
      -1 => SpecialPositive_Negative.Negative,
       0 => SpecialPositive_Negative.Neutral,
       1 => SpecialPositive_Negative.Positive,
       _ => SpecialPositive_Negative.Neutral
    };

    public void ApplySpecialAbilities(string playerName, SpecialAbilities sa)
    {
      if (!_lookup.TryGetValue(playerName, out var fields))
        return;

      foreach (var (key, val) in fields)
      {
        switch (key)
        {
          // General
          case "IsStar": sa.General.IsStar = val.GetBoolean(); break;
          case "Durability": sa.General.Durability = ToSpecial2_4(val.GetInt32()); break;
          case "Morale": sa.General.Morale = ToPN(val.GetInt32()); break;

          // Hitter - Situational
          case "Consistency" when !IsPitcherField(fields):
            sa.Hitter.SituationalHitting.Consistency = ToSpecial2_4(val.GetInt32()); break;
          case "VersusLefty" when !IsPitcherField(fields):
            sa.Hitter.SituationalHitting.VersusLefty = ToSpecial1_5(val.GetInt32()); break;
          case "IsTableSetter": sa.Hitter.SituationalHitting.IsTableSetter = val.GetBoolean(); break;
          case "IsBackToBackHitter": sa.Hitter.SituationalHitting.IsBackToBackHitter = val.GetBoolean(); break;
          case "IsHotHitter": sa.Hitter.SituationalHitting.IsHotHitter = val.GetBoolean(); break;
          case "IsRallyHitter": sa.Hitter.SituationalHitting.IsRallyHitter = val.GetBoolean(); break;
          case "IsGoodPinchHitter": sa.Hitter.SituationalHitting.IsGoodPinchHitter = val.GetBoolean(); break;
          case "ClutchHitter": sa.Hitter.SituationalHitting.ClutchHitter = ToSpecial1_5(val.GetInt32()); break;

          // Hitter - Approach
          case "IsContactHitter": sa.Hitter.HittingApproach.IsContactHitter = val.GetBoolean(); break;
          case "IsPowerHitter": sa.Hitter.HittingApproach.IsPowerHitter = val.GetBoolean(); break;
          case "IsPushHitter": sa.Hitter.HittingApproach.IsPushHitter = val.GetBoolean(); break;
          case "IsPullHitter": sa.Hitter.HittingApproach.IsPullHitter = val.GetBoolean(); break;
          case "IsSprayHitter": sa.Hitter.HittingApproach.IsSprayHitter = val.GetBoolean(); break;
          case "IsFirstballHitter": sa.Hitter.HittingApproach.IsFirstballHitter = val.GetBoolean(); break;
          case "IsRefinedHitter": sa.Hitter.HittingApproach.IsRefinedHitter = val.GetBoolean(); break;
          case "IsFreeSwinger": sa.Hitter.HittingApproach.IsFreeSwinger = val.GetBoolean(); break;
          case "IsToughOut": sa.Hitter.HittingApproach.IsToughOut = val.GetBoolean(); break;
          case "IsIntimidator" when !IsPitcherField(fields):
            sa.Hitter.HittingApproach.IsIntimidator = val.GetBoolean(); break;
          case "IsSparkplug": sa.Hitter.HittingApproach.IsSparkplug = val.GetBoolean(); break;

          // Hitter - Small Ball
          case "SmallBall": sa.Hitter.SmallBall.SmallBall = ToPN(val.GetInt32()); break;

          // Hitter - Baserunning
          case "BaseRunning": sa.Hitter.BaseRunning.BaseRunning = ToSpecial2_4(val.GetInt32()); break;
          case "Stealing": sa.Hitter.BaseRunning.Stealing = ToSpecial2_4(val.GetInt32()); break;
          case "IsAggressiveRunner": sa.Hitter.BaseRunning.IsAggressiveRunner = val.GetBoolean(); break;
          case "IsToughRunner": sa.Hitter.BaseRunning.IsToughRunner = val.GetBoolean(); break;
          case "WillBreakupDoublePlay": sa.Hitter.BaseRunning.WillBreakupDoublePlay = val.GetBoolean(); break;
          case "WillSlideHeadFirst": sa.Hitter.BaseRunning.WillSlideHeadFirst = val.GetBoolean(); break;

          // Hitter - Fielding
          case "IsGoldGlover": sa.Hitter.Fielding.IsGoldGlover = val.GetBoolean(); break;
          case "CanBarehandCatch": sa.Hitter.Fielding.CanBarehandCatch = val.GetBoolean(); break;
          case "IsAggressiveFielder": sa.Hitter.Fielding.IsAggressiveFielder = val.GetBoolean(); break;
          case "IsPivotMan": sa.Hitter.Fielding.IsPivotMan = val.GetBoolean(); break;
          case "IsErrorProne": sa.Hitter.Fielding.IsErrorProne = val.GetBoolean(); break;
          case "IsGoodBlocker": sa.Hitter.Fielding.IsGoodBlocker = val.GetBoolean(); break;
          case "HasCannonArm": sa.Hitter.Fielding.HasCannonArm = val.GetBoolean(); break;
          case "Throwing": sa.Hitter.Fielding.Throwing = ToSpecial2_4(val.GetInt32()); break;

          // Pitcher - Situational
          case "Consistency" when IsPitcherField(fields):
            sa.Pitcher.SituationalPitching.Consistency = ToSpecial2_4(val.GetInt32()); break;
          case "VersusLefty" when IsPitcherField(fields):
            sa.Pitcher.SituationalPitching.VersusLefty = ToSpecial2_4(val.GetInt32()); break;
          case "Poise": sa.Pitcher.SituationalPitching.Poise = ToSpecial2_4(val.GetInt32()); break;
          case "WithRunnersInScoringPosition": sa.Pitcher.SituationalPitching.WithRunnersInSocringPosition = ToSpecial2_4(val.GetInt32()); break;
          case "IsSlowStarter": sa.Pitcher.SituationalPitching.IsSlowStarter = val.GetBoolean(); break;
          case "IsStarterFinisher": sa.Pitcher.SituationalPitching.IsStarterFinisher = val.GetBoolean(); break;
          case "DoctorK": sa.Pitcher.SituationalPitching.DoctorK = val.GetBoolean(); break;
          case "IsWalkProne": sa.Pitcher.SituationalPitching.IsWalkProne = val.GetBoolean(); break;
          case "Recovery": sa.Pitcher.SituationalPitching.Recovery = ToSpecial2_4(val.GetInt32()); break;

          // Pitcher - Demeanor
          case "IsIntimidator" when IsPitcherField(fields):
            sa.Pitcher.Demeanor.IsIntimidator = val.GetBoolean(); break;
          case "IsHotHead": sa.Pitcher.Demeanor.IsHotHead = val.GetBoolean(); break;

          // Pitcher - Mechanics
          case "GoodDelivery": sa.Pitcher.PitchingMechanics.GoodDelivery = val.GetBoolean(); break;
          case "GoodPace": sa.Pitcher.PitchingMechanics.GoodPace = val.GetBoolean(); break;
          case "GoodReflexes": sa.Pitcher.PitchingMechanics.GoodReflexes = val.GetBoolean(); break;
          case "GoodPickoff": sa.Pitcher.PitchingMechanics.GoodPickoff = val.GetBoolean(); break;

          // Pitcher - Pitch Qualities
          case "FastballLife": sa.Pitcher.PitchQuailities.FastballLife = ToSpecial2_4(val.GetInt32()); break;
          case "Spin": sa.Pitcher.PitchQuailities.Spin = ToSpecial2_4(val.GetInt32()); break;
          case "SafeOrFatPitch": sa.Pitcher.PitchQuailities.SafeOrFatPitch = ToPN(val.GetInt32()); break;
          case "GroundBallOrFlyBallPitcher": sa.Pitcher.PitchQuailities.GroundBallOrFlyBallPitcher = ToPN(val.GetInt32()); break;
          case "GoodLowPitch": sa.Pitcher.PitchQuailities.GoodLowPitch = val.GetBoolean(); break;
        }
      }
    }

    /// <summary>
    /// Disambiguate shared field names (Consistency, VersusLefty, IsIntimidator)
    /// by checking if the entry has pitcher-specific fields.
    /// </summary>
    private static bool IsPitcherField(Dictionary<string, JsonElement> fields)
    {
      return fields.ContainsKey("Poise") || fields.ContainsKey("DoctorK") ||
             fields.ContainsKey("FastballLife") || fields.ContainsKey("Spin") ||
             fields.ContainsKey("Recovery") || fields.ContainsKey("GoodDelivery");
    }
  }

  public class NoOpSpecialAbilitiesLookup : ISpecialAbilitiesLookup
  {
    public void ApplySpecialAbilities(string playerName, SpecialAbilities abilities) { }
  }
}
