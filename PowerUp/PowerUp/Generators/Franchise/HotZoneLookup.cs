using PowerUp.Entities.Players;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerUp.Generators.Franchise
{
  public interface IHotZoneLookup
  {
    void ApplyHotZones(string playerName, Player player);
  }

  public class HotZoneEntry
  {
    public int UpAndIn { get; set; }
    public int Up { get; set; }
    public int UpAndAway { get; set; }
    public int MiddleIn { get; set; }
    public int Middle { get; set; }
    public int MiddleAway { get; set; }
    public int DownAndIn { get; set; }
    public int Down { get; set; }
    public int DownAndAway { get; set; }
  }

  /// <summary>
  /// Reads a JSON file mapping player names to hot zone grids (3x3, values 0/1/3).
  /// Overrides the default all-neutral zones when a match exists.
  /// </summary>
  public class JsonHotZoneLookup : IHotZoneLookup
  {
    private readonly Dictionary<string, HotZoneEntry> _lookup;

    public JsonHotZoneLookup(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        _lookup = JsonSerializer.Deserialize<Dictionary<string, HotZoneEntry>>(json)
          ?? new Dictionary<string, HotZoneEntry>();
      }
      else
      {
        _lookup = new Dictionary<string, HotZoneEntry>();
      }
    }

    public void ApplyHotZones(string playerName, Player player)
    {
      if (!_lookup.TryGetValue(playerName, out var entry))
        return;

      player.HitterAbilities.HotZones.UpAndIn = (HotZonePreference)entry.UpAndIn;
      player.HitterAbilities.HotZones.Up = (HotZonePreference)entry.Up;
      player.HitterAbilities.HotZones.UpAndAway = (HotZonePreference)entry.UpAndAway;
      player.HitterAbilities.HotZones.MiddleIn = (HotZonePreference)entry.MiddleIn;
      player.HitterAbilities.HotZones.Middle = (HotZonePreference)entry.Middle;
      player.HitterAbilities.HotZones.MiddleAway = (HotZonePreference)entry.MiddleAway;
      player.HitterAbilities.HotZones.DownAndIn = (HotZonePreference)entry.DownAndIn;
      player.HitterAbilities.HotZones.Down = (HotZonePreference)entry.Down;
      player.HitterAbilities.HotZones.DownAndAway = (HotZonePreference)entry.DownAndAway;
    }
  }

  /// <summary>No-op lookup that does nothing.</summary>
  public class NoOpHotZoneLookup : IHotZoneLookup
  {
    public void ApplyHotZones(string playerName, Player player) { }
  }
}
