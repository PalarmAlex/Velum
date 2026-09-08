using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>Загрузка оконной иконки Velum из выходной папки сборки.</summary>
  internal static class VelumFormIcon
  {
    /// <summary>
    /// Ищет <c>velum.ico</c> рядом со сборкой или в подпапке <c>icons</c> (как при сборке проекта).
    /// </summary>
    internal static Icon TryLoad()
    {
      try
      {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(dir))
          return null;

        string[] candidates =
        {
          Path.Combine(dir, "icons", "velum.ico"),
          Path.Combine(dir, "velum.ico"),
        };
        foreach (string path in candidates)
        {
          if (File.Exists(path))
            return new Icon(path);
        }
      }
      catch
      {
      }

      return null;
    }

    /// <summary>Назначает иконку форме, если файл найден.</summary>
    internal static void Apply(Form form)
    {
      if (form == null)
        return;
      Icon icon = TryLoad();
      if (icon != null)
        form.Icon = icon;
    }
  }
}
