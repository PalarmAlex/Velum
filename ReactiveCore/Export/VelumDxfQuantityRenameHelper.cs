using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace Velum.ReactiveCore.Export
{
  /// <summary>Переименование delivery DXF при устаревшем кол-ве в имени.</summary>
  internal static class VelumDxfQuantityRenameHelper
  {
    internal enum CollisionDecision
    {
      Ask,
      OverwriteAll,
      SkipAll
    }

    internal sealed class RenameResult
    {
      internal bool Success { get; set; }

      internal bool Skipped { get; set; }

      internal string Message { get; set; }

      internal string NewPath { get; set; }
    }

    internal static RenameResult TryRename(
        ModelDoc2 modelDoc,
        string configName,
        string sourcePath,
        string expectedBaseName,
        IWin32Window owner,
        ref CollisionDecision collisionDecision,
        bool writeProperties = false)
    {
      var result = new RenameResult();
      if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
      {
        result.Message = "Исходный DXF не найден.";
        return result;
      }

      if (string.IsNullOrWhiteSpace(expectedBaseName))
      {
        result.Message = "Не задано ожидаемое имя DXF.";
        return result;
      }

      string directory;
      try
      {
        directory = Path.GetDirectoryName(sourcePath);
      }
      catch
      {
        directory = null;
      }

      if (string.IsNullOrWhiteSpace(directory))
      {
        result.Message = "Не удалось определить каталог DXF.";
        return result;
      }

      string targetPath = Path.Combine(directory, expectedBaseName.Trim() + ".dxf");
      string sourceFull;
      string targetFull;
      try
      {
        sourceFull = Path.GetFullPath(sourcePath);
        targetFull = Path.GetFullPath(targetPath);
      }
      catch (Exception ex)
      {
        result.Message = ex.Message;
        return result;
      }

      if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase))
      {
        result.Success = true;
        result.NewPath = targetPath;
        result.Message = "Имя уже актуально.";
        return result;
      }

      if (File.Exists(targetPath))
      {
        if (collisionDecision == CollisionDecision.SkipAll)
        {
          result.Skipped = true;
          result.Message = "Пропущено: целевой файл уже существует.";
          return result;
        }

        if (collisionDecision == CollisionDecision.Ask)
        {
          DialogResult answer = AskOverwrite(owner, targetPath);
          if (answer == DialogResult.No)
          {
            result.Skipped = true;
            result.Message = "Пропущено: конфликт имени.";
            return result;
          }

          if (answer == DialogResult.Retry)
            collisionDecision = CollisionDecision.OverwriteAll;
          else if (answer == DialogResult.Ignore)
          {
            collisionDecision = CollisionDecision.SkipAll;
            result.Skipped = true;
            result.Message = "Пропущено: конфликт имени.";
            return result;
          }
          // Yes / Abort unused — Yes = overwrite once (DialogResult.Yes)
        }

        try
        {
          File.Delete(targetPath);
        }
        catch (Exception ex)
        {
          result.Message = "Не удалось удалить целевой файл: " + ex.Message;
          return result;
        }
      }

      try
      {
        File.Move(sourcePath, targetPath);
      }
      catch (Exception ex)
      {
        result.Message = "Не удалось переименовать: " + ex.Message;
        return result;
      }

      if (writeProperties && modelDoc != null)
      {
        VelumDxfFileNameHelper.TryWriteDxfCatalogProperty(modelDoc, directory, out _);
        VelumDxfArtifactResolver.TryWritePerConfigFileName(
            modelDoc,
            configName,
            expectedBaseName.Trim(),
            out _);
      }

      result.Success = true;
      result.NewPath = targetPath;
      result.Message = "Переименовано: " + Path.GetFileName(targetPath);
      return result;
    }

    /// <summary>
    /// Yes = перезаписать; Retry = да для всех; No = нет; Ignore = нет для всех.
    /// </summary>
    private static DialogResult AskOverwrite(IWin32Window owner, string targetPath)
    {
      using (var dialog = new Form())
      {
        dialog.Text = "Конфликт имени DXF";
        dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.MinimizeBox = false;
        dialog.MaximizeBox = false;
        dialog.ShowInTaskbar = false;
        dialog.ClientSize = new Size(440, 140);
        dialog.Font = SystemFonts.MessageBoxFont;

        var label = new Label
        {
          AutoSize = false,
          Bounds = new Rectangle(12, 12, 416, 60),
          Text = "Файл уже существует:\r\n" + targetPath + "\r\n\r\nПерезаписать?"
        };
        dialog.Controls.Add(label);

        var btnYes = new Button { Text = "Да", Bounds = new Rectangle(12, 100, 90, 28), DialogResult = DialogResult.Yes };
        var btnYesAll = new Button { Text = "Да для всех", Bounds = new Rectangle(108, 100, 100, 28), DialogResult = DialogResult.Retry };
        var btnNo = new Button { Text = "Нет", Bounds = new Rectangle(214, 100, 90, 28), DialogResult = DialogResult.No };
        var btnNoAll = new Button { Text = "Нет для всех", Bounds = new Rectangle(310, 100, 110, 28), DialogResult = DialogResult.Ignore };
        dialog.Controls.Add(btnYes);
        dialog.Controls.Add(btnYesAll);
        dialog.Controls.Add(btnNo);
        dialog.Controls.Add(btnNoAll);
        dialog.AcceptButton = btnYes;
        dialog.CancelButton = btnNo;

        return dialog.ShowDialog(owner);
      }
    }
  }
}
