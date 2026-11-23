using System;
using System.Collections.Generic;
using System.Globalization;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Handler <c>sketch_extrude_plate</c>: рисует прямоугольный эскиз на базовой плоскости и
  /// вытягивает его в пластину прямыми вызовами <c>SketchManager</c>/<c>FeatureManager</c>
  /// (не через <c>RunCommand</c>: у <c>ISldWorks.RunCommand</c> второй параметр — <c>NewTitle</c>,
  /// а не параметры команды; параметрической загрузки команд в PIA нет — см. DEBUG-разбор).
  /// <para>
  /// Параметры (значения — миллиметры, допускают шаблоны <c>{NAME}</c> и переменные психики
  /// <c>{V:имя}</c>): <c>width</c>, <c>height</c> — габарит прямоугольника; <c>depth</c> —
  /// глубина вытяжки; <c>plane</c> — базовая плоскость (<c>front</c>/<c>top</c>/<c>right</c>,
  /// по умолчанию <c>front</c>). Прямоугольник строится от начала координат плоскости.
  /// </para>
  /// </summary>
  internal static class RecipeExecutorHandlersGeometry
  {
    /// <summary>Идентификатор handler'а (совпадает с ключом в <c>handlers-catalog.json</c>).</summary>
    public const string ExtrudePlateHandlerId = "sketch_extrude_plate";

    // Числовые коды swconst (имена enum в разных PIA расходятся — константы с комментарием, см. AGENTS.md).
    private const int SwEndCondBlind = 0;   // swEndConditions_e.swEndCondBlind — вытяжка на глубину.
    private const string SelectTypePlane = "PLANE"; // Тип выбора SelectByID2 для базовой плоскости.
    private const string RefPlaneTypeName = "RefPlane"; // GetTypeName2() базовой плоскости (не локализуется).

    /// <summary>
    /// Исполняет шаг <c>sketch_extrude_plate</c> на UI-потоке SolidWorks.
    /// </summary>
    /// <param name="index">Индекс шага в рецепте.</param>
    /// <param name="args">Параметры шага (width/height/depth/plane).</param>
    /// <param name="modelDoc">Активный документ (должен быть частью).</param>
    /// <param name="templateContext">Контекст окружения для шаблонов.</param>
    /// <param name="psychicVariables">Переменные психики для <c>{V:...}</c>.</param>
    /// <param name="result">Результат шага.</param>
    /// <returns><c>true</c>, если шаг обработан без исключения (в т.ч. пропущен).</returns>
    public static bool TryExecuteExtrudePlate(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out RecipeStepExecutionResult result)
    {
      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (!(modelDoc is PartDoc))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_not_part");
        Logger.Info("Velum Recipe sketch_extrude_plate skipped (not a part)");
        return true;
      }

      if (!TryResolveDimension(args, "width", templateContext, psychicVariables, out double widthM, out string widthErr))
        return Fail(index, "width:" + widthErr, out result);
      if (!TryResolveDimension(args, "height", templateContext, psychicVariables, out double heightM, out string heightErr))
        return Fail(index, "height:" + heightErr, out result);
      if (!TryResolveDimension(args, "depth", templateContext, psychicVariables, out double depthM, out string depthErr))
        return Fail(index, "depth:" + depthErr, out result);

      string plane = (RecipeArgsParser.Get(args, "plane") ?? string.Empty).Trim().ToLowerInvariant();
      if (string.IsNullOrEmpty(plane))
        plane = "front";
      if (!TryResolvePlane(modelDoc, plane, out string planeName, out string planeErr))
        return Fail(index, "plane:" + planeErr, out result);

      try
      {
        // Выбор базовой плоскости и вход в эскиз.
        bool selected = modelDoc.Extension.SelectByID2(
            planeName, SelectTypePlane, 0, 0, 0, false, 0, null, 0);
        if (!selected)
          return Fail(index, "plane_select_failed:" + planeName, out result);

        SketchManager sketchMgr = modelDoc.SketchManager;
        if (sketchMgr == null)
          return Fail(index, "sketch_manager_null", out result);

        sketchMgr.InsertSketch(true);

        // Прямоугольник от начала координат плоскости (метры).
        object rect = sketchMgr.CreateCornerRectangle(0, 0, 0, widthM, heightM, 0);
        if (rect == null)
        {
          sketchMgr.InsertSketch(true);
          return Fail(index, "create_rectangle_failed", out result);
        }

        sketchMgr.InsertSketch(true); // Выход из эскиза.

        FeatureManager featMgr = modelDoc.FeatureManager;
        if (featMgr == null)
          return Fail(index, "feature_manager_null", out result);

        // Слепая вытяжка на глубину (FeatureExtrusion3, 23 параметра — сигнатура подтверждена
        // рефлексией PIA 32.x и 27.5, идентична в обеих).
        Feature plate = featMgr.FeatureExtrusion3(
            true,   // Sd — сквозное направление одно
            false,  // Flip
            false,  // Dir
            SwEndCondBlind, // T1 — blind
            0,      // T2
            depthM, // D1 — глубина (метры)
            0,      // D2
            false,  // Dchk1
            false,  // Dchk2
            false,  // Ddir1
            false,  // Ddir2
            0,      // Dang1
            0,      // Dang2
            false,  // OffsetReverse1
            false,  // OffsetReverse2
            false,  // TranslateSurface1
            false,  // TranslateSurface2
            true,   // Merge — объединять с телом
            false,  // UseFeatScope
            true,   // UseAutoSelect — использовать текущий эскиз
            0,      // T0
            0,      // StartOffset
            false); // FlipStartOffset

        if (plate == null)
          return Fail(index, "extrude_failed", out result);

        modelDoc.EditRebuild3();

        string summary =
            "width_mm=" + ToStr(widthM / VelumRecipeGeometryRules.MillimetersToMeters) +
            " height_mm=" + ToStr(heightM / VelumRecipeGeometryRules.MillimetersToMeters) +
            " depth_mm=" + ToStr(depthM / VelumRecipeGeometryRules.MillimetersToMeters) +
            " plane=" + planeName;
        result = new RecipeStepExecutionResult(index, "invoke", true, false, summary);
        Logger.Info("Velum Recipe sketch_extrude_plate OK " + summary);
        return true;
      }
      catch (Exception ex)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        Logger.Warning("Velum Recipe sketch_extrude_plate FAIL: " + ex.Message);
        return false;
      }
    }

    private static bool TryResolveDimension(
        IReadOnlyDictionary<string, string> args,
        string key,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out double meters,
        out string error)
    {
      string raw = RecipeArgsParser.Get(args, key);
      if (string.IsNullOrWhiteSpace(raw))
      {
        meters = 0;
        error = "missing_" + key;
        return false;
      }

      // Шаблон (окружение {NAME} + переменные психики {V:имя}) разрешает SolidWorks-резолвер;
      // разбор числа и мм→м вынесены в чистый VelumRecipeGeometryRules (тестируется изолированно).
      string resolved = VelumRecipeTemplateResolver.Resolve(raw, templateContext, psychicVariables);
      return VelumRecipeGeometryRules.TryResolvedToMeters(resolved, out meters, out error);
    }

    /// <summary>
    /// Резолвит логическое имя плоскости (<c>front</c>/<c>top</c>/<c>right</c>) в фактическое
    /// локализованное имя базовой плоскости перебором <c>RefPlane</c>-фич в порядке дерева SW
    /// (Front → Top → Right). Это снимает зависимость от языка SolidWorks.
    /// </summary>
    private static bool TryResolvePlane(ModelDoc2 modelDoc, string logicalName, out string planeName, out string error)
    {
      planeName = null;
      error = string.Empty;

      int wantedIndex;
      switch (logicalName)
      {
        case "front": wantedIndex = 0; break;
        case "top": wantedIndex = 1; break;
        case "right": wantedIndex = 2; break;
        default:
          error = "unknown_plane:" + logicalName;
          return false;
      }

      try
      {
        Feature feature = modelDoc.FirstFeature() as Feature;
        int refPlaneOrdinal = 0;
        while (feature != null)
        {
          string typeName = null;
          try { typeName = feature.GetTypeName2(); }
          catch { typeName = null; }

          if (string.Equals(typeName, RefPlaneTypeName, StringComparison.OrdinalIgnoreCase))
          {
            if (refPlaneOrdinal == wantedIndex)
            {
              planeName = feature.Name;
              if (string.IsNullOrWhiteSpace(planeName))
              {
                error = "plane_name_empty";
                return false;
              }

              return true;
            }

            refPlaneOrdinal++;
          }

          feature = feature.GetNextFeature() as Feature;
        }

        error = "plane_not_found:" + logicalName + "(found_ref_planes=" + refPlaneOrdinal + ")";
        return false;
      }
      catch (Exception ex)
      {
        error = "plane_enumerate_failed:" + ex.Message;
        return false;
      }
    }

    private static bool Fail(int index, string message, out RecipeStepExecutionResult result)
    {
      result = new RecipeStepExecutionResult(index, "invoke", false, false, message);
      Logger.Warning("Velum Recipe sketch_extrude_plate FAIL: " + message);
      return false;
    }

    private static string ToStr(double value)
    {
      return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
  }
}
