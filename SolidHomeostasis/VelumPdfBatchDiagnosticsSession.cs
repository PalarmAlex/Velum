using System;
using System.Collections.Generic;

namespace Velum.SolidHomeostasis
{
  /// <summary>Статус строки пакетной диагностики PDF.</summary>
  internal enum VelumPdfBatchRowStatus
  {
    Ok,
    NeedExport,
    Outdated,
    /// <summary>Канонический PDF обновлён, но его копия в delivery не создана.</summary>
    ExportedNotDelivered,
    Junk,
    Error,
    /// <summary>Чертёж добавлен из каталога без диагностики SW — можно сразу экспортировать.</summary>
    Unchecked
  }

  /// <summary>Строка результата диагностики PDF.</summary>
  internal sealed class VelumPdfBatchDiagnosticRow
  {
    internal string DrawingPath { get; set; }

    internal string DrawingDisplayName { get; set; }

    /// <summary>Путь детали/сборки-источника чертежа (если известен из состава).</summary>
    internal string SourceModelPath { get; set; }

    internal VelumPdfBatchRowStatus Status { get; set; }

    internal string StatusText { get; set; }

    internal bool Selected { get; set; }

    internal string PdfPath { get; set; }
  }

  /// <summary>
  /// Сессия пакетной формы PDF: активна после «Диагностика», сбрасывается при закрытии формы.
  /// </summary>
  internal static class VelumPdfBatchDiagnosticsSession
  {
    private static readonly object Sync = new object();
    private static bool _isActive;
    private static List<VelumPdfBatchDiagnosticRow> _rows = new List<VelumPdfBatchDiagnosticRow>();
    private static List<string> _orphanPdfFiles = new List<string>();

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
          return CountProblems(_rows, _orphanPdfFiles);
      }
    }

    internal static IReadOnlyList<VelumPdfBatchDiagnosticRow> Rows
    {
      get
      {
        lock (Sync)
          return new List<VelumPdfBatchDiagnosticRow>(_rows);
      }
    }

    internal static IReadOnlyList<string> OrphanPdfFiles
    {
      get
      {
        lock (Sync)
          return new List<string>(_orphanPdfFiles);
      }
    }

    internal static void BeginDiagnostics(
        IReadOnlyList<VelumPdfBatchDiagnosticRow> rows,
        IReadOnlyList<string> orphanPdfFiles)
    {
      lock (Sync)
      {
        _rows = rows != null
            ? new List<VelumPdfBatchDiagnosticRow>(rows)
            : new List<VelumPdfBatchDiagnosticRow>();
        _orphanPdfFiles = orphanPdfFiles != null
            ? new List<string>(orphanPdfFiles)
            : new List<string>();
        _isActive = true;
      }
    }

    internal static void ReplaceRows(IReadOnlyList<VelumPdfBatchDiagnosticRow> rows)
    {
      ReplaceRows(rows, null);
    }

    internal static void ReplaceRows(
        IReadOnlyList<VelumPdfBatchDiagnosticRow> rows,
        IReadOnlyList<string> orphanPdfFiles)
    {
      lock (Sync)
      {
        _rows = rows != null
            ? new List<VelumPdfBatchDiagnosticRow>(rows)
            : new List<VelumPdfBatchDiagnosticRow>();
        if (orphanPdfFiles != null)
          _orphanPdfFiles = new List<string>(orphanPdfFiles);
      }
    }

    internal static void DisposeSession()
    {
      lock (Sync)
      {
        _isActive = false;
        _rows = new List<VelumPdfBatchDiagnosticRow>();
        _orphanPdfFiles = new List<string>();
      }
    }

    internal static bool HasProblems()
    {
      lock (Sync)
        return CountProblems(_rows, _orphanPdfFiles) > 0;
    }

    private static int CountProblems(
        IReadOnlyList<VelumPdfBatchDiagnosticRow> rows,
        IReadOnlyList<string> orphanPdfFiles)
    {
      int count = orphanPdfFiles != null ? orphanPdfFiles.Count : 0;
      if (rows == null || rows.Count == 0)
        return count;

      for (int i = 0; i < rows.Count; i++)
      {
        VelumPdfBatchRowStatus status = rows[i].Status;
        if (status == VelumPdfBatchRowStatus.NeedExport ||
            status == VelumPdfBatchRowStatus.Outdated ||
            status == VelumPdfBatchRowStatus.ExportedNotDelivered ||
            status == VelumPdfBatchRowStatus.Junk ||
            status == VelumPdfBatchRowStatus.Error)
          count++;
      }

      return count;
    }
  }
}
