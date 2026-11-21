using System.Collections.Generic;

namespace Velum.UI
{
  /// <summary>
  /// Чистые правила кнопок «Поощрить» / «Наказать» пульта агента: сопоставление кнопки с
  /// ID воздействия-триггера из настроек и сбор множества скрытых ID для фильтра пикера
  /// воздействий. Без зависимостей от WinForms и isida.dll — класс слинкован в тестовый
  /// проект (<c>tests\Velum.ReactiveCore.Tests.csproj</c>).
  /// </summary>
  internal static class VelumInfluenceButtonRules
  {
    /// <summary>
    /// True — кнопка настроена (указан положительный ID воздействия). 0 — не настроена.
    /// </summary>
    /// <param name="influenceId">ID воздействия-триггера из настройки (0 — не задан).</param>
    public static bool IsConfigured(int influenceId)
    {
      return influenceId > 0;
    }

    /// <summary>
    /// Список из одного воздействия для отправки стимула; пуст, если кнопка не настроена.
    /// </summary>
    /// <param name="influenceId">ID воздействия-триггера из настройки (0 — не задан).</param>
    public static List<int> ResolveTriggerIds(int influenceId)
    {
      return influenceId > 0 ? new List<int> { influenceId } : new List<int>();
    }

    /// <summary>
    /// Множество ID воздействий кнопок «Поощрить»/«Наказать» (для фильтра пикера: эти
    /// воздействия не предлагаем выбрать вручную, чтобы не дублировать кнопки).
    /// Пустые/отрицательные ID игнорируются.
    /// </summary>
    /// <param name="rewardInfluenceId">Воздействие кнопки «Поощрить».</param>
    /// <param name="punishInfluenceId">Воздействие кнопки «Наказать».</param>
    public static HashSet<int> BuildHiddenInfluenceIds(int rewardInfluenceId, int punishInfluenceId)
    {
      var hidden = new HashSet<int>();
      if (rewardInfluenceId > 0)
        hidden.Add(rewardInfluenceId);
      if (punishInfluenceId > 0)
        hidden.Add(punishInfluenceId);
      return hidden;
    }

    /// <summary>Подсказка кнопки «Поощрить», когда воздействие не настроено.</summary>
    public const string RewardNotConfiguredTooltip =
        "Кнопка «Поощрить»: укажите ID воздействия на вкладке «Документы» настроек проекта.";

    /// <summary>Подсказка кнопки «Наказать», когда воздействие не настроено.</summary>
    public const string PunishNotConfiguredTooltip =
        "Кнопка «Наказать»: укажите ID воздействия на вкладке «Документы» настроек проекта.";

    /// <summary>
    /// Подсказка настроенной кнопки с ID воздействия.
    /// </summary>
    /// <param name="buttonCaption">Подпись кнопки («Поощрить» / «Наказать»).</param>
    /// <param name="influenceId">ID воздействия-триггера.</param>
    public static string BuildConfiguredTooltip(string buttonCaption, int influenceId)
    {
      return "Кнопка «" + buttonCaption + "»: применить воздействие ID=" + influenceId + ".";
    }

    /// <summary>Сообщение, когда кнопка нажата, но воздействие не настроено.</summary>
    public const string NotConfiguredMessage =
        "Кнопка не настроена: укажите ID воздействия на вкладке «Документы» настроек проекта.";

    /// <summary>
    /// Сообщение, когда воздействие из настройки не найдено среди загруженных воздействий.
    /// </summary>
    /// <param name="influenceId">ID воздействия из настройки.</param>
    public static string BuildInfluenceNotFoundMessage(int influenceId)
    {
      return "Воздействие ID=" + influenceId + " не найдено среди загруженных воздействий оператора.";
    }
  }
}
