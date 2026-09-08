using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Velum.Isida;

namespace Velum.UI
{
  /// <summary>
  /// Список новых первичников из буферного файла (Command или Verbal) с подтверждением переноса в справочник.
  /// </summary>
  internal sealed partial class VelumSensorPrimariesBufferForm : Form
  {
    private readonly VelumSensorPrimariesChannel _channel;
    private readonly bool _runtimeMode;

    /// <summary>Конструктор для конструктора форм Visual Studio.</summary>
    public VelumSensorPrimariesBufferForm()
    {
      _channel = VelumSensorPrimariesChannel.Command;
      _runtimeMode = false;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.SensorBuffer);
    }

    public VelumSensorPrimariesBufferForm(VelumSensorPrimariesChannel channel)
    {
      _channel = channel;
      _runtimeMode = true;
      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.SensorBuffer);
      ApplyChannelCaption();
      TryApplyWindowIcon();
      var tip = new ToolTip();
      tip.SetToolTip(_btnAdd, "Перенести первичники из буфера в справочник");
      tip.SetToolTip(_btnRemoveSelected, "Удалить выделенные записи из буфера");
      tip.SetToolTip(_btnClearAll, "Очистить весь буфер");
      tip.SetToolTip(_btnClose, "Закрыть");
      ReloadList();
    }

    private void ApplyChannelCaption()
    {
      Text = _channel == VelumSensorPrimariesChannel.Command
          ? "Новые первичники командного канала"
          : "Новые первичники вербального канала";
    }

    private void TryApplyWindowIcon()
    {
      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;
    }

    private void ReloadList()
    {
      if (!_runtimeMode || _listEntries == null)
        return;

      _listEntries.Items.Clear();
      IReadOnlyList<string> entries = VelumSensorPrimariesBuffer.ReadBufferEntries(_channel);
      foreach (string entry in entries)
        _listEntries.Items.Add(entry);
    }

    private void BtnAdd_Click(object sender, EventArgs e)
    {
      if (!_runtimeMode)
        return;

      if (_listEntries.Items.Count == 0)
      {
        MessageBox.Show(
            this,
            "Буфер пуст — нет новых первичников для добавления.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (!VelumSensorPrimariesBuffer.TryCommit(_channel, out int addedCount, out string error))
      {
        MessageBox.Show(
            this,
            error ?? "Не удалось добавить первичники.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      ReloadList();
      string message = addedCount > 0
          ? "Добавлено первичников: " + addedCount + "."
          : "Все записи буфера уже были в справочнике; буфер очищен.";
      MessageBox.Show(this, message, "Velum", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnClearAll_Click(object sender, EventArgs e)
    {
      if (!_runtimeMode)
        return;

      if (_listEntries.Items.Count == 0)
      {
        MessageBox.Show(
            this,
            "Буфер уже пуст.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (!VelumSensorPrimariesBuffer.TryClearBuffer(_channel, out string error))
      {
        MessageBox.Show(
            this,
            error ?? "Не удалось очистить буфер.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      ReloadList();
      MessageBox.Show(
          this,
          "Все новые сенсоры из буфера удалены.",
          "Velum",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    private void BtnRemoveSelected_Click(object sender, EventArgs e)
    {
      RemoveSelectedEntries();
    }

    private void ListEntries_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Delete)
      {
        RemoveSelectedEntries();
        e.Handled = true;
      }
    }

    private void RemoveSelectedEntries()
    {
      if (!_runtimeMode)
        return;

      if (_listEntries.SelectedItems.Count == 0)
      {
        MessageBox.Show(
            this,
            "Выделите записи для удаления.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      var selected = _listEntries.SelectedItems
          .Cast<object>()
          .Select(item => item?.ToString())
          .Where(s => !string.IsNullOrWhiteSpace(s))
          .ToList();

      if (!VelumSensorPrimariesBuffer.TryRemoveBufferEntries(
              _channel,
              selected,
              out int removedCount,
              out string error))
      {
        MessageBox.Show(
            this,
            error ?? "Не удалось удалить записи из буфера.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      ReloadList();
      MessageBox.Show(
          this,
          removedCount == 1
              ? "Выделенный сенсор удалён из буфера."
              : "Выделенные сенсоры удалены из буфера (" + removedCount + ").",
          "Velum",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information);
    }

    private static Icon TryLoadVelumWindowIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string[] candidates =
        {
          Path.Combine(dir, "velum.ico"),
          Path.Combine(dir, "icons", "velum.ico"),
        };
        foreach (string path in candidates)
        {
          if (File.Exists(path))
            return new Icon(path);
        }
      }
      catch
      {
      }

      return null;
    }
  }
}
