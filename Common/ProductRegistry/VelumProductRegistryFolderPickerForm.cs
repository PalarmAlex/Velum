using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.UI.ProductRegistry;

namespace Velum.UI
{
  /// <summary>Выбор каталога реестра изделий из дерева.</summary>
  internal sealed partial class VelumProductRegistryFolderPickerForm : Form
  {
    private readonly VelumProductRegistryStore _store;
    private readonly int? _initialFolderId;
    private int? _selectedFolderId;

    public int? SelectedFolderId { get; private set; }

    public VelumProductRegistryFolderPickerForm()
    {
      InitializeComponent();
    }

    public VelumProductRegistryFolderPickerForm(VelumProductRegistryStore store, int? selectedFolderId)
        : this()
    {
      _store = store ?? throw new ArgumentNullException(nameof(store));
      _initialFolderId = selectedFolderId;
      InitializeRuntime();
    }

    private void InitializeRuntime()
    {
      Icon icon = TryLoadFormIcon();
      if (icon != null)
        Icon = icon;

      var tip = new ToolTip();
      tip.SetToolTip(_btnApply, "Выбрать каталог");
      tip.SetToolTip(_btnClose, "Закрыть");

      _folderTreeView.BeginUpdate();
      try
      {
        _folderTreeView.Nodes.Clear();
        foreach (VelumProductFolder folder in _store.GetChildFolders(0))
          _folderTreeView.Nodes.Add(BuildFolderNode(folder));
        _folderTreeView.CollapseAll();
      }
      finally
      {
        _folderTreeView.EndUpdate();
      }

      if (_initialFolderId != null)
      {
        TreeNode node = FindNodeByFolderId(_folderTreeView.Nodes, _initialFolderId.Value);
        if (node != null)
        {
          ExpandParents(node);
          _folderTreeView.SelectedNode = node;
          node.EnsureVisible();
          _selectedFolderId = _initialFolderId;
        }
      }
      else if (_folderTreeView.Nodes.Count > 0)
      {
        _folderTreeView.SelectedNode = _folderTreeView.Nodes[0];
        if (_folderTreeView.SelectedNode.Tag is int)
          _selectedFolderId = (int)_folderTreeView.SelectedNode.Tag;
      }

      _folderTreeView.AfterSelect += (s, e) =>
      {
        if (e.Node != null && e.Node.Tag is int)
          _selectedFolderId = (int)e.Node.Tag;
      };

      _folderTreeView.NodeMouseClick += (s, e) =>
      {
        if (e.Node != null && e.Node.Tag is int)
        {
          _folderTreeView.SelectedNode = e.Node;
          _selectedFolderId = (int)e.Node.Tag;
        }
      };

      _folderTreeView.NodeMouseDoubleClick += (s, e) =>
      {
        if (e.Node != null && e.Node.Tag is int)
        {
          _selectedFolderId = (int)e.Node.Tag;
          ApplySelection();
        }
      };
    }

    private TreeNode BuildFolderNode(VelumProductFolder folder)
    {
      var node = new TreeNode(folder.Name) { Tag = folder.Id };
      foreach (VelumProductFolder child in _store.GetChildFolders(folder.Id))
        node.Nodes.Add(BuildFolderNode(child));
      return node;
    }

    private void OnApply(object sender, EventArgs e)
    {
      ApplySelection();
    }

    private void ApplySelection()
    {
      int? folderId = _selectedFolderId;
      if (folderId == null && _folderTreeView.SelectedNode != null && _folderTreeView.SelectedNode.Tag is int)
        folderId = (int)_folderTreeView.SelectedNode.Tag;

      if (folderId == null)
      {
        MessageBox.Show(
            this,
            "Выберите каталог в дереве.",
            "Связать с узлом",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      if (_store.GetFolder(folderId.Value) == null)
      {
        MessageBox.Show(
            this,
            "Выбранный каталог не найден.",
            "Связать с узлом",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return;
      }

      SelectedFolderId = folderId;
      DialogResult = DialogResult.OK;
      Close();
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }

    private static void ExpandParents(TreeNode node)
    {
      TreeNode parent = node.Parent;
      while (parent != null)
      {
        parent.Expand();
        parent = parent.Parent;
      }
    }

    private static TreeNode FindNodeByFolderId(TreeNodeCollection nodes, int folderId)
    {
      foreach (TreeNode node in nodes)
      {
        if (node.Tag is int && (int)node.Tag == folderId)
          return node;
        TreeNode found = FindNodeByFolderId(node.Nodes, folderId);
        if (found != null)
          return found;
      }

      return null;
    }

    private static Icon TryLoadFormIcon()
    {
      return VelumFormIcon.TryLoad();
    }
  }
}
