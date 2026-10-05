namespace Velum.UI
{
  /// <summary>
  /// Чистые правила кнопки «Запретить»: выбор цели штрафа по пути активации эпизода
  /// (сенсорный гейт CS₁→CS₂ или у-рефлекс целиком) и тексты подсказок/подтверждений.
  /// Без зависимостей от WinForms и isida.dll — класс слинкован в тестовый проект
  /// (<c>tests\Velum.ReactiveCore.Tests.csproj</c>).
  /// </summary>
  internal static class VelumReflexForbidTargetRules
  {
    /// <summary>
    /// True — эпизод активирован через сенсорный гейт (бедный CS₁ открыл дорогу богатому CS₂),
    /// и «Запретить» должен наказать связь CS₁→CS₂, а не у-рефлекс: нормальный exact-match
    /// триггер того же УР ни при чём и наказывать его нельзя.
    /// </summary>
    /// <param name="gateCs1">CS₁ эпизода (0 — активация шла не через гейт)</param>
    /// <param name="gateCs2">CS₂ эпизода (0 — активация шла не через гейт)</param>
    public static bool TargetsSensoryLink(int gateCs1, int gateCs2)
    {
      return gateCs1 > 0 && gateCs2 > 0 && gateCs1 != gateCs2;
    }

    /// <summary>Назначение кнопки «Запретить»: штраф сенсорной связи (гейт-активация).</summary>
    public const string ForbidLinkTooltip =
        "Понижение готовности сенсорной связи, из-за которой сработала эта форма " +
        "(У-рефлекс остаётся рабочим для своего точного стимула).";

    /// <summary>Назначение кнопки «Запретить»: сброс крепости У-рефлекса (обычная активация).</summary>
    public const string ForbidReflexTooltip =
        "Понижение крепости условного рефлекса, вызвавшего эту форму.";

    /// <summary>Префикс подсказки с текущей крепостью у-рефлекса.</summary>
    public const string StrengthTooltipPrefix = "Текущая крепость У-рефлекса: ";

    /// <summary>Префикс подсказки с текущей готовностью сенсорной связи.</summary>
    public const string LinkAssociabilityTooltipPrefix = "Текущая готовность связи: ";

    /// <summary>Значение параметра, когда оно недоступно.</summary>
    public const string StrengthUnavailableText = "неизвестна";

    /// <summary>
    /// Текст подтверждения «Запретить» для сенсорной связи (штраф по адресу CS₁→CS₂).
    /// </summary>
    /// <param name="gateCs1">CS₁ эпизода</param>
    /// <param name="gateCs2">CS₂ эпизода</param>
    public static string BuildLinkConfirmText(int gateCs1, int gateCs2)
    {
      return "Вы уверены, что хотите понизить готовность сенсорной связи " +
             gateCs1 + "→" + gateCs2 +
             "? У-рефлекс для своего точного стимула останется рабочим.";
    }

    /// <summary>
    /// Текст подтверждения «Запретить» для у-рефлекса (сброс крепости, обычный путь активации).
    /// </summary>
    /// <param name="reflexId">ID у-рефлекса</param>
    public static string BuildReflexConfirmText(int reflexId)
    {
      return "Вы уверены, что хотите понизить крепость условного рефлекса ID=" +
             reflexId + "?";
    }
  }
}
