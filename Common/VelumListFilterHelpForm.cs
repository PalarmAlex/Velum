using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>Справка по маскам унифицированных фильтров списков.</summary>
  internal sealed partial class VelumListFilterHelpForm : Form
  {
    public VelumListFilterHelpForm()
    {
      InitializeComponent();
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;
      _txtHelp.Text = BuildHelpText();
      _txtHelp.SelectionStart = 0;
      _txtHelp.SelectionLength = 0;
      var tip = new ToolTip();
      tip.SetToolTip(_btnClose, "Закрыть");
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      Close();
    }

    private static string BuildHelpText()
    {
      var sb = new StringBuilder();
      sb.AppendLine("Маски фильтра списка");
      sb.AppendLine();
      sb.AppendLine("Несколько столбцов объединяются через И (AND).");
      sb.AppendLine("В одном поле несколько значений через | — через ИЛИ (OR).");
      sb.AppendLine("Спецсимвол в начале строки действует на все токены поля.");
      sb.AppendLine();
      sb.AppendLine("Операторы:");
      sb.AppendLine("  (без символа) или *  — вхождение (LIKE %текст%)");
      sb.AppendLine("  =     — точное совпадение");
      sb.AppendLine("  !=    — не равно (точное)");
      sb.AppendLine("  !     — исключить вхождение (NOT LIKE)");
      sb.AppendLine("  >     — больше (число)");
      sb.AppendLine("  >=    — больше или равно (число)");
      sb.AppendLine("  <     — меньше (число)");
      sb.AppendLine("  <=    — меньше или равно (число)");
      sb.AppendLine();
      sb.AppendLine("Примеры:");
      sb.AppendLine("  Ф1|Ф2|Ф3          — содержит Ф1 или Ф2 или Ф3");
      sb.AppendLine("  =Ф1|Ф2            — равно Ф1 или Ф2");
      sb.AppendLine("  !=Ф1|Ф2|Ф3        — не равно ни одному из набора");
      sb.AppendLine("  !чертеж|эскиз     — не содержит «чертеж» и не содержит «эскиз»");
      sb.AppendLine("  *плита            — содержит «плита» (явный *)");
      sb.AppendLine("  >10               — число больше 10");
      sb.AppendLine("  >=1,5|2           — число ≥ 1,5 или ≥ 2");
      sb.AppendLine("  <100|50           — число < 100 или < 50");
      sb.AppendLine();
      sb.AppendLine("Числовые сравнения (> < >= <=) используют сырое значение ячейки.");
      sb.AppendLine("Остальные операторы — по тексту, как в списке.");
      sb.AppendLine();
      sb.AppendLine("Фильтр применяется кнопкой «Применить» (или Enter в поле фильтра).");
      sb.AppendLine("«Сброс» очищает поля и показывает полный список.");
      return sb.ToString();
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "velum.ico");
        return File.Exists(path) ? new Icon(path) : null;
      }
      catch
      {
        return null;
      }
    }
  }
}
