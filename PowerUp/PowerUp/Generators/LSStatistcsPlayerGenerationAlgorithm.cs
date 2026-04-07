using PowerUp;
using PowerUp.Entities.Players;
using PowerUp.Fetchers.MLBStatsApi;
using PowerUp.Libraries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerUp.Generators
{
  public class LSStatistcsPlayerGenerationAlgorithm : PlayerGenerationAlgorithm
  {
    public override HashSet<PlayerGenerationDataset> DatasetDependencies => new HashSet<PlayerGenerationDataset>() 
    { 
      PlayerGenerationDataset.LSPlayerInfo,
      PlayerGenerationDataset.LSHittingStats,
      PlayerGenerationDataset.LSFieldingStats,
      PlayerGenerationDataset.LSPitchingStats,
      PlayerGenerationDataset.BaseballReferenceIdDataset
    };

    public LSStatistcsPlayerGenerationAlgorithm
    ( IVoiceLibrary voiceLibrary
    , IComplexionGuesser complexionGuesser
    , IBattingStanceGuesser battingStanceGuesser
    , IPitchingMechanicsGuesser pitchingMechanicsGuesser
    , Franchise.IPre2008PitchArsenalLookup pre2008ArsenalLookup
    , Franchise.IHotZoneLookup? hotZoneLookup = null
    , Franchise.IComplexionLookup? complexionLookup = null
    , Franchise.ISpecialAbilitiesLookup? specialAbilitiesLookup = null
    , Franchise.IPlayerFormLookup? playerFormLookup = null
    , Franchise.IAppearanceLookup? appearanceLookup = null
    , bool enhancedMode = true
    )
    {
      // Player Info
      SetProperty("FirstName", (player, data) => player.FirstName = data.PlayerInfo!.FirstNameUsed.RemoveAccents().ShortenNameToLength(14));
      SetProperty("LastName", (player, data) => player.LastName = data.PlayerInfo!.LastName.RemoveAccents().ShortenNameToLength(14));
      SetProperty(new SavedName());
      SetProperty(new UniformNumber());
      SetProperty("PrimaryPosition", (player, data) => player.PrimaryPosition = data.PrimaryPosition);
      SetProperty(new PitcherTypeSetter());
      SetProperty("VoiceId", (player, data) => player.VoiceId = voiceLibrary.FindClosestTo(data.PlayerInfo!.FirstNameUsed, data.PlayerInfo!.LastName).Key);
      SetProperty("BattingSide", (player, data) => player.BattingSide = data.PlayerInfo!.BattingSide);
      SetProperty("ThrowingArm", (player, data) => player.ThrowingArm = data.PlayerInfo!.ThrowingArm);
      SetProperty("GeneratedPlayer_FullFirstName", (player, data) => player.GeneratedPlayer_FullFirstName = data.PlayerInfo!.FirstNameUsed);
      SetProperty("GeneratedPlayer_FullLastName", (player, data) => player.GeneratedPlayer_FullLastName = data.PlayerInfo!.LastName);
      SetProperty("GeneratedPlayer_ProDebutDate", (player, data) => player.GeneratedPlayer_ProDebutDate = data.PlayerInfo!.ProDebutDate);
      SetProperty(new AgeSetter());
      SetProperty(new YearsInMajorsSetter());
      SetProperty(new BirthMonthSetter());
      SetProperty(new BirthDaySetter());
      SetProperty(new BattingAverageSetter());
      SetProperty(new RBIsSetter());
      SetProperty(new HomeRunsSetter());
      SetProperty(new ERASetter());

      // Appearance
      SetProperty(new ComplexionSetter(complexionGuesser));

      // Position Capabilities
      SetProperty(new PitcherCapabilitySetter());
      SetProperty(new CatcherCapabilitySetter());
      SetProperty(new FirstBaseCapabilitySetter());
      SetProperty(new SecondBaseCapabilitySetter());
      SetProperty(new ThirdBaseCapabilitySetter());
      SetProperty(new ShortstopCapabilitySetter());
      SetProperty(new LeftFieldCapabilitySetter());
      SetProperty(new CenterFieldCapabilitySetter());
      SetProperty(new RightFieldCapabilitySetter());

      // Hitter Abilities
      SetProperty(new TrajectorySetter());
      SetProperty(new ContactSetter());
      SetProperty(new PowerSetter());
      SetProperty(new RunSpeedSetter());
      SetProperty(new ArmStrengthSetter());
      SetProperty(new FieldingSetter());
      SetProperty(new ErrorResistanceSetter());

      // Hot Zones (from real API data when available)
      SetProperty(new HotZoneSetter());

      // Pitcher Abilities
      SetProperty(new ControlSetter());
      SetProperty(new StaminaSetter());
      SetProperty(new TopSpeedSetter());
      // In enhanced mode, use curated pre-2008 arsenal as fallback.
      // In non-enhanced mode, only API data + heuristic.
      SetProperty(new PitchArsenalSetter(
        enhancedMode ? pre2008ArsenalLookup : new Franchise.NoOpPre2008PitchArsenalLookup()));

      // TODO: Do Special Abilities

      // Batting Stance and Pitching Mechanics
      SetProperty(new BattingStanceSetter(battingStanceGuesser));
      SetProperty(new PitchingMechanicsSetter(pitchingMechanicsGuesser));

      // Algorithmic appearance — always applied regardless of enhancedMode.
      // Gives every generated player varied, era-appropriate appearance.
      SetProperty(new AlgorithmicAppearanceSetter());

      // JSON Lookup overrides (curated data). Only applied in enhanced mode.
      // Same-key setters act as fallbacks (only run if the earlier setter returned false);
      // different-key setters always run and override formula-based values.
      if (enhancedMode)
      {
        if (hotZoneLookup != null)
          SetProperty(new HotZoneLookupSetter(hotZoneLookup));
        if (complexionLookup != null)
          SetProperty(new ComplexionLookupSetter(complexionLookup));
        if (specialAbilitiesLookup != null)
          SetProperty(new SpecialAbilitiesLookupSetter(specialAbilitiesLookup));
        if (playerFormLookup != null)
          SetProperty(new PlayerFormLookupSetter(playerFormLookup));
        if (appearanceLookup != null)
          SetProperty(new AppearanceLookupSetter(appearanceLookup));
      }
    }

    public class SavedName : PlayerPropertySetter
    {
      public override string PropertyKey => "SavedName";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.SavedName = NameUtils.GetSavedName(
          datasetCollection.PlayerInfo!.FirstNameUsed, 
          datasetCollection.PlayerInfo!.LastName
        );
        return true;
      }
    }

    public class UniformNumber : PlayerPropertySetter
    {
      public override string PropertyKey => "UniformNumber";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        var uniformNumber = datasetCollection.PlayerInfo!.UniformNumber;
        if(uniformNumber == null || !int.TryParse(uniformNumber, out var _))
          return false;

        player.UniformNumber = uniformNumber;
        return true;
      }
    }

    public class PitcherTypeSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "PitcherRole";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (datasetCollection.PitchingStats == null)
        {
          if(datasetCollection.PrimaryPosition == Position.Pitcher)
            player.GeneratorWarnings.Add(GeneratorWarning.NoPitchingStats(PropertyKey));
          return false;
        }

        if (datasetCollection.PitchingStats.GamesStarted > 10)
          player.PitcherType = PitcherType.Starter;
        else if (datasetCollection.PitchingStats.SaveOpportunities > 20)
          player.PitcherType = PitcherType.Closer;
        else if(datasetCollection.PitchingStats.SaveOpportunities == null && datasetCollection.PitchingStats.GamesFinished > 30)
          player.PitcherType = PitcherType.Closer;
        else
          player.PitcherType = PitcherType.Reliever;

        return true;
      }
    }

    public class BattingStanceSetter : PlayerPropertySetter
    {
      private readonly IBattingStanceGuesser _battingStanceGuesser;
      public override string PropertyKey => "BattingStanceId";

      public BattingStanceSetter(IBattingStanceGuesser battingStanceGuesser)
      {
        _battingStanceGuesser = battingStanceGuesser;
      }
      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.BattingStanceId = _battingStanceGuesser.GuessBattingStance
          ( datasetCollection.Year
          , player.HitterAbilities.Contact
          , player.HitterAbilities.Power
          );
        return true;
      }
    }

    public class PitchingMechanicsSetter : PlayerPropertySetter
    {
      private readonly IPitchingMechanicsGuesser _pitchingMechanicsGuesser;
      public override string PropertyKey => "PitchingMechanicsId";

      public PitchingMechanicsSetter(IPitchingMechanicsGuesser pitchingMechanicsGuesser)
      {
        _pitchingMechanicsGuesser = pitchingMechanicsGuesser;
      }
      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PitchingMechanicsId = _pitchingMechanicsGuesser.GuessPitchingMechanics(datasetCollection.Year, player.PitcherType);
        return true;
      }
    }

    public class AgeSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "Age";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (!datasetCollection.PlayerInfo!.BirthDate.HasValue)
        {
          player.GeneratorWarnings.Add(GeneratorWarning.NoBirthDate(PropertyKey));
          return false;
        }

        player.Age = MLBSeasonUtils.GetEstimatedStartOfSeason(datasetCollection.Year).YearsElapsedSince(datasetCollection.PlayerInfo.BirthDate.Value);
        return true;
      }
    }

    public class YearsInMajorsSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "YearsInMajors";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (!datasetCollection.PlayerInfo!.ProDebutDate.HasValue)
        {
          player.GeneratorWarnings.Add(GeneratorWarning.NoDebutDate(PropertyKey));
          return false;
        }

        var yearsInMajors = MLBSeasonUtils.GetEstimatedStartOfSeason(datasetCollection.Year).YearsElapsedSince(datasetCollection.PlayerInfo.ProDebutDate.Value);
        player.YearsInMajors = yearsInMajors < 0
          ? 0
          : yearsInMajors;
        return true;
      }
    }

    public class BirthMonthSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "BirthMonth";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (!datasetCollection.PlayerInfo!.BirthDate.HasValue)
        {
          player.GeneratorWarnings.Add(GeneratorWarning.NoBirthDate(PropertyKey));
          return false;
        }

        player.BirthMonth = datasetCollection.PlayerInfo.BirthDate.Value.Month;
        return true;
      }
    }

    public class BirthDaySetter : PlayerPropertySetter
    {
      public override string PropertyKey => "BirthDay";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (!datasetCollection.PlayerInfo!.BirthDate.HasValue)
        {
          player.GeneratorWarnings.Add(GeneratorWarning.NoBirthDate(PropertyKey));
          return false;
        }

        player.BirthDay = datasetCollection.PlayerInfo.BirthDate.Value.Day;
        return true;
      }
    }

    public class BattingAverageSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "BattingAverage";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (datasetCollection.HittingStats == null)
        {
          if (datasetCollection.PrimaryPosition != Position.Pitcher)
            player.GeneratorWarnings.Add(GeneratorWarning.NoHittingStats(PropertyKey));
          return false;
        }

        var battingAverage = datasetCollection.HittingStats.BattingAverage;
        if (!battingAverage.HasValue || double.IsNaN(battingAverage.Value))
          return false;

        player.BattingAverage = battingAverage;
        return true;
      }
    }

    public class RBIsSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "RunsBattedIn";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (datasetCollection.HittingStats == null)
        {
          if (datasetCollection.PrimaryPosition != Position.Pitcher)
            player.GeneratorWarnings.Add(GeneratorWarning.NoHittingStats(PropertyKey));
          return false;
        }

        player.RunsBattedIn = datasetCollection.HittingStats.RunsBattedIn;
        return true;
      }
    }

    public class HomeRunsSetter : PlayerPropertySetter
    {
      public override string PropertyKey => "HomeRuns";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (datasetCollection.HittingStats == null)
        {
          if (datasetCollection.PrimaryPosition != Position.Pitcher)
            player.GeneratorWarnings.Add(GeneratorWarning.NoHittingStats(PropertyKey));
          return false;
        }

        player.HomeRuns = datasetCollection.HittingStats.HomeRuns;
        return true;
      }
    }

    public class ERASetter : PlayerPropertySetter
    {
      public override string PropertyKey => "EarnedRunAverage";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        if (datasetCollection.PitchingStats == null)
        {
          if (datasetCollection.PrimaryPosition == Position.Pitcher)
            player.GeneratorWarnings.Add(GeneratorWarning.NoPitchingStats(PropertyKey));
          return false;
        }

        var era = datasetCollection.PitchingStats.EarnedRunAverage;
        if (!era.HasValue || double.IsNaN(era.Value))
          return false;

        player.EarnedRunAverage = era;
        return true;
      }
    }

    public class ComplexionSetter : PlayerPropertySetter
    {
      private readonly IComplexionGuesser _complexionGuesser;
      public override string PropertyKey => "Appearance_Complexion";

      public ComplexionSetter(IComplexionGuesser complexionGuesser)
      {
        _complexionGuesser = complexionGuesser;
      }

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        var complexion = _complexionGuesser.GuessComplexion(datasetCollection.Year, datasetCollection.PlayerInfo!.BirthCountry);
        player.Appearance.Complexion = complexion;
        return true;
      }
    }

    public abstract class PositionCapabilitySetter : PlayerPropertySetter
    {
      protected Grade GetGradeForPosition(Position position, PlayerGenerationData datasetCollection)
      {
        var primaryPosition = datasetCollection.PrimaryPosition;
        if (position == primaryPosition)
          return Grade.A;

        LSFieldingStats? stats = null;
        datasetCollection.FieldingStats?.FieldingByPosition.TryGetValue(position, out stats);
        if (stats != null && stats.TotalChances > 75)
          return Grade.B;
        if (stats != null && stats.TotalChances > 50)
          return Grade.C;
        if (stats != null && stats.TotalChances > 25)
          return Grade.D;

        switch (position)
        {
          case Position.Pitcher:
            return Grade.G;
          case Position.Catcher:
            return Grade.G;
          case Position.FirstBase:
            return primaryPosition == Position.SecondBase || primaryPosition == Position.ThirdBase || primaryPosition == Position.Shortstop
              ? Grade.F
              : Grade.G;
          case Position.SecondBase:
            return primaryPosition == Position.ThirdBase || primaryPosition == Position.Shortstop
              ? Grade.E
              : primaryPosition == Position.FirstBase
                ? Grade.F
                : Grade.G;
          case Position.ThirdBase:
            return primaryPosition == Position.SecondBase || primaryPosition == Position.Shortstop
              ? Grade.E
              : primaryPosition == Position.FirstBase
                ? Grade.F
                : Grade.G;
          case Position.Shortstop:
            return primaryPosition == Position.SecondBase || primaryPosition == Position.ThirdBase
              ? Grade.E
              : primaryPosition == Position.FirstBase
                ? Grade.F
                : Grade.G;
          case Position.LeftField:
            return primaryPosition == Position.CenterField || primaryPosition == Position.RightField
              ? Grade.E
              : Grade.G;
          case Position.CenterField:
            return primaryPosition == Position.LeftField || primaryPosition == Position.RightField
              ? Grade.E
              : Grade.G;
          case Position.RightField:
            return primaryPosition == Position.LeftField || primaryPosition == Position.CenterField
              ? Grade.E
              : Grade.G;
          default:
            return Grade.G;
        }
      }
    }

    public class PitcherCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_Pitcher";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.Pitcher = GetGradeForPosition(Position.Pitcher, datasetCollection);
        return true;
      }
    }

    public class CatcherCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_Catcher";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.Catcher = GetGradeForPosition(Position.Catcher, datasetCollection);
        return true;
      }
    }

    public class FirstBaseCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_FirstBase";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.FirstBase = GetGradeForPosition(Position.FirstBase, datasetCollection);
        return true;
      }
    }

    public class SecondBaseCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_SecondBase";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.SecondBase = GetGradeForPosition(Position.SecondBase, datasetCollection);
        return true;
      }
    }


    public class ThirdBaseCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_ThirdBase";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.ThirdBase = GetGradeForPosition(Position.ThirdBase, datasetCollection);
        return true;
      }
    }

    public class ShortstopCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_Shortstop";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.Shortstop = GetGradeForPosition(Position.Shortstop, datasetCollection);
        return true;
      }
    }

    public class LeftFieldCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_LeftField";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.LeftField = GetGradeForPosition(Position.LeftField, datasetCollection);
        return true;
      }
    }

    public class CenterFieldCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_CenterField";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.CenterField = GetGradeForPosition(Position.CenterField, datasetCollection);
        return true;
      }
    }

    public class RightFieldCapabilitySetter : PositionCapabilitySetter
    {
      public override string PropertyKey => "PositionCapabilities_RightField";

      public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
      {
        player.PositionCapabilities.RightField = GetGradeForPosition(Position.RightField, datasetCollection);
        return true;
      }
    }
  }

  public class TrajectorySetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_Trajcetory";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      if(datasetCollection.HittingStats == null)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoHittingStats(PropertyKey));
        return false;
      }

      if (!datasetCollection.HittingStats.HomeRuns.HasValue)
      {

        return false;
      }

      player.HitterAbilities.Trajectory = GetTrajectoryForHomeRuns(datasetCollection.HittingStats.HomeRuns.Value);
      return true;
    }

    private int GetTrajectoryForHomeRuns(int homeRuns)
    {
      if (homeRuns < 2)
        return 1;
      else if (homeRuns < 14)
        return 2;
      else if (homeRuns < 35)
        return 3;
      else
        return 4;
    }
  }

  public class ContactSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_Contact";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      if(datasetCollection.HittingStats == null)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoHittingStats(PropertyKey));
        return false;
      }

      if (datasetCollection.HittingStats.AtBats < 100 || !datasetCollection.HittingStats.BattingAverage.HasValue)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientHittingStats(PropertyKey));
        return false;
      }

      player.HitterAbilities.Contact = GetContactForBattingAverage(datasetCollection.HittingStats.BattingAverage.Value);
      return true;
    }

    private int GetContactForBattingAverage(double battingAverage)
    {
      if (battingAverage < 0.15)
        return 1;
      else if (battingAverage < 0.175)
        return 2;
      else if (battingAverage < .2)
        return 3;
      else if (battingAverage < .23)
        return 4;
      else if (battingAverage < .24)
        return 5;
      else if (battingAverage < .25)
        return 6;
      else if (battingAverage < .263)
        return 7;
      else if (battingAverage < .282)
        return 8;
      else if (battingAverage < .295)
        return 9;
      else if (battingAverage < .31)
        return 10;
      else if (battingAverage < .32)
        return 11;
      else if (battingAverage < .335)
        return 12;
      else if (battingAverage < .345)
        return 13;
      else if (battingAverage < .35)
        return 14;
      else
        return 15;
    }
  }

  public class PowerSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_Power";
    private static readonly IEnumerable<(double atBatsPerHomerun, double power)> powerFunctionCoordinates = new (double x, double y)[]
    {
      (10.5, 250),
      (10.75, 245),
      (11, 240),
      (11.25, 235),
      (11.6, 230),
      (11.9, 225),
      (12.3, 220),
      (12.8, 215),
      (13.4, 210),
      (14, 205),
      (14.7, 200),
      (15.5, 195),
      (16.35, 190),
      (17.45, 185),
      (18.45, 180),
      (19.5, 175),
      (20.6, 170),
      (21.75, 165),
      (23.5, 160),
      (25, 155),
      (26.8, 150),
      (28.6, 145),
      (31, 140),
      (35, 135),
      (41, 130),
      (50, 125),
      (65, 120),
      (90, 115),
      (145, 110),
      (250, 105),
      (460, 100),
      (900, 95)
    };
    private static readonly Func<double, double> calculatePower = MathUtils.PiecewiseFunctionFor(powerFunctionCoordinates);

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      if (datasetCollection.HittingStats == null)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoHittingStats(PropertyKey));
        return false;
      }

      if (!datasetCollection.HittingStats.AtBats.HasValue || datasetCollection.HittingStats.AtBats < 100 || !datasetCollection.HittingStats.HomeRuns.HasValue)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientHittingStats(PropertyKey));
        return false;
      }

      if (datasetCollection.HittingStats.HomeRuns == 0)
      {
        if (player.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(new GeneratorWarning(PropertyKey, "NoHomeRuns", "No home-runs hit by player"));
        return false;
      }

      var atBatsPerHomeRun = (double) datasetCollection.HittingStats.AtBats.Value / datasetCollection.HittingStats.HomeRuns.Value;
      var power = calculatePower(atBatsPerHomeRun);
      player.HitterAbilities.Power = power.Round().MinAt(0).CapAt(255);
      return true;
    }
  }

  public class RunSpeedSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_RunSpeed";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      // Use real sabermetrics SPD (speed score) when available — works for ALL eras
      if (datasetCollection.SabermetricsSPD.HasValue)
      {
        var spd = datasetCollection.SabermetricsSPD.Value;
        // SPD 0-2 → RunSpeed 4-6, SPD 2-4 → 6-8, SPD 4-6 → 8-10, SPD 6-8 → 10-12, SPD 8+ → 12-15
        var runSpeed = MapSpdToRunSpeed(spd);
        player.HitterAbilities.RunSpeed = runSpeed.Round().MinAt(1).CapAt(15);
        return true;
      }

      // Fallback: original heuristic (position base + SB + runs/AB)
      var baseRunSpeed = GetBaseRunSpeedForPosition(datasetCollection.PrimaryPosition);
      var stolenBaseBonus = .06 * (datasetCollection.HittingStats?.StolenBases ?? 0);
      var runsPerAtBatBonus = 7 * GetRunsPerAtBat(datasetCollection, warning => player.GeneratorWarnings.Add(warning));
      var runSpeedHeuristic = baseRunSpeed + stolenBaseBonus + runsPerAtBatBonus;
      player.HitterAbilities.RunSpeed = runSpeedHeuristic.Round().MinAt(1).CapAt(15);
      return true;
    }

    private static double MapSpdToRunSpeed(double spd)
    {
      // SPD (FanGraphs speed score) typically ranges 0-10
      // Map to game's RunSpeed scale (1-15)
      var gradient = MathUtils.BuildLinearGradientFunction(0, 9, 4, 15);
      return gradient(spd);
    }

    private double GetBaseRunSpeedForPosition(Position position) => position switch
    {
      Position.Pitcher => 3.8,
      Position.Catcher => 6,
      Position.FirstBase => 6.75,
      Position.SecondBase => 9.5,
      Position.ThirdBase => 8.4,
      Position.Shortstop => 9.8,
      Position.LeftField => 9,
      Position.CenterField => 10.4,
      Position.RightField => 9,
      Position.DesignatedHitter => 6.740740741,
      _ => 4
    };

    private double GetRunsPerAtBat(PlayerGenerationData datasetCollection, Action<GeneratorWarning> addWarning)
    {
      if (datasetCollection.HittingStats == null)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          addWarning(GeneratorWarning.NoHittingStats(PropertyKey));
        return 0;
      }

      if (!datasetCollection.HittingStats.AtBats.HasValue || datasetCollection.HittingStats.AtBats < 100 || !datasetCollection.HittingStats.Runs.HasValue)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          addWarning(GeneratorWarning.InsufficientHittingStats(PropertyKey));
        return 0;
      }

      return ((double)datasetCollection.HittingStats.Runs) / datasetCollection.HittingStats.AtBats!.Value;
    }
  }

  public class ArmStrengthSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_ArmStrength";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      var armStrength = GetArmStrength(datasetCollection, warning => player.GeneratorWarnings.Add(warning));
      player.HitterAbilities.ArmStrength = armStrength.Round().MinAt(1).CapAt(15);
      return true;
    }

    private double GetArmStrength(PlayerGenerationData datasetCollection, Action<GeneratorWarning> addWarning)
    {
      switch (datasetCollection.PrimaryPosition)
      {
        case Position.Pitcher:
          return 10;
        case Position.Catcher:
          return 9 + GetCatcherCaughtStealingPercentageBonus(datasetCollection, addWarning);
        case Position.FirstBase:
          return 9 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.SecondBase:
          return 9 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.ThirdBase:
          return 9 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.Shortstop:
          return 10 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.LeftField:
          return 9 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.CenterField:
          return 9 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.RightField:
          return 9 + GetAssistsBonus(datasetCollection, addWarning);
        case Position.DesignatedHitter:
          return 8;
        default:
          throw new InvalidOperationException("Invalid value for Position");
      }
    }

    private double GetCatcherCaughtStealingPercentageBonus(PlayerGenerationData datasetCollection, Action<GeneratorWarning> addWarning)
    {
      if(datasetCollection.FieldingStats == null)
      {
        addWarning(GeneratorWarning.NoFieldingStats(PropertyKey));
        return 0;
      }

      if (!datasetCollection.FieldingStats.OverallFielding.Catcher_StolenBasesAllowed.HasValue ||
        !datasetCollection.FieldingStats.OverallFielding.Catcher_RunnersThrownOut.HasValue
      )
      {
        addWarning(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return 0;
      }

      var attempts = datasetCollection.FieldingStats.OverallFielding.Catcher_StolenBasesAllowed + datasetCollection.FieldingStats.OverallFielding.Catcher_RunnersThrownOut;
      if(attempts <  10)
      {
        addWarning(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return 0;
      }

      var caughtStealingPercentage = datasetCollection.FieldingStats.OverallFielding.Catcher_RunnersThrownOut.Value / ((double)attempts);
      var linearGradient = MathUtils.BuildLinearGradientFunction(.5, .3, 6, 0.5);
      return linearGradient(caughtStealingPercentage);
    }

    private double GetAssistsBonus(PlayerGenerationData datasetCollection, Action<GeneratorWarning> addWarning)
    {
      if (datasetCollection.FieldingStats == null)
      {
        addWarning(GeneratorWarning.NoFieldingStats(PropertyKey));
        return 0;
      }

      var relevantAssists = GetAssistsForPrimaryAndComparable(datasetCollection.PrimaryPosition, datasetCollection.FieldingStats);
      if (!relevantAssists.HasValue)
      {
        addWarning(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return 0;
      }

      var positionGradient = GetLinearGradientForPosition(datasetCollection.PrimaryPosition);
      return positionGradient(relevantAssists.Value);
    }

    public int? GetAssistsForPrimaryAndComparable(Position primaryPosition, LSFieldingStatDataset fieldingStats)
    {
      var validPositionStats = fieldingStats.FieldingByPosition.Where(kvp => kvp.Key.GetPositionType() == primaryPosition.GetPositionType());
      return validPositionStats.SumOrNull(r => r.Value.Assists);
    }

    public Func<double, double> GetLinearGradientForPosition(Position position) => position switch
    {
      Position.FirstBase => MathUtils.BuildLinearGradientFunction(116, 57.075, 4, .5),
      Position.SecondBase => MathUtils.BuildLinearGradientFunction(510, 273.5, 5, .25),
      Position.ThirdBase => MathUtils.BuildLinearGradientFunction(398, 165, 5, .75),
      Position.Shortstop => MathUtils.BuildLinearGradientFunction(492, 250, 5, .75),
      Position.LeftField => MathUtils.BuildLinearGradientFunction(13, 3.26, 5, .25),
      Position.CenterField => MathUtils.BuildLinearGradientFunction(13, 4.08, 5, 1),
      Position.RightField => MathUtils.BuildLinearGradientFunction(16, 4.33, 5, .5),
      _ => throw new InvalidOperationException("position not valid for this function")
    };
  }

  public class FieldingSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_Fielding";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      // Use OAA (Outs Above Average) when available — 2016+ Statcast data
      if (datasetCollection.OutsAboveAverage.HasValue &&
          datasetCollection.PrimaryPosition != Position.Pitcher &&
          datasetCollection.PrimaryPosition != Position.DesignatedHitter)
      {
        var oaa = datasetCollection.OutsAboveAverage.Value;
        // OAA typically ranges from -15 (terrible) to +25 (elite)
        // Map to game's 4-15 fielding scale: OAA -10 → 6, OAA 0 → 10, OAA +15 → 15
        var oaaGradient = MathUtils.BuildLinearGradientFunction(-10, 15, 6, 15);
        var oaaFielding = oaaGradient(oaa);
        player.HitterAbilities.Fielding = oaaFielding.Round().MinAt(4).CapAt(15);
        return true;
      }

      // Fallback: RangeFactor heuristic
      if (datasetCollection.FieldingStats == null)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoFieldingStats(PropertyKey));
        return false;
      }

      if (datasetCollection.FieldingStats.OverallFielding.Innings < 30)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return false;
      }

      var relevantRF = datasetCollection.FieldingStats.FieldingByPosition[datasetCollection.PrimaryPosition]?.RangeFactor;
      if (!relevantRF.HasValue)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return false;
      }

      var linearGradient = GetRangeFactorGradientForPosition(datasetCollection.PrimaryPosition);
      var fielding = linearGradient(relevantRF.Value);
      player.HitterAbilities.Fielding = fielding.Round().MinAt(4).CapAt(15);
      return true;
    }

    private Func<double, double> GetRangeFactorGradientForPosition(Position position) => position switch
    {
      Position.Catcher => (value) => 8.5 + MathUtils.BuildLinearGradientFunction(8, 6.1, 5, 1)(value),
      Position.FirstBase => MathUtils.BuildLinearGradientFunction(10.2, 8.05, 14, 8.5),
      Position.SecondBase => MathUtils.BuildLinearGradientFunction(5.38, 4.3, 14, 9.5),
      Position.ThirdBase => MathUtils.BuildLinearGradientFunction(3.35, 12.62, 14, 10),
      Position.Shortstop => MathUtils.BuildLinearGradientFunction(4.88, 4.13, 14, 11.25),
      Position.LeftField => MathUtils.BuildLinearGradientFunction(2.73, 1.58, 14, 8.5),
      Position.CenterField => MathUtils.BuildLinearGradientFunction(2.72, 2.25, 14, 10),
      Position.RightField => MathUtils.BuildLinearGradientFunction(2.44, 1.75, 14, 8.5),
      Position.DesignatedHitter => value => 6,
      _ => value => 6
    };
  }

  public class ErrorResistanceSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_ErrorResistance";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      if(datasetCollection.FieldingStats == null)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoFieldingStats(PropertyKey));
        return false;
      }

      if (datasetCollection.FieldingStats.OverallFielding.TotalChances < 100)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return false;
      }

      var relevantFpct = datasetCollection.FieldingStats.FieldingByPosition[datasetCollection.PrimaryPosition]?.FieldingPercentage;
      if (!relevantFpct.HasValue)
      {
        if (datasetCollection.PrimaryPosition != Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientFieldingStats(PropertyKey));
        return false;
      }

      var linearGradient = GetFieldingPercentageGradientForPosition(datasetCollection.PrimaryPosition);
      var errorResistance = linearGradient(relevantFpct.Value);
      player.HitterAbilities.ErrorResistance = errorResistance.Round().MinAt(4).CapAt(15);
      return true;
    }

    private Func<double, double> GetFieldingPercentageGradientForPosition(Position position) => position switch
    {
      Position.Pitcher => ValueTuple => 5,
      Position.Catcher => MathUtils.BuildLinearGradientFunction(1, .992, 14, 10.75),
      Position.FirstBase => MathUtils.BuildLinearGradientFunction(1, .994, 14, 11.25),
      Position.SecondBase => MathUtils.BuildLinearGradientFunction(1, .983, 14, 9.5),
      Position.ThirdBase => MathUtils.BuildLinearGradientFunction(.987, .954, 14, 10.25),
      Position.Shortstop => MathUtils.BuildLinearGradientFunction(.993, .972, 14, 9.75),
      Position.LeftField => MathUtils.BuildLinearGradientFunction(1, .986, 14, 10.25),
      Position.CenterField => MathUtils.BuildLinearGradientFunction(1, .989, 14, 10.5),
      Position.RightField => MathUtils.BuildLinearGradientFunction(1, .982, 14, 10.25),
      Position.DesignatedHitter => value => 6,
      _ => value => 6
    };
  }

  public class ControlSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "PitcherAbilities_Control";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      if(datasetCollection.PitchingStats == null)
      {
        if(datasetCollection.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoPitchingStats(PropertyKey));
        return false;
      }

      if (!datasetCollection.PitchingStats.WalksPer9.HasValue ||
        datasetCollection.PitchingStats.MathematicalInnings < 15
      )
      {
        if (datasetCollection.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientPitchingStats(PropertyKey));
        return false;
      }

      var linearGradient = MathUtils.BuildLinearGradientFunction(0.9, 3.58, 190, 134.3);
      var control = linearGradient(datasetCollection.PitchingStats.WalksPer9.Value);
      player.PitcherAbilities.Control = control.Round().MinAt(60).CapAt(255);
      return true;
    }
  }

  public class StaminaSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "PitcherAbilities_Stamina";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      if (player.PitcherType == PitcherType.SwingMan)
        return false;

      if (datasetCollection.PitchingStats == null)
      { 
        if (datasetCollection.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoPitchingStats(PropertyKey));
        return false;
      }

      if (
        !datasetCollection.PitchingStats.GamesPitched.HasValue ||
        !datasetCollection.PitchingStats.MathematicalInnings.HasValue ||
        datasetCollection.PitchingStats.MathematicalInnings < 15
      )
      {
        if (datasetCollection.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientPitchingStats(PropertyKey));
        return false;
      }

      var inningsPerGamePitched = datasetCollection.PitchingStats.MathematicalInnings.Value / datasetCollection.PitchingStats.GamesPitched.Value;
      var linearGradient = player.PitcherType == PitcherType.Starter
        ? MathUtils.BuildLinearGradientFunction(7.12, 5.71, 200, 167)
        : MathUtils.BuildLinearGradientFunction(6.11, 1.57, 155, 92);

      var stamina = linearGradient(inningsPerGamePitched);
      player.PitcherAbilities.Stamina = stamina.Round().MinAt(60).CapAt(255);
      return true;
    }
  }

  public class TopSpeedSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "PitcherAbilities_TopSpeedMph";

    public override bool SetProperty(Player player, PlayerGenerationData datasetCollection)
    {
      // Use real fastball velocity from pitchArsenal API when available
      if (datasetCollection.PitchArsenalData != null)
      {
        var fastball = datasetCollection.PitchArsenalData
          .FirstOrDefault(s => s.Stat?.Type?.Code == "FF" || s.Stat?.Type?.Code == "FA");
        if (fastball?.Stat?.AverageSpeed != null)
        {
          // Average speed + 2 mph ≈ top speed
          player.PitcherAbilities.TopSpeedMph = Math.Min(105, Math.Max(49, fastball.Stat.AverageSpeed.Value + 2));
          return true;
        }
        // If no four-seam, try sinker/two-seam
        var altFb = datasetCollection.PitchArsenalData
          .FirstOrDefault(s => s.Stat?.Type?.Code == "SI" || s.Stat?.Type?.Code == "FT");
        if (altFb?.Stat?.AverageSpeed != null)
        {
          player.PitcherAbilities.TopSpeedMph = Math.Min(105, Math.Max(49, altFb.Stat.AverageSpeed.Value + 2));
          return true;
        }
      }

      // Fallback: K/9 heuristic for pre-2008
      if (datasetCollection.PitchingStats == null)
      {
        if(datasetCollection.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoPitchingStats(PropertyKey));
        return false;
      }

      if (!datasetCollection.PitchingStats.StrikeoutsPer9.HasValue ||
        datasetCollection.PitchingStats.MathematicalInnings < 15)
      {
        if (datasetCollection.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientPitchingStats(PropertyKey));
        return false;
      }

      var linearGradient = MathUtils.BuildLinearGradientFunction(12.89, 6.81, 100, 94.1);
      var topSpeed = linearGradient(datasetCollection.PitchingStats.StrikeoutsPer9.Value);
      player.PitcherAbilities.TopSpeedMph = topSpeed.MinAt(49).CapAt(105);
      return true;
    }
  }

  public class PitchArsenalSetter : PlayerPropertySetter
  {
    private readonly Franchise.IPre2008PitchArsenalLookup _pre2008Lookup;

    public PitchArsenalSetter(Franchise.IPre2008PitchArsenalLookup pre2008Lookup)
    {
      _pre2008Lookup = pre2008Lookup;
    }

    public override string PropertyKey => "PitcherAbilities_PitchArsenal";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      // Priority 1: Real pitch arsenal from API (2008+)
      if (data.PitchArsenalData != null)
      {
        ApplyRealArsenal(data.PitchArsenalData, player);
        player.GeneratedPlayer_PitchArsenalSource = "api";
        return true;
      }

      // Priority 2: Curated pre-2008 arsenal from JSON lookup
      var playerName = $"{data.PlayerInfo?.FirstNameUsed} {data.PlayerInfo?.LastName}".Trim();
      if (_pre2008Lookup.HasPlayer(playerName))
      {
        _pre2008Lookup.ApplyArsenal(playerName, player);
        return true;
      }

      player.GeneratedPlayer_PitchArsenalSource = "heuristic";

      // Priority 3: BAA-based heuristic (random pitch types by era)
      if (data.PitchingStats == null)
      {
        if (data.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.NoPitchingStats(PropertyKey));
        return false;
      }

      if (!data.PitchingStats.BattingAverageAgainst.HasValue || data.PitchingStats.MathematicalInnings < 15)
      {
        if (data.PrimaryPosition == Position.Pitcher)
          player.GeneratorWarnings.Add(GeneratorWarning.InsufficientPitchingStats(PropertyKey));
        return false;
      }

      var breakGradient = MathUtils.BuildLinearGradientFunction(.107, .264, 6, 3);
      var firstBreak = breakGradient(data.PitchingStats.BattingAverageAgainst.Value).Round().MinAt(1).CapAt(7);
      var secondBreak = (firstBreak * .7).Round().MinAt(1).CapAt(7);
      var thirdBreak = (secondBreak * .7).Round().MinAt(1).CapAt(7);

      var sliderType = GetRandomSliderTypeByYear(data.Year);
      var curveType = GetRandomCurveTypeByYear(data.Year);
      var changeType = GetForkTypeForYear(data.Year);

      if (player.PitcherType == PitcherType.Starter)
      {
        player.PitcherAbilities.Slider1Type = sliderType;
        player.PitcherAbilities.Slider1Movement = sliderType.HasValue ? firstBreak : null;
        player.PitcherAbilities.Curve1Type = curveType;
        player.PitcherAbilities.Curve1Movement = sliderType.HasValue ? secondBreak : firstBreak;
        player.PitcherAbilities.Fork1Type = changeType;
        player.PitcherAbilities.Fork1Movement = sliderType.HasValue ? thirdBreak : secondBreak;
      }
      else
      {
        player.PitcherAbilities.Slider1Type = sliderType;
        player.PitcherAbilities.Slider1Movement = sliderType.HasValue ? firstBreak : null;
        player.PitcherAbilities.Curve1Type = sliderType.HasValue ? null : curveType;
        player.PitcherAbilities.Curve1Movement = sliderType.HasValue ? null : firstBreak;
        player.PitcherAbilities.Fork1Type = changeType;
        player.PitcherAbilities.Fork1Movement = secondBreak;
      }
      return true;
    }

    /// <summary>Maps real MLB pitch arsenal data to game pitch slots.</summary>
    private static void ApplyRealArsenal(Fetchers.MLBStatsApi.PitchArsenalSplit[] splits, Player player)
    {
      var p = player.PitcherAbilities;
      // Clear existing
      p.HasTwoSeam = false; p.TwoSeamMovement = null;
      p.Slider1Type = null; p.Slider1Movement = null; p.Slider2Type = null; p.Slider2Movement = null;
      p.Curve1Type = null; p.Curve1Movement = null; p.Curve2Type = null; p.Curve2Movement = null;
      p.Fork1Type = null; p.Fork1Movement = null; p.Fork2Type = null; p.Fork2Movement = null;
      p.Sinker1Type = null; p.Sinker1Movement = null; p.Sinker2Type = null; p.Sinker2Movement = null;
      p.SinkingFastball1Type = null; p.SinkingFastball1Movement = null;
      p.SinkingFastball2Type = null; p.SinkingFastball2Movement = null;

      var era = player.EarnedRunAverage ?? 4.0;
      int baseMove = era < 2.5 ? 6 : era < 3.25 ? 5 : era < 4.0 ? 4 : era < 5.0 ? 3 : 2;
      int tier = 0;
      int sc = 0, cc = 0, fc = 0, snc = 0;

      foreach (var split in splits)
      {
        var code = split.Stat?.Type?.Code ?? "";
        if (code == "FF" || code == "FA") continue; // fastball handled by TopSpeedSetter
        if (code == "FT") { p.HasTwoSeam = true; p.TwoSeamMovement = Clamp(baseMove - 1); continue; }

        int mv = Clamp(baseMove - tier);
        tier++;

        switch (code)
        {
          case "SL": case "ST":
            if (sc == 0) { p.Slider1Type = SliderType.Slider; p.Slider1Movement = mv; sc++; }
            else if (sc == 1) { p.Slider2Type = SliderType.Slider; p.Slider2Movement = mv; sc++; }
            break;
          case "FC":
            if (sc == 0) { p.Slider1Type = SliderType.Cutter; p.Slider1Movement = mv; sc++; }
            else if (sc == 1) { p.Slider2Type = SliderType.Cutter; p.Slider2Movement = mv; sc++; }
            break;
          case "CU": case "CB":
            if (cc == 0) { p.Curve1Type = CurveType.Curve; p.Curve1Movement = mv; cc++; }
            else if (cc == 1) { p.Curve2Type = CurveType.Curve; p.Curve2Movement = mv; cc++; }
            break;
          case "KC":
            if (cc == 0) { p.Curve1Type = CurveType.KnuckleCurve; p.Curve1Movement = mv; cc++; }
            else if (cc == 1) { p.Curve2Type = CurveType.KnuckleCurve; p.Curve2Movement = mv; cc++; }
            break;
          case "SV":
            if (cc == 0) { p.Curve1Type = CurveType.Slurve; p.Curve1Movement = mv; cc++; }
            break;
          case "EP":
            if (cc == 0) { p.Curve1Type = CurveType.SlowCurve; p.Curve1Movement = mv; cc++; }
            break;
          case "CH":
            if (fc == 0) { p.Fork1Type = ForkType.ChangeUp; p.Fork1Movement = mv; fc++; }
            else if (fc == 1) { p.Fork2Type = ForkType.ChangeUp; p.Fork2Movement = mv; fc++; }
            break;
          case "FS":
            if (fc == 0) { p.Fork1Type = ForkType.Splitter; p.Fork1Movement = mv; fc++; }
            else if (fc == 1) { p.Fork2Type = ForkType.Splitter; p.Fork2Movement = mv; fc++; }
            break;
          case "KN":
            if (fc == 0) { p.Fork1Type = ForkType.Knuckleball; p.Fork1Movement = mv; fc++; }
            break;
          case "SI":
            if (snc == 0) { p.Sinker1Type = SinkerType.Sinker; p.Sinker1Movement = mv; snc++; }
            break;
          case "SC":
            if (snc == 0) { p.Sinker1Type = SinkerType.Screwball; p.Sinker1Movement = mv; snc++; }
            break;
          default: tier--; break;
        }
      }
    }

    private static int Clamp(int v) => Math.Max(1, Math.Min(7, v));

    private SliderType? GetRandomSliderTypeByYear(int year)
    {
      if (year < 1925) return null;
      if (year < 1955) return SliderType.Slider;
      var rand = Random.Shared.NextDouble();
      return rand < .76 ? SliderType.Slider : rand < .9 ? SliderType.Cutter : SliderType.HardSlider;
    }

    private CurveType GetRandomCurveTypeByYear(int year)
    {
      if (year < 1945) return CurveType.Curve;
      var rand = Random.Shared.NextDouble();
      return rand < .78 ? CurveType.Curve : rand < .9 ? CurveType.DropCurve : CurveType.Slurve;
    }

    private ForkType GetForkTypeForYear(int year) => year < 1945 ? ForkType.Palmball : ForkType.ChangeUp;
  }

  /// <summary>Sets hot zones from real API BA-by-zone data (2008+).</summary>
  public class HotZoneSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "HitterAbilities_HotZones";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      if (data.HotZoneData == null || data.HotZoneData.Length < 9)
        return false; // Leave at neutral (or let JSON lookup override later)

      // Map zone numbers (01-09) to HotZoneGrid fields
      // Zone layout: 01=UpAndIn, 02=Up, 03=UpAndAway, 04=MiddleIn, 05=Middle,
      //              06=MiddleAway, 07=DownAndIn, 08=Down, 09=DownAndAway
      foreach (var zone in data.HotZoneData)
      {
        var pref = ZoneBaToPreference(zone.Value);
        switch (zone.Zone)
        {
          case "01": player.HitterAbilities.HotZones.UpAndIn = pref; break;
          case "02": player.HitterAbilities.HotZones.Up = pref; break;
          case "03": player.HitterAbilities.HotZones.UpAndAway = pref; break;
          case "04": player.HitterAbilities.HotZones.MiddleIn = pref; break;
          case "05": player.HitterAbilities.HotZones.Middle = pref; break;
          case "06": player.HitterAbilities.HotZones.MiddleAway = pref; break;
          case "07": player.HitterAbilities.HotZones.DownAndIn = pref; break;
          case "08": player.HitterAbilities.HotZones.Down = pref; break;
          case "09": player.HitterAbilities.HotZones.DownAndAway = pref; break;
        }
      }
      return true;
    }

    private static HotZonePreference ZoneBaToPreference(double ba)
    {
      if (ba >= .300) return HotZonePreference.Hot;
      if (ba <= .200) return HotZonePreference.Cold;
      return HotZonePreference.Neutral;
    }
  }

  /// <summary>
  /// Helper to get the lookup name from PlayerGenerationData.
  /// Prefers RosterDisplayName (which handles nicknames), falls back to API name.
  /// </summary>
  internal static class LookupNameHelper
  {
    /// <summary>
    /// Get the lookup name for JSON dictionary matching.
    /// Strips accents so "Pedro Martínez" matches "Pedro Martinez" in JSON keys.
    /// </summary>
    public static string? GetName(PlayerGenerationData data)
    {
      string? raw = null;
      if (!string.IsNullOrEmpty(data.RosterDisplayName))
        raw = data.RosterDisplayName;
      else if (data.PlayerInfo != null)
        raw = data.PlayerInfo.FirstNameUsed + " " + data.PlayerInfo.LastName;
      return raw?.RemoveAccents();
    }
  }

  /// <summary>
  /// Fallback hot zone setter: applies curated JSON hot zones when API data wasn't available.
  /// Same PropertyKey as HotZoneSetter so it only runs when the API setter returned false.
  /// </summary>
  public class HotZoneLookupSetter : PlayerPropertySetter
  {
    private readonly Franchise.IHotZoneLookup _lookup;
    public HotZoneLookupSetter(Franchise.IHotZoneLookup lookup) => _lookup = lookup;
    public override string PropertyKey => "HitterAbilities_HotZones";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      var name = LookupNameHelper.GetName(data);
      if (name == null) return false;
      var before = player.HitterAbilities.HotZones.Middle;
      _lookup.ApplyHotZones(name, player);
      // Return true if the lookup actually changed something
      return player.HitterAbilities.HotZones.Middle != before
          || player.HitterAbilities.HotZones.UpAndIn != HotZonePreference.Neutral;
    }
  }

  /// <summary>
  /// Overrides skin color from curated JSON lookup.
  /// Uses a different key so it always runs after the guesser.
  /// </summary>
  public class ComplexionLookupSetter : PlayerPropertySetter
  {
    private readonly Franchise.IComplexionLookup _lookup;
    public ComplexionLookupSetter(Franchise.IComplexionLookup lookup) => _lookup = lookup;
    public override string PropertyKey => "Appearance_Complexion_Override";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      var name = LookupNameHelper.GetName(data);
      if (name == null) return false;
      var complexion = _lookup.GetComplexion(name);
      if (!complexion.HasValue) return false;
      player.Appearance.Complexion = complexion.Value;
      return true;
    }
  }

  /// <summary>
  /// Applies curated special abilities from JSON lookup.
  /// </summary>
  public class SpecialAbilitiesLookupSetter : PlayerPropertySetter
  {
    private readonly Franchise.ISpecialAbilitiesLookup _lookup;
    public SpecialAbilitiesLookupSetter(Franchise.ISpecialAbilitiesLookup lookup) => _lookup = lookup;
    public override string PropertyKey => "SpecialAbilities_Lookup";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      var name = LookupNameHelper.GetName(data);
      if (name == null) return false;
      _lookup.ApplySpecialAbilities(name, player.SpecialAbilities);
      return true;
    }
  }

  /// <summary>
  /// Overrides batting stance and pitching mechanics from curated JSON lookup.
  /// Uses a different key so it always runs after the guessers.
  /// </summary>
  public class PlayerFormLookupSetter : PlayerPropertySetter
  {
    private readonly Franchise.IPlayerFormLookup _lookup;
    public PlayerFormLookupSetter(Franchise.IPlayerFormLookup lookup) => _lookup = lookup;
    public override string PropertyKey => "PlayerForm_Override";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      var name = LookupNameHelper.GetName(data);
      if (name == null) return false;
      _lookup.ApplyForms(name, player);
      return true;
    }
  }

  /// <summary>
  /// Algorithmic appearance — always runs regardless of enhancedMode.
  /// Gives every player a varied, era-appropriate look using deterministic hashing.
  /// </summary>
  public class AlgorithmicAppearanceSetter : PlayerPropertySetter
  {
    public override string PropertyKey => "Appearance_Algorithmic";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      var name = LookupNameHelper.GetName(data);
      if (name == null) return false;
      AlgorithmicAppearance.Apply(name, data.Year, player);
      return true;
    }
  }

  /// <summary>
  /// Applies curated appearance from JSON lookup (enhanced mode only).
  /// Overrides the algorithmic appearance for players with curated entries.
  /// </summary>
  public class AppearanceLookupSetter : PlayerPropertySetter
  {
    private readonly Franchise.IAppearanceLookup _lookup;
    public AppearanceLookupSetter(Franchise.IAppearanceLookup lookup) => _lookup = lookup;
    public override string PropertyKey => "Appearance_Lookup";

    public override bool SetProperty(Player player, PlayerGenerationData data)
    {
      var name = LookupNameHelper.GetName(data);
      if (name == null) return false;
      _lookup.ApplyAppearance(name, data.Year, player);
      return true;
    }
  }
}
