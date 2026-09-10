using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ISIDA.Common;
using Velum.Configuration;
using Velum.ReactiveCore;
using Velum.UI;

namespace Velum.UI.AssemblyRegistry
{
  /// <summary>
  /// Форма диалога запуска формирования CSV-файла обмена с 1C.
  /// Содержит поле каталога обмена и кнопку запуска рефлекса.
  /// </summary>
  internal sealed partial class VelumBomExchangeForm : Form
  {
    public VelumBomExchangeForm()
    {
      InitializeComponent();
      VelumFormIcon.Apply(this);
      VelumAppConfig.EnsureInitialized();
      _folderBox.Text = VelumAppConfig.BomExchangeFolder;

      // Включить кнопку экспорта только если указан каталог обмена.
      UpdateExportButtonState();
      _folderBox.TextChanged += (s, e) => UpdateExportButtonState();

      // Загрузить список строк для экспорта.
      LoadDiscrepancyList();
    }

    private void UpdateExportButtonState()
    {
      string folder = (_folderBox.Text ?? string.Empty).Trim();
      _exportButton.Enabled = !string.IsNullOrEmpty(folder);
    }

    private void OnBrowseClick(object sender, EventArgs e)
    {
      string currentFolder = (_folderBox.Text ?? string.Empty).Trim();
      if (VelumFolderBrowser.TrySelect(this, "Выберите каталог обмена с 1C:", currentFolder, out string selected))
      {
        _folderBox.Text = selected;
        VelumAppConfig.SetBomExchangeFolder(selected);
      }
    }

    /// <summary>
    /// Загрузить список расхождений, доступных для экспорта, в список.
    /// </summary>
    private void LoadDiscrepancyList()
    {
      try
      {
        var store = new VelumAssemblyBomMirrorStore();
        store.Load();
        var entries = store.GetDiscrepancyEntries()
            .Where(e => !string.IsNullOrWhiteSpace(e.ExternalId))
            .ToList();

        _listView.BeginUpdate();
        try
        {
          _listView.Items.Clear();
          foreach (var entry in entries)
          {
            var item = new ListViewItem(entry.ExternalId ?? string.Empty);
            item.SubItems.Add(entry.Quantity > 0 ? "Assembly" : "Part");
            item.SubItems.Add(entry.Designation ?? string.Empty);
            item.SubItems.Add(entry.Name ?? string.Empty);
            item.SubItems.Add(entry.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _listView.Items.Add(item);
          }
        }
        finally
        {
          _listView.EndUpdate();
        }

        noteLabel.Text = entries.Count == 0
            ? "Экспортируемые строки не обнаружены."
            : $"Компоненты без заполненного ExternalId будут пропущены при экспорте. Найдено строк для экспорта: {entries.Count}.";
      }
      catch (Exception ex)
      {
        Logger.Warning("Velum bomExchange: unable to load discrepancy list: " + ex.Message);
        _listView.Items.Clear();
        noteLabel.Text = "Ошибка загрузки списка: " + ex.Message;
      }
    }

    private void OnSettingsClick(object sender, EventArgs e)
    {
      using (var form = new VelumBomTrackedPropertiesEditForm())
      {
        form.ShowDialog(this);
      }
    }

    private void OnExportClick(object sender, EventArgs e)
    {
      string folder = (_folderBox.Text ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(folder))
      {
        MessageBox.Show(
            this,
            "Укажите каталог обмена.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      if (!Directory.Exists(folder))
      {
        try
        {
          Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
          MessageBox.Show(
              this,
              "Не удалось создать каталог: " + ex.Message,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }
      }

      // Сохранить настройку каталога.
      VelumAppConfig.SetBomExchangeFolder(folder);

      // Выполнить рефлекс.
      if (RecipeExecutorHandlersBomExport.TryExecuteBomExchangeExport(
              0,
              new System.Collections.Generic.Dictionary<string, string> { { "exchangeFolder", folder } },
              out RecipeStepExecutionResult result))
      {
        if (result.Success)
        {
          MessageBox.Show(
              this,
              "Экспорт выполнен успешно.\n" + result.Message,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
          // previousHash обновлён внутри TryExecuteBomExchangeExport.
          LoadDiscrepancyList();
          this.DialogResult = DialogResult.OK;
          Close();
        }
        else
        {
          MessageBox.Show(
              this,
              "Экспорт не выполнен: " + result.Message,
              Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
        }
      }
      else
      {
        MessageBox.Show(
            this,
            "Ошибка при выполнении экспорта.",
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
      }
    }
  }
}
