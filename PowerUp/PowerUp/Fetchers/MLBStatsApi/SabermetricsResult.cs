using System.Text.Json.Serialization;

namespace PowerUp.Fetchers.MLBStatsApi
{
  public class SabermetricsResponse
  {
    [JsonPropertyName("stats")]
    public SabermetricsStatGroup[] Stats { get; set; } = [];
  }

  public class SabermetricsStatGroup
  {
    [JsonPropertyName("splits")]
    public SabermetricsSplit[] Splits { get; set; } = [];
  }

  public class SabermetricsSplit
  {
    [JsonPropertyName("stat")]
    public SabermetricsStat? Stat { get; set; }
  }

  public class SabermetricsStat
  {
    [JsonPropertyName("spd")]
    public double? Spd { get; set; }

    [JsonPropertyName("war")]
    public double? War { get; set; }

    [JsonPropertyName("wRcPlus")]
    public double? WRcPlus { get; set; }

    [JsonPropertyName("woba")]
    public double? Woba { get; set; }
  }
}
