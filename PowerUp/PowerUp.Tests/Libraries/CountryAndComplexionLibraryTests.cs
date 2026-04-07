using NUnit.Framework;
using PowerUp.Libraries;
using Shouldly;

namespace PowerUp.Tests.Libraries
{
  public class CountryAndComplexionLibraryTests
  {
    ICountryAndComplexionLibrary library;

    [SetUp]
    public void SetUp()
    {
      library = TestConfig.CountryAndComplexionLibrary.Value;
    }


    [Test]
    public void Library_GetsComplexionForCountry()
    {
      library["United States of America"].ShouldBe(1);
      library["Dominican Republic"].ShouldBe(4);
      library["Venezuela"].ShouldBe(3);
      library["Mexico"].ShouldBe(3);
      library["Puerto Rico"].ShouldBe(4);
      library["Japan"].ShouldBe(2);
      library["Republic of Korea"].ShouldBe(2);
    }
  }
}
