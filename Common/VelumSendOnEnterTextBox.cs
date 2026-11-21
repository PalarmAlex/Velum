using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Velum.UI
{
  /// <summary>
  /// Многострочный TextBox, перехватывающий Enter для отправки сообщения
  /// одним нажатием. В SolidWorks Task Pane хост обрабатывает Enter как
  /// default-button/accelerator на этапе PreTranslateMessage: до WndProc
  /// контрола не доходят ни <c>WM_KEYDOWN</c>, ни <c>WM_CHAR</c> (перевод
  /// строки не вставляется), а вот <c>WM_KEYUP</c> проходит. Перехватываем
  /// именно <c>WM_KEYUP</c> с VK_RETURN. Ctrl/Alt/Shift+Enter — стандартная
  /// вставка новой строки (не отправляется).
  /// </summary>
  public sealed class VelumSendOnEnterTextBox : TextBox
  {
    /// <summary>Вызывается при нажатии Enter без модификаторов (дубль кнопки «Отправить»).</summary>
    public event EventHandler EnterRequested;

    private const int WM_KEYUP = 0x0101;
    private const int WM_CHAR = 0x0102;
    private const int VK_RETURN = 0x0D;

    /// <inheritdoc/>
    protected override void WndProc(ref Message m)
    {
      switch (m.Msg)
      {
        case WM_CHAR:
          {
            int code = m.WParam.ToInt32();
            if ((code == '\r' || code == '\n') && !IsModifierPressed())
            {
              // На случай, если в другой хост-среде WM_CHAR всё же доходит:
              // глушим вставку пустой строки. Отправка идёт по WM_KEYUP.
              m.Result = IntPtr.Zero;
              return;
            }
            break;
          }

        case WM_KEYUP:
          {
            if (m.WParam.ToInt32() == VK_RETURN && !IsModifierPressed())
            {
              RaiseEnter();
              // Гасим отпускание, чтобы хост не отработал Enter повторно.
              m.Result = IntPtr.Zero;
              return;
            }
            break;
          }
      }

      base.WndProc(ref m);
    }

    /// <summary>Вызывает <see cref="EnterRequested"/>; исключение подписчика не ломает петлю.</summary>
    private void RaiseEnter()
    {
      try
      {
        EnterRequested?.Invoke(this, EventArgs.Empty);
      }
      catch
      {
        // Исключение подписчика не должно ломать петлю сообщений.
      }
    }

    /// <summary>Возвращает true, если нажаты Ctrl, Alt или Shift.</summary>
    private static bool IsModifierPressed()
    {
      const int VK_SHIFT = 0x10;
      const int VK_CONTROL = 0x11;
      const int VK_MENU = 0x12;
      return IsDown(VK_SHIFT) || IsDown(VK_CONTROL) || IsDown(VK_MENU);
    }

    private static bool IsDown(int vk)
    {
      short state = GetAsyncKeyState(vk);
      return (state & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
  }
}
