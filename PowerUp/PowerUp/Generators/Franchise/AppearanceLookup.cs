using PowerUp.Entities.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PowerUp.Generators.Franchise
{
  public interface IAppearanceLookup
  {
    void ApplyAppearance(string playerName, int year, Player player);
  }

  public class AppearanceEntry
  {
    public int? FaceId { get; set; }
    public int? HairStyle { get; set; }
    public int? HairColor { get; set; }
    public int? FacialHairStyle { get; set; }
    public int? FacialHairColor { get; set; }
    public int? EyewearType { get; set; }
    public int? EyewearFrameColor { get; set; }
    public int? EyewearLensColor { get; set; }
    public int? EarringSide { get; set; }
    public int? EarringColor { get; set; }
    public int? RightWristband { get; set; }
    public int? LeftWristband { get; set; }
    public int? BatColor { get; set; }
    public int? GloveColor { get; set; }
    public int? EyeColor { get; set; }
    public int? EyebrowThickness { get; set; }
  }

  /// <summary>
  /// Applies player appearance from a curated JSON library for iconic players,
  /// with an algorithmic fallback for uncurated players that varies by era/skin/name
  /// to prevent identical-looking rosters.
  /// </summary>
  public class JsonAppearanceLookup : IAppearanceLookup
  {
    private readonly Dictionary<string, AppearanceEntry> _lookup;

    public JsonAppearanceLookup(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, AppearanceEntry>>(json,
          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
          ?? new();
        _lookup = new Dictionary<string, AppearanceEntry>();
        foreach (var kvp in raw)
          _lookup[kvp.Key.RemoveAccents()] = kvp.Value;
      }
      else
      {
        _lookup = new();
      }
    }

    public void ApplyAppearance(string playerName, int year, Player player)
    {
      if (_lookup.TryGetValue(playerName, out var entry))
        ApplyFromEntry(entry, player);
      else
        ApplyAlgorithmic(playerName, year, player);
    }

    private static void ApplyFromEntry(AppearanceEntry e, Player player)
    {
      var a = player.Appearance;
      if (e.FaceId.HasValue) a.FaceId = e.FaceId.Value;
      if (e.HairStyle.HasValue) a.HairStyle = e.HairStyle.Value == 0 ? null : (HairStyle)e.HairStyle.Value;
      if (e.HairColor.HasValue) a.HairColor = (HairColor)e.HairColor.Value;
      if (e.FacialHairStyle.HasValue) a.FacialHairStyle = e.FacialHairStyle.Value == 0 ? null : (FacialHairStyle)e.FacialHairStyle.Value;
      if (e.FacialHairColor.HasValue) a.FacialHairColor = (HairColor)e.FacialHairColor.Value;
      if (e.EyewearType.HasValue) a.EyewearType = e.EyewearType.Value == 0 ? null : (EyewearType)e.EyewearType.Value;
      if (e.EyewearFrameColor.HasValue) a.EyewearFrameColor = (EyewearFrameColor)e.EyewearFrameColor.Value;
      if (e.EyewearLensColor.HasValue) a.EyewearLensColor = (EyewearLensColor)e.EyewearLensColor.Value;
      if (e.EarringSide.HasValue) a.EarringSide = e.EarringSide.Value == 0 ? null : (EarringSide)e.EarringSide.Value;
      if (e.EarringColor.HasValue) a.EarringColor = (AccessoryColor)e.EarringColor.Value;
      if (e.RightWristband.HasValue) a.RightWristbandColor = (AccessoryColor)e.RightWristband.Value;
      if (e.LeftWristband.HasValue) a.LeftWristbandColor = (AccessoryColor)e.LeftWristband.Value;
      if (e.BatColor.HasValue) a.BatColor = (BatColor)e.BatColor.Value;
      if (e.GloveColor.HasValue) a.GloveColor = (GloveColor)e.GloveColor.Value;
      if (e.EyeColor.HasValue) a.EyeColor = (EyeColor)e.EyeColor.Value;
      if (e.EyebrowThickness.HasValue) a.EyebrowThickness = (EyebrowThickness)e.EyebrowThickness.Value;
    }

    private static void ApplyAlgorithmic(string playerName, int year, Player player)
      => AlgorithmicAppearance.Apply(playerName, year, player);
  }

  public class NoOpAppearanceLookup : IAppearanceLookup
  {
    public void ApplyAppearance(string playerName, int year, Player player) { }
  }
}
