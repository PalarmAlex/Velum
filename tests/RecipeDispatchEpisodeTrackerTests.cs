using System;
using ISIDA.Common;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты RecipeDispatchEpisodeTracker — эпизоды dispatch и рефрактерность.
  /// Работают на заглушках ISIDA.Common (GlobalTimer / Logger).
  /// </summary>
  public class RecipeDispatchEpisodeTrackerTests : IDisposable
  {
    public RecipeDispatchEpisodeTrackerTests()
    {
      RecipeDispatchEpisodeTracker.Clear();
      GlobalTimer.GlobalPulsCount = 0;
      Logger.InfoMessages.Clear();
    }

    public void Dispose()
    {
      RecipeDispatchEpisodeTracker.Clear();
      GlobalTimer.GlobalPulsCount = 0;
    }

    // ----- Episodes -----

    [Xunit.Fact]
    public void WasSuccessfullyDispatched_UnknownEpisode_ReturnsFalse()
    {
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.WasSuccessfullyDispatched("a|1|doc"));
    }

    [Xunit.Fact]
    public void MarkThenWas_ReturnsTrue_CaseInsensitive()
    {
      RecipeDispatchEpisodeTracker.MarkSuccessfullyDispatched("Act|5|Doc");
      Xunit.Assert.True(RecipeDispatchEpisodeTracker.WasSuccessfullyDispatched("act|5|doc"));
    }

    [Xunit.Fact]
    public void EmptyEpisodeKey_Ignored()
    {
      RecipeDispatchEpisodeTracker.MarkSuccessfullyDispatched(null);
      RecipeDispatchEpisodeTracker.MarkSuccessfullyDispatched("   ");
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.WasSuccessfullyDispatched(null));
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.WasSuccessfullyDispatched(""));
    }

    // ----- Refractory -----

    [Xunit.Fact]
    public void MarkRefractory_BlocksUntilPulseReached()
    {
      GlobalTimer.GlobalPulsCount = 100;
      RecipeDispatchEpisodeTracker.MarkRefractory(actionId: 7, refractoryPulses: 30);

      Xunit.Assert.True(RecipeDispatchEpisodeTracker.IsRefractory(7));

      GlobalTimer.GlobalPulsCount = 129;
      Xunit.Assert.True(RecipeDispatchEpisodeTracker.IsRefractory(7));

      GlobalTimer.GlobalPulsCount = 130;
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.IsRefractory(7));
    }

    [Xunit.Fact]
    public void IsRefractory_UnknownAction_ReturnsFalse()
    {
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.IsRefractory(42));
    }

    [Xunit.Fact]
    public void MarkRefractory_DefaultPulsesIsThirty()
    {
      GlobalTimer.GlobalPulsCount = 0;
      RecipeDispatchEpisodeTracker.MarkRefractory(actionId: 1);
      GlobalTimer.GlobalPulsCount = 29;
      Xunit.Assert.True(RecipeDispatchEpisodeTracker.IsRefractory(1));
      GlobalTimer.GlobalPulsCount = 30;
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.IsRefractory(1));
    }

    [Xunit.Fact]
    public void MarkRefractory_ReplacesPreviousWindow()
    {
      GlobalTimer.GlobalPulsCount = 0;
      RecipeDispatchEpisodeTracker.MarkRefractory(3, refractoryPulses: 10);
      GlobalTimer.GlobalPulsCount = 5;
      RecipeDispatchEpisodeTracker.MarkRefractory(3, refractoryPulses: 10); // до 15
      GlobalTimer.GlobalPulsCount = 12;
      Xunit.Assert.True(RecipeDispatchEpisodeTracker.IsRefractory(3));
    }

    [Xunit.Fact]
    public void Refractory_ExpiredThenRechecked_LogsExpiry()
    {
      GlobalTimer.GlobalPulsCount = 0;
      RecipeDispatchEpisodeTracker.MarkRefractory(9, refractoryPulses: 5);
      GlobalTimer.GlobalPulsCount = 5;
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.IsRefractory(9));
      Xunit.Assert.True(Logger.InfoMessages.Exists(m => m.Contains("expired")));
    }

    // ----- Clear -----

    [Xunit.Fact]
    public void Clear_RemovesEpisodesAndRefractory()
    {
      RecipeDispatchEpisodeTracker.MarkSuccessfullyDispatched("a|1|doc");
      RecipeDispatchEpisodeTracker.MarkRefractory(7, 100);

      RecipeDispatchEpisodeTracker.Clear();

      Xunit.Assert.False(RecipeDispatchEpisodeTracker.WasSuccessfullyDispatched("a|1|doc"));
      Xunit.Assert.False(RecipeDispatchEpisodeTracker.IsRefractory(7));
    }
  }
}
