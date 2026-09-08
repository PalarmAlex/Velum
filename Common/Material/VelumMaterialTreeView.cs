using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>Дерево материалов SolidWorks с поиском и навигацией.</summary>
  internal sealed class VelumMaterialTreeView : IDisposable
  {
    private readonly TreeView _treeView;
    private readonly VelumMaterialDatabaseManager _materialManager;
    private readonly Image _folderIcon;
    private readonly Image _databaseIcon;
    private readonly Image _categoryIcon;
    private readonly Image _materialIcon;
    private List<TreeNode> _searchResults = new List<TreeNode>();
    private int _currentSearchIndex = -1;

    internal TextBox SearchTextBox { get; private set; }
    internal Button SearchButton { get; private set; }
    internal Button NextButton { get; private set; }
    internal Button PrevButton { get; private set; }
    internal Label StatusLabel { get; private set; }

    internal VelumMaterialTreeView(
        TreeView existingTreeView,
        VelumMaterialDatabaseManager materialManager,
        Image folderIcon,
        Image databaseIcon,
        Image categoryIcon,
        Image materialIcon)
    {
      _materialManager = materialManager ?? throw new ArgumentNullException(nameof(materialManager));
      _folderIcon = folderIcon;
      _databaseIcon = databaseIcon;
      _categoryIcon = categoryIcon;
      _materialIcon = materialIcon;
      _treeView = existingTreeView ?? throw new ArgumentNullException(nameof(existingTreeView));
      InitializeTreeView();
    }

    internal TreeView TreeViewControl => _treeView;

    private void InitializeTreeView()
    {
      _treeView.BeginUpdate();
      _treeView.Nodes.Clear();

      _treeView.ImageList = new ImageList
      {
        ColorDepth = ColorDepth.Depth32Bit,
        ImageSize = new Size(16, 16)
      };

      _treeView.ItemHeight = 21;
      _treeView.ShowLines = true;
      _treeView.ShowPlusMinus = true;
      _treeView.FullRowSelect = true;
      _treeView.HotTracking = true;
      _treeView.HideSelection = false;

      _treeView.ImageList.Images.Add("Folder", _folderIcon);
      _treeView.ImageList.Images.Add("Database", _databaseIcon);
      _treeView.ImageList.Images.Add("Category", _categoryIcon);
      _treeView.ImageList.Images.Add("Material", _materialIcon);

      _treeView.EndUpdate();
      _treeView.BeforeExpand += TreeView_BeforeExpand;
    }

    internal void BindSearchControls(
        TextBox searchBox,
        Button searchBtn,
        Button nextBtn,
        Button prevBtn,
        Label statusLabel)
    {
      SearchTextBox = searchBox;
      SearchButton = searchBtn;
      NextButton = nextBtn;
      PrevButton = prevBtn;
      StatusLabel = statusLabel;

      SearchButton.Click += (s, e) => SearchMaterials();
      NextButton.Click += (s, e) => ShowNextResult();
      PrevButton.Click += (s, e) => ShowPreviousResult();

      SearchTextBox.KeyDown += (s, e) =>
      {
        if (e.KeyCode == Keys.Enter)
          SearchMaterials();
      };

      SearchTextBox.TextChanged += (s, e) =>
      {
        if (string.IsNullOrEmpty(SearchTextBox.Text))
        {
          _searchResults.Clear();
          _currentSearchIndex = -1;
          if (StatusLabel != null)
            StatusLabel.Text = string.Empty;
        }
      };
    }

    internal void LoadMaterialDatabases()
    {
      _treeView.BeginUpdate();
      _treeView.Nodes.Clear();

      Dictionary<string, List<string>> materials = _materialManager.LoadMaterialsFromFiles();
      string[] dbPaths = _materialManager.GetMaterialDatabasePaths();

      foreach (KeyValuePair<string, List<string>> db in materials)
      {
        string dbPath = dbPaths.FirstOrDefault(p =>
            Path.GetFileNameWithoutExtension(p).Equals(db.Key, StringComparison.OrdinalIgnoreCase));

        var dbNode = new TreeNode(db.Key)
        {
          ImageKey = "Database",
          SelectedImageKey = "Database",
          Tag = new VelumMaterialInfo
          {
            IsDatabase = true,
            Name = db.Key,
            DatabasePath = dbPath
          }
        };

        dbNode.Nodes.Add(new TreeNode("Loading...") { Tag = null });
        _treeView.Nodes.Add(dbNode);
      }

      _treeView.EndUpdate();
    }

    private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
    {
      if (e.Node.Nodes.Count != 1 || e.Node.Nodes[0].Tag != null)
        return;

      e.Node.Nodes.Clear();
      var nodeInfo = e.Node.Tag as VelumMaterialInfo;
      if (nodeInfo == null)
        return;

      if (nodeInfo.IsDatabase)
        LoadCategories(e.Node, nodeInfo);
      else if (nodeInfo.IsCategory)
        LoadMaterials(e.Node, nodeInfo);
    }

    private void LoadCategories(TreeNode dbNode, VelumMaterialInfo dbInfo)
    {
      Dictionary<string, List<string>> materials = _materialManager.LoadMaterialsFromFiles(dbInfo.Name);
      if (!materials.TryGetValue(dbInfo.Name, out List<string> categories))
        return;

      for (int i = 0; i < categories.Count; i++)
      {
        string item = categories[i];
        if (item.StartsWith("  ", StringComparison.Ordinal))
          continue;

        var categoryNode = new TreeNode(item)
        {
          ImageKey = "Category",
          SelectedImageKey = "Category",
          Tag = new VelumMaterialInfo
          {
            IsCategory = true,
            Name = item,
            DatabaseName = dbInfo.Name,
            DatabasePath = dbInfo.DatabasePath
          }
        };

        categoryNode.Nodes.Add(new TreeNode("Loading...") { Tag = null });
        dbNode.Nodes.Add(categoryNode);
      }
    }

    private void LoadMaterials(TreeNode categoryNode, VelumMaterialInfo categoryInfo)
    {
      Dictionary<string, List<string>> materials = _materialManager.LoadMaterialsFromFiles(categoryInfo.DatabaseName);
      if (!materials.TryGetValue(categoryInfo.DatabaseName, out List<string> categories))
        return;

      bool categoryFound = false;
      for (int i = 0; i < categories.Count; i++)
      {
        string item = categories[i];
        if (item == categoryInfo.Name)
        {
          categoryFound = true;
          continue;
        }

        if (categoryFound && item.StartsWith("  ", StringComparison.Ordinal))
        {
          string materialName = item.Trim();
          categoryNode.Nodes.Add(CreateMaterialNode(materialName, categoryInfo));
        }
        else if (categoryFound)
        {
          break;
        }
      }
    }

    private TreeNode CreateMaterialNode(string materialName, VelumMaterialInfo parentInfo)
    {
      return new TreeNode(materialName)
      {
        ImageKey = "Material",
        SelectedImageKey = "Material",
        Tag = new VelumMaterialInfo
        {
          DatabaseName = parentInfo.DatabaseName,
          Category = parentInfo.Name,
          MaterialName = materialName,
          DatabasePath = parentInfo.DatabasePath
        }
      };
    }

    internal VelumMaterialInfo GetSelectedMaterial()
    {
      if (!(_treeView.SelectedNode?.Tag is VelumMaterialInfo materialInfo))
        return null;

      if (materialInfo.MaterialName == null && !string.IsNullOrEmpty(_treeView.SelectedNode.Text))
      {
        TreeNode parent = _treeView.SelectedNode.Parent;
        if (parent?.Tag is VelumMaterialInfo parentInfo)
        {
          materialInfo = new VelumMaterialInfo
          {
            DatabaseName = parentInfo.DatabaseName,
            Category = parentInfo.Name,
            MaterialName = _treeView.SelectedNode.Text.Trim(),
            DatabasePath = parentInfo.DatabasePath
          };
          _treeView.SelectedNode.Tag = materialInfo;
        }
      }

      if (string.IsNullOrWhiteSpace(materialInfo.MaterialName))
        return null;

      return materialInfo;
    }

    private void SearchMaterials()
    {
      string searchText = SearchTextBox?.Text.Trim();
      if (string.IsNullOrEmpty(searchText))
      {
        MessageBox.Show(
            "Введите текст для поиска.",
            "Пакетное присвоение материалов",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      ClearHighlights();
      _treeView.CollapseAll();
      _searchResults = FindNodes(_treeView.Nodes, searchText);
      _currentSearchIndex = -1;

      if (_searchResults.Count == 0)
      {
        if (StatusLabel != null)
          StatusLabel.Text = "Совпадений не найдено";
        return;
      }

      ShowNextResult();
    }

    private List<TreeNode> FindNodes(TreeNodeCollection nodes, string searchText)
    {
      var matches = new List<TreeNode>();

      foreach (TreeNode node in nodes)
      {
        if (node.Nodes.Count == 1 && node.Nodes[0].Tag == null)
        {
          var args = new TreeViewCancelEventArgs(node, false, TreeViewAction.Expand);
          TreeView_BeforeExpand(null, args);
        }

        if (node.Tag is VelumMaterialInfo info &&
            info.MaterialName != null &&
            info.MaterialName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
          matches.Add(node);

        matches.AddRange(FindNodes(node.Nodes, searchText));
      }

      return matches;
    }

    private void ShowNextResult()
    {
      if (_searchResults.Count == 0)
        return;

      _currentSearchIndex++;
      if (_currentSearchIndex >= _searchResults.Count)
        _currentSearchIndex = 0;

      ShowCurrentResult();
    }

    private void ShowPreviousResult()
    {
      if (_searchResults.Count == 0)
        return;

      _currentSearchIndex--;
      if (_currentSearchIndex < 0)
        _currentSearchIndex = _searchResults.Count - 1;

      ShowCurrentResult();
    }

    private void ShowCurrentResult()
    {
      if (_currentSearchIndex < 0 || _currentSearchIndex >= _searchResults.Count)
        return;

      for (int i = 0; i < _searchResults.Count; i++)
      {
        TreeNode resultNode = _searchResults[i];
        resultNode.BackColor = _treeView.BackColor;
        resultNode.ForeColor = _treeView.ForeColor;
        resultNode.NodeFont = _treeView.Font;
      }

      TreeNode currentNode = _searchResults[_currentSearchIndex];
      if (currentNode.Tag == null && !string.IsNullOrEmpty(currentNode.Text))
      {
        TreeNode parent = currentNode.Parent;
        if (parent?.Tag is VelumMaterialInfo parentInfo)
        {
          currentNode.Tag = new VelumMaterialInfo
          {
            DatabaseName = parentInfo.DatabaseName,
            Category = parentInfo.Name,
            MaterialName = currentNode.Text.Trim(),
            DatabasePath = parentInfo.DatabasePath
          };
        }
      }

      currentNode.BackColor = Color.Yellow;
      currentNode.ForeColor = Color.Black;
      currentNode.NodeFont = new Font(_treeView.Font, FontStyle.Bold);

      ExpandParentNodes(currentNode);
      _treeView.SelectedNode = currentNode;
      currentNode.EnsureVisible();

      if (StatusLabel != null)
        StatusLabel.Text = "Найдено: " + (_currentSearchIndex + 1) + " из " + _searchResults.Count;
    }

    private void ClearHighlights()
    {
      foreach (TreeNode node in _treeView.Nodes)
        ClearNodeHighlight(node);
    }

    private void ClearNodeHighlight(TreeNode node)
    {
      node.BackColor = _treeView.BackColor;
      node.ForeColor = _treeView.ForeColor;
      node.NodeFont = _treeView.Font;

      foreach (TreeNode child in node.Nodes)
        ClearNodeHighlight(child);
    }

    private void ExpandParentNodes(TreeNode node)
    {
      if (node.Parent == null)
        return;

      ExpandParentNodes(node.Parent);
      node.Parent.Expand();
    }

    public void Dispose()
    {
      if (_treeView != null)
        _treeView.BeforeExpand -= TreeView_BeforeExpand;
    }
  }

  internal sealed class VelumMaterialInfo
  {
    internal bool IsDatabase { get; set; }

    internal bool IsCategory { get; set; }

    internal string Name { get; set; }

    internal string DatabaseName { get; set; }

    internal string Category { get; set; }

    internal string MaterialName { get; set; }

    internal string DatabasePath { get; set; }
  }
}
