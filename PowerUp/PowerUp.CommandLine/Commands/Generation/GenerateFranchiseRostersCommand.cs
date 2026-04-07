using Microsoft.Extensions.Logging;
using PowerUp.Generators;
using PowerUp.Generators.Franchise;
using PowerUp.Libraries;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;

namespace PowerUp.CommandLine.Commands.Generation
{
  public class GenerateFranchiseRostersCommand(
    ILogger<GenerateFranchiseRostersCommand> logger,
    IFranchiseRosterGenerator franchiseGenerator,
    IVoiceLibrary voiceLibrary,
    ISkinColorGuesser skinColorGuesser,
    IBattingStanceGuesser battingStanceGuesser,
    IPitchingMechanicsGuesser pitchingMechanicsGuesser
  ) : ICommand
  {
    public Command Build()
    {
      var command = new Command("generate-franchise-rosters")
      {
        new Option<string>("--roster-file", "Path to ROSTER_all_30_franchises.md") { IsRequired = true },
        new Option<string?>("--team", "Generate only this team (substring match, e.g. 'Yankees')"),
      };
      command.Handler = GetHandler();
      return command;
    }

    private ICommandHandler GetHandler()
    {
      return CommandHandler.Create((string rosterFile, string? team) =>
      {
        if (!File.Exists(rosterFile))
        {
          logger.LogError($"Roster file not found: {rosterFile}");
          return;
        }

        var algorithm = new LSStatistcsPlayerGenerationAlgorithm(
          voiceLibrary,
          skinColorGuesser,
          battingStanceGuesser,
          pitchingMechanicsGuesser
        );

        HashSet<string>? teamFilter = null;
        if (!string.IsNullOrEmpty(team))
          teamFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { team };

        var result = franchiseGenerator.GenerateRoster(
          rosterFile,
          algorithm,
          onLog: msg => logger.LogInformation(msg),
          teamFilter: teamFilter
        );

        if (result.Warnings.Count > 0)
        {
          logger.LogWarning($"\n{result.Warnings.Count} warnings:");
          foreach (var w in result.Warnings)
            logger.LogWarning($"  {w}");
        }
      });
    }
  }
}
