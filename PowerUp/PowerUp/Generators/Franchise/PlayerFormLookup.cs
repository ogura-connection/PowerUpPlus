using PowerUp.Entities.Players;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerUp.Generators.Franchise
{
  public interface IPlayerFormLookup
  {
    void ApplyForms(string playerName, Player player);
  }

  public class PlayerFormEntry
  {
    [System.Text.Json.Serialization.JsonPropertyName("batting_form_id")]
    public int? BattingFormId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("pitching_form_id")]
    public int? PitchingFormId { get; set; }
  }

  /// <summary>
  /// Reads a JSON file mapping player names to batting stance IDs and pitching mechanics IDs.
  /// Overrides the random guesser assignments when a match exists.
  /// </summary>
  public class JsonPlayerFormLookup : IPlayerFormLookup
  {
    private readonly Dictionary<string, PlayerFormEntry> _lookup;

    public JsonPlayerFormLookup(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        _lookup = JsonSerializer.Deserialize<Dictionary<string, PlayerFormEntry>>(json)
          ?? new Dictionary<string, PlayerFormEntry>();
      }
      else
      {
        _lookup = new Dictionary<string, PlayerFormEntry>();
      }
    }

    public void ApplyForms(string playerName, Player player)
    {
      if (!_lookup.TryGetValue(playerName, out var entry))
        return;

      if (entry.BattingFormId.HasValue)
        player.BattingStanceId = entry.BattingFormId.Value;

      if (entry.PitchingFormId.HasValue)
        player.PitchingMechanicsId = entry.PitchingFormId.Value;
    }
  }

  /// <summary>No-op lookup that does nothing.</summary>
  public class NoOpPlayerFormLookup : IPlayerFormLookup
  {
    public void ApplyForms(string playerName, Player player) { }
  }
}
