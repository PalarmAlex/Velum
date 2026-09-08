using Xarial.XCad;

namespace Velum.ReactiveCore
{
  /// <summary>
  /// Снятие <see cref="SolidWorksSessionSnapshot"/> из сессии SolidWorks (COM на UI-потоке).
  /// </summary>
  public interface ISolidWorksSessionProbe
  {
    /// <summary>
    /// Захватывает снимок активного документа.
    /// </summary>
    /// <param name="app">Сессия XCad; при <c>null</c> возвращается <see cref="SolidWorksSessionSnapshot.Empty"/>.</param>
    SolidWorksSessionSnapshot Capture(IXApplication app);
  }
}
