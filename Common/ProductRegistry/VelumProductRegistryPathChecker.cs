using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Velum.UI.ProductRegistry
{
  /// <summary>Результат интерактивной проверки путей реестра.</summary>
  internal enum VelumProductRegistryPathScanResult
  {
    Completed,
    StoppedEarly,
    AbortLoad
  }

  /// <summary>
  /// Проверка существования файлов по путям реестра с коротким таймаутом
  /// (для UNC / недоступных шар <see cref="File.Exists"/> может зависать).
  /// </summary>
  internal static class VelumProductRegistryPathChecker
  {
    /// <summary>Таймаут одной проверки пути, мс.</summary>
    internal const int TimeoutMilliseconds = 2000;

    /// <summary>
    /// Кэш «буква диска → сетевой ли том» (DriveInfo — один WMI/registry-запрос
    /// на букву; без кэша — по запросу на каждую запись реестра).
    /// </summary>
    private static readonly Dictionary<char, bool> _networkDriveCache =
        new Dictionary<char, bool>();

    /// <summary>
    /// true, если путь лежит на медленном сетевом хранилище: UNC-путь
    /// (два разделителя в начале) или буква диска, маппированная на сетевой шар.
    /// Для таких путей File.Exists может зависать (недоступный шар по VPN) —
    /// нужна ветка с таймаутом; локальные тома проверяются напрямую.
    /// </summary>
    internal static bool IsSlowNetworkPath(string path)
    {
      if (string.IsNullOrEmpty(path))
        return false;

      char sep = Path.DirectorySeparatorChar;
      if (path.Length >= 2 && path[0] == sep && path[1] == sep)
        return true;

      if (path.Length >= 2 && path[1] == ':' && IsAsciiLetter(path[0]))
      {
        char letter = char.ToUpperInvariant(path[0]);
        lock (_networkDriveCache)
        {
          bool isNetwork;
          if (_networkDriveCache.TryGetValue(letter, out isNetwork))
            return isNetwork;

          try
          {
            isNetwork = new DriveInfo(letter.ToString()).DriveType == DriveType.Network;
          }
          catch
          {
            isNetwork = false;
          }

          _networkDriveCache[letter] = isNetwork;
          return isNetwork;
        }
      }

      return false;
    }

    /// <summary>true для латинских букв A–Z / a–z.</summary>
    private static bool IsAsciiLetter(char c)
    {
      return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    internal static VelumProductRegistryPathStatus CheckOne(string filePath, int timeoutMs)
    {
      string path = VelumProductRegistryStore.NormalizeFilePathKey(filePath);
      if (string.IsNullOrEmpty(path))
        return VelumProductRegistryPathStatus.No;

      // Локальный том: File.Exists не зависает — прямой вызов без Task.Run/Wait
      // (раньше оверхед пула потоков платился на каждую запись реестра).
      // Сетевые пути (UNC / маппированный шар) — с таймаутом: недоступный шар
      // по VPN вешает File.Exists на десятки секунд.
      if (!IsSlowNetworkPath(path))
      {
        try
        {
          return File.Exists(path)
              ? VelumProductRegistryPathStatus.Ok
              : VelumProductRegistryPathStatus.No;
        }
        catch
        {
          return VelumProductRegistryPathStatus.No;
        }
      }

      try
      {
        Task<bool> task = Task.Run(() =>
        {
          try
          {
            return File.Exists(path);
          }
          catch
          {
            return false;
          }
        });

        if (!task.Wait(timeoutMs))
          return VelumProductRegistryPathStatus.Unknown;

        return task.Result
            ? VelumProductRegistryPathStatus.Ok
            : VelumProductRegistryPathStatus.No;
      }
      catch
      {
        return VelumProductRegistryPathStatus.No;
      }
    }

    /// <summary>
    /// Проверяет пути записей; при таймауте показывает диалог выбора.
    /// </summary>
    /// <param name="owner">Владелец диалога таймаута (может быть <c>null</c>).</param>
    /// <param name="items">Записи реестра для проверки.</param>
    /// <param name="statuses">Словарь статусов по Id записи (заполняется/обновляется).</param>
    /// <param name="allowAbortLoad">
    /// Если true — третья кнопка «Остановить загрузку реестра»;
    /// иначе — только пропуск / прекратить проверку.
    /// </param>
    /// <param name="checkedCount">Число записей, для которых получен явный OK/NO (без «?»).</param>
    /// <param name="okCount">Число записей со статусом OK.</param>
    /// <param name="noCount">Число записей со статусом NO.</param>
    /// <param name="unknownCount">Число записей со статусом «?».</param>
    /// <returns>Итог прохода: завершён, прерван рано или отмена загрузки формы.</returns>
    internal static VelumProductRegistryPathScanResult Run(
        IWin32Window owner,
        IReadOnlyList<VelumProductItem> items,
        IDictionary<int, VelumProductRegistryPathStatus> statuses,
        bool allowAbortLoad,
        out int checkedCount,
        out int okCount,
        out int noCount,
        out int unknownCount)
    {
      checkedCount = 0;
      okCount = 0;
      noCount = 0;
      unknownCount = 0;

      if (items == null || items.Count == 0)
        return VelumProductRegistryPathScanResult.Completed;

      if (statuses == null)
        throw new ArgumentNullException(nameof(statuses));

      for (int i = 0; i < items.Count; i++)
      {
        VelumProductItem item = items[i];
        if (item == null || item.Id <= 0)
          continue;

        VelumProductRegistryPathStatus status = CheckOne(item.FilePath, TimeoutMilliseconds);
        if (status == VelumProductRegistryPathStatus.Unknown)
        {
          TimeoutDialogChoice choice = ShowTimeoutDialog(
              owner,
              VelumProductRegistryStore.NormalizeFilePathKey(item.FilePath),
              allowAbortLoad);

          if (choice == TimeoutDialogChoice.AbortLoad)
          {
            statuses[item.Id] = VelumProductRegistryPathStatus.Unknown;
            MarkRemainingUnknown(items, i, statuses);
            Recount(statuses, out okCount, out noCount, out unknownCount);
            checkedCount = CountKnown(statuses);
            return VelumProductRegistryPathScanResult.AbortLoad;
          }

          statuses[item.Id] = VelumProductRegistryPathStatus.Unknown;
          unknownCount++;
          checkedCount++;

          if (choice == TimeoutDialogChoice.StopChecking)
          {
            MarkRemainingUnknown(items, i + 1, statuses);
            Recount(statuses, out okCount, out noCount, out unknownCount);
            return VelumProductRegistryPathScanResult.StoppedEarly;
          }

          continue;
        }

        statuses[item.Id] = status;
        checkedCount++;
        if (status == VelumProductRegistryPathStatus.Ok)
          okCount++;
        else
          noCount++;
      }

      Recount(statuses, out okCount, out noCount, out unknownCount);
      return VelumProductRegistryPathScanResult.Completed;
    }

    private static void MarkRemainingUnknown(
        IReadOnlyList<VelumProductItem> items,
        int startIndex,
        IDictionary<int, VelumProductRegistryPathStatus> statuses)
    {
      for (int i = startIndex; i < items.Count; i++)
      {
        VelumProductItem item = items[i];
        if (item == null || item.Id <= 0)
          continue;
        if (!statuses.ContainsKey(item.Id))
          statuses[item.Id] = VelumProductRegistryPathStatus.Unknown;
      }
    }

    private static int CountKnown(IDictionary<int, VelumProductRegistryPathStatus> statuses)
    {
      int n = 0;
      foreach (KeyValuePair<int, VelumProductRegistryPathStatus> pair in statuses)
      {
        if (pair.Value != VelumProductRegistryPathStatus.Unknown)
          n++;
      }

      return n;
    }

    private static void Recount(
        IDictionary<int, VelumProductRegistryPathStatus> statuses,
        out int okCount,
        out int noCount,
        out int unknownCount)
    {
      okCount = 0;
      noCount = 0;
      unknownCount = 0;
      foreach (KeyValuePair<int, VelumProductRegistryPathStatus> pair in statuses)
      {
        switch (pair.Value)
        {
          case VelumProductRegistryPathStatus.Ok:
            okCount++;
            break;
          case VelumProductRegistryPathStatus.No:
            noCount++;
            break;
          default:
            unknownCount++;
            break;
        }
      }
    }

    private enum TimeoutDialogChoice
    {
      SkipAndContinue,
      StopChecking,
      AbortLoad
    }

    private static TimeoutDialogChoice ShowTimeoutDialog(
        IWin32Window owner,
        string path,
        bool allowAbortLoad)
    {
      string displayPath = string.IsNullOrEmpty(path) ? "(путь пуст)" : path;
      using (var dialog = new Form())
      {
        dialog.Text = "Реестр документов";
        dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.MinimizeBox = false;
        dialog.MaximizeBox = false;
        dialog.ShowInTaskbar = false;
        dialog.ClientSize = new Size(520, allowAbortLoad ? 168 : 140);

        var label = new Label
        {
          AutoSize = false,
          Location = new Point(12, 12),
          Size = new Size(496, 52),
          Text = "Файл недоступен для проверки (таймаут):" + Environment.NewLine + displayPath
        };

        var btnSkip = new Button
        {
          Text = "Пропустить и продолжить",
          Size = new Size(160, 28),
          Location = new Point(12, 72),
          DialogResult = DialogResult.Retry
        };
        var btnStop = new Button
        {
          Text = allowAbortLoad ? "Прекратить проверку" : "Прекратить проверку",
          Size = new Size(160, 28),
          Location = new Point(180, 72),
          DialogResult = DialogResult.Ignore
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(btnSkip);
        dialog.Controls.Add(btnStop);

        if (allowAbortLoad)
        {
          var btnAbort = new Button
          {
            Text = "Остановить загрузку",
            Size = new Size(160, 28),
            Location = new Point(348, 72),
            DialogResult = DialogResult.Abort
          };
          dialog.Controls.Add(btnAbort);
          dialog.CancelButton = btnAbort;
        }
        else
        {
          dialog.CancelButton = btnStop;
          btnStop.Location = new Point(348, 72);
        }

        dialog.AcceptButton = btnSkip;

        DialogResult result = owner != null
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();

        if (result == DialogResult.Abort)
          return TimeoutDialogChoice.AbortLoad;
        if (result == DialogResult.Ignore)
          return TimeoutDialogChoice.StopChecking;
        return TimeoutDialogChoice.SkipAndContinue;
      }
    }
  }
}
