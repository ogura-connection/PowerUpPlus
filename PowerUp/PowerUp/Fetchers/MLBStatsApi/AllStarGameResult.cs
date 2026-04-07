using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerUp.Fetchers.MLBStatsApi
{
  public class AllStarGameRosters
  {
    public List<long> ALPlayerIds { get; set; } = new();
    public List<long> NLPlayerIds { get; set; } = new();
  }

  public class ScheduleResponse
  {
    [JsonPropertyName("dates")]
    public List<ScheduleDate>? Dates { get; set; }
  }

  public class ScheduleDate
  {
    [JsonPropertyName("games")]
    public List<ScheduleGame>? Games { get; set; }
  }

  public class ScheduleGame
  {
    [JsonPropertyName("gamePk")]
    public long GamePk { get; set; }
  }

  public class GameFeedResponse
  {
    [JsonPropertyName("liveData")]
    public LiveData? LiveData { get; set; }
  }

  public class LiveData
  {
    [JsonPropertyName("boxscore")]
    public Boxscore? Boxscore { get; set; }
  }

  public class Boxscore
  {
    [JsonPropertyName("teams")]
    public BoxscoreTeams? Teams { get; set; }
  }

  public class BoxscoreTeams
  {
    [JsonPropertyName("away")]
    public BoxscoreTeamData? Away { get; set; }

    [JsonPropertyName("home")]
    public BoxscoreTeamData? Home { get; set; }
  }

  public class BoxscoreTeamData
  {
    [JsonPropertyName("team")]
    public BoxscoreTeamInfo? Team { get; set; }

    [JsonPropertyName("players")]
    public Dictionary<string, BoxscorePlayer>? Players { get; set; }
  }

  public class BoxscoreTeamInfo
  {
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
  }

  public class BoxscorePlayer
  {
    [JsonPropertyName("person")]
    public BoxscorePersonInfo? Person { get; set; }

    [JsonPropertyName("jerseyNumber")]
    public string? JerseyNumber { get; set; }

    [JsonPropertyName("position")]
    public BoxscorePositionInfo? Position { get; set; }

    [JsonPropertyName("allPositions")]
    public List<BoxscorePositionInfo>? AllPositions { get; set; }
  }

  public class BoxscorePersonInfo
  {
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }
  }

  public class BoxscorePositionInfo
  {
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("abbreviation")]
    public string? Abbreviation { get; set; }
  }
}
