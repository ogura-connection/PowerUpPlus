using Microsoft.Extensions.Logging;
using PowerUp.Databases;
using PowerUp.Entities.Rosters;
using PowerUp.Generators;
using PowerUp.Generators.Franchise;
using PowerUp.Libraries;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;

namespace PowerUp.CommandLine.Commands.Generation
{
  public class GenerateRosterCommand(
    ILogger<GenerateRosterCommand> logger,
    IRosterGenerator rosterGenerator,
    IVoiceLibrary voiceLibrary,
    IComplexionGuesser complexionGuesser,
    IBattingStanceGuesser battingStanceGuesser,
    IPitchingMechanicsGuesser pitchingMechanicsGuesser,
    IPre2008PitchArsenalLookup pre2008ArsenalLookup,
    IHotZoneLookup hotZoneLookup,
    IComplexionLookup complexionLookup,
    ISpecialAbilitiesLookup specialAbilitiesLookup,
    IPlayerFormLookup playerFormLookup,
    IAppearanceLookup appearanceLookup
  ) : ICommand
  {
    public Command Build()
    {
      var command = new Command("generate-roster")
      {
        new Option<int>("--year", "MLB season year to generate rosters for") { IsRequired = true },
      };
      command.Handler = GetHandler();
      return command;
    }

    private ICommandHandler GetHandler()
    {
      return CommandHandler.Create((int year) =>
      {
        logger.LogInformation($"Generating {year} MLB rosters (formula-based)...");

        var algorithm = new LSStatistcsPlayerGenerationAlgorithm(
          voiceLibrary,
          complexionGuesser,
          battingStanceGuesser,
          pitchingMechanicsGuesser,
          pre2008ArsenalLookup,
          hotZoneLookup,
          complexionLookup,
          specialAbilitiesLookup,
          playerFormLookup,
          appearanceLookup
        );

        var teamCount = 0;
        var result = rosterGenerator.GenerateRoster(
          year,
          algorithm,
          onTeamProgressUpdate: update =>
          {
            teamCount++;
            logger.LogInformation($"[{teamCount}] {update.CurrentAction}");
          },
          onPlayerProgressUpdate: update =>
          {
            if (update.CurrentAction != "--")
              logger.LogInformation($"  {update.CurrentAction}");
          }
        );

        DatabaseConfig.Database.Save(result.Roster);
        logger.LogInformation($"Done. Roster \"{result.Roster.Name}\" saved with id {result.Roster.Id}");

        if (result.Warnings.Any())
        {
          logger.LogWarning($"{result.Warnings.Count()} warnings:");
          foreach (var warning in result.Warnings)
            logger.LogWarning($"  {warning.PropertyKey}: {warning.Message}");
        }
      });
    }
  }
}
