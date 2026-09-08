using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Выбор каталога реестра изделия (только folder-узлы).</summary>
  internal sealed partial class VelumAssemblyRegistryFolderPickerForm : Form
  {
    private readonly List<VelumAssemblyRegistrySectionPath> _folders;
    private readonly VelumAssemblyRegistrySectionPath _initial;
    private VelumAssemblyRegistrySectionPath _selected;

    public VelumAssemblyRegistrySectionPath SelectedPath { get; private set; }

    public VelumAssemblyRegistryFolderPickerForm()
    {
      InitializeComponent();
      _folders = new List<VelumAssemblyRegistrySectionPath>();
    }

    public VelumAssemblyRegistryFolderPickerForm(
        IEnumerable<VelumAssemblyRegistrySectionPath> folders,
        VelumAssemblyRegistrySectionPath initialSelected)
        : this()
    {
      _folders = new List<VelumAssemblyRegistrySectionPath>();
      if (folders != null)
      {
        foreach (VelumAssemblyRegistrySectionPath path in folders)
        {
          if (path != null)
            _folders.Add(path);
        }
      }

      _initial = initialSelected;
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

      EnsureRoot(_folders, VelumAssemblyRegistryFamily.Assembly);
      EnsureRoot(_folders, VelumAssemblyRegistryFamily.Part);
      EnsureRoot(_folders, VelumAssemblyRegistryFamily.Standard);

      _folderTreeView.BeginUpdate();
      try
      {
        _folderTreeView.Nodes.Clear();
        BuildFamilyRoot(VelumAssemblyRegistryFamily.Assembly, VelumAssemblyRegistrySectionPath.PrefixAssemblies);
        BuildFamilyRoot(VelumAssemblyRegistryFamily.Part, VelumAssemblyRegistrySectionPath.PrefixParts);
        BuildFamilyRoot(VelumAssemblyRegistryFamily.Standard, VelumAssemblyRegistrySectionPath.PrefixStandards);
        _folderTreeView.CollapseAll();
      }
      finally
      {
        _folderTreeView.EndUpdate();
      }

      TreeNode select = _initial != null ? FindNode(_folderTreeView.Nodes, _initial) : null;
      if (select == null && _folderTreeView.Nodes.Count > 0)
        select = _folderTreeView.Nodes[0];
      if (select != null)
      {
        ExpandParents(select);
        _folderTreeView.SelectedNode = select;
        select.EnsureVisible();
        _selected = select.Tag as VelumAssemblyRegistrySectionPath;
      }

      _folderTreeView.AfterSelect += (s, e) =>
      {
        _selected = e.Node != null ? e.Node.Tag as VelumAssemblyRegistrySectionPath : null;
      };
      _folderTreeView.NodeMouseDoubleClick += (s, e) =>
      {
        if (e.Node != null && e.Node.Tag is VelumAssemblyRegistrySectionPath)
        {
          _selected = (VelumAssemblyRegistrySectionPath)e.Node.Tag;
          ApplySelection();
        }
      };
    }

    private void BuildFamilyRoot(VelumAssemblyRegistryFamily family, string title)
    {
      var rootPath = new VelumAssemblyRegistrySectionPath(family, Array.Empty<string>());
      var root = new TreeNode(title) { Tag = rootPath };
      foreach (VelumAssemblyRegistrySectionPath path in _folders)
      {
        if (path == null || path.Family != family || path.IsRoot)
          continue;
        EnsureNode(root, path);
      }

      _folderTreeView.Nodes.Add(root);
    }

    private static void EnsureNode(TreeNode familyRoot, VelumAssemblyRegistrySectionPath path)
    {
      TreeNode current = familyRoot;
      var built = new List<string>();
      for (int i = 0; i < path.Segments.Length; i++)
      {
        built.Add(path.Segments[i]);
        TreeNode child = FindChild(current, path.Segments[i]);
        if (child == null)
        {
          var childPath = new VelumAssemblyRegistrySectionPath(path.Family, built.ToArray());
          child = new TreeNode(path.Segments[i]) { Tag = childPath };
          current.Nodes.Add(child);
        }

        current = child;
      }
    }

    private static TreeNode FindChild(TreeNode parent, string name)
    {
      foreach (TreeNode node in parent.Nodes)
      {
        if (string.Equals(node.Text, name, StringComparison.OrdinalIgnoreCase))
          return node;
      }

      return null;
    }

    private static void EnsureRoot(List<VelumAssemblyRegistrySectionPath> folders, VelumAssemblyRegistryFamily family)
    {
      foreach (VelumAssemblyRegistrySectionPath path in folders)
      {
        if (path != null && path.Family == family && path.IsRoot)
          return;
      }

      folders.Add(new VelumAssemblyRegistrySectionPath(family, Array.Empty<string>()));
    }

    private static TreeNode FindNode(TreeNodeCollection nodes, VelumAssemblyRegistrySectionPath path)
    {
      foreach (TreeNode node in nodes)
      {
        var tag = node.Tag as VelumAssemblyRegistrySectionPath;
        if (tag != null && tag.Equals(path))
          return node;
        TreeNode nested = FindNode(node.Nodes, path);
        if (nested != null)
          return nested;
      }

      return null;
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

    private void OnApply(object sender, EventArgs e)
    {
      ApplySelection();
    }

    private void ApplySelection()
    {
      if (_selected == null && _folderTreeView.SelectedNode != null)
        _selected = _folderTreeView.SelectedNode.Tag as VelumAssemblyRegistrySectionPath;

      if (_selected == null)
      {
        MessageBox.Show(
            this,
            "Выберите каталог в дереве.",
            "Изменить каталог",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return;
      }

      SelectedPath = _selected;
      DialogResult = DialogResult.OK;
      Close();
    }

    private void OnCloseClick(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }

    private static Icon TryLoadFormIcon()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;
        string path = Path.Combine(dir, "icons", "Folder.png");
        if (!File.Exists(path))
          return null;
        using (var bmp = new Bitmap(path))
        {
          IntPtr handle = bmp.GetHicon();
          using (Icon temp = Icon.FromHandle(handle))
            return (Icon)temp.Clone();
        }
      }
      catch
      {
        return null;
      }
    }
  }
}
