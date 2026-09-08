using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ISIDA.Common;
using Newtonsoft.Json;
using Velum.Configuration;

namespace Velum.UI
{
  /// <summary>
  /// Хранилище шаблонов ТТ в JSON
  /// (<c>techRequirements.json</c> в <see cref="VelumAppConfig.TechRequirementsFolderPath"/>).
  /// </summary>
  internal sealed class VelumTechRequirementsStore
  {
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
      Formatting = Formatting.Indented,
      NullValueHandling = NullValueHandling.Ignore
    };

    private VelumTechRequirementsFile _data = new VelumTechRequirementsFile();

    internal static string FolderPath => VelumAppConfig.TechRequirementsFolderPath;

    internal static string FilePath => Path.Combine(FolderPath, "techRequirements.json");

    internal IReadOnlyList<VelumTechRequirementsGroup> Groups =>
        _data.Groups ?? (IReadOnlyList<VelumTechRequirementsGroup>)Array.Empty<VelumTechRequirementsGroup>();

    internal void Load()
    {
      Directory.CreateDirectory(FolderPath);

      if (!File.Exists(FilePath))
      {
        _data = new VelumTechRequirementsFile();
        Save();
        return;
      }

      try
      {
        string json = File.ReadAllText(FilePath);
        _data = JsonConvert.DeserializeObject<VelumTechRequirementsFile>(json, JsonSettings)
            ?? new VelumTechRequirementsFile();
      }
      catch (Exception ex)
      {
        Logger.Error("TechRequirements load failed: " + ex.Message);
        _data = new VelumTechRequirementsFile();
      }

      Normalize();
    }

    internal void Save()
    {
      Directory.CreateDirectory(FolderPath);
      Normalize();
      string json = JsonConvert.SerializeObject(_data, JsonSettings);
      File.WriteAllText(FilePath, json);
    }

    internal VelumTechRequirementsGroup FindGroup(string name)
    {
      if (string.IsNullOrEmpty(name) || _data.Groups == null)
        return null;

      return _data.Groups.FirstOrDefault(g =>
          string.Equals(g.Name, name, StringComparison.Ordinal));
    }

    internal bool GroupNameExists(string name, VelumTechRequirementsGroup except = null)
    {
      if (_data.Groups == null)
        return false;

      return _data.Groups.Any(g =>
          g != except && string.Equals(g.Name, name, StringComparison.Ordinal));
    }

    internal void AddGroup(VelumTechRequirementsGroup group)
    {
      if (group == null)
        return;
      if (_data.Groups == null)
        _data.Groups = new List<VelumTechRequirementsGroup>();
      _data.Groups.Add(group);
    }

    internal void RemoveGroup(VelumTechRequirementsGroup group)
    {
      if (group == null || _data.Groups == null)
        return;
      _data.Groups.Remove(group);
    }

    internal int AllocateItemId()
    {
      int id = Math.Max(1, _data.NextItemId);
      _data.NextItemId = id + 1;
      return id;
    }

    internal VelumTechRequirementsItem FindItem(string groupName, int itemId)
    {
      VelumTechRequirementsGroup group = FindGroup(groupName);
      if (group?.Items == null)
        return null;
      return group.Items.FirstOrDefault(i => i.Id == itemId);
    }

    private void Normalize()
    {
      if (_data == null)
        _data = new VelumTechRequirementsFile();
      if (_data.Groups == null)
        _data.Groups = new List<VelumTechRequirementsGroup>();

      int maxId = Math.Max(0, _data.NextItemId - 1);
      foreach (VelumTechRequirementsGroup group in _data.Groups)
      {
        if (group == null)
          continue;
        if (string.IsNullOrWhiteSpace(group.Name))
          group.Name = "Без имени";
        if (group.Items == null)
          group.Items = new List<VelumTechRequirementsItem>();

        foreach (VelumTechRequirementsItem item in group.Items)
        {
          if (item == null)
            continue;
          if (item.Text == null)
            item.Text = string.Empty;
          if (item.Id > maxId)
            maxId = item.Id;
        }
      }

      foreach (VelumTechRequirementsGroup group in _data.Groups)
      {
        if (group?.Items == null)
          continue;
        foreach (VelumTechRequirementsItem item in group.Items)
        {
          if (item == null || item.Id > 0)
            continue;
          maxId++;
          item.Id = maxId;
        }
      }

      _data.NextItemId = maxId + 1;

      _data.Groups.RemoveAll(g => g == null || g.Items == null || g.Items.Count == 0);
    }
  }
}
