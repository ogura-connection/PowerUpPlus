using PowerUp.Databases;
using PowerUp.Entities.GenerationResults;
using PowerUp.Generators;
using PowerUp.Generators.Franchise;
using PowerUp.Libraries;

namespace PowerUp.ElectronUI.Api.Generation
{
  public class TeamGenerationCommand : ICommand<TeamGenerationRequest, TeamGenerationResponse>
  {
    private readonly ITeamGenerator _teamGenerator;
    private readonly IVoiceLibrary _voiceLibrary;
    private readonly IComplexionGuesser _complexionGuesser;
    private readonly IBattingStanceGuesser _batttingStanceGuesser;
    private readonly IPitchingMechanicsGuesser _pitchingMechanicsGuesser;

    public TeamGenerationCommand
    ( ITeamGenerator teamGenerator
    , IVoiceLibrary voiceLibrary
    , IComplexionGuesser complexionGuesser
    , IBattingStanceGuesser batttingStanceGuesser
    , IPitchingMechanicsGuesser pitchingMechanicsGuesser
    )
    {
      _teamGenerator = teamGenerator;
      _voiceLibrary = voiceLibrary;
      _complexionGuesser = complexionGuesser;
      _batttingStanceGuesser = batttingStanceGuesser;
      _pitchingMechanicsGuesser = pitchingMechanicsGuesser;
    }

    public Task<TeamGenerationResponse> Execute(TeamGenerationRequest request)
    {
      var teamGenerationProgress = new TeamGenerationStatus(request.LSTeamId, request.Year, DateTime.Now);
      DatabaseConfig.Database.Save(teamGenerationProgress);

      Task.Run(() => {
        var result = _teamGenerator.GenerateTeam
        ( lsTeamId: request.LSTeamId
        , year: request.Year
        , name: request.TeamName
        , playerGenerationAlgorithm: new LSStatistcsPlayerGenerationAlgorithm
          ( _voiceLibrary
          , _complexionGuesser
          , _batttingStanceGuesser
          , _pitchingMechanicsGuesser
          , new NoOpPre2008PitchArsenalLookup()
          )
        , onProgressUpdate: update => UpdateProgressAndSave(update, teamGenerationProgress)
        );

        DatabaseConfig.Database.Save(result.Team);
        teamGenerationProgress.Complete(result.Team.Id!.Value);
        DatabaseConfig.Database.Save(teamGenerationProgress);
      });

      return Task.FromResult(new TeamGenerationResponse { GenerationStatusId = teamGenerationProgress.Id!.Value });
    }

    private void UpdateProgressAndSave(ProgressUpdate update, TeamGenerationStatus status)
    {
      status.Update(update.CurrentAction, update.CurrentActionIndex, update.TotalActions);
      DatabaseConfig.Database.Save(status);
    }
  }

  public class TeamGenerationRequest
  {
    public long LSTeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int Year { get; set; }
  }

  public class TeamGenerationResponse
  {
    public int GenerationStatusId { get; set; }
  }
}
