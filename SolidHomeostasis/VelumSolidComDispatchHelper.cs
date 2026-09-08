using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Velum.SolidHomeostasis
{
  /// <summary>
  /// Вызов COM через <c>Type.InvokeMember</c> с диагностикой перехваченных исключений.
  /// </summary>
  internal static class VelumSolidComDispatchHelper
  {
    internal static bool TryInvokeMethod(
        string site,
        object comTarget,
        string memberName,
        object[] args,
        string context = null)
    {
      return TryInvoke(site, comTarget, memberName, isMethod: true, args, out object unused, context);
    }

    internal static bool TryInvokeMethod(
        string site,
        object comTarget,
        string memberName,
        object[] args,
        out object result,
        string context = null)
    {
      return TryInvoke(site, comTarget, memberName, isMethod: true, args, out result, context);
    }

    internal static bool TryGetProperty(
        string site,
        object comTarget,
        string memberName,
        out object result,
        string context = null)
    {
      return TryInvoke(site, comTarget, memberName, isMethod: false, null, out result, context);
    }

    internal static bool TryReadInt32(
        string site,
        object comTarget,
        string memberName,
        bool isMethod,
        out int value,
        string context = null)
    {
      value = 0;
      object result;
      if (!TryInvoke(site, comTarget, memberName, isMethod, null, out result, context))
        return false;

      try
      {
        value = Convert.ToInt32(result);
        return true;
      }
      catch (Exception ex)
      {
        LogSwallowed(site, memberName, comTarget, context, ex);
        return false;
      }
    }

    private static bool TryInvoke(
        string site,
        object comTarget,
        string memberName,
        bool isMethod,
        object[] args,
        out object result,
        string context)
    {
      result = null;
      if (comTarget == null)
        return false;

      try
      {
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        flags |= isMethod ? BindingFlags.InvokeMethod : BindingFlags.GetProperty;
        result = comTarget.GetType().InvokeMember(memberName, flags, null, comTarget, args);
        return true;
      }
      catch (Exception ex)
      {
        LogSwallowed(site, memberName, comTarget, context, Unwrap(ex));
        return false;
      }
    }

    internal static void LogSwallowed(
        string site,
        string memberName,
        object comTarget,
        string context,
        Exception ex)
    {
      string rcw = comTarget == null ? "(null)" : comTarget.GetType().Name;
      string ctx = string.IsNullOrEmpty(context) ? string.Empty : " ctx=\"" + context + "\"";
      VelumSolidDiagLog.WriteComProbe(site, rcw, memberName, ctx, ex);
    }

    private static Exception Unwrap(Exception ex)
    {
      if (ex is TargetInvocationException tie && tie.InnerException != null)
        return tie.InnerException;
      return ex;
    }
  }
}
