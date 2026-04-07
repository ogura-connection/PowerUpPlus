using PowerUp.Entities.Players;
using PowerUp.Fetchers.MLBStatsApi;
using System;

namespace PowerUp.Fetchers.MLBLookupService
{
  public class PlayerInfoResult
  {
    public long LSPlayerId { get; }
    public Position Position { get; }
    public string? NamePrefix { get; }
    public string FirstName { get; }
    public string FirstNameUsed { get; }
    public string? MiddleName { get; }
    public string LastName { get; }
    public string FormalDisplayName { get; }
    public string InformalDisplayName { get; }
    public string? NickName { get; }
    public string? UniformNumber { get; }
    public BattingSide BattingSide { get; }
    public ThrowingArm ThrowingArm { get; }
    public int? Weight { get; }
    public int? HeightFeet { get; }
    public int? HeightInches { get; }
    public DateTime? BirthDate { get; }
    public string? BirthCountry { get; }
    public string? BirthState { get; }
    public string? BirthCity { get; }
    public DateTime? DeathDate { get; }
    public string? DeathCountry { get; }
    public string? DeathState { get; }
    public string? DeathCity { get; }
    public int? Age { get; }
    public string? HighSchool { get; }
    public string? College { get; }
    public DateTime? ProDebutDate { get; }
    public DateTime? StartDate { get; }
    public DateTime? EndDate { get; }
    public int? ServiceYears { get; }
    public string? TeamName { get; }

    public PlayerInfoResult(Person person)
    {
      LSPlayerId = person.Id;
      // NamePrefix
      FirstName = person.FirstName;
      FirstNameUsed = person.UseName;
      MiddleName = person.MiddleName;
      LastName = LastName ?? person.UseLastName;
      FormalDisplayName = person.LastFirstName;
      InformalDisplayName = person.FirstLastName;
      NickName = person.NickName;
      UniformNumber = string.IsNullOrEmpty(person.PrimaryNumber) ? null : person.PrimaryNumber;
      Position = LookupServiceValueMapper.MapPosition(person.PrimaryPosition?.Code);
      BattingSide = LookupServiceValueMapper.MapBatingSide(person.BatSide?.Code);
      ThrowingArm = LookupServiceValueMapper.MapThrowingArm(person.PitchHand?.Code);
      Weight = person.Weight;
      var parsedHeight = LookupServiceValueMapper.ParseHeight(person.Height);
      HeightFeet = parsedHeight?.heightFeet;
      HeightInches = parsedHeight?.heightInches;
      BirthDate = person.BirthDate;
      BirthCountry = person.BirthCountry;
      BirthState = person.BirthStateProvince;
      BirthCity = person.BirthCity;
      DeathDate = person.DeathDate;
      DeathCountry = person.DeathCountry;
      DeathState = person.DeathStateProvince;
      DeathCity = person.DeathCity;
      Age = person.CurrentAge;
      // HighSchool
      // College
      ProDebutDate = person.MlbDebutDate;
      StartDate = person.MlbDebutDate;
      EndDate = person.LastPlayedDate;
      // ServiceYears
      TeamName = person.CurrentTeam?.Name;
    }
  }
}
