using System;
using System.Collections.Generic;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Реестр «идентификатор:значение» — переменные психики для подстановки в параметры моторов.
  /// <para>
  /// Ментальные автоматизмы (будущее: цикл <c>ThinkingCyclesSystem</c> + правила реакций)
  /// записывают в реестр значения <c>id → string</c> (например <c>"ГабаритX" → "120"</c>),
  /// моторные handler'ы читают их через <see cref="RecipeStepExecutionContext.PsychicVariables"/>
  /// и подставляют в числовые/строковые параметры. Это даёт психике возможность менять
  /// параметры моторов без правки рецепта.
  /// </para>
  /// <para>
  /// Пока ментальные автоматизмы в ISIDA не пишут в реестр (нужен API), он остаётся пустым;
  /// неразрешённые <c>{V:...}</c> в шаблоне оставляются как есть и отсекаются политикой
  /// «пустое не писать» (<see cref="VelumRecipeTemplateResolver.IsMeaninglessResolvedValue"/>).
  /// </para>
  /// <para>
  /// Потокобезопасность: <see cref="Dictionary{TKey,TValue}"/> под <c>lock</c>; запись из
  /// произвольного потока (психика), чтение на UI-потоке SolidWorks внутри <see cref="RecipeExecutor"/>.
  /// </para>
  /// </summary>
  internal static class VelumRecipeMotorParameters
  {
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, string> Registry =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Снимок текущего состояния реестра (для передачи в контекст шага рецепта).
    /// </summary>
    /// <returns>Регистронезависимый словарь id → значение.</returns>
    public static IReadOnlyDictionary<string, string> Snapshot()
    {
      lock (Sync)
        return new Dictionary<string, string>(Registry, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Записать значение переменной психики (для ментальных автоматизмов / правил реакций).
    /// </summary>
    /// <param name="id">Идентификатор переменной (например <c>"ГабаритX"</c>).</param>
    /// <param name="value">Значение (число или строка в инвариантной культуре).</param>
    public static void Set(string id, string value)
    {
      if (string.IsNullOrWhiteSpace(id))
        return;

      lock (Sync)
        Registry[id.Trim()] = value ?? string.Empty;
    }

    /// <summary>
    /// Прочитать значение переменной психики (для отладки и диагностики).
    /// </summary>
    /// <param name="id">Идентификатор переменной.</param>
    /// <returns>Значение или <c>null</c>, если не найдено.</returns>
    public static string TryGet(string id)
    {
      if (string.IsNullOrWhiteSpace(id))
        return null;

      lock (Sync)
      {
        string value;
        return Registry.TryGetValue(id.Trim(), out value) ? value : null;
      }
    }

    /// <summary>Очистить реестр (при смене документа/эпизода, остановке пульсации).</summary>
    public static void Clear()
    {
      lock (Sync)
        Registry.Clear();
    }
  }
}
