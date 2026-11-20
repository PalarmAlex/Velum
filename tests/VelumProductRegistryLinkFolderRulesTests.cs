using System;
using System.Collections.Generic;
using Velum.UI.ProductRegistry;
using Xunit;

namespace Velum.ReactiveCore.Tests;

/// <summary>
/// Регрессионные тесты выбора узла раскрытия дерева при «Связать с узлом»:
/// одна папка — на ней; разные папки с общим непосредственным родителем — на родителе;
/// разные родители — null (дерево свёрнуто, как раньше).
/// </summary>
public sealed class VelumProductRegistryLinkFolderRulesTests
{
  // Дерево: 10 (корень) → 20, 30; 20 → 40, 50; 30 → 60.
  private static readonly Dictionary<int, int> Parents = new Dictionary<int, int>
  {
    [10] = 0,
    [20] = 10,
    [30] = 10,
    [40] = 20,
    [50] = 20,
    [60] = 30,
  };

  private static int ParentOf(int id) => Parents.TryGetValue(id, out int p) ? p : 0;

  [Fact]
  public void ОднаПапка_РаскрываемНаНей()
  {
    Assert.Equal(40, VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40 }, ParentOf));
  }

  [Fact]
  public void НесколькоЗаписейВОднойПапке_РаскрываемНаНей()
  {
    Assert.Equal(40, VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40, 40, 40 }, ParentOf));
  }

  [Fact]
  public void РазныеПапкиОбщийРодитель_РаскрываемНаРодителе()
  {
    // 40 и 50 — дети 20: раскрываем на 20 (на 1 уровень выше самих папок).
    Assert.Equal(20, VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40, 50 }, ParentOf));
  }

  [Fact]
  public void РазныеРодители_Null()
  {
    // 40 (родитель 20) и 60 (родитель 30) — разные родители → null, дерево свёрнуто.
    Assert.Null(VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40, 60 }, ParentOf));
  }

  [Fact]
  public void ТриПапкиОбщийРодитель_РаскрываемНаРодителе()
  {
    // 40, 50 (родитель 20) и ещё одна в 20 — общий родитель один → 20.
    Assert.Equal(20, VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40, 50, 40 }, ParentOf));
  }

  [Fact]
  public void ОдинРодительОтличается_Null()
  {
    // 40, 50 → родитель 20, но 60 → родитель 30: хотя бы одна запись в другой ветке → null.
    Assert.Null(VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40, 50, 60 }, ParentOf));
  }

  [Fact]
  public void РодительСамихПапокЭтоКорень_ВозвращаемКорень()
  {
    // 20 и 30 — дети корня 10: общий непосредственный родитель — 10.
    Assert.Equal(10, VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 20, 30 }, ParentOf));
  }

  [Fact]
  public void ПустойСписок_Null()
  {
    Assert.Null(VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new int[0], ParentOf));
  }

  [Fact]
  public void ТолькоНевалидныеИдентификаторы_Null()
  {
    Assert.Null(VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 0, -1 }, ParentOf));
  }

  [Fact]
  public void НулевойДелегат_Null()
  {
    Assert.Null(VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 40, 50 }, null));
  }

  [Fact]
  public void РодительНеизвестнойПапкиНулевой_Null()
  {
    // 99 нет в словаре → родитель 0 → общего родителя нет.
    Assert.Null(VelumProductRegistryLinkFolderRules.ResolveInitialFolderId(new[] { 99, 40 }, ParentOf));
  }
}
