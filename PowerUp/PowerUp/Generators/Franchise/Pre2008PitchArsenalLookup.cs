using PowerUp.Entities.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerUp.Generators.Franchise
{
  public interface IPre2008PitchArsenalLookup
  {
    void ApplyArsenal(string playerName, Player player);
    bool HasPlayer(string playerName);
  }

  public class Pre2008ArsenalEntry
  {
    [JsonPropertyName("top_speed_mph")]
    public double? TopSpeedMph { get; set; }

    [JsonPropertyName("has_two_seam")]
    public bool? HasTwoSeam { get; set; }

    [JsonPropertyName("two_seam_movement")]
    public int? TwoSeamMovement { get; set; }

    [JsonPropertyName("slider1_type")]
    public int? Slider1Type { get; set; }

    [JsonPropertyName("slider1_movement")]
    public int? Slider1Movement { get; set; }

    [JsonPropertyName("slider2_type")]
    public int? Slider2Type { get; set; }

    [JsonPropertyName("slider2_movement")]
    public int? Slider2Movement { get; set; }

    [JsonPropertyName("curve1_type")]
    public int? Curve1Type { get; set; }

    [JsonPropertyName("curve1_movement")]
    public int? Curve1Movement { get; set; }

    [JsonPropertyName("curve2_type")]
    public int? Curve2Type { get; set; }

    [JsonPropertyName("curve2_movement")]
    public int? Curve2Movement { get; set; }

    [JsonPropertyName("fork1_type")]
    public int? Fork1Type { get; set; }

    [JsonPropertyName("fork1_movement")]
    public int? Fork1Movement { get; set; }

    [JsonPropertyName("fork2_type")]
    public int? Fork2Type { get; set; }

    [JsonPropertyName("fork2_movement")]
    public int? Fork2Movement { get; set; }

    [JsonPropertyName("sinker1_type")]
    public int? Sinker1Type { get; set; }

    [JsonPropertyName("sinker1_movement")]
    public int? Sinker1Movement { get; set; }
  }

  public class JsonPre2008PitchArsenalLookup : IPre2008PitchArsenalLookup
  {
    private readonly Dictionary<string, Pre2008ArsenalEntry> _lookup;

    public JsonPre2008PitchArsenalLookup(string jsonFilePath)
    {
      if (File.Exists(jsonFilePath))
      {
        var json = File.ReadAllText(jsonFilePath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, Pre2008ArsenalEntry>>(json)
          ?? new();
        _lookup = new Dictionary<string, Pre2008ArsenalEntry>();
        foreach (var kvp in raw)
          _lookup[kvp.Key.RemoveAccents()] = kvp.Value;
      }
      else
      {
        _lookup = new();
      }
    }

    public bool HasPlayer(string playerName) => _lookup.ContainsKey(playerName);

    public void ApplyArsenal(string playerName, Player player)
    {
      if (!_lookup.TryGetValue(playerName, out var e))
        return;

      var p = player.PitcherAbilities;

      // Clear existing heuristic pitches
      p.HasTwoSeam = false; p.TwoSeamMovement = null;
      p.Slider1Type = null; p.Slider1Movement = null;
      p.Slider2Type = null; p.Slider2Movement = null;
      p.Curve1Type = null; p.Curve1Movement = null;
      p.Curve2Type = null; p.Curve2Movement = null;
      p.Fork1Type = null; p.Fork1Movement = null;
      p.Fork2Type = null; p.Fork2Movement = null;
      p.Sinker1Type = null; p.Sinker1Movement = null;
      p.Sinker2Type = null; p.Sinker2Movement = null;
      p.SinkingFastball1Type = null; p.SinkingFastball1Movement = null;
      p.SinkingFastball2Type = null; p.SinkingFastball2Movement = null;

      if (e.TopSpeedMph.HasValue)
        p.TopSpeedMph = Math.Min(105, Math.Max(49, e.TopSpeedMph.Value));

      if (e.HasTwoSeam == true)
      {
        p.HasTwoSeam = true;
        p.TwoSeamMovement = e.TwoSeamMovement;
      }

      if (e.Slider1Type.HasValue) { p.Slider1Type = (SliderType)e.Slider1Type.Value; p.Slider1Movement = e.Slider1Movement; }
      if (e.Slider2Type.HasValue) { p.Slider2Type = (SliderType)e.Slider2Type.Value; p.Slider2Movement = e.Slider2Movement; }
      if (e.Curve1Type.HasValue) { p.Curve1Type = (CurveType)e.Curve1Type.Value; p.Curve1Movement = e.Curve1Movement; }
      if (e.Curve2Type.HasValue) { p.Curve2Type = (CurveType)e.Curve2Type.Value; p.Curve2Movement = e.Curve2Movement; }
      if (e.Fork1Type.HasValue) { p.Fork1Type = (ForkType)e.Fork1Type.Value; p.Fork1Movement = e.Fork1Movement; }
      if (e.Fork2Type.HasValue) { p.Fork2Type = (ForkType)e.Fork2Type.Value; p.Fork2Movement = e.Fork2Movement; }
      if (e.Sinker1Type.HasValue) { p.Sinker1Type = (SinkerType)e.Sinker1Type.Value; p.Sinker1Movement = e.Sinker1Movement; }

      player.GeneratedPlayer_PitchArsenalSource = "curated";
    }
  }

  public class NoOpPre2008PitchArsenalLookup : IPre2008PitchArsenalLookup
  {
    public void ApplyArsenal(string playerName, Player player) { }
    public bool HasPlayer(string playerName) => false;
  }
}
