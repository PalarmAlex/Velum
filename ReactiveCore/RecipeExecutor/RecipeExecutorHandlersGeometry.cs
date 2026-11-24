using System;
using System.Collections.Generic;
using System.Globalization;
using ISIDA.Common;
using SolidWorks.Interop.sldworks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Handler <c>sketch_extrude_plate</c>: рисует прямоугольный эскиз на базовой плоскости прямым
  /// вызовом <c>SketchManager</c> (примитив эскиза, без операции выдавливания).
  /// <para>
  /// Параметры (значения — миллиметры, допускают шаблоны <c>{NAME}</c> и переменные психики
  /// <c>{V:имя}</c>): <c>width</c>, <c>height</c> — габарит прямоугольника; <c>plane</c> —
  /// базовая плоскость (<c>front</c>/<c>top</c>/<c>right</c>, по умолчанию <c>front</c>).
  /// Прямоугольник строится от начала координат плоскости.
  /// </para>
  /// </summary>
  internal static class RecipeExecutorHandlersGeometry
  {
    /// <summary>Идентификатор handler'а (совпадает с ключом в <c>handlers-catalog.json</c>).</summary>
    public const string ExtrudePlateHandlerId = "sketch_extrude_plate";

    /// <summary>Идентификатор handler'а рисования линии.</summary>
    public const string SketchLineHandlerId = "sketch_line";

    /// <summary>Идентификатор handler'а «прямоугольник из центра».</summary>
    public const string CenteredRectangleHandlerId = "sketch_centered_rectangle";

    /// <summary>Идентификатор handler'а «окружность».</summary>
    public const string CircleHandlerId = "sketch_circle";

    /// <summary>Идентификатор handler'а «правильный многоугольник».</summary>
    public const string PolygonHandlerId = "sketch_polygon";

    /// <summary>Идентификатор handler'а «многоугольник по вписанной окружности».</summary>
    public const string PolygonInscribedHandlerId = "sketch_polygon_inscribed";

    private const string SelectTypePlane = "PLANE"; // Тип выбора SelectByID2 для базовой плоскости.
    private const string RefPlaneTypeName = "RefPlane"; // GetTypeName2() базовой плоскости (не локализуется).

    /// <summary>
    /// Исполняет шаг <c>sketch_extrude_plate</c> на UI-потоке SolidWorks: создаёт эскиз прямоугольника.
    /// </summary>
    /// <param name="index">Индекс шага в рецепте.</param>
    /// <param name="args">Параметры шага (width/height/plane).</param>
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

        TryFullyDefineSketch(modelDoc); // Полностью определить эскиз (проставить размеры) до выхода.

        sketchMgr.InsertSketch(true); // Выход из эскиза.

        modelDoc.EditRebuild3();

        string summary =
            "width_mm=" + ToStr(widthM / VelumRecipeGeometryRules.MillimetersToMeters) +
            " height_mm=" + ToStr(heightM / VelumRecipeGeometryRules.MillimetersToMeters) +
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

    /// <summary>
    /// Исполняет шаг <c>sketch_line</c>: рисует отрезок в эскизе прямым вызовом
    /// <c>SketchManager.CreateLine</c>. Работает в двух режимах (универсальный графический токен):
    /// <list type="bullet">
    /// <item>если в активном документе <b>уже открыт эскиз</b> (<c>SketchManager.ActiveSketch != null</c>) —
    /// линия дописывается в текущий эскиз: выбор плоскости и вход/выход из эскиза НЕ выполняются,
    /// пользовательский режим эскиза сохраняется;</item>
    /// <item>если эскиза нет — выбирается базовая плоскость (<c>plane</c>), открывается новый эскиз,
    /// рисуется линия и эскиз закрывается.</item>
    /// </list>
    /// <para>
    /// Координаты <c>x1,y1,z1,x2,y2,z2</c> — в миллиметрах (допускают отрицательные и 0, шаблоны
    /// <c>{NAME}</c>/<c>{V:имя}</c>). Альтернативно можно задать <c>length</c> (+ необязательный
    /// <c>angle</c> в градусах от оси X): тогда конец вычисляется от начала. Если заданы явные
    /// <c>x2</c>/<c>y2</c> — они имеют приоритет над <c>length</c>.
    /// </para>
    /// </summary>
    public static bool TryExecuteLine(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (!(modelDoc is PartDoc))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_not_part");
        Logger.Info("Velum Recipe sketch_line skipped (not a part)");
        return true;
      }

      SketchManager sketchMgr = modelDoc.SketchManager;
      if (sketchMgr == null)
        return FailLine(index, "sketch_manager_null", out result);

      // Режим: уже в эскизе — дорисовываем; иначе — свой новый эскиз на плоскости.
      // ActiveSketch может не проставиться, если оператор вошёл в эскиз вручную; дополнительно
      // спрашиваем ModelDoc2.GetInSketch() (через рефлексию — сигнатура есть не во всех PIA),
      // иначе InsertSketch(true) для уже открытого эскиза выйдет из него, и CreateLine вернёт null.
      bool alreadyInSketch = DetectInSketch(modelDoc, sketchMgr);

      // Начало отрезка (по умолчанию 0,0,0).
      if (!TryResolveCoordinate(args, "x1", 0, templateContext, psychicVariables, out double x1M, out string x1Err))
        return FailLine(index, "x1:" + x1Err, out result);
      if (!TryResolveCoordinate(args, "y1", 0, templateContext, psychicVariables, out double y1M, out string y1Err))
        return FailLine(index, "y1:" + y1Err, out result);
      if (!TryResolveCoordinate(args, "z1", 0, templateContext, psychicVariables, out double z1M, out string z1Err))
        return FailLine(index, "z1:" + z1Err, out result);

      // Конец: явные x2/y2 имеют приоритет; иначе вычисляем из length (+angle).
      bool hasExplicitEnd =
          !string.IsNullOrWhiteSpace(RecipeArgsParser.Get(args, "x2")) ||
          !string.IsNullOrWhiteSpace(RecipeArgsParser.Get(args, "y2"));

      double x2M, y2M, z2M;
      if (hasExplicitEnd)
      {
        if (!TryResolveCoordinate(args, "x2", 0, templateContext, psychicVariables, out x2M, out string x2Err))
          return FailLine(index, "x2:" + x2Err, out result);
        if (!TryResolveCoordinate(args, "y2", 0, templateContext, psychicVariables, out y2M, out string y2Err))
          return FailLine(index, "y2:" + y2Err, out result);
        if (!TryResolveCoordinate(args, "z2", 0, templateContext, psychicVariables, out z2M, out string z2Err))
          return FailLine(index, "z2:" + z2Err, out result);
      }
      else
      {
        // length — в мм, angle — в градусах: парсим «как написано» (без мм→м, т.к. TryResolveCoordinate
        // уже переводит в метры, а двойная конвертация давала бы линию в 0.1 мм → CreateLine вернул null).
        if (!TryResolveScalarRaw(args, "length", 0, templateContext, psychicVariables, out double lengthMm, out string lenErr))
          return FailLine(index, "length:" + lenErr, out result);
        if (lengthMm == 0)
          return FailLine(index, "missing_end_or_length", out result);

        if (!TryResolveScalarRaw(args, "angle", 0, templateContext, psychicVariables, out double angleDeg, out string angErr))
          return FailLine(index, "angle:" + angErr, out result);

        double angleRad = angleDeg * Math.PI / 180.0;
        double lengthMeters = lengthMm * VelumRecipeGeometryRules.MillimetersToMeters;
        x2M = x1M + lengthMeters * Math.Cos(angleRad);
        y2M = y1M + lengthMeters * Math.Sin(angleRad);
        z2M = z1M;
      }

      string planeName = null;
      try
      {
        if (!alreadyInSketch)
        {
          string plane = (RecipeArgsParser.Get(args, "plane") ?? string.Empty).Trim().ToLowerInvariant();
          if (string.IsNullOrEmpty(plane))
            plane = "front";
          if (!TryResolvePlane(modelDoc, plane, out planeName, out string planeErr))
            return FailLine(index, "plane:" + planeErr, out result);

          bool selected = modelDoc.Extension.SelectByID2(
              planeName, SelectTypePlane, 0, 0, 0, false, 0, null, 0);
          if (!selected)
            return FailLine(index, "plane_select_failed:" + planeName, out result);

          sketchMgr.InsertSketch(true); // Вход в эскиз.
          // Снять выделение плоскости: CreateLine поверх активной selected-сущности возвращает null
          // (макрос-эталон тоже делает ClearSelection2 после InsertSketch перед CreateLine).
          modelDoc.ClearSelection2(true);
        }

        // CreateLine не рисует вырожденный отрезок (совпадающие точки) — вернёт null.
        if (IsDegenerateSegment(x1M, y1M, z1M, x2M, y2M, z2M))
        {
          if (!alreadyInSketch)
            sketchMgr.InsertSketch(true);
          return FailLine(index, "degenerate_segment", out result);
        }

        object line = sketchMgr.CreateLine(x1M, y1M, z1M, x2M, y2M, z2M);
        if (line == null)
        {
          string diag = BuildLineDiag(alreadyInSketch, planeName, x1M, y1M, z1M, x2M, y2M, z2M);
          if (!alreadyInSketch)
            sketchMgr.InsertSketch(true); // Закрыть созданный эскиз даже при неудаче.
          return FailLine(index, "create_line_failed " + diag, out result);
        }

        if (!alreadyInSketch)
        {
          // Полностью определить эскиз только когда создали свой; в чужой активный эскиз
          // (alreadyInSketch) не лезем — там пользователь рисует сам.
          TryFullyDefineSketch(modelDoc);
          sketchMgr.InsertSketch(true); // Выход из созданного эскиза.
        }

        string summary =
            "mode=" + (alreadyInSketch ? "append" : "new_sketch") +
            (planeName != null ? " plane=" + planeName : string.Empty) +
            " x1_mm=" + ToStr(x1M / VelumRecipeGeometryRules.MillimetersToMeters) +
            " y1_mm=" + ToStr(y1M / VelumRecipeGeometryRules.MillimetersToMeters) +
            " x2_mm=" + ToStr(x2M / VelumRecipeGeometryRules.MillimetersToMeters) +
            " y2_mm=" + ToStr(y2M / VelumRecipeGeometryRules.MillimetersToMeters);
        result = new RecipeStepExecutionResult(index, "invoke", true, false, summary);
        Logger.Info("Velum Recipe sketch_line OK " + summary);
        return true;
      }
      catch (Exception ex)
      {
        // Если входили сами и бросило внутри — пробуем закрыть эскиз, не оставлять «висящий» режим.
        if (!alreadyInSketch)
        {
          try { sketchMgr.InsertSketch(true); }
          catch { }
        }
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        Logger.Warning("Velum Recipe sketch_line FAIL: " + ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Исполняет шаг <c>sketch_centered_rectangle</c>: рисует прямоугольник с центром в
    /// (<c>cx</c>,<c>cy</c>) и габаритом <c>width</c>×<c>height</c> (мм, шаблоны <c>{NAME}</c>/
    /// <c>{V:имя}</c>). Прямоугольник строится через <c>CreateCenterRectangle</c>. Эскиз создаётся
    /// на плоскости <c>plane</c> (по умолчанию front), если ещё не открыт; в уже открытом эскизе
    /// оператора фигура дорисовывается без создания/закрытия эскиза. Перед закрытием созданного
    /// эскиза он полностью определяется (проставляются размеры). Только SLDPRT.
    /// </summary>
    public static bool TryExecuteCenteredRectangle(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out RecipeStepExecutionResult result)
    {
      if (!TryResolveDimension(args, "width", templateContext, psychicVariables, out double widthM, out string widthErr))
        return FailSketch(index, CenteredRectangleHandlerId, "width:" + widthErr, out result);
      if (!TryResolveDimension(args, "height", templateContext, psychicVariables, out double heightM, out string heightErr))
        return FailSketch(index, CenteredRectangleHandlerId, "height:" + heightErr, out result);
      if (!TryResolveCoordinate(args, "cx", 0, templateContext, psychicVariables, out double cxM, out string cxErr))
        return FailSketch(index, CenteredRectangleHandlerId, "cx:" + cxErr, out result);
      if (!TryResolveCoordinate(args, "cy", 0, templateContext, psychicVariables, out double cyM, out string cyErr))
        return FailSketch(index, CenteredRectangleHandlerId, "cy:" + cyErr, out result);

      double halfW = widthM / 2.0;
      double halfH = heightM / 2.0;

      return RunSketchPrimitive(index, CenteredRectangleHandlerId, args, modelDoc, templateContext, psychicVariables,
          (ISketchManager ism, out string summary) =>
          {
            // CreateCenterRectangle: центр (cx,cy,0) и точка угла (cx+½W, cy+½H, 0), метры.
            object rect = ism.CreateCenterRectangle(cxM, cyM, 0, cxM + halfW, cyM + halfH, 0);
            if (rect == null)
            {
              summary = null;
              return "create_centered_rectangle_failed";
            }

            summary = "width_mm=" + ToStr(widthM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " height_mm=" + ToStr(heightM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cx_mm=" + ToStr(cxM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cy_mm=" + ToStr(cyM / VelumRecipeGeometryRules.MillimetersToMeters);
            return null;
          },
          out result);
    }

    /// <summary>
    /// Исполняет шаг <c>sketch_circle</c>: рисует окружность с центром (<c>cx</c>,<c>cy</c>) и
    /// диаметром <c>diameter</c> (мм, шаблоны <c>{NAME}</c>/<c>{V:имя}</c>) через
    /// <c>CreateCircle</c> (центр + точка на окружности). Эскиз создаётся на плоскости
    /// <c>plane</c> (по умолчанию front), если ещё не открыт; в уже открытом эскизе оператора
    /// окружность дорисовывается без создания/закрытия эскиза. Перед закрытием созданного эскиза
    /// он полностью определяется. Только SLDPRT.
    /// </summary>
    public static bool TryExecuteCircle(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out RecipeStepExecutionResult result)
    {
      if (!TryResolveDimension(args, "diameter", templateContext, psychicVariables, out double diameterM, out string diaErr))
        return FailSketch(index, CircleHandlerId, "diameter:" + diaErr, out result);
      if (!TryResolveCoordinate(args, "cx", 0, templateContext, psychicVariables, out double cxM, out string cxErr))
        return FailSketch(index, CircleHandlerId, "cx:" + cxErr, out result);
      if (!TryResolveCoordinate(args, "cy", 0, templateContext, psychicVariables, out double cyM, out string cyErr))
        return FailSketch(index, CircleHandlerId, "cy:" + cyErr, out result);

      double radiusM = diameterM / 2.0;

      return RunSketchPrimitive(index, CircleHandlerId, args, modelDoc, templateContext, psychicVariables,
          (ISketchManager ism, out string summary) =>
          {
            // CreateCircle: центр (cx,cy,0) и точка на окружности (cx+R, cy, 0), метры.
            object circle = ism.CreateCircle(cxM, cyM, 0, cxM + radiusM, cyM, 0);
            if (circle == null)
            {
              summary = null;
              return "create_circle_failed";
            }

            summary = "diameter_mm=" + ToStr(diameterM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cx_mm=" + ToStr(cxM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cy_mm=" + ToStr(cyM / VelumRecipeGeometryRules.MillimetersToMeters);
            return null;
          },
          out result);
    }

    /// <summary>
    /// Исполняет шаг <c>sketch_polygon</c>: рисует правильный многоугольник с центром
    /// (<c>cx</c>,<c>cy</c>), числом сторон <c>sides</c> и <b>вписанной</b> окружностью диаметра
    /// <c>diameter</c> (мм, шаблоны <c>{NAME}</c>/<c>{V:имя}</c>) через <c>CreatePolygon</c>.
    /// Флаг <c>inscribed=false</c> (многоугольник описан вокруг окружности) — тогда опорная точка
    /// задаёт апофему = радиус вписанной окружности, поэтому заданный <c>diameter</c> совпадает с
    /// диаметром вписанной окружности. Эскиз создаётся на плоскости <c>plane</c> (по умолчанию
    /// front), если ещё не открыт; в уже открытом эскизе оператора фигура дорисовывается без
    /// создания/закрытия эскиза. Перед закрытием созданного эскиза он полностью определяется.
    /// Только SLDPRT.
    /// </summary>
    public static bool TryExecutePolygon(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out RecipeStepExecutionResult result)
    {
      if (!TryResolveInt(args, "sides", templateContext, psychicVariables, out int sides, out string sidesErr))
        return FailSketch(index, PolygonHandlerId, "sides:" + sidesErr, out result);
      if (sides < 3)
        return FailSketch(index, PolygonHandlerId, "sides_min_3:" + sides, out result);
      if (!TryResolveDimension(args, "diameter", templateContext, psychicVariables, out double diameterM, out string diaErr))
        return FailSketch(index, PolygonHandlerId, "diameter:" + diaErr, out result);
      if (!TryResolveCoordinate(args, "cx", 0, templateContext, psychicVariables, out double cxM, out string cxErr))
        return FailSketch(index, PolygonHandlerId, "cx:" + cxErr, out result);
      if (!TryResolveCoordinate(args, "cy", 0, templateContext, psychicVariables, out double cyM, out string cyErr))
        return FailSketch(index, PolygonHandlerId, "cy:" + cyErr, out result);

      // Вписанная окружность Ø=diameter: радиус (апофема) = diameter/2. Опорная точка задаёт
      // апофему (середину стороны) при inscribed=false — иначе (inscribed=true) точка трактуется
      // как вершина (радиус описанной окружности) и вписанная Ø выходит diameter·cos(π/sides)
      // (для гексагона 100 → 86.60).
      double apothemM = diameterM / 2.0;

      return RunSketchPrimitive(index, PolygonHandlerId, args, modelDoc, templateContext, psychicVariables,
          (ISketchManager ism, out string summary) =>
          {
            // CreatePolygon: центр, опорная точка на середине стороны (апофема), число сторон,
            // inscribed=false (полигон описан вокруг вписанной окружности заданного диаметра).
            object poly = ism.CreatePolygon(cxM, cyM, 0, cxM + apothemM, cyM, 0, sides, false);
            if (poly == null)
            {
              summary = null;
              return "create_polygon_failed";
            }

            summary = "sides=" + sides +
                " diameter_mm=" + ToStr(diameterM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cx_mm=" + ToStr(cxM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cy_mm=" + ToStr(cyM / VelumRecipeGeometryRules.MillimetersToMeters);
            return null;
          },
          out result);
    }

    /// <summary>
    /// Исполняет шаг <c>sketch_polygon_inscribed</c>: рисует правильный многоугольник с центром
    /// (<c>cx</c>,<c>cy</c>), числом сторон <c>sides</c> и <b>вписанной</b> окружностью диаметра
    /// <c>diameter</c> (стороны касаются окружности Ø=diameter). В отличие от <c>sketch_polygon</c>
    /// (где <c>diameter</c> — описанная окружность через вершины), здесь опорная точка отодвигается
    /// на радиус описанной окружности <c>(diameter/2)/cos(π/sides)</c>, чтобы апофема (расстояние
    /// центр→середина стороны) стала ровно <c>diameter/2</c>. Чистая математика — в
    /// <see cref="VelumRecipeGeometryRules.TryInscribedDiameterToCircumRadius"/>. Эскиз создаётся на
    /// плоскости <c>plane</c> (по умолчанию front), если ещё не открыт; в уже открытом эскизе
    /// фигура дорисовывается без создания/закрытия. Перед закрытием — полное определение размеров.
    /// Только SLDPRT.
    /// </summary>
    public static bool TryExecutePolygonInscribed(
        int index,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out RecipeStepExecutionResult result)
    {
      if (!TryResolveInt(args, "sides", templateContext, psychicVariables, out int sides, out string sidesErr))
        return FailSketch(index, PolygonInscribedHandlerId, "sides:" + sidesErr, out result);
      if (sides < 3)
        return FailSketch(index, PolygonInscribedHandlerId, "sides_min_3:" + sides, out result);
      if (!TryResolveDimension(args, "diameter", templateContext, psychicVariables, out double diameterM, out string diaErr))
        return FailSketch(index, PolygonInscribedHandlerId, "diameter:" + diaErr, out result);
      if (!TryResolveCoordinate(args, "cx", 0, templateContext, psychicVariables, out double cxM, out string cxErr))
        return FailSketch(index, PolygonInscribedHandlerId, "cx:" + cxErr, out result);
      if (!TryResolveCoordinate(args, "cy", 0, templateContext, psychicVariables, out double cyM, out string cyErr))
        return FailSketch(index, PolygonInscribedHandlerId, "cy:" + cyErr, out result);

      double diameterMm = diameterM / VelumRecipeGeometryRules.MillimetersToMeters;
      if (!VelumRecipeGeometryRules.TryInscribedDiameterToCircumRadius(diameterMm, sides, out double circumRadiusMm, out string geoErr))
        return FailSketch(index, PolygonInscribedHandlerId, "geometry:" + geoErr, out result);

      // Опорная точка на расстоянии радиуса описанной окружности (вершина) — тогда вписанная Ø=diameter.
      double circumRadiusM = circumRadiusMm * VelumRecipeGeometryRules.MillimetersToMeters;

      return RunSketchPrimitive(index, PolygonInscribedHandlerId, args, modelDoc, templateContext, psychicVariables,
          (ISketchManager ism, out string summary) =>
          {
            object poly = ism.CreatePolygon(cxM, cyM, 0, cxM + circumRadiusM, cyM, 0, sides, true);
            if (poly == null)
            {
              summary = null;
              return "create_polygon_inscribed_failed";
            }

            summary = "sides=" + sides +
                " inscribed_diameter_mm=" + ToStr(diameterMm) +
                " circum_radius_mm=" + ToStr(circumRadiusMm) +
                " cx_mm=" + ToStr(cxM / VelumRecipeGeometryRules.MillimetersToMeters) +
                " cy_mm=" + ToStr(cyM / VelumRecipeGeometryRules.MillimetersToMeters);
            return null;
          },
          out result);
    }

    /// <summary>
    /// Общий каркас примитива эскиза: проверяет часть, определяет режим (дорисовать в открытом
    /// эскизе оператора или создать новый на плоскости <c>plane</c>), вызывает
    /// <paramref name="draw"/>, а для созданного эскиза — полностью определяет его и закрывает.
    /// В открытом чужом эскизе определение/закрытие НЕ выполняются (пользователь рисует сам).
    /// </summary>
    /// <param name="index">Индекс шага в рецепте.</param>
    /// <param name="handlerLabel">Id handler'а (для логов).</param>
    /// <param name="args">Параметры шага.</param>
    /// <param name="modelDoc">Активный документ.</param>
    /// <param name="templateContext">Контекст окружения для шаблонов.</param>
    /// <param name="psychicVariables">Переменные психики для <c>{V:...}</c>.</param>
    /// <param name="draw">Рисование примитива; возвращает текст ошибки или <c>null</c> при успехе,
    /// в <c>summary</c> — строку параметров для лога.</param>
    /// <param name="result">Результат шага.</param>
    /// <returns><c>true</c>, если шаг обработан без исключения (в т.ч. пропущен).</returns>
    private static bool RunSketchPrimitive(
        int index,
        string handlerLabel,
        IReadOnlyDictionary<string, string> args,
        ModelDoc2 modelDoc,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        SketchDrawAction draw,
        out RecipeStepExecutionResult result)
    {
      result = null;

      if (modelDoc == null)
      {
        result = new RecipeStepExecutionResult(index, "invoke", false, false, "model_doc_null");
        return false;
      }

      if (!(modelDoc is PartDoc))
      {
        result = new RecipeStepExecutionResult(index, "invoke", true, true, "skipped_not_part");
        Logger.Info("Velum Recipe " + handlerLabel + " skipped (not a part)");
        return true;
      }

      SketchManager sketchMgr = modelDoc.SketchManager;
      if (sketchMgr == null)
        return FailSketch(index, handlerLabel, "sketch_manager_null", out result);

      ISketchManager ism = sketchMgr as ISketchManager;
      if (ism == null)
        return FailSketch(index, handlerLabel, "sketch_manager_not_isketchmanager", out result);

      bool alreadyInSketch = DetectInSketch(modelDoc, sketchMgr);

      string planeName = null;
      if (!alreadyInSketch)
      {
        string plane = (RecipeArgsParser.Get(args, "plane") ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(plane))
          plane = "front";
        if (!TryResolvePlane(modelDoc, plane, out planeName, out string planeErr))
          return FailSketch(index, handlerLabel, "plane:" + planeErr, out result);

        bool selected = modelDoc.Extension.SelectByID2(
            planeName, SelectTypePlane, 0, 0, 0, false, 0, null, 0);
        if (!selected)
          return FailSketch(index, handlerLabel, "plane_select_failed:" + planeName, out result);

        sketchMgr.InsertSketch(true); // Вход в эскиз.
        modelDoc.ClearSelection2(true); // Снять выделение — иначе Create* вернёт null (как в sketch_line).
      }

      try
      {
        string drawErr = draw(ism, out string drawSummary);
        if (drawErr != null)
        {
          if (!alreadyInSketch)
            sketchMgr.InsertSketch(true); // Закрыть созданный эскиз при неудаче.
          return FailSketch(index, handlerLabel, drawErr, out result);
        }

        if (!alreadyInSketch)
        {
          TryFullyDefineSketch(modelDoc); // Определяем только свой эскиз.
          sketchMgr.InsertSketch(true);   // Выход из созданного эскиза.
          modelDoc.EditRebuild3();
        }

        string summary = "mode=" + (alreadyInSketch ? "append" : "new_sketch") +
            (planeName != null ? " plane=" + planeName : string.Empty) +
            (string.IsNullOrEmpty(drawSummary) ? string.Empty : " " + drawSummary);
        result = new RecipeStepExecutionResult(index, "invoke", true, false, summary);
        Logger.Info("Velum Recipe " + handlerLabel + " OK " + summary);
        return true;
      }
      catch (Exception ex)
      {
        if (!alreadyInSketch)
        {
          try { sketchMgr.InsertSketch(true); }
          catch { }
        }
        result = new RecipeStepExecutionResult(index, "invoke", false, false, ex.Message);
        Logger.Warning("Velum Recipe " + handlerLabel + " FAIL: " + ex.Message);
        return false;
      }
    }

    /// <summary>Делегат рисования примитива в открытом эскизе (возвращает ошибку или <c>null</c>).</summary>
    /// <param name="ism">Интерфейс <c>ISketchManager</c> активного эскиза.</param>
    /// <param name="summary">Строка параметров для лога (при успехе).</param>
    /// <returns>Текст ошибки или <c>null</c>, если нарисовано.</returns>
    private delegate string SketchDrawAction(ISketchManager ism, out string summary);

    /// <summary>
    /// Резолвит целочисленный параметр (например число сторон), допуская шаблоны <c>{NAME}</c>/
    /// <c>{V:имя}</c> и десятичный ввод с запятой. Пустой параметр → <c>0</c> (вызывающий валидирует).
    /// </summary>
    private static bool TryResolveInt(
        IReadOnlyDictionary<string, string> args,
        string key,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out int value,
        out string error)
    {
      value = 0;
      error = string.Empty;

      string raw = RecipeArgsParser.Get(args, key);
      if (string.IsNullOrWhiteSpace(raw))
      {
        error = "missing_" + key;
        return false;
      }

      string resolved = VelumRecipeTemplateResolver.Resolve(raw, templateContext, psychicVariables);
      if (VelumRecipeGeometryRules.ContainsUnresolvedToken(resolved))
      {
        error = "unresolved_token:" + resolved;
        return false;
      }

      string norm = resolved.Trim().Replace(',', '.');
      if (!double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
      {
        error = "not_a_number:" + resolved;
        return false;
      }

      value = (int)Math.Round(d);
      return true;
    }

    /// <summary>Фиксирует падение шага примитива эскиза с указанием handler'а в логе.</summary>
    private static bool FailSketch(int index, string handlerLabel, string message, out RecipeStepExecutionResult result)
    {
      result = new RecipeStepExecutionResult(index, "invoke", false, false, message);
      Logger.Warning("Velum Recipe " + handlerLabel + " FAIL: " + message);
      return false;
    }

    /// <summary>
    /// Резолвит координату/размер в метры, допуская отрицательные значения и 0 (в отличие от
    /// <see cref="TryResolveDimension"/>, требующего положительного числа). Значение по умолчанию
    /// используется, если параметр отсутствует.
    /// </summary>
    private static bool TryResolveCoordinate(
        IReadOnlyDictionary<string, string> args,
        string key,
        double defaultMillimeters,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out double meters,
        out string error)
    {
      meters = 0;
      error = string.Empty;

      string raw = RecipeArgsParser.Get(args, key);
      if (string.IsNullOrWhiteSpace(raw))
      {
        meters = defaultMillimeters * VelumRecipeGeometryRules.MillimetersToMeters;
        return true;
      }

      string resolved = VelumRecipeTemplateResolver.Resolve(raw, templateContext, psychicVariables);
      if (VelumRecipeGeometryRules.ContainsUnresolvedToken(resolved))
      {
        error = "unresolved_token:" + resolved;
        return false;
      }

      if (!VelumRecipeGeometryRules.TryParseMeters(resolved, out double millimeters, out error))
        return false;

      meters = millimeters * VelumRecipeGeometryRules.MillimetersToMeters;
      return true;
    }

    private static bool FailLine(int index, string message, out RecipeStepExecutionResult result)
    {
      result = new RecipeStepExecutionResult(index, "invoke", false, false, message);
      Logger.Warning("Velum Recipe sketch_line FAIL: " + message);
      return false;
    }

    /// <summary>
    /// Резолвит скаляр «как написано» (без перевода мм→м) — для <c>length</c> (мм) и <c>angle</c> (град),
    /// которые далее интерпретируются сами вызывающий код. Пустой параметр → значение по умолчанию.
    /// </summary>
    private static bool TryResolveScalarRaw(
        IReadOnlyDictionary<string, string> args,
        string key,
        double defaultValue,
        IReadOnlyDictionary<string, string> templateContext,
        IReadOnlyDictionary<string, string> psychicVariables,
        out double value,
        out string error)
    {
      value = defaultValue;
      error = string.Empty;

      string raw = RecipeArgsParser.Get(args, key);
      if (string.IsNullOrWhiteSpace(raw))
        return true;

      string resolved = VelumRecipeTemplateResolver.Resolve(raw, templateContext, psychicVariables);
      if (VelumRecipeGeometryRules.ContainsUnresolvedToken(resolved))
      {
        error = "unresolved_token:" + resolved;
        return false;
      }

      return VelumRecipeGeometryRules.TryParseMeters(resolved, out value, out error);
    }

    /// <summary>
    /// Определяет, находится ли активный документ в режиме редактирования эскиза. Проверяет
    /// <c>SketchManager.ActiveSketch</c> и (если метод доступен в данной версии PIA)
    /// <c>ModelDoc2.GetInSketch()</c> — последний надёжнее反映 при ручном входе оператора в эскиз.
    /// </summary>
    private static bool DetectInSketch(ModelDoc2 modelDoc, SketchManager sketchMgr)
    {
      try
      {
        if (sketchMgr != null && sketchMgr.ActiveSketch != null)
          return true;
      }
      catch { }

      try
      {
        // У ModelDoc2 есть собственный COM-член GetType() (тип документа, int) — он затеняет
        // Object.GetType(), поэтому приводим к object перед рефлексией.
        System.Reflection.MethodInfo mi = ((object)modelDoc).GetType().GetMethod("GetInSketch", Type.EmptyTypes);
        if (mi != null)
        {
          object val = mi.Invoke(modelDoc, null);
          if (val is bool b && b)
            return true;
        }
      }
      catch { }

      return false;
    }

    /// <summary>
    /// Вызывает <c>SketchManager.FullyDefineSketch</c> — «Полностью определить эскиз» (проставляет
    /// размеры/связи, чтобы эскиз стал полностью определённым). Вызывается ДО выхода из эскиза.
    /// <para>
    /// Метод объявлен в PIA-интерфейсе <c>ISketchManager</c> (10 параметров), но отсутствует в
    /// <c>SketchManager</c>, который возвращает <c>ModelDoc2.SketchManager</c>, — поэтому COM-объект
    /// приводится к <c>ISketchManager</c> и вызывается напрямую (null для Object-параметров маршалер
    /// передаёт как Nothing, как в VBA-макросе). Best-effort: сбой определения эскиза не валит шаг —
    /// эскиз нарисован, просто остаётся не полностью определённым.
    /// </para>
    /// <para>
    /// Аргументы соответствуют макросу-эталону:
    /// <c>(true, true, 1023, true, 1, null, 1, null, -1, -1)</c> — EntitiesToFullyDefine, UseRelations,
    /// RelationsToApply (битовая маска swSketchRelationTypes, 1023 = все), UseDimensions,
    /// HorizontalDimScheme, HorizontalDatumDisp (null = авто), VerticalDimScheme, VerticalDatumDisp,
    /// HorizontalDimPlacement (-1 = авто), VerticalDimPlacement (-1 = авто).
    /// </para>
    /// </summary>
    /// <param name="modelDoc">Активный документ (часть, в открытом эскизе).</param>
    private static void TryFullyDefineSketch(ModelDoc2 modelDoc)
    {
      try
      {
        // Метод FullyDefineSketch объявлен в PIA-интерфейсе ISketchManager (10 параметров), но
        // НЕ в SketchManager, который возвращает ModelDoc2.SketchManager. Живой COM-объект
        // реализует ISketchManager — приводим и вызываем напрямую: маршалер передаёт null для
        // Object-параметров как VT_DISPATCH/Nothing (как в VBA-макросе). Через InvokeMember
        // тот же null шёл бы как VT_ERROR (Type.Missing) и SolidWorks отвечал DISP_E_TYPEMISMATCH.
        ISketchManager ism = modelDoc.SketchManager as ISketchManager;
        if (ism == null)
        {
          Logger.Warning("Velum Recipe FullyDefineSketch SKIP: SketchManager не ISketchManager");
          return;
        }

        ism.FullyDefineSketch(
            true,  // EntitiesToFullyDefine — определять ВСЕ сущности эскиза (не только выделенные)
            true,  // UseRelations — проставить геометрические связи
            1023,  // RelationsToApply — маска swSketchRelation (все типы связей)
            true,  // UseDimensions — проставить размеры
            1,     // HorizontalDimScheme — схема горизонтальных размеров
            null,  // HorizontalDatumDisp — базовая линия: null (Nothing) = авто
            1,     // VerticalDimScheme — схема вертикальных размеров
            null,  // VerticalDatumDisp — базовая линия: null (Nothing) = авто
            -1,    // HorizontalDimPlacement — размещение (авто)
            -1);   // VerticalDimPlacement — размещение (авто)
      }
      catch (Exception ex)
      {
        // Не критично: эскиз нарисован, просто может остаться не полностью определённым.
        Logger.Warning("Velum Recipe FullyDefineSketch FAIL (ignored): " + DescribeComFailure(ex));
      }
    }

    /// <summary>
    /// Разворачивает цепочку исключений от <c>InvokeMember</c> в читаемую строку: для
    /// <see cref="System.Reflection.TargetInvocationException"/> берёт <c>InnerException</c>, а для
    /// <see cref="System.Runtime.InteropServices.COMException"/> дополнительно печатает HResult
    /// (обычно <c>0x800...</c>) — без него COM-ошибку SolidWorks невозможно диагностировать.
    /// </summary>
    /// <param name="ex">Перехваченное исключение (обёртка или сама COM-ошибка).</param>
    /// <returns>Строка с типом, HResult и текстом глубочайшего исключения.</returns>
    private static string DescribeComFailure(Exception ex)
    {
      Exception root = ex;
      while (root.InnerException != null)
        root = root.InnerException;

      string hr = (root is System.Runtime.InteropServices.COMException com)
          ? " HRESULT=0x" + com.HResult.ToString("X8")
          : string.Empty;
      return root.GetType().Name + hr + ": " + root.Message;
    }

    /// <summary>Истинно, если концы отрезка совпадают (CreateLine для такого вернёт null).</summary>
    private static bool IsDegenerateSegment(
        double x1, double y1, double z1, double x2, double y2, double z2)
    {
      const double eps = 1e-9; // метры (~ нанометры)
      double dx = x2 - x1, dy = y2 - y1, dz = z2 - z1;
      return (dx * dx + dy * dy + dz * dz) < eps;
    }

    /// <summary>Строка диагностики для лога падения: режим и координаты в мм.</summary>
    private static string BuildLineDiag(
        bool alreadyInSketch, string planeName,
        double x1M, double y1M, double z1M, double x2M, double y2M, double z2M)
    {
      const double mm = VelumRecipeGeometryRules.MillimetersToMeters;
      return "mode=" + (alreadyInSketch ? "append" : "new_sketch") +
          (string.IsNullOrEmpty(planeName) ? string.Empty : " plane=" + planeName) +
          " x1_mm=" + ToStr(x1M / mm) + " y1_mm=" + ToStr(y1M / mm) + " z1_mm=" + ToStr(z1M / mm) +
          " x2_mm=" + ToStr(x2M / mm) + " y2_mm=" + ToStr(y2M / mm) + " z2_mm=" + ToStr(z2M / mm);
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
