using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Velum.UI.AssemblyRegistry;
using Xarial.XCad.SolidWorks;

namespace Velum.UI
{
  internal sealed partial class VelumAssemblyRegistryForm
  {
    private TableLayoutPanel _exportHubPanel;
    private Button _btnExportDxf;
    private Button _btnExportPdf;
    private Label _lblProductQuantity;
    private NumericUpDown _nudProductQuantity;

    private void InitializeExportHubUi()
    {
      _exportHubPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        AutoSize = true,
        ColumnCount = 1,
        RowCount = 1,
        Padding = new Padding(0, 2, 0, 2)
      };
      _exportHubPanel.RowStyles.Add(new RowStyle());

      var buttons = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        AutoSize = true,
        WrapContents = true,
        Margin = new Padding(0)
      };
      _btnExportDxf = MakeHubButton("Экспорт DXF", OnExportDxfFromRegistry);
      _btnExportPdf = MakeHubButton("Экспорт PDF", OnExportPdfFromRegistry);
      _btnExportDxf.Enabled = false;
      _btnExportPdf.Enabled = false;

      _lblProductQuantity = new Label
      {
        AutoSize = true,
        Text = "Кол-во изделия:",
        Margin = new Padding(12, 8, 4, 2),
        Anchor = AnchorStyles.Left
      };
      _nudProductQuantity = new NumericUpDown
      {
        Minimum = 1,
        Maximum = 999999,
        Value = 1,
        Width = 60,
        Margin = new Padding(0, 4, 6, 2)
      };
      _nudProductQuantity.ValueChanged += OnProductQuantityChanged;

      buttons.Controls.Add(_btnExportDxf);
      buttons.Controls.Add(_btnExportPdf);
      buttons.Controls.Add(_lblProductQuantity);
      buttons.Controls.Add(_nudProductQuantity);
      _exportHubPanel.Controls.Add(buttons, 0, 0);

