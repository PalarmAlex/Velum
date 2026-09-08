using System.Collections.Generic;
using Velum.SolidHomeostasis;
using Velum.UI.AssemblyRegistry;

namespace Velum.UI
{
  /// <summary>Запуск пакетной формы DXF (меню или реестр изделия; состав уже проверен).</summary>
  internal sealed class VelumDxfBomLaunchContext
  {
    /// <summary>Каталог delivery (last-used или пусто). Не подставлять корень сборки по умолчанию.</summary>
    internal string DxfExportFolder { get; set; }

    /// <summary>Корень файла сборки — якорь Обзора.</summary>
    internal string AssemblyFolder { get; set; }

    internal IReadOnlyList<VelumDxfBatchDiagnosticRow> Rows { get; set; }

    /// <summary>Состав для повторной диагностики на форме.</summary>
    internal IReadOnlyList<VelumAssemblyRegistryComponent> Components { get; set; }

    /// <summary>
    /// true — активна сборка / запуск из реестра: можно добавлять кол-во в имя delivery.
    /// </summary>
    internal bool EnableAssemblyQuantity { get; set; }

    /// <summary>Множитель «Кол-во изделия» (по умолчанию 1).</summary>
    internal int ProductQuantity { get; set; } = 1;
  }

  /// <summary>Запуск пакетной формы PDF (меню или реестр изделия; состав уже проверен).</summary>
  internal sealed class VelumPdfBomLaunchContext
  {
    /// <summary>Каталог delivery (last-used или пусто).</summary>
    internal string PdfExportFolder { get; set; }

    /// <summary>Корень файла сборки — якорь Обзора.</summary>
    internal string AssemblyFolder { get; set; }

    internal IReadOnlyList<VelumPdfBatchDiagnosticRow> Rows { get; set; }

    /// <summary>Состав для повторной диагностики на форме.</summary>
    internal IReadOnlyList<VelumAssemblyRegistryComponent> Components { get; set; }
  }
}
