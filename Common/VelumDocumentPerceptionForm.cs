using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Velum.ReactiveCore.DocumentTemplates;

namespace Velum.UI
{
  /// <summary>
  /// Панель «Документ»: показывает снимок восприятия активного документа SolidWorks —
  /// распознанный шаблон и <c>confidence</c>, значения параметров с зонами допуска
  /// (<c>good</c>/<c>warn</c>/<c>bad</c>) и оценки болевых точек (групп параметров).
  /// Отчёт строится по уже собранному снимку; сбор снимка через COM выполняется вызывающим
  /// кодом в UI-потоке SolidWorks.
  /// </summary>
  internal sealed class VelumDocumentPerceptionForm : Form
  {
    private readonly TextBox _txt;

    /// <summary>Создаёт панель и заполняет её отчётом по снимку.</summary>
    /// <param name="snapshot">Снимок восприятия (может быть <c>null</c>).</param>
    public VelumDocumentPerceptionForm(VelumDocumentSnapshot snapshot)
    {
      Text = "Документ";
      StartPosition = FormStartPosition.CenterParent;
      MinimizeBox = false;
      MinimumSize = new Size(420, 320);
      Size = new Size(520, 460);
      Font = new Font("Segoe UI", 9f);

      Icon icon = TryLoadVelumWindowIcon();
      if (icon != null)
        Icon = icon;

      var layout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        RowCount = 2,
        ColumnCount = 1,
        Padding = new Padding(8)
      };
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      _txt = new TextBox
      {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.White,
        Font = new Font("Consolas", 9.5f),
        Text = BuildReport(snapshot)
      };

      var buttons = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.RightToLeft,
        AutoSize = true,
        Margin = new Padding(0, 8, 0, 0)
      };

      var btnClose = new Button
      {
        Text = "Закрыть",
        DialogResult = DialogResult.OK,
        AutoSize = true
      };
      btnClose.Click += (s, e) => Close();

      buttons.Controls.Add(btnClose);
      layout.Controls.Add(_txt, 0, 0);
      layout.Controls.Add(buttons, 0, 1);
      Controls.Add(layout);

      var tip = new ToolTip();
      tip.SetToolTip(btnClose, "Закрыть");
    }

    protected override void OnShown(EventArgs e)
    {
      base.OnShown(e);
      _txt.SelectionStart = 0;
      _txt.SelectionLength = 0;
    }

    /// <summary>
    /// Формирует текстовый отчёт по снимку: шапка (шаблон/тип/confidence), параметры с зонами
    /// и болевые точки. Для <c>null</c> или нераспознанного документа — понятная заглушка.
    /// </summary>
    /// <param name="snapshot">Снимок восприятия.</param>
    /// <returns>Готовый текст отчёта.</returns>
    internal static string BuildReport(VelumDocumentSnapshot snapshot)
    {
      if (snapshot == null)
        return "Нет активного документа.\r\nОткройте деталь или сборку SolidWorks.";

      var sb = new StringBuilder(512);

      sb.AppendLine("Тип: " + TypeToText(snapshot.DocumentType));

      if (!snapshot.Recognized)
      {
        sb.Append("Шаблон: не распознан (сторонняя деталь)");
        if (!string.IsNullOrWhiteSpace(snapshot.BestGuessTemplateId))
          sb.Append("  · вероятно «").Append(snapshot.BestGuessTemplateId).Append('»');
        sb.AppendLine();
        sb.AppendLine("confidence: " + FormatConfidence(snapshot.Confidence));
        sb.AppendLine();
        sb.AppendLine("Параметры недоступны: сначала пометьте документ шаблоном либо");
        sb.AppendLine("настройте отпечаток распознавания в библиотеке шаблонов.");
        return sb.ToString().TrimEnd();
      }

      sb.Append("Шаблон: ").Append(snapshot.TemplateId);
      sb.AppendLine("  · confidence " + FormatConfidence(snapshot.Confidence));
      sb.AppendLine();

      AppendParameters(sb, snapshot);
      AppendGroups(sb, snapshot);

      return sb.ToString().TrimEnd();
    }

    private static void AppendParameters(StringBuilder sb, VelumDocumentSnapshot snapshot)
    {
      sb.AppendLine("Параметры:");
      if (snapshot.Parameters.Count == 0)
      {
        sb.AppendLine("  — (в шаблоне нет параметров)");
        return;
      }

      foreach (KeyValuePair<string, VelumDocumentParameterReading> kv in snapshot.Parameters)
      {
        VelumDocumentParameterReading r = kv.Value;
        sb.Append("  ")
          .Append(ZoneGlyph(r.Zone))
          .Append(' ')
          .Append(r.Label);
        if (!string.IsNullOrWhiteSpace(r.Unit))
          sb.Append(" [").Append(r.Unit).Append(']');
        sb.Append(": ");

        if (r.Found)
          sb.Append(r.RawValue ?? "—");
        else
          sb.Append("не найден");

        sb.Append("  · ").Append(ZoneToText(r.Zone));
        sb.Append(" (").Append(r.Score.ToString("0", CultureInfo.InvariantCulture)).Append(')');
        sb.AppendLine();
      }
    }

    private static void AppendGroups(StringBuilder sb, VelumDocumentSnapshot snapshot)
    {
      if (snapshot.GroupScores.Count == 0)
        return;

      sb.AppendLine();
      sb.AppendLine("Болевые точки:");
      foreach (KeyValuePair<string, float> kv in snapshot.GroupScores)
      {
        VelumDocumentParameterProbeRules.Zone zone = ZoneFromScore(kv.Value);
        sb.Append("  ")
          .Append(ZoneGlyph(zone))
          .Append(' ')
          .Append(kv.Key)
          .Append(": ")
          .Append(kv.Value.ToString("0", CultureInfo.InvariantCulture))
          .Append(" / 100")
          .AppendLine();
      }
    }

    private static VelumDocumentParameterProbeRules.Zone ZoneFromScore(float score)
    {
      if (score >= VelumDocumentParameterProbeRules.GoodScore - 0.5f)
        return VelumDocumentParameterProbeRules.Zone.Good;
      if (score >= VelumDocumentParameterProbeRules.WarnBadBoundaryScore)
        return VelumDocumentParameterProbeRules.Zone.Warn;
      return VelumDocumentParameterProbeRules.Zone.Bad;
    }

    private static string ZoneGlyph(VelumDocumentParameterProbeRules.Zone zone)
    {
      switch (zone)
      {
        case VelumDocumentParameterProbeRules.Zone.Warn:
          return "!";
        case VelumDocumentParameterProbeRules.Zone.Bad:
          return "x";
        default:
          return "+";
      }
    }

    private static string ZoneToText(VelumDocumentParameterProbeRules.Zone zone)
    {
      switch (zone)
      {
        case VelumDocumentParameterProbeRules.Zone.Warn:
          return "warn";
        case VelumDocumentParameterProbeRules.Zone.Bad:
          return "плохо";
        default:
          return "норма";
      }
    }

    private static string TypeToText(string docType)
    {
      switch ((docType ?? string.Empty).Trim().ToLowerInvariant())
      {
        case "part":
          return "деталь";
        case "asm":
          return "сборка";
        case "drw":
          return "чертёж";
        default:
          return "—";
      }
    }

    private static string FormatConfidence(double confidence)
    {
      return confidence.ToString("0.###", CultureInfo.InvariantCulture);
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
          Path.Combine(dir, "icons", "velum.ico")
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