using System;
using Velum.ReactiveCore;

namespace Velum.ReactiveCore.Tests
{
  /// <summary>
  /// Тесты RecipeDispatchCooldownTracker — кулдаун dispatch по ключу в тактах пульса.
  /// </summary>
  public class RecipeDispatchCooldownTrackerTests : IDisposable
  {
    public RecipeDispatchCooldownTrackerTests()
    {
      RecipeDispatchCooldownTracker.Clear();
    }

    public void Dispose()
    {
      RecipeDispatchCooldownTracker.Clear();
    }

    [Xunit.Fact]
    public void TryAllowDispatch_WithoutRegistration_ReturnsTrue()
    {
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", currentPulse: 0, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void TryAllowDispatch_ImmediatelyAfterRegistration_ReturnsFalse()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("r1|doc", currentPulse: 10);
      Xunit.Assert.False(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", currentPulse: 10, cooldownPulses: 5));
      Xunit.Assert.False(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", currentPulse: 14, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void TryAllowDispatch_AfterFullCooldown_ReturnsTrue()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("r1|doc", currentPulse: 10);
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", currentPulse: 15, cooldownPulses: 5));
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", currentPulse: 99, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void TryAllowDispatch_EmptyKey_AlwaysAllowed()
    {
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch(null, 0, 5));
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("   ", 0, 5));
    }

    [Xunit.Fact]
    public void TryAllowDispatch_NonPositiveCooldown_AlwaysAllowed()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("r1|doc", 10);
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 10, cooldownPulses: 0));
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 10, cooldownPulses: -1));
    }

    [Xunit.Fact]
    public void Keys_AreCaseInsensitive()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("R1|DOC", 10);
      Xunit.Assert.False(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 12, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void DifferentKeys_DoNotInterfere()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("a", 10);
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("b", 10, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void RegisterDispatch_EmptyKey_IsIgnored()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch(null, 10);
      RecipeDispatchCooldownTracker.RegisterDispatch("  ", 10);
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 10, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void Clear_ResetsAllCooldowns()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("r1|doc", 10);
      RecipeDispatchCooldownTracker.Clear();
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 10, cooldownPulses: 5));
    }

    [Xunit.Fact]
    public void ReRegister_UpdatesCooldownWindow()
    {
      RecipeDispatchCooldownTracker.RegisterDispatch("r1|doc", 10);
      RecipeDispatchCooldownTracker.RegisterDispatch("r1|doc", 20);
      Xunit.Assert.False(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 24, cooldownPulses: 5));
      Xunit.Assert.True(RecipeDispatchCooldownTracker.TryAllowDispatch("r1|doc", 25, cooldownPulses: 5));
    }
  }
}
