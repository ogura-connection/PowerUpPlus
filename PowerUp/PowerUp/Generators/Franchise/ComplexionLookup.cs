using PowerUp.Entities.Players;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerUp.Generators.Franchise
{
  public interface IComplexionLookup
  {
    Complexion? GetComplexion(string playerName);
  }

  /// <summary>
  /// Reads a JSON file mapping player names to skin color values (1-5).
  /// Falls back to null (letting ComplexionGuesser handle it) when no match.
  /// </summary>
  public class JsonComplexionLookup : IComplexionLookup
  {
    private readonly Dictionary<string, int> _lookup;

    public JsonComplexionLookup(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        _lookup = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
          ?? new Dictionary<string, int>();
      }
      else
      {
        _lookup = new Dictionary<string, int>();
      }
    }

    public Complexion? GetComplexion(string playerName)
    {
      if (_lookup.TryGetValue(playerName, out var value) && value >= 1 && value <= 5)
        return (Complexion)(value - 1); // Complexion enum is 0-based (One=0, Five=4)

      return null;
    }
  }

  /// <summary>No-op lookup that always returns null.</summary>
  public class NoOpComplexionLookup : IComplexionLookup
  {
    public Complexion? GetComplexion(string playerName) => null;
  }
}
