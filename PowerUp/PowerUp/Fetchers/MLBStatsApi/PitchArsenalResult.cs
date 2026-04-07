using System.Text.Json.Serialization;

namespace PowerUp.Fetchers.MLBStatsApi
{
  public class PitchArsenalResponse
  {
    [JsonPropertyName("stats")]
    public PitchArsenalStatGroup[] Stats { get; set; } = [];
  }

  public class PitchArsenalStatGroup
  {
    [JsonPropertyName("type")]
    public Group? Type { get; set; }

    [JsonPropertyName("group")]
    public Group? Group { get; set; }

    [JsonPropertyName("splits")]
    public PitchArsenalSplit[] Splits { get; set; } = [];
  }

  public class PitchArsenalSplit
  {
    [JsonPropertyName("stat")]
    public PitchArsenalStat? Stat { get; set; }
  }

  public class PitchArsenalStat
  {
    [JsonPropertyName("percentage")]
    public double? Percentage { get; set; }

    [JsonPropertyName("count")]
    public long? Count { get; set; }

    [JsonPropertyName("averageSpeed")]
    public double? AverageSpeed { get; set; }

    [JsonPropertyName("totalPitches")]
    public long? TotalPitches { get; set; }

    [JsonPropertyName("type")]
    public PitchTypeInfo? Type { get; set; }
  }

  public class PitchTypeInfo
  {
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
  }
}
