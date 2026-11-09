using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>The frame is bounded, honestly labels simulated data, and shows missing observations.</summary>
public class ScreenTests
{
    /// <summary>V2 scheduling is visible and identifies configurations where it has no effect.</summary>
    [Theory]
    [InlineData(false, true, true, "V2 dispatch=ThreadPool", "L: toggle")]
    [InlineData(true, true, true, "V2 dispatch=LongRunning", "L: toggle")]
    [InlineData(true, false, true, "V2 dispatch=LongRunning", "inactive: Pool V1")]
    [InlineData(true, true, false, "V2 dispatch=LongRunning", "inactive: non-pooled")]
    public void Render_DispatchSelection_ShowsModeAndApplicability(
        bool longRunning, bool poolV2, bool pooling, string mode, string applicability)
    {
        // Act
        string screen = Screen.Render(
            Settings.Default() with { LongRunningOpen = longRunning, Pooling = pooling },
            new History(), null, 0, 100, 30, 0, 0, null, "", false, true, false, poolV2);

        // Assert
        Assert.Contains(mode, screen);
        Assert.Contains(applicability, screen);
    }

    /// <summary>The header distinguishes pool implementations and disabled pooling.</summary>
    [Theory]
    [InlineData(false, true, "Pool V1 (WaitHandle)")]
    [InlineData(true, true, "Pool V2 (Channel)")]
    [InlineData(false, false, "Pool V1 (inactive)")]
    [InlineData(true, false, "Pool V2 (inactive)")]
    public void Render_PoolVersion_ShowsSelectedImplementation(
        bool poolV2, bool pooling, string label)
    {
        // Act
        string screen = Screen.Render(Settings.Default() with { Pooling = pooling },
            new History(), null, 0, 120, 30, 0, 0, null, "", false, true, false, poolV2);

        // Assert
        Assert.Contains(label, screen);
    }

    /// <summary>An observer must not claim zero delay when another process owns netem.</summary>
    [Fact]
    public void Render_UnmanagedNetwork_DoesNotClaimZeroDelay()
    {
        // Act
        string screen = Screen.Render(Settings.Default(), new History(), null, 0,
            120, 30, 0, 0, null, "", false, true, false);

        // Assert
        Assert.Contains("Delay=unmanaged", screen);
        Assert.DoesNotContain("Delay=0", screen);
    }

    /// <summary>The selected workload identifies whether a SQL round trip will run.</summary>
    [Theory]
    [InlineData(0, "Open/close")]
    [InlineData(1, "Open/query/close (SELECT 1)")]
    public void Render_SelectedWorkload_ShowsOperation(int kind, string label)
    {
        // Act
        string screen = Screen.Render(
            Settings.Default() with { Kind = (WorkloadKind)kind },
            new History(), null, 0, 100, 30, 0, 0, null, "", false, true, false);

        // Assert
        Assert.Contains("Workload=" + label, screen);
        Assert.Contains("W: workload", screen);
    }

    /// <summary>The live header identifies the connection mode and advertises both controls.</summary>
    [Theory]
    [InlineData(true, "POOLED OpenAsync")]
    [InlineData(false, "NON-POOLED OpenAsync")]
    public void Render_ConnectionMode_ShowsModeAndPoolControls(bool pooling, string label)
    {
        // Act
        string screen = Screen.Render(Settings.Default() with { Pooling = pooling },
            new History(), null, 0, 100, 30, 0, 0, null, "", false, true, false);

        // Assert
        Assert.Contains("| " + label, screen);
        Assert.Contains("P: pooled/non-pooled", screen);
        Assert.Contains("C: clear pool", screen);
    }

    /// <summary>An unchanged frame generates no terminal traffic or erase sequences.</summary>
    [Fact]
    public void Update_UnchangedFrame_WritesNothing()
    {
        // Act
        string output = Screen.Update("unchanged", "unchanged");

        // Assert
        Assert.Empty(output);
    }

    /// <summary>Updating one row leaves all other rows visible without clearing the screen.</summary>
    [Fact]
    public void Update_ChangedRow_OverwritesOnlyThatRow()
    {
        // Arrange
        string previous = string.Join(Environment.NewLine, "header", "long old value", "footer");
        string frame = string.Join(Environment.NewLine, "header", "new", "footer");

        // Act
        string output = Screen.Update(frame, previous);

        // Assert
        Assert.Equal("\u001b[2;1Hnew\u001b[K", output);
    }

    /// <summary>Rows removed by paging are erased without disturbing retained rows.</summary>
    [Fact]
    public void Update_RemovedRow_ErasesStaleContent()
    {
        // Arrange
        string previous = string.Join(Environment.NewLine, "header", "stale row");

        // Act
        string output = Screen.Update("header", previous);

        // Assert
        Assert.Equal("\u001b[2;1H\u001b[K", output);
    }

    /// <summary>Initial display and resize invalidate the prior frame and redraw once.</summary>
    [Fact]
    public void Update_NoPreviousFrame_ClearsAndDraws()
    {
        // Act
        string output = Screen.Update("header", null);

        // Assert
        Assert.Equal("\u001b[2J\u001b[1;1Hheader\u001b[K", output);
    }

    /// <summary>Very small windows must not wrap or scroll outside their physical bounds.</summary>
    [Fact]
    public void Render_TinyTerminal_RespectsPhysicalBounds()
    {
        // Act
        string screen = Screen.Render(Settings.Default(), new History(), null, 0,
            20, 5, 0, 0, null, "paused", true, false, false);

        // Assert
        string[] lines = screen.Split(Environment.NewLine);
        Assert.True(lines.Length <= 5);
        Assert.All(lines, line => Assert.True(line.Length <= 20));
    }

    /// <summary>Small terminals page metrics instead of scrolling over the controls.</summary>
    [Fact]
    public void Render_SmallTerminal_BoundsLinesAndLabelsMissingData()
    {
        // Arrange
        var settings = new Settings(8, 100, 4, 100, 150);
        var history = new History();
        var sample = new Sample(1, new() { ["Open p99 ~ms"] = null }, false);
        history.Add(sample);

        // Act
        string screen = Screen.Render(settings, history, sample, 1, 80, 24,
            0, 0, null, "preview", true, true, true);

        // Assert
        Assert.Contains("SIMULATED", screen);
        Assert.Contains("N/A", screen);
        Assert.Contains("300 ms RTT", screen);
        Assert.All(screen.Split(Environment.NewLine), line => Assert.True(line.Length <= 80));
        Assert.True(screen.Split(Environment.NewLine).Length <= 24);
    }
}
