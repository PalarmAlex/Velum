namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Набор из пяти выражений фильтра формы обмена (родитель, компонент,
  /// операция, ExternalId, дата). Один и тот же тип обслуживает вкладки
  /// «Структура» и «Все»: поля совпадают, различается лишь набор записей.
  /// </summary>
  internal sealed class BomExchangeFilterValues
  {
    /// <summary>Выражение фильтра по обозначению/ExternalId родителя.</summary>
    public string Parent;

    /// <summary>Выражение фильтра по обозначению компонента.</summary>
    public string Child;

    /// <summary>Выражение фильтра по операции (add/update/delete).</summary>
    public string Action;

    /// <summary>Выражение фильтра по ExternalId ребёнка.</summary>
    public string ExternalId;

    /// <summary>Выражение фильтра по дате записи (ISO-вид местного времени).</summary>
    public string Date;

    /// <summary>Пустой набор (без условий) — все строки проходят.</summary>
    internal static BomExchangeFilterValues Empty()
    {
      return new BomExchangeFilterValues();
    }
  }
}
