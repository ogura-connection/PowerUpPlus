using System.Text.Json.Serialization;

namespace PowerUp.Fetchers.MLBStatsApi
{
  public class OutsAboveAverageResponse
  {
    [JsonPropertyName("stats")]
    public OAAStatGroup[] Stats { get; set; } = [];
  }

  public class OAAStatGroup
  {
    [JsonPropertyName("splits")]
    public OAASplit[] Splits { get; set; } = [];
  }

  public class OAASplit
  {
    [JsonPropertyName("stat")]
    public OAAStat? Stat { get; set; }
  }

  public class OAAStat
  {
    [JsonPropertyName("totalOutsAboveAverage")]
    public int? TotalOutsAboveAverage { get; set; }

    [JsonPropertyName("attempts")]
    public int? Attempts { get; set; }
  }
}