      // Вставляем панель под шаблон столбцов.
      _listPanel.SuspendLayout();
      _listPanel.RowCount = 7;
      _listPanel.Controls.Add(_exportHubPanel, 0, 1);
      _listPanel.SetRow(_filtersHost, 2);
      _listPanel.SetRow(_filterButtonsHost, 3);
      _listPanel.SetRow(_listStatusLabel, 4);
      _listPanel.SetRow(_listView, 5);
      _listPanel.SetRow(_totalsHost, 6);
      _listPanel.RowStyles.Clear();
      _listPanel.RowStyles.Add(new RowStyle());
      _listPanel.RowStyles.Add(new RowStyle());
      // Строка фильтров: стартовая высота; BuildFilterEditors пересчитает по числу столбцов.
      _listPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
      _listPanel.RowStyles.Add(new RowStyle());
      _listPanel.RowStyles.Add(new RowStyle());
      _listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
      _listPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
      _listPanel.ResumeLayout();
    }

    private void OnProductQuantityChanged(object sender, EventArgs e)
    {
      BindList();
    }

    private int ReadProductQuantity()
    {
      if (_nudProductQuantity == null)
        return 1;
      int qty = (int)_nudProductQuantity.Value;
      return qty < 1 ? 1 : qty;
    }

    private static Button MakeHubButton(string text, EventHandler onClick)
    {
      var btn = new Button
      {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 2, 6, 2)
      };
      btn.Click += onClick;
      return btn;
    }

    private string TryGetActiveAssemblyFolder()
    {
      try
      {
        ModelDoc2 active = _swApp?.Sw?.IActiveDoc2 as ModelDoc2;
        if (active == null || active.GetType() != (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY)
          return string.Empty;
        string path = active.GetPathName();
        if (string.IsNullOrWhiteSpace(path))
          return string.Empty;
        return Path.GetDirectoryName(path) ?? string.Empty;
      }
      catch
      {
        return string.Empty;
      }
    }

    private bool EnsureGraphReady()
    {
      if (_graph == null || _graph.Components == null || _graph.Components.Count == 0)
      {
        MessageBox.Show(
            this,
            "Состав сборки пуст. Нажмите «Обновить».",
            "Реестр изделия",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
      }

      return true;
    }

    private void OnExportDxfFromRegistry(object sender, EventArgs e)
    {
      RunDxfBomFlow();
    }

    private void OnExportPdfFromRegistry(object sender, EventArgs e)
    {
      RunPdfBomFlow();
    }

    private void RunDxfBomFlow()
    {
      if (!EnsureGraphReady())
        return;

      List<VelumAssemblyRegistryComponent> components = GetBatchExportComponents();
      if (components.Count == 0)
      {
        MessageBox.Show(
            this,
            "В составе сборки нет позиций (с учётом фильтров списка).",
            "Экспорт DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      _stopRequested = false;
      _loading = true;
      SetLoadingUi(true);
      VelumAssemblyRegistryBomDiagnostics.DxfResult result;
      try
      {
        SetProgress(0, Math.Max(1, components.Count), "Подготовка DXF…");
        result = VelumAssemblyRegistryBomDiagnostics.BuildUncheckedDxf(
            _swApp,
            components,
            () => _stopRequested,
            SetProgress);
      }
      finally
      {
        _loading = false;
        SetLoadingUi(false);
        _progressBar.Value = 0;
        if (_stopRequested)
          _progressLabel.Text = "Прервано";
        else
          _progressLabel.Text = string.Empty;
      }

      if (_stopRequested)
        return;

      if (result.Rows.Count == 0)
      {
        MessageBox.Show(
            this,
            "Нет деталей с «Нужен dxf = Да».",
            "Экспорт DXF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      VelumDxfBatchDiagnosticsFormHost.TryShowFromAssemblyRegistry(
          _swApp,
          new VelumDxfBomLaunchContext
          {
            DxfExportFolder = string.Empty,
            AssemblyFolder = TryGetActiveAssemblyFolder(),
            Rows = result.Rows,
            Components = components,
            EnableAssemblyQuantity = true,
            ProductQuantity = ReadProductQuantity()
          });
    }

    private void RunPdfBomFlow()
    {
      if (!EnsureGraphReady())
        return;

      List<VelumAssemblyRegistryComponent> components = GetBatchExportComponents();
      if (components.Count == 0)
      {
        MessageBox.Show(
            this,
            "В составе сборки нет позиций (с учётом фильтров списка).",
            "Экспорт PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      _stopRequested = false;
      _loading = true;
      SetLoadingUi(true);
      VelumAssemblyRegistryBomDiagnostics.PdfResult result;
      try
      {
        SetProgress(0, Math.Max(1, components.Count), "Подготовка PDF…");
        result = VelumAssemblyRegistryBomDiagnostics.BuildUncheckedPdf(
            _swApp,
            components,
            () => _stopRequested,
            SetProgress);
      }
      finally
      {
        _loading = false;
        SetLoadingUi(false);
        _progressBar.Value = 0;
        if (_stopRequested)
          _progressLabel.Text = "Прервано";
        else
          _progressLabel.Text = string.Empty;
      }

      if (_stopRequested)
        return;

      if (result.Rows.Count == 0)
      {
        MessageBox.Show(
            this,
            "По составу сборки нет позиций для PDF\n" +
            "(все отфильтрованы или с «Нужен чертеж/pdf = Нет»).",
            "Экспорт PDF",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      VelumPdfBatchDiagnosticsFormHost.TryShowFromAssemblyRegistry(
          _swApp,
          new VelumPdfBomLaunchContext
          {
            PdfExportFolder = string.Empty,
            AssemblyFolder = TryGetActiveAssemblyFolder(),
            Rows = result.Rows,
            Components = components
          });
    }

    /// <summary>
    /// Тот же набор, что в списке формы: узел/раздел дерева + фильтры столбцов.
    /// </summary>
    private List<VelumAssemblyRegistryComponent> GetBatchExportComponents()
    {
      if (_graph?.Components == null || _graph.Components.Count == 0)
        return new List<VelumAssemblyRegistryComponent>();

      return ApplyItemFilters(ResolveRowsForSelection());
    }
  }
}
