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
      services.AddTransient<ICountryAndSkinColorLibrary>(provider => new CountryAndSkinColorLibrary(Path.Combine(dataDirectory, "./data/CountryAndSkinColor_Library.csv")));
      services.AddTransient<IBaseGameSavePathProvider>(provider => new BaseGameSavePathProvider(Path.Combine(dataDirectory, "./data/BASE.pm2maus.dat")));
      services.AddTransient<IFranchisesAndNamesLibrary>(provider => new FranchisesAndNamesLibrary(Path.Combine(dataDirectory, "./data/FranchisesAndNames_Library.csv")));
      services.AddTransient<IPlayerSalariesLibrary>(provider => new PlayerSalariesLibrary(Path.Combine(dataDirectory, "./data/PlayerSalaries_Library.csv")));
      services.AddSingleton<ISkinColorLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/PlayerSkinColor_Library.json");
        return File.Exists(path) ? new JsonSkinColorLookup(path) : new NoOpSkinColorLookup();
      });
      services.AddSingleton<ISpecialAbilitiesLookup>(provider =>
      {
        var path = Path.Combine(dataDirectory, "./data/PlayerSpecialAbilities_Library.json");
        return File.Exists(path) ? new JsonSpecialAbilitiesLookup(path) : new NoOpSpecialAbilitiesLookup();
      });
      services.AddSingleton(provider =>
        new ManualPlayerBuilder(Path.Combine(dataDirectory, "./data/ManualPlayers_Library.json")));
    }
  }
}
