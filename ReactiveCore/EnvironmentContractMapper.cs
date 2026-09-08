using System;
using System.Collections.Generic;
using ISIDA.SymbiontEnv.Contract;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Преобразование DTO <see cref="EnvironmentRecipeData"/> (SymbiontEnv.Contract) ↔ модели runtime Velum.
  /// </summary>
  internal static class EnvironmentContractMapper
  {
    public static RecipeDefinition ToRecipeDefinition(EnvironmentRecipeData data, string sourceFilePath)
    {
      if (data == null)
        return null;

      var steps = new List<RecipeStepDefinition>();
      if (data.Steps != null)
      {
        foreach (EnvironmentRecipeStepData step in data.Steps)
        {
          steps.Add(new RecipeStepDefinition(
              step?.Type ?? string.Empty,
              BuildStepParameters(step)));
        }
      }

      return new RecipeDefinition(
          data.Id,
          data.AdaptiveActionId,
          data.DisplayName,
          data.Description,
          data.ReactiveEligible,
          steps,
          sourceFilePath ?? string.Empty);
    }

    public static EnvironmentRecipeData ToRecipeData(RecipeDefinition recipe)
    {
      if (recipe == null)
        return null;

      var data = new EnvironmentRecipeData
      {
        Id = recipe.RecipeId,
        AdaptiveActionId = recipe.AdaptiveActionId,
        DisplayName = recipe.DisplayName,
        Description = recipe.Description,
        ReactiveEligible = recipe.ReactiveEligible
      };

      if (recipe.Steps != null)
      {
        foreach (RecipeStepDefinition step in recipe.Steps)
          data.Steps.Add(FromStepParameters(step?.Type, step?.Parameters));
      }

      return data;
    }

    private static IReadOnlyDictionary<string, string> BuildStepParameters(EnvironmentRecipeStepData step)
    {
      var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      if (step == null)
        return parameters;

      string type = (step.Type ?? string.Empty).Trim().ToLowerInvariant();
      if (string.Equals(type, "comment", StringComparison.Ordinal))
      {
        if (!string.IsNullOrWhiteSpace(step.Text))
          parameters["text"] = step.Text;
        return parameters;
      }

      if (!string.IsNullOrWhiteSpace(step.Handler))
        parameters["handler"] = step.Handler;

      if (step.Args != null)
      {
        foreach (KeyValuePair<string, string> kv in step.Args)
          parameters[kv.Key] = kv.Value;
      }

      return parameters;
    }

    private static EnvironmentRecipeStepData FromStepParameters(string type, IReadOnlyDictionary<string, string> parameters)
    {
      string normalizedType = (type ?? string.Empty).Trim().ToLowerInvariant();
      if (string.Equals(normalizedType, "comment", StringComparison.Ordinal))
      {
        string text = string.Empty;
        if (parameters != null && parameters.TryGetValue("text", out string textValue))
          text = textValue;
        return new EnvironmentRecipeStepData { Type = "comment", Text = text };
      }

      var step = new EnvironmentRecipeStepData { Type = "invoke" };
      if (parameters == null)
        return step;

      if (parameters.TryGetValue("handler", out string handler))
        step.Handler = handler;

      foreach (KeyValuePair<string, string> kv in parameters)
      {
        if (string.Equals(kv.Key, "handler", StringComparison.OrdinalIgnoreCase))
          continue;
        step.Args[kv.Key] = kv.Value;
      }

      return step;
    }
  }
}
