using System;
using System.Collections.Generic;

namespace Velum.SolidHomeostasis
{
  /// <summary>Статус строки пакетной диагностики DXF.</summary>
  internal enum VelumDxfBatchRowStatus
  {
    Ok,
    NeedExport,
    Outdated,
    /// <summary>Файл с другим кол-вом в имени; геометрия актуальна — достаточно переименовать.</summary>
    QuantityOutdated,
    /// <summary>Строка из состава сборки без диагностики штампов — можно сразу экспортировать.</summary>
    Unchecked,
    /// <summary>Первый canonical DXF выполняется вручную в одиночном диалоге.</summary>
    FirstExport,
    /// <summary>Указанный в «Путь dxf» каталог отсутствует.</summary>
    CatalogUnavailable,
    /// <summary>Канонический DXF обновлён, но его копия в delivery не создана.</summary>
    ExportedNotDelivered,
    /// <summary>Нет валидного «Вид проекции dxf» у конфигурации — пакет запрещён.</summary>
    MissingProjection,
    Junk,
    Error,
    EmptyDocument
  }

  /// <summary>Строка результата диагностики DXF.</summary>
  internal sealed class VelumDxfBatchDiagnosticRow
  {
    internal string PartPath { get; set; }

    internal string PartDisplayName { get; set; }

    internal string ConfigName { get; set; }

    internal VelumDxfBatchRowStatus Status { get; set; }

    internal string StatusText { get; set; }

    internal bool Selected { get; set; }

    internal string DxfPath { get; set; }

    /// <summary>Кол-во в составе; null означает, что оно неизвестно.</summary>
    internal int? Quantity { get; set; }

    /// <summary>Ожидаемое имя без расширения (для rename при QuantityOutdated).</summary>
    internal string ExpectedDxfBaseName { get; set; }
  }

  /// <summary>
  /// Сессия пакетной формы: активна после «Диагностика», сбрасывается при закрытии формы.
  /// </summary>
  internal static class VelumDxfBatchDiagnosticsSession
  {
    private static readonly object Sync = new object();
    private static bool _isActive;
    private static List<VelumDxfBatchDiagnosticRow> _rows = new List<VelumDxfBatchDiagnosticRow>();
    private static List<string> _orphanDxfFiles = new List<string>();

    internal static bool IsActive
    {
      get
      {
        lock (Sync)
          return _isActive;
      }
    }

    internal static int ProblemCount
    {
      get
      {
        lock (Sync)
          return CountProblems(_rows, _orphanDxfFiles);
      }
    }

    internal static IReadOnlyList<VelumDxfBatchDiagnosticRow> Rows
    {
      get
      {
        lock (Sync)
          return new List<VelumDxfBatchDiagnosticRow>(_rows);
      }
    }

    internal static IReadOnlyList<string> OrphanDxfFiles
    {
      get
      {
        lock (Sync)
          return new List<string>(_orphanDxfFiles);
      }
    }

    internal static void BeginDiagnostics(
        IReadOnlyList<VelumDxfBatchDiagnosticRow> rows,
        IReadOnlyList<string> orphanDxfFiles)
    {
      lock (Sync)
      {
        _rows = rows != null
            ? new List<VelumDxfBatchDiagnosticRow>(rows)
            : new List<VelumDxfBatchDiagnosticRow>();
        _orphanDxfFiles = orphanDxfFiles != null
            ? new List<string>(orphanDxfFiles)
            : new List<string>();
        _isActive = true;
      }
    }

    internal static void ReplaceRows(IReadOnlyList<VelumDxfBatchDiagnosticRow> rows)
    {
      ReplaceRows(rows, null);
    }

    internal static void ReplaceRows(
        IReadOnlyList<VelumDxfBatchDiagnosticRow> rows,
        IReadOnlyList<string> orphanDxfFiles)
    {
      lock (Sync)
      {
        _rows = rows != null
            ? new List<VelumDxfBatchDiagnosticRow>(rows)
            : new List<VelumDxfBatchDiagnosticRow>();
        if (orphanDxfFiles != null)
          _orphanDxfFiles = new List<string>(orphanDxfFiles);
      }
    }

    internal static void DisposeSession()
    {
      lock (Sync)
      {
        _isActive = false;
        _rows = new List<VelumDxfBatchDiagnosticRow>();
        _orphanDxfFiles = new List<string>();
      }
    }

    internal static bool HasProblems()
    {
      lock (Sync)
        return CountProblems(_rows, _orphanDxfFiles) > 0;
    }

    private static int CountProblems(
        IReadOnlyList<VelumDxfBatchDiagnosticRow> rows,
        IReadOnlyList<string> orphanDxfFiles)
    {
      int count = orphanDxfFiles != null ? orphanDxfFiles.Count : 0;
      if (rows == null || rows.Count == 0)
        return count;

      for (int i = 0; i < rows.Count; i++)
      {
        VelumDxfBatchRowStatus status = rows[i].Status;
        if (status == VelumDxfBatchRowStatus.NeedExport ||
            status == VelumDxfBatchRowStatus.Outdated ||
            status == VelumDxfBatchRowStatus.QuantityOutdated ||
            status == VelumDxfBatchRowStatus.FirstExport ||
            status == VelumDxfBatchRowStatus.CatalogUnavailable ||
            status == VelumDxfBatchRowStatus.ExportedNotDelivered ||
            status == VelumDxfBatchRowStatus.MissingProjection ||
            status == VelumDxfBatchRowStatus.Junk ||
            status == VelumDxfBatchRowStatus.Error ||
            status == VelumDxfBatchRowStatus.EmptyDocument)
          count++;
      }

      return count;
    }
  }
}
