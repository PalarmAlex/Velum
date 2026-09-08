using System;
using System.Collections.Generic;

namespace Velum.UI
{
  /// <summary>Группа шаблонов технических требований.</summary>
  internal sealed class VelumTechRequirementsGroup
  {
    public string Name { get; set; }

    public List<VelumTechRequirementsItem> Items { get; set; } = new List<VelumTechRequirementsItem>();
  }

  /// <summary>Один шаблон технического требования.</summary>
  internal sealed class VelumTechRequirementsItem
  {
    public int Id { get; set; }

    public string Text { get; set; }
  }

  /// <summary>Контейнер файла шаблонов ТТ.</summary>
  internal sealed class VelumTechRequirementsFile
  {
    public int NextItemId { get; set; } = 1;

    public List<VelumTechRequirementsGroup> Groups { get; set; } = new List<VelumTechRequirementsGroup>();
  }
}
