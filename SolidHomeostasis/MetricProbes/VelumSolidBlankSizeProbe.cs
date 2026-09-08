using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Velum.ReactiveCore;
using Velum.ReactiveCore.Export;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Метрика <see cref="VelumBlankSizeProperties.ProbeKeyLinksOk"/>:
  /// для листовой детали с «Нужен dxf» ссылки Толщина/Длина/Ширина заполнены и резолвятся.
  /// </summary>
  internal static class VelumSolidBlankSizeProbe
  {
    internal static float ScoreLinksOk(ModelDoc2 partModel, out string detail)
    {
      detail = string.Empty;
      if (partModel == null || partModel.GetType() != (int)swDocumentTypes_e.swDocPART)
      {
        detail = "Габарит заготовки:" + System.Environment.NewLine +
                 "Не деталь — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!VelumDxfNeedFlagResolver.HasAnyExportable(partModel))
      {
        detail = "Габарит заготовки:" + System.Environment.NewLine +
                 "«Нужен dxf» не Yes — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!VelumSolidSheetMetalHelper.IsSheetMetalPart(partModel) ||
          VelumSolidSheetMetalHelper.TryFindFlatPatternFeature(partModel) == null)
      {
        detail = "Габарит заготовки:" + System.Environment.NewLine +
                 "Не листовая / нет FlatPattern — проверка не требуется";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
      }

      if (!VelumBlankSizePropertyLinksService.TryEvaluateLinksValidity(
              partModel,
              out bool allValid,
              out string validityDetail))
      {
        detail = "Габарит заготовки:" + System.Environment.NewLine +
                 (string.IsNullOrWhiteSpace(validityDetail)
                     ? "Не удалось прочитать свойства"
                     : validityDetail);
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

      if (!allValid)
      {
        detail = "Габарит заготовки:" + System.Environment.NewLine +
                 (string.IsNullOrWhiteSpace(validityDetail)
                     ? "Ссылки Толщина/Длина/Ширина пусты или невалидны"
                     : validityDetail);
        return VelumSolidWorksHomeostasisMetrics.PartNoMaterialScore;
      }

        detail = "Габарит заготовки:" + System.Environment.NewLine +
                 "Ссылки Толщина/Длина/Ширина и Прокат=Лист валидны";
        return VelumSolidWorksHomeostasisMetrics.MaterialCompleteScore;
    }
  }
}
