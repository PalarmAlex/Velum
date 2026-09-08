using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.Configuration;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Создание/обновление логического CPM «Нужен чертеж».
  /// Add3+YesOrNo на деталях часто даёт GenericFail; AddCustomInfo3 и перебор option надёжнее.
  /// </summary>
  internal static class VelumNeedDrawingPropertyWriter
  {
    /// <summary>Создаёт YesOrNo по умолчанию из настроек (<see cref="VelumAppConfig.NeedDrawingDefault"/>), если свойства ещё нет.</summary>
    internal static bool EnsureYes(ModelDoc2 modelDoc, out string message)
    {
      return VelumYesOrNoCustomPropertyWriter.Ensure(
          modelDoc,
          VelumExportDocumentationProperties.NeedDrawing,
          VelumAppConfig.NeedDrawingDefault,
          out message);
    }

    /// <summary>
    /// Если свойство есть — Set2; если нет — создать логическое и записать значение.
    /// </summary>
    internal static bool TryWrite(ModelDoc2 modelDoc, bool needDrawing, out string error)
    {
      return VelumYesOrNoCustomPropertyWriter.TryWrite(
          modelDoc,
          VelumExportDocumentationProperties.NeedDrawing,
          needDrawing,
          out error);
    }
  }

  /// <summary>
  /// Логические свойства документа (Yes/No): «Нужен чертеж», «Нужен dxf».
  /// </summary>
  internal static class VelumYesOrNoCustomPropertyWriter
  {
    /// <summary>Создаёт свойство с заданным значением, если его ещё нет.</summary>
    internal static bool Ensure(ModelDoc2 modelDoc, string propertyName, bool value, out string message)
    {
      message = string.Empty;
      if (modelDoc == null)
      {
        message = "model_null";
        return false;
      }

      string name = (propertyName ?? string.Empty).Trim();
      if (name.Length == 0)
      {
        message = "property_name_missing";
        return false;
      }

      CustomPropertyManager cpm =
          VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document");
      if (cpm == null)
      {
        message = "cpm_null";
        return false;
      }

      if (VelumRecipeSolidWorksCustomProperties.TryResolvePropertyName(cpm, name, out _))
      {
        message = "already_exists";
        return true;
      }

      string flag = value
          ? VelumExportDocumentationProperties.FlagYes
          : VelumExportDocumentationProperties.FlagNo;
      return TryCreate(modelDoc, cpm, name, flag, string.Empty, out message);
    }

    /// <summary>
    /// Если свойство есть — Set2; если нет — создать логическое и записать значение.
    /// Пишет на общую вкладку документа.
    /// </summary>
    internal static bool TryWrite(ModelDoc2 modelDoc, string propertyName, bool value, out string error)
    {
      return TryWrite(modelDoc, propertyName, value, null, out error);
    }

    /// <summary>
    /// Запись Yes/No на вкладку конфигурации или на общую (пустое имя).
    /// Пустой <paramref name="configurationName"/> — общая вкладка документа.
    /// </summary>
    internal static bool TryWrite(
        ModelDoc2 modelDoc,
        string propertyName,
        bool value,
        string configurationName,
        out string error)
    {
      error = string.Empty;
      if (modelDoc == null)
      {
        error = "Нет документа";
        return false;
      }

      string name = (propertyName ?? string.Empty).Trim();
      if (name.Length == 0)
      {
        error = "property_name_missing";
        return false;
      }

      string config = (configurationName ?? string.Empty).Trim();
      CustomPropertyManager cpm = string.IsNullOrEmpty(config)
          ? VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, "document")
          : VelumRecipeSolidWorksCustomProperties.TryGetManager(modelDoc, config);
      if (cpm == null)
      {
        error = "Менеджер свойств недоступен";
        return false;
      }

      string flag = value
          ? VelumExportDocumentationProperties.FlagYes
          : VelumExportDocumentationProperties.FlagNo;

      if (VelumRecipeSolidWorksCustomProperties.TryResolvePropertyName(
              cpm,
              name,
              out string canonical))
      {
        try
        {
          if (cpm.Set2(canonical, flag) == 0)
            return true;
        }
        catch
        {
        }

        try
        {
          cpm.Delete2(canonical);
        }
        catch
        {
        }

        return TryCreate(modelDoc, cpm, name, flag, config, out error);
      }

      return TryCreate(modelDoc, cpm, name, flag, config, out error);
    }

    private static bool TryCreate(
        ModelDoc2 modelDoc,
        CustomPropertyManager cpm,
        string name,
        string value,
        string configurationName,
        out string error)
    {
      error = string.Empty;
      int yesNo = (int)swCustomInfoType_e.swCustomInfoYesOrNo;
      var codes = new System.Text.StringBuilder();
      string configArg = configurationName ?? string.Empty;

      try
      {
        if (modelDoc.AddCustomInfo3(configArg, name, yesNo, value))
          return true;
        codes.Append("AddCustomInfo3=false;");
      }
      catch (Exception ex)
      {
        codes.Append("AddCustomInfo3:").Append(ex.Message).Append(';');
      }

      int[] options = { 0, 1, 2 };
      for (int i = 0; i < options.Length; i++)
      {
        try
        {
          int r = cpm.Add3(name, yesNo, value, options[i]);
          if (r == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
            return true;
          codes.Append("Add3(opt").Append(options[i]).Append(")=").Append(r).Append(';');
        }
        catch (Exception ex)
        {
          codes.Append("Add3(opt").Append(options[i]).Append("):").Append(ex.Message).Append(';');
        }
      }

      try
      {
        int r2 = cpm.Add2(name, yesNo, value);
        if (r2 == 0)
          return true;
        codes.Append("Add2=").Append(r2).Append(';');
      }
      catch (Exception ex)
      {
        codes.Append("Add2:").Append(ex.Message).Append(';');
      }

      try
      {
        int textType = (int)swCustomInfoType_e.swCustomInfoText;
        int r = cpm.Add3(name, textType, value, 0);
        if (r == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
          return true;
        r = cpm.Add3(name, textType, value, 1);
        if (r == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
          return true;
        codes.Append("Add3(text)=").Append(r).Append(';');
      }
      catch (Exception ex)
      {
        codes.Append("Add3(text):").Append(ex.Message).Append(';');
      }

      error = codes.Length > 0 ? codes.ToString().TrimEnd(';') : "create_failed";
      return false;
    }
  }
}
