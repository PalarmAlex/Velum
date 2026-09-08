using System.Windows.Forms;
using Velum.Configuration;

namespace Velum.UI
{
  /// <summary>
  /// Проверки уровня доступа <c>ProductRegistryAccessLevel</c> (admin / user).
  /// User: только реестр документов (чтение); пульсация и остальные формы запрещены.
  /// </summary>
  internal static class VelumAdminAccess
  {
    internal const string DefaultDeniedMessage =
        "Действие доступно только администраторам проекта.";

    internal const string PulseStartDeniedMessage =
        "Запуск пульсации разрешён только администраторам проекта.";

    internal const string FormDeniedMessage =
        "Открытие этой формы разрешено только администраторам проекта.";

    /// <summary>
    /// True, если в Settings.xml задан уровень <c>admin</c>.
    /// </summary>
    internal static bool IsAdmin
    {
      get
      {
        VelumAppConfig.EnsureInitialized();
        return VelumAppConfig.IsProductRegistryAdmin;
      }
    }

    /// <summary>
    /// Если текущий уровень не admin — показывает сообщение и возвращает false.
    /// </summary>
    internal static bool TryRequireAdmin(IWin32Window owner, string deniedMessage = null)
    {
      if (IsAdmin)
        return true;

      IWin32Window messageOwner = owner is Control c ? c : null;
      MessageBox.Show(
          messageOwner,
          string.IsNullOrWhiteSpace(deniedMessage) ? DefaultDeniedMessage : deniedMessage,
          "Velum",
          MessageBoxButtons.OK,
          MessageBoxIcon.Warning);
      return false;
    }
  }
}
