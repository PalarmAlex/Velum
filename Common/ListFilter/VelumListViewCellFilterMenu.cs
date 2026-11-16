using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>
  /// Контекстное меню ListView: «Фильтр по выделенному» / «Исключить выделенное»
  /// по значениям столбца у выделенных строк (с слиянием масок через
  /// <see cref="VelumListFilterHelper"/>).
  /// </summary>
  internal sealed class VelumListViewCellFilterMenu
  {
    private const int LvmFirst = 0x1000;
    private const int LvmSubItemHitTest = LvmFirst + 57;
    private const int SbHorz = 0;

    private readonly ListView _listView;
    private readonly IWin32Window _owner;
    private readonly Func<int, TextBox> _resolveFilterBox;
    private readonly Action _applyFilters;
    private readonly Func<bool> _canInteract;
    private readonly Func<ListViewItem, int, string> _valueSelector;

    private ToolStripMenuItem _miFilterBy;
    private ToolStripMenuItem _miExclude;
    /// <summary>Столбец, зафиксированный на ПКМ (не пересчитывать при клике по меню).</summary>
    private int _column = -1;

    /// <param name="listView">Список, к которому привязывается меню.</param>
    /// <param name="owner">Владелец модальных диалогов; при <c>null</c> — форма списка.</param>
    /// <param name="resolveFilterBox">Столбец → поле фильтра; <c>null</c> — пункт недоступен.</param>
    /// <param name="applyFilters">Применить фильтры после записи в TextBox.</param>
    /// <param name="canInteract">Доп. условие (не loading и т. п.); <c>null</c> — всегда можно.</param>
    /// <param name="valueSelector">Значение ячейки для фильтра; <c>null</c> — текст SubItems.</param>
    internal VelumListViewCellFilterMenu(
        ListView listView,
        IWin32Window owner,
        Func<int, TextBox> resolveFilterBox,
        Action applyFilters,
        Func<bool> canInteract,
        Func<ListViewItem, int, string> valueSelector)
    {
      _listView = listView ?? throw new ArgumentNullException(nameof(listView));
      _owner = owner;
      _resolveFilterBox = resolveFilterBox ?? throw new ArgumentNullException(nameof(resolveFilterBox));
      _applyFilters = applyFilters ?? throw new ArgumentNullException(nameof(applyFilters));
      _canInteract = canInteract;
      _valueSelector = valueSelector;

      // Фиксируем столбец в момент ПКМ: при клике по пункту меню курсор уже не над ячейкой.
      _listView.MouseDown += OnListMouseDown;
    }

    /// <summary>
    /// Вставляет пункты фильтра в указанную позицию или в конец (без автоматических разделителей).
    /// </summary>
    internal void InsertInto(ContextMenuStrip menu, int insertIndex, Image filterIcon, Image excludeIcon)
    {
      if (menu == null)
        throw new ArgumentNullException(nameof(menu));

      _miFilterBy = new ToolStripMenuItem("Фильтр по выделенному", filterIcon);
      _miFilterBy.Click += (s, e) => Apply(exclude: false);
      _miExclude = new ToolStripMenuItem("Исключить выделенное", excludeIcon);
      _miExclude.Click += (s, e) => Apply(exclude: true);

      int index = insertIndex;
      if (index < 0 || index > menu.Items.Count)
        index = menu.Items.Count;

      menu.Items.Insert(index, _miFilterBy);
      menu.Items.Insert(index + 1, _miExclude);
      menu.Opening += OnMenuOpening;
    }

    /// <summary>Добавляет разделитель (если меню не пусто) и пункты фильтра в конец.</summary>
    internal void AppendTo(ContextMenuStrip menu, Image filterIcon, Image excludeIcon)
    {
      if (menu == null)
        throw new ArgumentNullException(nameof(menu));
      if (menu.Items.Count > 0)
        menu.Items.Add(new ToolStripSeparator());
      InsertInto(menu, -1, filterIcon, excludeIcon);
    }

    private void OnListMouseDown(object sender, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
        return;

      CaptureColumnAt(e.Location);
    }

    private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      // Клавиатура / редкие случаи без MouseDown — один раз по курсору.
      if (_column < 0)
        CaptureColumnAt(_listView.PointToClient(Cursor.Position));

      bool idle = _canInteract == null || _canInteract();
      bool canFilter = idle && _column >= 0 && _resolveFilterBox(_column) != null
          && _listView.SelectedItems.Count > 0;
      if (_miFilterBy != null)
        _miFilterBy.Enabled = canFilter;
      if (_miExclude != null)
        _miExclude.Enabled = canFilter;
    }

    private void CaptureColumnAt(Point clientPoint)
    {
      _column = -1;
      if (_listView.Columns.Count == 0)
        return;

      int column = ResolveColumnAt(clientPoint);
      if (column < 0)
        return;

      if (_resolveFilterBox(column) == null)
        return;

      _column = column;
    }

    private void Apply(bool exclude)
    {
      // Не пересчитывать столбец: курсор уже над пунктом меню.
      int column = _column;
      if (column < 0)
        return;

      TextBox box = _resolveFilterBox(column);
      if (box == null)
        return;

      IWin32Window owner = _owner ?? _listView.FindForm();
      List<string> values = VelumListFilterHelper.CollectSelectedColumnValues(
          _listView,
          column,
          _valueSelector);
      if (values.Count == 0)
        return;

      string newExpression;
      if (!VelumListFilterHelper.TryMergeSelectionFilter(
          owner,
          box.Text,
          values,
          exclude,
          out newExpression))
        return;

      box.Text = newExpression;
      _applyFilters();
    }

    private int ResolveColumnAt(Point clientPoint)
    {
      if (_listView.Columns.Count == 0)
        return -1;

      // Managed HitTest при FullRowSelect часто отдаёт SubItem столбца 0 — не используем его.
      if (_listView.IsHandleCreated)
      {
        int native = NativeSubItemHitTest(clientPoint);
        if (native >= 0 && native < _listView.Columns.Count)
          return native;
      }

      return ResolveColumnByX(clientPoint.X);
    }

    private int NativeSubItemHitTest(Point clientPoint)
    {
      var info = new LvHitTestInfo
      {
        pt = clientPoint
      };

      SendMessageLvHitTest(_listView.Handle, LvmSubItemHitTest, IntPtr.Zero, ref info);
      if (info.iItem < 0)
        return -1;
      if (info.iSubItem < 0)
        return -1;
      return info.iSubItem;
    }

    private int ResolveColumnByX(int clientX)
    {
      if (_listView.Columns.Count == 0)
        return -1;

      int x = clientX;
      if (_listView.IsHandleCreated)
      {
        try
        {
          x += GetScrollPos(_listView.Handle, SbHorz);
        }
        catch
        {
        }
      }

      // CheckBoxes / state image съедают место слева у первой колонки.
      x -= GetLeftItemPadding();

      if (x < 0)
        x = 0;

      int edge = 0;
      for (int i = 0; i < _listView.Columns.Count; i++)
      {
        edge += Math.Max(0, _listView.Columns[i].Width);
        if (x < edge)
          return i;
      }

      return _listView.Columns.Count - 1;
    }

    private int GetLeftItemPadding()
    {
      int pad = 0;
      if (_listView.CheckBoxes)
        pad += SystemInformation.MenuCheckSize.Width;
      if (_listView.StateImageList != null)
        pad += _listView.StateImageList.ImageSize.Width;
      if (_listView.SmallImageList != null)
        pad += _listView.SmallImageList.ImageSize.Width;
      return pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LvHitTestInfo
    {
      public Point pt;
      public int flags;
      public int iItem;
      public int iSubItem;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageLvHitTest(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        ref LvHitTestInfo lParam);

    [DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);
  }
}
