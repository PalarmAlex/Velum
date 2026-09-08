using System;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.SolidWorks;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Снимок сессии SolidWorks через XCad.
  /// </summary>
  public sealed class VelumSolidWorksSessionProbe : ISolidWorksSessionProbe
  {
    /// <inheritdoc />
    public SolidWorksSessionSnapshot Capture(IXApplication app)
    {
      if (app == null)
        return SolidWorksSessionSnapshot.Empty;

      IXDocument ixDoc = null;
      try
      {
        ixDoc = app.Documents.Active;
      }
      catch
      {
        return SolidWorksSessionSnapshot.Empty;
      }

      if (ixDoc == null)
        return SolidWorksSessionSnapshot.Empty;

      VelumSolidDocumentKind kind = ClassifyDocument(ixDoc);
      bool readOnly = false;
      bool dirty = false;
      string path = string.Empty;
      string title = string.Empty;

      try
      {
        readOnly = (ixDoc.State & DocumentState_e.ReadOnly) != 0;
      }
      catch
      {
      }

      try
      {
        dirty = ixDoc.IsDirty;
      }
      catch
      {
      }

      try
      {
        path = ixDoc.Path ?? string.Empty;
      }
      catch
      {
      }

      try
      {
        title = ixDoc.Title ?? string.Empty;
      }
      catch
      {
      }

      return new SolidWorksSessionSnapshot(
          hasActiveDocument: true,
          documentKind: kind,
          documentPath: path,
          documentTitle: title,
          isSketchEditMode: false,
          isReadOnly: readOnly,
          isDirty: dirty,
          pdmCheckedOut: null,
          capturedUtc: DateTime.UtcNow);
    }

    private static VelumSolidDocumentKind ClassifyDocument(IXDocument ixDoc)
    {
      if (ixDoc is IXPart)
        return VelumSolidDocumentKind.Part;
      if (ixDoc is IXAssembly)
        return VelumSolidDocumentKind.Assembly;
      if (ixDoc is IXDrawing)
        return VelumSolidDocumentKind.Drawing;
      return VelumSolidDocumentKind.Other;
    }
  }
}
