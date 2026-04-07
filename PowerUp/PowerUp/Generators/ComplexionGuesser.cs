using PowerUp.Entities.Players;
using PowerUp.Libraries;
using System;

namespace PowerUp.Generators
{
  public interface IComplexionGuesser
  {
    public Complexion GuessComplexion(int year, string? birthCountry);
  }

  public class ComplexionGuesser : IComplexionGuesser
  {
    private readonly ICountryAndComplexionLibrary _countryAndComplexionLibrary;

    public ComplexionGuesser(ICountryAndComplexionLibrary countryAndComplexionLibrary)
    {
      _countryAndComplexionLibrary = countryAndComplexionLibrary;
    }
        
    public Complexion GuessComplexion(int year, string? birthCountry)
    {
      if (birthCountry == "United States of America" || birthCountry == "USA" || birthCountry == null)
        return GuessAmericanComplexionForYear(year);

      var complexionForCountry = _countryAndComplexionLibrary[birthCountry];
      return complexionForCountry.HasValue
        ? (Complexion)(complexionForCountry - 1)
        : Complexion.Three;
    }

    private Complexion GuessAmericanComplexionForYear(int year)
    {
      var rand = Random.Shared.NextDouble();

      if (year < 1947)
        return Complexion.One;
      else if (year < 1957)
        return ForPercentagesAndRandomNumber(.9, .06, .04, rand);
      else if (year < 1967)
        return ForPercentagesAndRandomNumber(.8, .15, .05, rand);
      else if (year < 1977)
        return ForPercentagesAndRandomNumber(.7, .2, .1, rand);
      else if (year < 1987)
        return ForPercentagesAndRandomNumber(.68, .22, .1, rand);
      else if (year < 1997)
        return ForPercentagesAndRandomNumber(.75, .15, .1, rand);
      else if (year < 2007)
        return ForPercentagesAndRandomNumber(.8, .08, .08, rand);
      else if (year < 2017)
        return ForPercentagesAndRandomNumber(.85, .07, .8, rand);
      else
        return ForPercentagesAndRandomNumber(.85, .05, .1, rand);
    }

    private Complexion ForPercentagesAndRandomNumber(double white, double africanAmerican, double latino, double rand)
    {
      var rand2 = Random.Shared.NextDouble();

      if (rand < white)
        return Complexion.One;
      else if (rand < white + africanAmerican)
        return rand2 > .5
          ? Complexion.Five
          : Complexion.Four;
      else if (rand < white + africanAmerican + latino)
        return rand2 > .5
          ? Complexion.Three
          : Complexion.Four;
      else
        return Complexion.Two;
    }
  }
}
