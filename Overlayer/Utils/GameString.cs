using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Overlayer.Utils;

// RDString.Get / RDString.GetWithCheck gained no new parameter on 145+, but older-2.9.8 (r136)
// declares both with one extra trailing `LangSection section = LangSection.Translations`
// (confirmed via decompile). Optional parameters are filled in at the CALL SITE, so a direct
// `RDString.Get(key)` compiled against alpha binds to `Get(string, Dictionary)` exactly and throws
// MissingMethodException on r136 the first time the calling method is JIT'd. Route every game
// localization lookup through here: the direct call lives in its own never-inlined method that is
// only ever called when that exact overload exists, and r136 goes through reflection, filling the
// extra parameters from their declared defaults.
public static class GameString {
    private static readonly bool HasDirectOverloads =
        AccessTools.Method(typeof(RDString), "Get", [typeof(string), typeof(Dictionary<string, object>)]) != null &&
        AccessTools.Method(typeof(RDString), "GetWithCheck", [typeof(string), typeof(bool).MakeByRefType(), typeof(Dictionary<string, object>)]) != null;

    public static string Get(string key) => HasDirectOverloads ? GetDirect(key) : GetReflected(key);

    public static string GetWithCheck(string key, out bool exists) {
        if(HasDirectOverloads) {
            return GetWithCheckDirect(key, out exists);
        }
        return GetWithCheckReflected(key, out exists);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetDirect(string key) => RDString.Get(key);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetWithCheckDirect(string key, out bool exists) => RDString.GetWithCheck(key, out exists);

    private static MethodInfo getMethod;
    private static MethodInfo getWithCheckMethod;

    private static string GetReflected(string key) {
        getMethod ??= FindByFirstParams("Get", typeof(string));
        if(getMethod == null) {
            return key;
        }
        var args = DefaultArgs(getMethod);
        args[0] = key;
        return getMethod.Invoke(null, args) as string ?? key;
    }

    private static string GetWithCheckReflected(string key, out bool exists) {
        getWithCheckMethod ??= FindByFirstParams("GetWithCheck", typeof(string), typeof(bool).MakeByRefType());
        if(getWithCheckMethod == null) {
            exists = false;
            return key;
        }
        var args = DefaultArgs(getWithCheckMethod);
        args[0] = key;
        var result = getWithCheckMethod.Invoke(null, args) as string ?? key;
        exists = args[1] is bool b && b;
        return result;
    }

    private static MethodInfo FindByFirstParams(string name, params System.Type[] leading) =>
        typeof(RDString).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name)
            .Select(m => (m, ps: m.GetParameters()))
            .Where(t => t.ps.Length >= leading.Length
                && leading.Select((type, i) => t.ps[i].ParameterType == type).All(x => x)
                && t.ps.Skip(leading.Length).All(p => p.IsOptional))
            .OrderBy(t => t.ps.Length)
            .Select(t => t.m)
            .FirstOrDefault();

    // An enum parameter's DefaultValue can come back boxed as its underlying integer type
    // depending on the runtime, which Invoke then rejects - normalize it to the real enum type.
    private static object[] DefaultArgs(MethodInfo method) =>
        method.GetParameters().Select(DefaultArg).ToArray();

    private static object DefaultArg(ParameterInfo p) {
        if(!p.IsOptional) {
            return null;
        }
        var value = p.DefaultValue;
        if(value == null || value is System.DBNull || value == System.Type.Missing) {
            return null;
        }
        return p.ParameterType.IsEnum && value.GetType() != p.ParameterType
            ? System.Enum.ToObject(p.ParameterType, value)
            : value;
    }
}
