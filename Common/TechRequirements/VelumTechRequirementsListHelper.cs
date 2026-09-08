using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>Список шаблонов ТТ из JSON с фильтрами, CRUD и импортом из txt.</summary>
  internal sealed class VelumTechRequirementsListHelper
  {
    private readonly ListView _listView;
    private readonly TextBox _groupFilterTextBox;
    private readonly TextBox _itemFilterTextBox;
    private readonly VelumTechRequirementsStore _store = new VelumTechRequirementsStore();
    private string _activeGroupFilter = string.Empty;
    private string _activeItemFilter = string.Empty;
    private ToolStripMenuItem _miMoveUp;
    private ToolStripMenuItem _miMoveDown;
    private VelumListViewCellFilterMenu _listCellFilterMenu;

    internal VelumTechRequirementsListHelper(
        ListView listView,
        TextBox groupFilterTextBox,
        TextBox itemFilterTextBox)
    {
      _listView = listView ?? throw new ArgumentNullException(nameof(listView));
      _groupFilterTextBox = groupFilterTextBox ?? throw new ArgumentNullException(nameof(groupFilterTextBox));
      _itemFilterTextBox = itemFilterTextBox ?? throw new ArgumentNullException(nameof(itemFilterTextBox));

      InitializeListView();
      InitializeContextMenu();
    }

    internal void ApplyFiltersFromUi()
    {
      _activeGroupFilter = (_groupFilterTextBox.Text ?? string.Empty).Trim();
      _activeItemFilter = (_itemFilterTextBox.Text ?? string.Empty).Trim();
      RefreshListView();
    }

    internal void ResetFilters()
    {
      _groupFilterTextBox.Text = string.Empty;
      _itemFilterTextBox.Text = string.Empty;
      _activeGroupFilter = string.Empty;
      _activeItemFilter = string.Empty;
      RefreshListView();
    }

    private void InitializeListView()
    {
      _listView.View = View.Details;
      _listView.FullRowSelect = true;
      _listView.MultiSelect = true;
      _listView.HideSelection = false;
      _listView.ShowGroups = true;
      _listView.HeaderStyle = ColumnHeaderStyle.Clickable;

      _listView.Columns.Clear();
      _listView.Columns.Add("ID", 0);
      _listView.Columns.Add("Тех. требования", 450);

      KeyEventHandler enterApply = (s, e) =>
      {
        if (e.KeyCode != Keys.Enter)
          return;
        ApplyFiltersFromUi();
        e.Handled = true;
        e.SuppressKeyPress = true;
      };
      _groupFilterTextBox.KeyDown += enterApply;
      _itemFilterTextBox.KeyDown += enterApply;

      // Как в KmdEdit ListViewXmlHelper: Ctrl+↑/↓ через KeyDown;
      // PreviewKeyDown — чтобы стрелки и PgUp/PgDn доходили до KeyDown.
      _listView.PreviewKeyDown += OnListViewPreviewKeyDown;
      _listView.KeyDown += OnListViewKeyDown;
      _listView.DoubleClick += (s, e) => EditItem();
      _listView.SizeChanged += (s, e) => AdjustColumns();

      _store.Load();
      RefreshListView();
    }

    private void AdjustColumns()
    {
      if (_listView.Columns.Count < 2)
        return;

      _listView.Columns[0].Width = 0;
      _listView.Columns[1].Width = Math.Max(100, _listView.ClientSize.Width - 2);
    }

    private static void OnListViewPreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
      if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down ||
          e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
        e.IsInputKey = true;
    }

    private void OnListViewKeyDown(object sender, KeyEventArgs e)
    {
      if (_listView.SelectedItems.Count == 0)
        return;

      int direction = 0;

      // Как в KmdEdit: Ctrl+↑ / Ctrl+↓; дополнительно PgUp / PgDn.
      if (e.Control && e.KeyCode == Keys.Up)
        direction = -1;
      else if (e.Control && e.KeyCode == Keys.Down)
        direction = 1;
      else if (e.KeyCode == Keys.PageUp)
        direction = -1;
      else if (e.KeyCode == Keys.PageDown)
        direction = 1;
      else if (e.KeyCode == Keys.Delete)
      {
        DeleteItems();
        e.Handled = true;
        e.SuppressKeyPress = true;
        return;
      }
      else
        return;

      MoveSelectedItem(direction);
      e.Handled = true;
      e.SuppressKeyPress = true;
    }

    /// <summary>
    /// Перехват до навигации ListView (форма вызывает из ProcessCmdKey).
    /// Нужен и при включённом MultiSelect — иначе Ctrl+стрелки съедает сам список.
    /// </summary>
    internal bool TryProcessMoveKey(Keys keyData)
    {
      Keys code = keyData & Keys.KeyCode;
      bool control = (keyData & Keys.Control) == Keys.Control;

      int direction = 0;
      if (code == Keys.PageUp || (control && code == Keys.Up))
        direction = -1;
      else if (code == Keys.PageDown || (control && code == Keys.Down))
        direction = 1;
      else
        return false;

      if (_listView.SelectedItems.Count == 0)
        return false;

      MoveSelectedItem(direction);
      return true;
    }

    private void MoveSelectedItem(int direction)
    {
      if (direction == 0 || _listView.SelectedItems.Count == 0)
        return;

      if (_listView.SelectedItems.Count > 1)
      {
        MessageBox.Show(
            "Для перемещения выделите одну строку тех. требования.",
            "Перемещение строк",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      ListViewItem selectedItem = _listView.SelectedItems[0];
      int itemId = GetItemId(selectedItem);

      try
      {
        string groupName = selectedItem.Group?.Header;
        VelumTechRequirementsGroup group = _store.FindGroup(groupName);
        if (group?.Items == null)
          return;

        int index = group.Items.FindIndex(i => i.Id == itemId);
        if (index < 0)
          return;

        int newIndex = index + direction;
        if (newIndex < 0 || newIndex >= group.Items.Count)
          return;

        VelumTechRequirementsItem item = group.Items[index];
        group.Items.RemoveAt(index);
        group.Items.Insert(newIndex, item);
        _store.Save();
        RefreshListViewAndRestoreSelection(itemId);
      }
      catch (Exception ex)
      {
        MessageBox.Show(
            "Ошибка при перемещении: " + ex.Message,
            "Ошибка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
      }
    }

    private void RefreshListViewAndRestoreSelection(int itemId)
    {
      _listView.BeginUpdate();
      try
      {
        int topIndex = _listView.TopItem?.Index ?? 0;
        RefreshListView();

        foreach (ListViewItem item in _listView.Items)
        {
          if (GetItemId(item) == itemId)
          {
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            if (item.Index > topIndex && topIndex < _listView.Items.Count)
              _listView.TopItem = _listView.Items[topIndex];
            break;
          }
        }
      }
      finally
      {
        _listView.EndUpdate();
      }
    }

    private void InitializeContextMenu()
    {
      Image addIcon = TryLoadMenuBitmap("Add.png");
      Image editIcon = TryLoadMenuBitmap("Modify.png");
      Image deleteIcon = TryLoadMenuBitmap("Erase.png");
      Image copyIcon = TryLoadMenuBitmap("Copy.png");
      Image upIcon = TryLoadMenuBitmap("Up.png");
      Image downIcon = TryLoadMenuBitmap("Down.png");

      var menu = new ContextMenuStrip();
      menu.Items.Add(new ToolStripMenuItem("Добавить группу", addIcon, (s, e) => AddGroup()));
      menu.Items.Add(new ToolStripMenuItem("Редактировать группу", editIcon, (s, e) => EditGroup()));
      menu.Items.Add(new ToolStripMenuItem("Копировать группу", copyIcon, (s, e) => CopyGroup()));
      menu.Items.Add(new ToolStripMenuItem("Удалить группу", deleteIcon, (s, e) => DeleteGroup()));
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(new ToolStripMenuItem("Добавить ТТ", addIcon, (s, e) => AddItem()));
      menu.Items.Add(new ToolStripMenuItem("Редактировать ТТ", editIcon, (s, e) => EditItem()));
      menu.Items.Add(new ToolStripMenuItem("Удалить ТТ", deleteIcon, (s, e) => DeleteItems()));
      menu.Items.Add(new ToolStripSeparator());
      _miMoveUp = new ToolStripMenuItem("Сдвинуть вверх", upIcon, (s, e) => MoveSelectedItem(-1));
      _miMoveDown = new ToolStripMenuItem("Сдвинуть вниз", downIcon, (s, e) => MoveSelectedItem(1));
      menu.Items.Add(_miMoveUp);
      menu.Items.Add(_miMoveDown);
      menu.Items.Add(new ToolStripSeparator());
      Image filterIcon = TryLoadMenuBitmap("Thumbs up.png");
      Image excludeIcon = TryLoadMenuBitmap("Thumbs down.png");
      _listCellFilterMenu = new VelumListViewCellFilterMenu(
          _listView,
          _listView.FindForm(),
          ResolveTechRequirementsFilterBox,
          ApplyFiltersFromUi,
          null,
          null);
      _listCellFilterMenu.InsertInto(menu, menu.Items.Count, filterIcon, excludeIcon);
      menu.Opening += OnContextMenuOpening;
      _listView.ContextMenuStrip = menu;
    }

    private TextBox ResolveTechRequirementsFilterBox(int column)
    {
      // col0 = Id, col1 = текст ТТ; группа фильтруется отдельно по имени группы.
      if (column == 1)
        return _itemFilterTextBox;
      return null;
    }

    private void OnContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
      // Порядок строк меняется только для выбранной строки ТТ (не для операций с группами).
      bool hasTechRequirementRow = _listView.SelectedItems.Count > 0;
      if (_miMoveUp != null)
        _miMoveUp.Enabled = hasTechRequirementRow;
      if (_miMoveDown != null)
        _miMoveDown.Enabled = hasTechRequirementRow;
    }

    private static Image TryLoadMenuBitmap(string fileName)
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;

        string path = Path.Combine(dir, "icons", fileName);
        return File.Exists(path) ? Image.FromFile(path) : null;
      }
      catch
      {
        return null;
      }
    }

    private void RefreshListView()
    {
      _listView.BeginUpdate();
      try
      {
        _listView.Items.Clear();
        _listView.Groups.Clear();

        string groupFilter = _activeGroupFilter;
        string itemFilter = _activeItemFilter;

        foreach (VelumTechRequirementsGroup group in _store.Groups)
        {
          if (group == null)
            continue;

          string groupName = group.Name ?? "Без имени";
          if (!VelumListFilterHelper.Matches(groupName, groupFilter))
            continue;

          var listViewGroup = new ListViewGroup(groupName, groupName);
          _listView.Groups.Add(listViewGroup);

          if (group.Items == null)
            continue;

          foreach (VelumTechRequirementsItem item in group.Items)
          {
            if (item == null)
              continue;

            string itemText = item.Text ?? string.Empty;
            if (!VelumListFilterHelper.Matches(itemText, itemFilter))
              continue;

            var listItem = new ListViewItem(new[] { item.Id.ToString(), itemText })
            {
              Group = listViewGroup,
              Tag = item.Id
            };
            _listView.Items.Add(listItem);
          }
        }
      }
      finally
      {
        _listView.EndUpdate();
        AdjustColumns();
      }
    }

    private static int GetItemId(ListViewItem item)
    {
      if (item?.Tag is int id)
        return id;
      if (item?.Tag != null && int.TryParse(item.Tag.ToString(), out int parsed))
        return parsed;
      return 0;
    }

    private void AddGroup()
    {
      using (var form = new VelumTechRequirementsInputForm("Добавить группу", "Название группы:"))
      {
        if (form.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(form.InputText))
          return;

        if (_store.GroupNameExists(form.InputText))
        {
          MessageBox.Show("Группа с таким именем уже существует!", "Ошибка",
              MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }

        var group = new VelumTechRequirementsGroup
        {
          Name = form.InputText.Trim(),
          Items = new List<VelumTechRequirementsItem>
          {
            new VelumTechRequirementsItem
            {
              Id = _store.AllocateItemId(),
              Text = "Новое ТТ"
            }
          }
        };
        _store.AddGroup(group);
        _store.Save();
        RefreshListView();
      }
    }

    private void EditGroup()
    {
      if (_listView.SelectedItems.Count == 0)
      {
        MessageBox.Show(
            "Выделите любой элемент в группе, которую нужно отредактировать.",
            "Не выбрана группа",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      string oldGroupName = _listView.SelectedItems[0].Group.Header;
      VelumTechRequirementsGroup group = _store.FindGroup(oldGroupName);
      if (group == null)
        return;

      using (var form = new VelumTechRequirementsInputForm(
          "Редактировать группу", "Новое название группы:", oldGroupName))
      {
        if (form.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(form.InputText))
          return;

        string newName = form.InputText.Trim();
        if (_store.GroupNameExists(newName, group))
        {
          MessageBox.Show("Группа с таким именем уже существует!", "Ошибка",
              MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }

        group.Name = newName;
        _store.Save();
        RefreshListView();
      }
    }

    private void DeleteGroup()
    {
      if (_listView.SelectedItems.Count == 0)
      {
        MessageBox.Show(
            "Выделите любой элемент в группе, которую нужно удалить.",
            "Не выбрана группа",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      string groupName = _listView.SelectedItems[0].Group.Header;
      VelumTechRequirementsGroup group = _store.FindGroup(groupName);
      if (group == null)
        return;

      int itemsCount = group.Items?.Count ?? 0;
      if (MessageBox.Show(
              "Удалить группу '" + groupName + "' со всеми её " + itemsCount + " элементами?",
              "Подтверждение удаления",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Warning) != DialogResult.Yes)
        return;

      _store.RemoveGroup(group);
      _store.Save();
      RefreshListView();
    }

    private void CopyGroup()
    {
      if (_listView.SelectedItems.Count == 0)
      {
        MessageBox.Show(
            "Выделите любой элемент в группе, которую нужно копировать.",
            "Не выбрана группа",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      string groupName = _listView.SelectedItems[0].Group.Header;
      VelumTechRequirementsGroup group = _store.FindGroup(groupName);
      if (group == null)
      {
        MessageBox.Show("Группа не найдена в данных.", "Ошибка",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      string newGroupNameBase = groupName + "_copy";
      string newGroupName = newGroupNameBase;
      int counter = 2;
      while (_store.GroupNameExists(newGroupName))
      {
        newGroupName = newGroupNameBase + counter;
        counter++;
      }

      if (MessageBox.Show(
              "Создать копию группы '" + groupName + "' с именем '" + newGroupName + "'?",
              "Подтверждение копирования",
              MessageBoxButtons.OKCancel,
              MessageBoxIcon.Question) != DialogResult.OK)
        return;

      var newGroup = new VelumTechRequirementsGroup { Name = newGroupName };
      if (group.Items != null)
      {
        foreach (VelumTechRequirementsItem item in group.Items)
        {
          if (item == null)
            continue;
          newGroup.Items.Add(new VelumTechRequirementsItem
          {
            Id = _store.AllocateItemId(),
            Text = item.Text
          });
        }
      }

      _store.AddGroup(newGroup);
      _store.Save();
      RefreshListView();
    }

    private void AddItem()
    {
      if (_listView.SelectedItems.Count == 0 && _listView.Groups.Count > 0)
      {
        MessageBox.Show(
            "Выберите элемент, чтобы определить группу для добавления.",
            "Информация",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (_store.Groups.Count == 0)
      {
        MessageBox.Show(
            "Сначала добавьте группу.",
            "Информация",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      string groupName = _listView.SelectedItems.Count > 0
          ? _listView.SelectedItems[0].Group.Header
          : _listView.Groups[0].Header;

      VelumTechRequirementsGroup group = _store.FindGroup(groupName);
      if (group == null)
        return;

      using (var form = new VelumTechRequirementsInputForm("Добавить требование", "Текст требования:"))
      {
        if (form.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(form.InputText))
          return;

        if (group.Items == null)
          group.Items = new List<VelumTechRequirementsItem>();

        group.Items.Add(new VelumTechRequirementsItem
        {
          Id = _store.AllocateItemId(),
          Text = form.InputText
        });
        _store.Save();
        RefreshListView();
      }
    }

    private void EditItem()
    {
      if (_listView.SelectedItems.Count == 0)
        return;

      ListViewItem selectedItem = _listView.SelectedItems[0];
      string oldText = selectedItem.SubItems[1].Text;
      string groupName = selectedItem.Group.Header;
      int itemId = GetItemId(selectedItem);

      VelumTechRequirementsItem item = _store.FindItem(groupName, itemId);
      if (item == null)
        return;

      using (var form = new VelumTechRequirementsInputForm(
          "Редактировать требование", "Новый текст требования:", oldText))
      {
        if (form.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(form.InputText))
          return;

        item.Text = form.InputText;
        _store.Save();
        RefreshListView();
      }
    }

    private void DeleteItems()
    {
      if (_listView.SelectedItems.Count == 0)
        return;

      if (MessageBox.Show(
              "Удалить выделенные элементы?",
              "Подтверждение удаления",
              MessageBoxButtons.YesNo,
              MessageBoxIcon.Warning) != DialogResult.Yes)
        return;

      List<ListViewItem> itemsToDelete = _listView.SelectedItems.Cast<ListViewItem>().ToList();
      var byGroup = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

      foreach (ListViewItem listItem in itemsToDelete)
      {
        string groupName = listItem.Group?.Header;
        if (string.IsNullOrEmpty(groupName))
          continue;

        HashSet<int> ids;
        if (!byGroup.TryGetValue(groupName, out ids))
        {
          ids = new HashSet<int>();
          byGroup[groupName] = ids;
        }

        ids.Add(GetItemId(listItem));
      }

      foreach (KeyValuePair<string, HashSet<int>> pair in byGroup)
      {
        VelumTechRequirementsGroup group = _store.FindGroup(pair.Key);
        if (group?.Items == null)
          continue;
        group.Items.RemoveAll(i => i != null && pair.Value.Contains(i.Id));

        if (group.Items.Count == 0)
          _store.RemoveGroup(group);
      }

      _store.Save();
      RefreshListView();
    }

    internal void ImportFromTextFile()
    {
      using (var openFileDialog = new OpenFileDialog())
      {
        openFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
        openFileDialog.Title = "Выберите файл для импорта";

        if (openFileDialog.ShowDialog() != DialogResult.OK)
          return;

        try
        {
          string[] fileContent = File.ReadAllLines(openFileDialog.FileName);
          Dictionary<string, List<string>> groups = ParseTextFile(fileContent);

          if (groups.Count == 0)
          {
            MessageBox.Show(
                "Файл не содержит данных для импорта в правильном формате.\n\n" +
                "Правильный формат:\n\n" +
                "@Группа 1\nстрока 1\nстрока 2\n@Группа 2\nстрока 3",
                "Ошибка формата",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
          }

          HashSet<string> existingNames = new HashSet<string>(
              _store.Groups.Select(g => g.Name),
              StringComparer.Ordinal);

          List<KeyValuePair<string, List<string>>> groupsToAdd = groups
              .Where(g => !existingNames.Contains(g.Key))
              .ToList();
          List<KeyValuePair<string, List<string>>> duplicateGroups = groups
              .Where(g => existingNames.Contains(g.Key))
              .ToList();

          if (duplicateGroups.Count > 0)
          {
            MessageBox.Show(
                "Следующие группы уже существуют и не будут импортированы:\n" +
                string.Join("\n", duplicateGroups.Select(g => g.Key)),
                "Предупреждение",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
          }

          if (groupsToAdd.Count == 0)
          {
            MessageBox.Show("Нет новых групп для импорта.", "Информация",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
          }

          if (MessageBox.Show(
                  "Импортировать выбранный файл в список тех. требований?",
                  "Подтверждение импорта",
                  MessageBoxButtons.YesNo,
                  MessageBoxIcon.Question) != DialogResult.Yes)
            return;

          foreach (KeyValuePair<string, List<string>> pair in groupsToAdd)
          {
            var newGroup = new VelumTechRequirementsGroup { Name = pair.Key };
            foreach (string text in pair.Value)
            {
              newGroup.Items.Add(new VelumTechRequirementsItem
              {
                Id = _store.AllocateItemId(),
                Text = text
              });
            }

            _store.AddGroup(newGroup);
          }

          _store.Save();
          RefreshListView();

          MessageBox.Show(
              "Успешно импортировано " + groupsToAdd.Count + " групп.",
              "Импорт завершен",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
          MessageBox.Show(
              "Ошибка при импорте файла: " + ex.Message,
              "Ошибка",
              MessageBoxButtons.OK,
              MessageBoxIcon.Error);
        }
      }
    }

    private static Dictionary<string, List<string>> ParseTextFile(string[] lines)
    {
      var groups = new Dictionary<string, List<string>>();
      string currentGroup = null;
      List<string> currentItems = null;

      foreach (string line in lines)
      {
        string trimmedLine = line.Trim();
        if (trimmedLine.StartsWith("@", StringComparison.Ordinal))
        {
          currentGroup = trimmedLine.Substring(1).Trim();
          currentItems = new List<string>();
          groups[currentGroup] = currentItems;
        }
        else if (trimmedLine.StartsWith("*", StringComparison.Ordinal))
        {
          if (currentGroup != null)
            currentItems.Add(trimmedLine.Substring(1).Trim());
        }
        else if (!string.IsNullOrWhiteSpace(trimmedLine) && currentGroup != null)
        {
          currentItems.Add(trimmedLine);
        }
      }

      return groups;
    }
  }
}
