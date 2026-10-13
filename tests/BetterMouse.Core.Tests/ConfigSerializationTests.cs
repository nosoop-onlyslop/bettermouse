using System.Text.Json;
using System.Text.Json.Serialization;
using BetterMouse.Core;
using Xunit;

namespace BetterMouse.Core.Tests;

public class ConfigSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void RoundTripsAllFields()
    {
        var config = new MouseMappingConfig
        {
            Enabled = true,
            Unstick = false,
            Jump = true,
            Wrap = true,
        };
        config.Borders.Add(new BorderProfile
        {
            AId = "left",
            BId = "right",
            ASide = Side.Right,
            Points =
            {
                new ControlPoint(0.0, 0.1),
                new ControlPoint(0.4, 0.65),
                new ControlPoint(1.0, 1.0),
            },
        });

        var json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<MouseMappingConfig>(json, Options);

        Assert.NotNull(restored);
        Assert.True(restored!.Enabled);
        Assert.False(restored.Unstick);
        Assert.True(restored.Jump);
        Assert.True(restored.Wrap);

        var border = Assert.Single(restored.Borders);
        Assert.Equal("left", border.AId);
        Assert.Equal("right", border.BId);
        Assert.Equal(Side.Right, border.ASide);
        Assert.Equal(3, border.Points.Count);
        Assert.Equal(0.4, border.Points[1].A, 6);
        Assert.Equal(0.65, border.Points[1].B, 6);
    }
}
