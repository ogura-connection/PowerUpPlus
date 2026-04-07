using Microsoft.Extensions.DependencyInjection;
using PowerUp.Generators.Franchise;
using System.IO;

namespace PowerUp.Libraries
{
  public static class LibraryConfig
  {
    public static void RegisterLibraries(this IServiceCollection services, string dataDirectory)
    {
      services.AddSingleton<ICharacterLibrary>(provider => new CharacterLibrary(Path.Combine(dataDirectory, "./data/Character_Library.csv")));
      services.AddSingleton<ISpecialSavedNameLibrary>(provider => new SpecialSavedNameLibrary(Path.Combine(dataDirectory, "./data/SpecialSavedName_Library.csv")));
      services.AddTransient<IVoiceLibrary>(provider => new VoiceLibrary(Path.Combine(dataDirectory, "./data/Voice_Library.csv")));
      services.AddTransient<IBattingStanceLibrary>(provider => new BattingStanceLibrary(Path.Combine(dataDirectory, "./data/BattingForm_Library.csv")));
      services.AddTransient<IPitchingMechanicsLibrary>(provider => new PitchingMechanicsLibrary(Path.Combine(dataDirectory, "./data/PitchingForm_Library.csv")));
      services.AddTransient<IFaceLibrary>(provider => new FaceLibrary(Path.Combine(dataDirectory, "./data/Face_Library.csv")));
      services.AddTransient<ICountryAndComplexionLibrary>(provider => new CountryAndComplexionLibrary(Path.Combine(dataDirectory, "./data/CountryAndComplexion_Library.csv")));
      services.AddTransient<IBaseGameSavePathProvider>(provider => new BaseGameSavePathProvider(Path.Combine(dataDirectory, "./data/BASE.pm2maus.dat")));
      services.AddTransient<IFranchisesAndNamesLibrary>(provider => new FranchisesAndNamesLibrary(Path.Combine(dataDirectory, "./data/FranchisesAndNames_Library.csv")));
      services.AddTransient<IPlayerSalariesLibrary>(provider => new PlayerSalariesLibrary(Path.Combine(dataDirectory, "./data/PlayerSalaries_Library.csv")));
      services.AddSingleton<IComplexionLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/PlayerComplexion_Library.json");
        return File.Exists(path) ? new JsonComplexionLookup(path) : new NoOpComplexionLookup();
      });
      services.AddSingleton<ISpecialAbilitiesLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/PlayerSpecialAbilities_Library.json");
        return File.Exists(path) ? new JsonSpecialAbilitiesLookup(path) : new NoOpSpecialAbilitiesLookup();
      });
      services.AddSingleton<IPlayerFormLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/PlayerStancesAndForms_Library.json");
        return File.Exists(path) ? new JsonPlayerFormLookup(path) : new NoOpPlayerFormLookup();
      });
      services.AddSingleton<IHotZoneLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/HotZones_Library.json");
        return File.Exists(path) ? new JsonHotZoneLookup(path) : new NoOpHotZoneLookup();
      });
      services.AddSingleton<IAppearanceLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/AppearanceLibrary.json");
        return File.Exists(path) ? new JsonAppearanceLookup(path) : new NoOpAppearanceLookup();
      });
      services.AddSingleton<IPre2008PitchArsenalLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/PitchArsenal_Pre2008_Library.json");
        return File.Exists(path) ? new JsonPre2008PitchArsenalLookup(path) : new NoOpPre2008PitchArsenalLookup();
      });
      services.AddSingleton(provider =>
        new ManualPlayerBuilder(Path.Combine(dataDirectory, "./data/ManualPlayers_Library.json")));
      services.AddTransient<PitchArsenalService>();
    }
  }
}
