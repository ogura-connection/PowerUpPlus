using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PowerUp.Libraries
{
  public interface ICountryAndComplexionLibrary
  {
    int? this[string key] { get; }
    public IEnumerable<KeyValuePair<string, int>> GetAll();
  }

  public class CountryAndComplexionLibrary : ICountryAndComplexionLibrary
  {
    private readonly Dictionary<string, int> _complexionByCountry;

    public CountryAndComplexionLibrary(string libraryFilePath)
    {
      var keyValuePairs = File.ReadAllLines(libraryFilePath)
        .Select(l => l.Split(','))
        .Select(l => new KeyValuePair<string, int>(l[0], int.Parse(l[1])));

      _complexionByCountry = keyValuePairs.ToDictionary(p => p.Key, p => p.Value);
    }

    public int? this[string key]
    {
      get
      {
        _complexionByCountry.TryGetValue(key, out var value);
        return value != 0
          ? value
          : null;
      }
    }

    public IEnumerable<KeyValuePair<string, int>> GetAll() => _complexionByCountry.AsEnumerable();
  }
}
