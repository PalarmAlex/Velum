using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Velum.UI
{
  /// <summary>Вставка шаблонов технических требований в активный чертеж.</summary>
  internal sealed partial class VelumTechRequirementsForm : Form
  {
    private readonly ISwApplication _swApp;
    private VelumTechRequirementsListHelper _listHelper;

    public VelumTechRequirementsForm(ISwApplication swApp)
    {
      _swApp = swApp;

      if (!ValidateSolidWorksState())
      {
        Dispose();
        return;
      }

      InitializeComponent();
      VelumFormHelp.Bind(this, VelumHelpTopics.TechRequirements);
      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      _listHelper = new VelumTechRequirementsListHelper(
          _listView,
          _groupFilterBox,
          _itemFilterBox);

      _btnFilterApply.Click += (s, e) => _listHelper.ApplyFiltersFromUi();
      _btnFilterReset.Click += (s, e) => _listHelper.ResetFilters();
      _btnFilterHelp.Click += (s, e) => VelumListFilterHelper.ShowHelp(this);

      _multiSelectCheck.Checked = true;
      _listView.MultiSelect = true;
      _fontDefaultCheck.Checked = true;
      _fontSizeBox.Enabled = false;

      var tip = new ToolTip();
      tip.SetToolTip(_groupFilterBox, "Маска фильтра групп (см. «?»)");
      tip.SetToolTip(_itemFilterBox, "Маска фильтра строк (см. «?»)");
      tip.SetToolTip(_btnFilterApply, "Применить фильтры");
      tip.SetToolTip(_btnFilterReset, "Очистить фильтры");
      tip.SetToolTip(_btnFilterHelp, "Справка по маскам фильтра");
      tip.SetToolTip(_btnImport, "Импортировать файл ТТ");
      tip.SetToolTip(_btnApply, "Вставить тех. требования в чертеж");
      tip.SetToolTip(_btnClose, "Закрыть");
      tip.SetToolTip(_listView, "Ctrl+↑ / Ctrl+↓ или PgUp / PgDn — перемещение строки");
      tip.SetToolTip(_multiSelectCheck, "Режим мультивыбора");
      tip.SetToolTip(_fontDefaultCheck, "Использовать шрифт из настроек чертежа");
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
      // ListView при MultiSelect=true съедает Ctrl+стрелки до KeyDown — ловим раньше.
      if (_listHelper == null || _listView == null || _listView.SelectedItems.Count == 0)
        return base.ProcessCmdKey(ref msg, keyData);

      Keys code = keyData & Keys.KeyCode;
      bool control = (keyData & Keys.Control) == Keys.Control;
      bool listFocused = ActiveControl == _listView || _listView.Focused || _listView.ContainsFocus;

      // Ctrl+↑/↓ — всегда при наличии выделения (фокус в SW-хосте может «теряться»).
      // PgUp/PgDn — только когда фокус в списке, чтобы не перехватывать из фильтров.
      bool isCtrlArrow = control && (code == Keys.Up || code == Keys.Down);
      bool isPage = code == Keys.PageUp || code == Keys.PageDown;
      if (!isCtrlArrow && !(isPage && listFocused))
        return base.ProcessCmdKey(ref msg, keyData);

      if (_listHelper.TryProcessMoveKey(keyData))
        return true;

      return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool ValidateSolidWorksState()
    {
      try
      {
        if (_swApp?.Documents?.Active == null)
        {
          MessageBox.Show(
              "Откройте чертеж в SolidWorks перед загрузкой.",
              "Внимание",
              MessageBoxButtons.OK,
              MessageBoxIcon.Warning);
          return false;
        }

        if (!(_swApp.Documents.Active is ISwDrawing))
        {
          MessageBox.Show(
              "Активный документ не является чертежом.",
              "Ошибка",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return false;
        }

        return true;
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            "Ошибка: " + ex.Message,
            "Ошибка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        return false;
      }
    }

    private void OnFontDefaultChanged(object sender, EventArgs e)
    {
      _fontSizeBox.Enabled = !_fontDefaultCheck.Checked;
    }

    private void OnMultiSelectChanged(object sender, EventArgs e)
    {
      _listView.MultiSelect = _multiSelectCheck.Checked;
    }

    private void OnImportClick(object sender, EventArgs e)
    {
      _listHelper?.ImportFromTextFile();
    }

    private void OnApplyClick(object sender, EventArgs e)
    {
      InsertSelectedToDrawing();
    }

    private void InsertSelectedToDrawing()
    {
      if (_listView.SelectedItems.Count == 0)
      {
        MessageBox.Show(
            "Не выбрано ни одного элемента для вставки.",
            "Ошибка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      try
      {
        if (!(_swApp.Documents.Active is ISwDrawing drawing))
        {
          MessageBox.Show(
              "Активный документ не является чертежом.",
              "Ошибка",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }

        var swModel = drawing.Model as IModelDoc2;
        var swDrawing = swModel as IDrawingDoc;
        if (swDrawing == null)
        {
          MessageBox.Show(
              "Не удалось получить интерфейс чертежа (IDrawingDoc).",
              "Ошибка",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }

        if (swDrawing.GetCurrentSheet() as ISheet == null)
        {
          MessageBox.Show(
              "Не удалось получить текущий лист чертежа.",
              "Ошибка",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }

        int fontSizePt = 14;
        if (!_fontDefaultCheck.Checked)
        {
          if (string.IsNullOrWhiteSpace(_fontSizeBox.Text) ||
              !int.TryParse(_fontSizeBox.Text, out fontSizePt))
          {
            MessageBox.Show(
                "Укажите размер шрифта.",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _fontSizeBox.Focus();
            return;
          }
        }

        var selectedItems = _listView.SelectedItems.Cast<ListViewItem>().ToList();
        string textContent = selectedItems.Count > 1
            ? string.Join("\n", selectedItems.Select((item, idx) =>
                (idx + 1) + ". " + item.SubItems[1].Text))
            : selectedItems[0].SubItems[1].Text;

        var swNote = swModel.InsertNote(textContent) as INote;
        if (swNote == null)
        {
          MessageBox.Show(
              "Не удалось создать заметку в чертеже.",
              "Ошибка",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
          return;
        }

        swNote.SetTextPoint(0.1, 0.1, 0);

        if (!_fontDefaultCheck.Checked)
        {
          bool status = swModel.Extension.SelectByID2(
              swNote.GetName(), "NOTE", 0, 0, 0, false, 0, null, 0);
          if (status)
          {
            swModel.FontPoints((short)fontSizePt);
            swModel.ClearSelection2(true);
          }
        }

        swModel.EditRebuild3();
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            "Ошибка при вставке в чертеж: " + ex.Message,
            "Ошибка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
      }
    }

    private static Icon TryLoadFormIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
