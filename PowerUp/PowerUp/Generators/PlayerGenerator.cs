using PowerUp.Entities;
using PowerUp.Entities.Players;
using PowerUp.Entities.Players.Api;
using PowerUp.Fetchers.BaseballReference;
using PowerUp.Fetchers.MLBLookupService;
using PowerUp.Fetchers.MLBStatsApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PowerUp.Generators
{
  public interface IPlayerGenerator
  {
    PlayerGenerationResult GeneratePlayer(long lsPlayerId, int year, PlayerGenerationAlgorithm generationAlgorithm, string? uniformNumber = null, string? rosterDisplayName = null);
  }

  public class PlayerGenerationResult
  {
    public long LSPlayerId { get; set; }
    public Player Player { get; set; }
    public long? LastTeamForYear_LSTeamId { get; set; }

    public PlayerGenerationResult(long lsPlayerId, Player player, long? lastTeamForYear_lsTeamId)
    {
      LSPlayerId = lsPlayerId;
      Player = player;
      LastTeamForYear_LSTeamId = lastTeamForYear_lsTeamId;
    }
  }

  public class PlayerGenerator : IPlayerGenerator
  {
    private readonly IPlayerApi _playerApi;
    private readonly IPlayerStatisticsFetcher _playerStatsFetcher;
    private readonly IBaseballReferenceClient _baseballReferenceClient;
    private readonly IMLBStatsApiClient _mlbStatsApi;

    public PlayerGenerator(
      IPlayerApi playerApi,
      IPlayerStatisticsFetcher playerStatsFetcher,
      IBaseballReferenceClient baseballReferenceClient,
      IMLBStatsApiClient mlbStatsApi
    )
    {
      _playerApi = playerApi;
      _playerStatsFetcher = playerStatsFetcher;
      _baseballReferenceClient = baseballReferenceClient;
      _mlbStatsApi = mlbStatsApi;
    }

    public PlayerGenerationResult GeneratePlayer(long lsPlayerId, int year, PlayerGenerationAlgorithm generationAlgorithm, string? uniformNumber = null, string? rosterDisplayName = null)
    {
      var fracYearPlayed = MLBSeasonUtils.GetFractionOfSeasonPlayed(year);
      PlayerStatisticsResult? currentYearStats = null;
      if(fracYearPlayed > 0)
      {
        currentYearStats = _playerStatsFetcher.GetStatistics(
          lsPlayerId, 
          year,
          excludePlayerInfo: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSPlayerInfo),
          excludeHittingStats: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSHittingStats),
          excludeFieldingStats: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSFieldingStats),
          excludePitchingStats: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSPitchingStats)
        );
      }

      PlayerStatisticsResult? previousYearStats = null;
      if(fracYearPlayed < 1)
        previousYearStats = _playerStatsFetcher.GetStatistics(
          lsPlayerId,
          year-1,
          excludePlayerInfo: true,
          excludeHittingStats: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSHittingStats),
          excludeFieldingStats: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSFieldingStats),
          excludePitchingStats: !generationAlgorithm.DatasetDependencies.Contains(PlayerGenerationDataset.LSPitchingStats)
      );

      var mostRecentInfo = currentYearStats?.PlayerInfo ?? previousYearStats?.PlayerInfo;

      // Fetch enhanced stats from MLB API in parallel (pitch arsenal 2008+, hot zones 2008+, sabermetrics all eras)
      PitchArsenalSplit[]? pitchArsenalData = null;
      HotZoneData[]? hotZoneData = null;
      double? sabermetricsSPD = null;
      int? outsAboveAverage = null;

      {
        // Launch all applicable API calls concurrently
        var saberTask = Task.Run(async () =>
        {
          try
          {
            var r = await _mlbStatsApi.GetSabermetrics(lsPlayerId, year).WaitAsync(TimeSpan.FromSeconds(8));
            return r.Stats?.FirstOrDefault()?.Splits?.FirstOrDefault()?.Stat?.Spd;
          }
          catch { return (double?)null; }
        });

        var oaaTask = year >= 2016
          ? Task.Run(async () =>
          {
            try
            {
              var r = await _mlbStatsApi.GetOutsAboveAverage(lsPlayerId, year).WaitAsync(TimeSpan.FromSeconds(8));
              return r.Stats?.FirstOrDefault()?.Splits?.FirstOrDefault()?.Stat?.TotalOutsAboveAverage;
            }
            catch { return (int?)null; }
          })
          : Task.FromResult((int?)null);

        var arsenalTask = year >= 2008
          ? Task.Run(async () =>
          {
            try
            {
              var r = await _mlbStatsApi.GetPitchArsenal(lsPlayerId, year).WaitAsync(TimeSpan.FromSeconds(8));
              var splits = r.Stats?.FirstOrDefault()?.Splits?
                .Where(s => s.Stat?.Percentage > 0 && s.Stat?.Type?.Code != null)
                .OrderByDescending(s => s.Stat!.Percentage)
                .ToArray();
              return splits?.Length > 0 ? splits : null;
            }
            catch { return null; }
          })
          : Task.FromResult((PitchArsenalSplit[]?)null);

        var hzTask = year >= 2008
          ? Task.Run(async () =>
          {
            try
            {
              var r = await _mlbStatsApi.GetHotColdZones(lsPlayerId, year).WaitAsync(TimeSpan.FromSeconds(8));
              var baSplit = r.Stats?.FirstOrDefault()?.Splits?
                .FirstOrDefault(s => s.Stat?.Name == "battingAverage");
              if (baSplit?.Stat?.Zones?.Length > 0)
              {
                var zones = baSplit.Stat.Zones
                  .Where(z => int.TryParse(z.Zone, out var zn) && zn >= 1 && zn <= 9)
                  .Select(z => new HotZoneData { Zone = z.Zone, Value = double.TryParse(z.Value, out var v) ? v : 0 })
                  .ToArray();
                return zones.Length > 0 ? zones : null;
              }
              return null;
            }
            catch { return null; }
          })
          : Task.FromResult((HotZoneData[]?)null);

        Task.WaitAll(saberTask, oaaTask, arsenalTask, hzTask);
        sabermetricsSPD = saberTask.Result;
        outsAboveAverage = oaaTask.Result;
        pitchArsenalData = arsenalTask.Result;
        hotZoneData = hzTask.Result;
      }

      var data = new PlayerGenerationData
      {
        Year = year,
        PlayerInfo = mostRecentInfo != null
          ? new LSPlayerInfoDataset(mostRecentInfo, uniformNumber)
          : null,
        HittingStats = LSHittingStatsDataset.BuildFor(currentYearStats?.HittingStats?.Results, previousYearStats?.HittingStats?.Results),
        FieldingStats = LSFieldingStatDataset.BuildFor(currentYearStats?.FieldingStats?.Results, previousYearStats?.FieldingStats?.Results),
        PitchingStats = LSPitchingStatsDataset.BuildFor(currentYearStats?.PitchingStats?.Results, previousYearStats?.PitchingStats?.Results),
        PitchArsenalData = pitchArsenalData,
        HotZoneData = hotZoneData,
        SabermetricsSPD = sabermetricsSPD,
        OutsAboveAverage = outsAboveAverage,
        RosterDisplayName = rosterDisplayName,
      };

      var player = _playerApi.CreateDefaultPlayer(EntitySourceType.Generated, isPitcher: data!.PrimaryPosition == Position.Pitcher);
      player.Year = year;
      player.GeneratedPlayer_LSPLayerId = lsPlayerId;
      player.GeneratedPlayer_IsUnedited = true;

      var propertiesThatHaveBeenSet = new HashSet<string>();
      foreach(var setter in generationAlgorithm.PropertySetters)
      {
        if (propertiesThatHaveBeenSet.Contains(setter.PropertyKey))
          continue;

        var wasSet = setter.SetProperty(player, data);
        if (wasSet)
          propertiesThatHaveBeenSet.Add(setter.PropertyKey);
      }
      
      return new PlayerGenerationResult(lsPlayerId, player, data.LastTeamForYear_LSTeamId);
    }
  }

  public abstract class PlayerGenerationAlgorithm : GenerationAlgorithm<Player, PlayerGenerationDataset, PlayerGenerationData> { }
  public abstract  class PlayerPropertySetter : PropertySetter<Player, PlayerGenerationData> { }
}
