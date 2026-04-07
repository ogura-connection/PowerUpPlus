using PowerUp.Entities.Players;
using System;
using System.Linq;

namespace PowerUp.Generators
{
  /// <summary>
  /// Deterministic hash-based appearance generator.
  /// Uses player name as a stable seed to produce varied but consistent appearances
  /// across regenerations. Era-aware: hair, facial hair, and accessories reflect the period.
  /// </summary>
  public static class AlgorithmicAppearance
  {
    // East Asian-appropriate face models:
    // 180 = Thin Oval Eyes, 190 = Pencil Thin Eyes (Upturn),
    // 191 = Pencil Thin Eyes, 192 = Pencil Thin Eyes (Downturn), 194 = Thin Eyes
    private static readonly int[] EastAsianFaces = new[] { 180, 190, 191, 192, 194 };

    // Common Korean surnames (romanized) — covers ~90% of Korean players
    private static readonly string[] KoreanSurnames = new[] {
      "Kim", "Lee", "Park", "Choi", "Jung", "Kang", "Yoon", "Cho", "Jang",
      "Lim", "Han", "Oh", "Seo", "Shin", "Kwon", "Hwang", "Ahn", "Song",
      "Yoo", "Hong", "Jeon", "Ryu", "Bae", "Noh", "Moon", "Yang", "Ha",
      "Choo", "Hyun", "Ko"
    };

    public static void Apply(string playerName, int year, Player player)
    {
      var seed = GetDeterministicHash(playerName);
      var a = player.Appearance;

      // Default eye color and eyebrow thickness
      a.EyeColor = EyeColor.Brown;
      a.EyebrowThickness = EyebrowThickness.Thick;

      // Face selection: East Asian players get appropriate face models
      if (a.Complexion == Complexion.Two && IsLikelyEastAsian(playerName))
      {
        a.FaceId = EastAsianFaces[seed % EastAsianFaces.Length];
      }
      else
      {
        var genericFaces = new[] { 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194 };
        a.FaceId = genericFaces[seed % genericFaces.Length];
      }

      // Hair style varies by era
      if (year < 1920)
        a.HairStyle = Pick(seed, 1, new[] { HairStyle.FlatShort, HairStyle.Basic, HairStyle.BasicShortSideburns });
      else if (year < 1950)
        a.HairStyle = Pick(seed, 1, new[] { HairStyle.FlatShort, HairStyle.FlatMedium, HairStyle.Basic, HairStyle.BasicShortSideburns });
      else if (year < 1975)
        a.HairStyle = Pick(seed, 1, new[] { HairStyle.FlatMedium, HairStyle.Basic, HairStyle.BasicMediumSideburns, HairStyle.BasicLongSideburns, HairStyle.CurlyShort, HairStyle.Afro });
      else if (year < 2000)
        a.HairStyle = Pick(seed, 1, new[] { HairStyle.FlatMedium, HairStyle.FlatShort, HairStyle.Basic, HairStyle.BasicShortSideburns, HairStyle.Fade, HairStyle.FadeShortSideburns, HairStyle.CurlyShort, HairStyle.CurlyMedium });
      else
        a.HairStyle = Pick(seed, 1, new[] { HairStyle.Fade, HairStyle.FadeShortSideburns, HairStyle.FadeMediumSideburns, HairStyle.Basic, HairStyle.BasicShortSideburns, HairStyle.FlatShort, HairStyle.CurlyShort, HairStyle.CurlyMedium });

      // Some players are bald (seed-based, ~8% chance)
      if (seed % 13 == 0) a.HairStyle = null;

      // Hair color based on complexion
      var skin = a.Complexion ?? Complexion.One;
      if (skin == Complexion.Four || skin == Complexion.Five)
        a.HairColor = HairColor.Black;
      else if (skin == Complexion.Three)
        a.HairColor = Pick(seed, 2, new[] { HairColor.Black, HairColor.DarkBrown, HairColor.Brown });
      else
        a.HairColor = Pick(seed, 2, new[] { HairColor.DarkBrown, HairColor.Brown, HairColor.LightBrown, HairColor.DarkBlonde, HairColor.Black });

      // Clean-shaven by default — facial hair is opt-in via curated AppearanceLibrary.json
      a.FacialHairStyle = null;

      // Bat color
      a.BatColor = Pick(seed, 4, new[] { BatColor.Natural, BatColor.Black, BatColor.Natural_Black, BatColor.Brown });

      // Glove color
      a.GloveColor = Pick(seed, 5, new[] { GloveColor.Tan, GloveColor.Brown, GloveColor.Black, GloveColor.Orange });

      // Wristbands — modern players only
      if (year >= 2005 && seed % 4 == 0)
      {
        a.RightWristbandColor = Pick(seed, 6, new[] { AccessoryColor.Black, AccessoryColor.White, AccessoryColor.Red, AccessoryColor.DarkBlue });
      }
      if (year >= 2005 && seed % 5 == 0)
      {
        a.LeftWristbandColor = Pick(seed, 7, new[] { AccessoryColor.Black, AccessoryColor.White, AccessoryColor.Red, AccessoryColor.DarkBlue });
      }
    }

    /// <summary>
    /// Detects likely East Asian (Japanese, Korean, Chinese) players by name patterns.
    /// Must be combined with Complexion check — name alone is not sufficient.
    /// </summary>
    private static bool IsLikelyEastAsian(string playerName)
    {
      var parts = playerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length < 2) return false;

      var lastName = parts[^1];

      // Check Korean surnames (exact match, case-insensitive)
      if (KoreanSurnames.Any(k => k.Equals(lastName, StringComparison.OrdinalIgnoreCase)))
        return true;

      // Japanese name heuristic: romanized Japanese names almost always end in
      // a vowel or 'n', and typically every consonant is followed by a vowel.
      // Check both first and last name for this pattern.
      return IsJapaneseRomanized(parts[0]) && IsJapaneseRomanized(lastName);
    }

    private static bool IsJapaneseRomanized(string name)
    {
      if (name.Length < 2) return false;
      var lower = name.ToLowerInvariant();

      // Must end in a vowel or 'n'
      var lastChar = lower[^1];
      if (lastChar != 'a' && lastChar != 'e' && lastChar != 'i' && lastChar != 'o' && lastChar != 'u' && lastChar != 'n')
        return false;

      // Should not contain consonant clusters uncommon in Japanese romanization
      // (Japanese has: ch, sh, ts, ky, gy, ny, hy, my, ry, by, py — all followed by vowel)
      var invalidClusters = new[] { "th", "ph", "ck", "wr", "wh", "str", "br", "cr", "dr", "fr", "gr", "pr", "tr", "bl", "cl", "fl", "gl", "pl", "sl" };
      return !invalidClusters.Any(c => lower.Contains(c));
    }

    private static T Pick<T>(int seed, int offset, T[] options)
    {
      return options[((seed >> offset) + offset) % options.Length];
    }

    public static int GetDeterministicHash(string s)
    {
      // Simple stable hash (not GetHashCode which varies across runs)
      unchecked
      {
        int hash = 17;
        foreach (char c in s)
          hash = hash * 31 + c;
        return Math.Abs(hash);
      }
    }
  }
}
