using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PowerUp.Fetchers.MLBStatsApi
{
  public interface IMLBStatsApiClient
  {
    Task<TeamListResult> GetTeams(int year);
    Task<TeamListResult> GetTeam(int teamId, int? year = null);
    Task<RosterResult> GetTeamRoster(long mlbTeamId, int year, string rosterType = "active");
    Task<AllStarGameRosters> GetAllStarGameRosters(int year);
    Task<VenueResult> GetVenues(IEnumerable<long> venueIds, int? year = null);
    Task<Person> GetPlayerInfo(long mlbPlayerId);
    Task<Person> GetPlayerStatistics(long mlbPlayerId, int year);
    Task<PeopleResults> SearchPlayers(string name);
    Task<PitchArsenalResponse> GetPitchArsenal(long mlbPlayerId, int year);
    Task<HotColdZonesResponse> GetHotColdZones(long mlbPlayerId, int year);
    Task<SabermetricsResponse> GetSabermetrics(long mlbPlayerId, int year);
    Task<OutsAboveAverageResponse> GetOutsAboveAverage(long mlbPlayerId, int year);
  }

  public class MLBStatsApiClient : IMLBStatsApiClient
  {
    private const string BASE_URL = "https://statsapi.mlb.com/api/v1";
    private readonly ApiClient _client = new ApiClient();

    public async Task<RosterResult> GetTeamRoster(long mlbTeamId, int year, string rosterType = "active")
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "teams", mlbTeamId.ToString(), "roster" },
        new { rosterType, season = year.ToString() }
      );
      return await _client.Get<RosterResult>(url);
    }

    public async Task<AllStarGameRosters> GetAllStarGameRosters(int year)
    {
      // Step 1: Find the All-Star Game from the schedule
      var scheduleUrl = UrlBuilder.Build(
        new[] { BASE_URL, "schedule" },
        new { sportId = 1, gameType = "A", season = year.ToString() }
      );
      var schedule = await _client.Get<ScheduleResponse>(scheduleUrl);
      var game = schedule.Dates?.SelectMany(d => d.Games ?? []).FirstOrDefault();
      if (game == null)
        return new AllStarGameRosters();

      // Step 2: Fetch the game feed boxscore for rosters
      var feedUrl = $"https://statsapi.mlb.com/api/v1.1/game/{game.GamePk}/feed/live";
      var feed = await _client.Get<GameFeedResponse>(feedUrl);

      var result = new AllStarGameRosters();
      var boxscore = feed.LiveData?.Boxscore;
      if (boxscore == null)
        return result;

      foreach (var side in new[] { "away", "home" })
      {
        var teamBox = side == "away" ? boxscore.Teams?.Away : boxscore.Teams?.Home;
        if (teamBox?.Players == null) continue;

        var teamId = teamBox.Team?.Id ?? 0;
        var playerIds = teamBox.Players.Values
          .Where(p => p.Person?.Id > 0 && p.Position?.Abbreviation != "P" || p.AllPositions?.Any(ap => ap.Abbreviation != "P") == true)
          .Select(p => p.Person!.Id)
          .ToList();
        // Include all players (pitchers and position players)
        playerIds = teamBox.Players.Values
          .Where(p => p.Person?.Id > 0)
          .Select(p => p.Person!.Id)
          .ToList();

        if (teamId == 159)
          result.ALPlayerIds = playerIds;
        else if (teamId == 160)
          result.NLPlayerIds = playerIds;
      }

      return result;
    }

    public async Task<TeamListResult> GetTeams(int year)
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "teams" },
        new { sportId = 1, season = year.ToString(), leagueIds = "103,104,106" }
      );
      return await _client.Get<TeamListResult>(url);
    }

    public async Task<TeamListResult> GetTeam(int teamId, int? year = null)
    {
      var parameters = new Dictionary<string, string>();
      if (year.HasValue) parameters.Add("season", year.Value.ToString());
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "teams", teamId.ToString() },
        parameters
      );
      return await _client.Get<TeamListResult>(url);
    }

    public Task<Person> GetPlayerInfo(long mlbPlayerId)
    {
      return GetPlayerData(mlbPlayerId, null);
    }

    public Task<Person> GetPlayerStatistics(long mlbPlayerId, int year)
    {
      return GetPlayerData(mlbPlayerId, year);
    }

    private async Task<Person> GetPlayerData(long mlbPlayerId, int? year)
    {
      var group = "[hitting,pitching,fielding]";
      var type = "season";
      var sportId = 1;
      var hydration = year.HasValue
        ? $"stats(group={group},type={type},sportId={sportId},season={year}),currentTeam"
        : "currentTeam";

      var url = UrlBuilder.Build(
        new[] { BASE_URL, "people" },
        new { personIds = mlbPlayerId, hydrate = hydration }
      );

      var response = await _client.Get<PeopleResults>(url);
      var person = response.People.SingleOrDefault();
      if (person is null)
        throw new InvalidOperationException("No player info found for this id");

      return person;
    }

    public async Task<PeopleResults> SearchPlayers(string name)
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "people", "search" },
        new { names = name, sportIds = 1 }
      );
      return await _client.Get<PeopleResults>(url);
    }

    public async Task<PitchArsenalResponse> GetPitchArsenal(long mlbPlayerId, int year)
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "people", mlbPlayerId.ToString(), "stats" },
        new { stats = "pitchArsenal", group = "pitching", season = year.ToString() }
      );
      try
      {
        return await _client.Get<PitchArsenalResponse>(url);
      }
      catch (HttpStatusException)
      {
        return new PitchArsenalResponse();
      }
    }

    public async Task<HotColdZonesResponse> GetHotColdZones(long mlbPlayerId, int year)
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "people", mlbPlayerId.ToString(), "stats" },
        new { stats = "hotColdZones", group = "hitting", season = year.ToString() }
      );
      try { return await _client.Get<HotColdZonesResponse>(url); }
      catch (HttpStatusException) { return new HotColdZonesResponse(); }
    }

    public async Task<SabermetricsResponse> GetSabermetrics(long mlbPlayerId, int year)
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "people", mlbPlayerId.ToString(), "stats" },
        new { stats = "sabermetrics", group = "hitting", season = year.ToString() }
      );
      try { return await _client.Get<SabermetricsResponse>(url); }
      catch (HttpStatusException) { return new SabermetricsResponse(); }
    }

    public async Task<OutsAboveAverageResponse> GetOutsAboveAverage(long mlbPlayerId, int year)
    {
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "people", mlbPlayerId.ToString(), "stats" },
        new { stats = "outsAboveAverage", group = "fielding", season = year.ToString() }
      );
      try { return await _client.Get<OutsAboveAverageResponse>(url); }
      catch (HttpStatusException) { return new OutsAboveAverageResponse(); }
    }

    public async Task<VenueResult> GetVenues(IEnumerable<long> venueIds, int? year = null)
    {
      var parameters = new Dictionary<string, string>
      {
        { "venueIds", venueIds.StringJoin(",") },
        { "hydrate", "location" }
      };
      if (year.HasValue) parameters.Add("season", year.Value.ToString());
      var url = UrlBuilder.Build(
        new[] { BASE_URL, "venues" },
        parameters
      );

      try
      {
        return await _client.Get<VenueResult>(url);
      }
      catch(HttpStatusException exception)
      {
        if (exception.StatusCode != HttpStatusCode.NotFound) throw;
        return new VenueResult { Venues = [] };
      }
    }
  }
}
