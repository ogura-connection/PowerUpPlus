using PowerUp.Fetchers.MLBStatsApi;
using System.Collections.Generic;
using System.Linq;

namespace PowerUp.Fetchers.MLBLookupService
{
  public class TeamsForYearResults
  {
    public long TotalResults { get; }
    public IEnumerable<TeamResult> Results { get; }

    public TeamsForYearResults(IEnumerable<(TeamEntry Team, VenueEntry? Venue)> teams)
    {
      var teamList = teams.ToList();
      TotalResults = teamList.Count;
      Results = teamList.Select(t => new TeamResult(t.Team, t.Venue)).ToList();
    }
  }

  public class TeamResult
  {
    public int LSTeamId { get; }
    public int Year { get; }
    public string LocationName { get; }
    public string TeamName { get; }
    public string FullName { get; }
    public string State { get; set; }
    public string City { get; set; }
    public string Venue { get; set; }
    public string League { get; set; }
    public string? Division { get; set; }

    public TeamResult(TeamEntry team, VenueEntry? venue)
    {
      LSTeamId = (int)team.Id;
      Year = (int)team.Season;
      LocationName = team.LocationName;
      TeamName = team.TeamName;
      FullName = team.Name;
      State = venue?.Location?.StateAbbrev ?? "";
      City = venue?.Location?.City ?? "";
      Venue = venue?.Name ?? team.Venue?.Name ?? "";
      League = team.League?.Abbreviation ?? team.League?.Name ?? "";
      Division = team.Division?.Name;
    }
  }
}
