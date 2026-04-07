using System.Text.Json.Serialization;

namespace PowerUp.Fetchers.MLBStatsApi
{
  public class HotColdZonesResponse
  {
    [JsonPropertyName("stats")]
    public HotColdZonesStatGroup[] Stats { get; set; } = [];
  }

  public class HotColdZonesStatGroup
  {
    [JsonPropertyName("splits")]
    public HotColdZonesSplit[] Splits { get; set; } = [];
  }

  public class HotColdZonesSplit
  {
    [JsonPropertyName("stat")]
    public HotColdZonesStat? Stat { get; set; }
  }

  public class HotColdZonesStat
  {
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("zones")]
    public ZoneData[] Zones { get; set; } = [];
  }

  public class ZoneData
  {
    [JsonPropertyName("zone")]
    public string Zone { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";

    [JsonPropertyName("temp")]
    public string Temp { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
  }
}
