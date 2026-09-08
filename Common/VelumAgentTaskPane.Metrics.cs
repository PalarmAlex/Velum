using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ISIDA.Actions;
using ISIDA.Gomeostas;
using Velum.Isida;
using Velum.SolidHomeostasis;

namespace Velum.UI
{
  public sealed partial class VelumAgentTaskPane
  {
    private void ClearMosaic()
    {
      _metricSignature = string.Empty;
      _metricBrickUnderMouse = null;
      _mosaicMetricColumnsApplied = -1;

      if (_brickGrid == null)
        return;

      _brickGrid.SuspendLayout();
      try
      {
        for (int i = 0; i < _mosaicMetricBricks.Count; i++)
        {
          Button b = _mosaicMetricBricks[i];
          _brickGrid.Controls.Remove(b);
          b.Dispose();
        }

        _mosaicMetricBricks.Clear();
        _brickGrid.ColumnStyles.Clear();
        _brickGrid.RowStyles.Clear();
        _brickGrid.ColumnCount = 1;
        _brickGrid.RowCount = 1;
        _brickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1f));
        _brickGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
      }
      finally
      {
        _brickGrid.ResumeLayout(true);
      }

      ApplyMetricBrickGridSizeFromMosaic();
    }

    private void RebuildMetricMosaic(List<InfluenceActionSystem.GomeostasisInfluenceAction> list)
    {
      if (_brickGrid == null)
        return;

      _brickGrid.SuspendLayout();
      try
      {
        for (int i = 0; i < _mosaicMetricBricks.Count; i++)
        {
          Button b = _mosaicMetricBricks[i];
          _brickGrid.Controls.Remove(b);
          b.Dispose();
        }

        _mosaicMetricBricks.Clear();
        _brickGrid.ColumnStyles.Clear();
        _brickGrid.RowStyles.Clear();

        if (list == null || list.Count == 0)
        {
          _brickGrid.ColumnCount = 1;
          _brickGrid.RowCount = 1;
          _brickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1f));
          _brickGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
          _mosaicMetricColumnsApplied = -1;
        }
        else
        {
          int n = list.Count;
          int innerW = GetMosaicViewportInnerWidth();
          int padH = _brickGrid.Padding.Horizontal;
          int cols = ComputeMosaicColumnsForCount(innerW, n, padH);
          int rows = (n + cols - 1) / cols;

          _brickGrid.ColumnCount = cols;
          _brickGrid.RowCount = rows;

          for (int c = 0; c < cols; c++)
            _brickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BrickCellOuterPx));
          for (int r = 0; r < rows; r++)
            _brickGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, BrickCellOuterPx));

          for (int i = 0; i < n; i++)
          {
            int col = i % cols;
            int row = i / cols;
            Button brick = CreateMetricBrickButton(list[i]);
            _mosaicMetricBricks.Add(brick);
            _brickGrid.Controls.Add(brick, col, row);
          }

          _mosaicMetricColumnsApplied = cols;
        }
      }
      finally
      {
        _brickGrid.ResumeLayout(true);
      }

      ApplyMetricBrickGridSizeFromMosaic();
    }

    private void UpdateMetricBricksOnly(List<InfluenceActionSystem.GomeostasisInfluenceAction> list)
    {
      if (list == null || list.Count != _mosaicMetricBricks.Count)
      {
        RebuildMetricMosaic(list);
        return;
      }

      for (int i = 0; i < list.Count; i++)
      {
        InfluenceActionSystem.GomeostasisInfluenceAction metric = list[i];
        Button brick = _mosaicMetricBricks[i];
        Color c = MetricBrickBackColor(metric);
        if (brick.BackColor != c)
          brick.BackColor = c;
        ApplyMetricBrickTooltip(brick, metric);
      }
    }

    private Button CreateMetricBrickButton(InfluenceActionSystem.GomeostasisInfluenceAction metric)
    {
      var brick = new Button
      {
        Text = string.Empty,
        TabStop = false,
        UseVisualStyleBackColor = false,
        FlatStyle = FlatStyle.Standard,
        BackColor = MetricBrickBackColor(metric),
        Cursor = Cursors.Hand,
        Dock = DockStyle.Fill,
        Margin = new Padding(1),
        Tag = metric?.Id ?? 0,
      };
      brick.MouseEnter += OnMetricBrickMouseEnter;
      brick.MouseLeave += OnMetricBrickMouseLeave;
      brick.Click += OnMetricBrickClick;
      ApplyMetricBrickTooltip(brick, metric);
      return brick;
    }

    private static Color MetricBrickBackColor(InfluenceActionSystem.GomeostasisInfluenceAction metric)
    {
      if (metric == null || !metric.IsActive)
        return MetricBrickDisabledColor;
      if (VelumSolidEnvironmentInfluenceCatalog.IsActiveMetricPressing(metric))
        return MetricBrickPressingColor;
      return MetricBrickIdleColor;
    }

    private static string BuildMetricHoverTooltip(InfluenceActionSystem.GomeostasisInfluenceAction metric)
    {
      if (metric == null)
        return string.Empty;
      var sb = new StringBuilder(160);
      sb.AppendLine("ID: " + metric.Id);
      sb.AppendLine("Имя: " + (metric.Name ?? string.Empty));
      sb.AppendLine("Описание: " + (metric.Description ?? string.Empty));
      return sb.ToString().TrimEnd();
    }

    private static string BuildMetricDetailText(
        InfluenceActionSystem.GomeostasisInfluenceAction metric,
        GomeostasSystem gomeostas)
    {
      if (metric == null)
        return string.Empty;

      var sb = new StringBuilder(256);
      sb.AppendLine("ID: " + metric.Id);
      sb.AppendLine("Имя: " + (metric.Name ?? string.Empty));
      sb.AppendLine("Описание: " + (metric.Description ?? string.Empty));
      sb.AppendLine("Активность: " + (metric.IsActive ? "включена" : "выключена"));
      AppendMetricParameterInfluences(sb, metric, gomeostas);
      return sb.ToString().TrimEnd();
    }

    private static void AppendMetricParameterInfluences(
        StringBuilder sb,
        InfluenceActionSystem.GomeostasisInfluenceAction metric,
        GomeostasSystem gomeostas)
    {
      sb.AppendLine();
      sb.AppendLine("Давление на параметры:");
      if (metric?.Influences == null || metric.Influences.Count == 0)
      {
        sb.AppendLine("—");
        return;
      }

      IReadOnlyDictionary<int, float> currentValues = null;
      try
      {
        if (gomeostas != null)
          currentValues = gomeostas.HostGetParameterValues(metric.Influences.Keys);
      }
      catch
      {
      }

      var rows = new List<string>(metric.Influences.Count);
      foreach (KeyValuePair<int, int> kv in metric.Influences.OrderBy(p => p.Key))
      {
        string pname = ResolveParameterName(gomeostas, kv.Key);
        string current = "?";
        if (currentValues != null && currentValues.TryGetValue(kv.Key, out float value))
          current = FormatParameterValue(value);
        rows.Add(pname + " (" + kv.Key + "): " + kv.Value + "/" + current);
      }

      for (int i = 0; i < rows.Count; i++)
        sb.AppendLine(rows[i]);
    }

    private static string FormatParameterValue(float value)
    {
      return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ResolveParameterName(GomeostasSystem gomeostas, int parameterId)
    {
      if (gomeostas == null)
        return "параметр " + parameterId;

      try
      {
        GomeostasSystem.ParameterData p = gomeostas.GetAllParameters()
            ?.FirstOrDefault(x => x != null && x.Id == parameterId);
        if (p == null || string.IsNullOrWhiteSpace(p.Name))
          return "параметр " + parameterId;
        return p.Name.Trim();
      }
      catch
      {
        return "параметр " + parameterId;
      }
    }

    private void ApplyMetricBrickTooltip(Button brick, InfluenceActionSystem.GomeostasisInfluenceAction metric)
    {
      if (brick == null || metric == null || _parameterToolTip == null)
        return;
      if (ReferenceEquals(brick, _metricBrickUnderMouse))
        return;
      _parameterToolTip.SetToolTip(brick, BuildMetricHoverTooltip(metric));
    }

    private void OnMetricBrickMouseEnter(object sender, EventArgs e)
    {
      _metricBrickUnderMouse = sender as Button;
    }

    private void OnMetricBrickMouseLeave(object sender, EventArgs e)
    {
      var b = sender as Button;
      if (ReferenceEquals(_metricBrickUnderMouse, b))
        _metricBrickUnderMouse = null;
      TryRefreshMetricBrickTooltipAfterHover(b);
    }

    private void TryRefreshMetricBrickTooltipAfterHover(Button brick)
    {
      if (brick == null || !VelumIsidaHost.IsReady)
        return;
      if (!(brick.Tag is int metricId))
        return;
      try
      {
        InfluenceActionSystem.GomeostasisInfluenceAction metric =
            VelumSolidEnvironmentInfluenceCatalog.GetAllEnvironmentActions()
                .FirstOrDefault(m => m != null && m.Id == metricId);
        if (metric == null)
          return;
        ApplyMetricBrickTooltip(brick, metric);
      }
      catch
      {
      }
    }

    private void OnMetricBrickClick(object sender, EventArgs e)
    {
      var b = sender as Button;
      if (b == null || !(b.Tag is int metricId))
        return;
      if (!VelumIsidaHost.IsReady)
        return;

      Form owner = FindForm();
      if (!VelumAdminAccess.TryRequireAdmin(owner, VelumAdminAccess.FormDeniedMessage))
        return;

      try
      {
        InfluenceActionSystem.GomeostasisInfluenceAction metric =
            VelumSolidEnvironmentInfluenceCatalog.GetAllEnvironmentActions()
                .FirstOrDefault(m => m != null && m.Id == metricId);
        if (metric == null)
          return;

        GomeostasSystem g = VelumIsidaHost.Context.Gomeostas;
        string body = BuildMetricDetailText(metric, g);
        using (var dlg = new VelumAgentBrickDetailForm("Метрика среды", body))
        {
          if (owner != null)
            dlg.ShowDialog(owner);
          else
            dlg.ShowDialog();
        }
      }
      catch
      {
      }
    }

    private void OnMetricsSettingsPickClick(object sender, EventArgs e)
    {
      if (!VelumAdminAccess.TryRequireAdmin(FindForm(), VelumAdminAccess.FormDeniedMessage))
        return;

      if (!VelumIsidaHost.TryInitialize(out string err))
      {
        MessageBox.Show(err, "Velum", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      if (!InfluenceActionSystem.IsInitialized)
      {
        MessageBox.Show(
            "Система воздействий ISIDA не инициализирована.",
            "Velum",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      IReadOnlyList<InfluenceActionSystem.GomeostasisInfluenceAction> metrics =
          VelumSolidEnvironmentInfluenceCatalog.GetAllEnvironmentActions().ToList();

      using (var dlg = new VelumEnvironmentMetricsPickerForm(metrics))
      {
        Form owner = FindForm();
        DialogResult dr = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
        if (dr != DialogResult.OK)
          return;
      }

      _metricSignature = string.Empty;
      RequestRefresh();
    }
  }
}
