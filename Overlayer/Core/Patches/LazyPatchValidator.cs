using HarmonyLib;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Overlayer.Core.Patches;

/// <summary>
/// Checks a <see cref="LazyPatchAttribute"/> and its patch methods against the real target,
/// so a renamed/removed game method or a mismatched Harmony parameter is reported with a reason
/// instead of silently doing nothing. Pure (no logging); the caller decides how to report.
/// </summary>
internal static class LazyPatchValidator {
    private const BindingFlags AllMethods = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const int MaxCandidates = 6;

    private static readonly HashSet<string> SpecialParams = ["__instance", "__state", "__runOriginal", "__originalMethod", "__args", "__exception"];
    private static readonly Dictionary<Type, string> Aliases = new() {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
    };

    public static List<string> Validate(LazyPatchAttribute attr, MethodBase target, MethodInfo prefix, MethodInfo postfix, MethodInfo transpiler, MethodInfo finalizer) {
        List<string> issues = [];
        // Incompatible patches are skipped on purpose (Resolve already reports them).
        if(!attr.IsCompatible) {
            return issues;
        }

        if(prefix == null && postfix == null && transpiler == null && finalizer == null) {
            issues.Add("Patch class has no Prefix/Postfix/Transpiler/Finalizer method.");
        }

        if(target == null) {
            issues.Add(DescribeUnresolved(attr));
            return issues;
        }

        CheckPatchMethod(issues, target, prefix, "Prefix");
        CheckPatchMethod(issues, target, postfix, "Postfix");
        CheckPatchMethod(issues, target, finalizer, "Finalizer");
        if(transpiler is { IsStatic: false }) {
            issues.Add("Transpiler must be static.");
        }
        if(prefix != null && prefix.ReturnType != typeof(void) && prefix.ReturnType != typeof(bool)) {
            issues.Add($"Prefix must return void or bool, but returns {TypeName(prefix.ReturnType)}.");
        }
        return issues;
    }

    private static string DescribeUnresolved(LazyPatchAttribute attr) {
        Type type = null;
        try {
            type = MiscUtils.TypeByName(attr.TargetType);
        } catch {
            // Treated as "type not found" below.
        }
        if(type == null) {
            return $"Target type '{attr.TargetType}' not found.";
        }

        var msg = $"Target method '{attr.TargetType}.{attr.TargetMethod}' not found (or ambiguous match).";
        var candidates = FindCandidates(type, attr.TargetMethod);
        if(candidates.Count > 0) {
            msg += " Similar methods: " + string.Join("; ", candidates.Select(Signature));
        }
        return msg;
    }

    private static List<MethodBase> FindCandidates(Type type, string name) {
        return type.GetMethods(AllMethods)
            .Where(m => m.Name != name
                && (m.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf(m.Name, StringComparison.OrdinalIgnoreCase) >= 0))
            .GroupBy(m => m.ToString())
            .Select(g => (MethodBase)g.First())
            .Take(MaxCandidates)
            .ToList();
    }

    private static void CheckPatchMethod(List<string> issues, MethodBase target, MethodInfo patch, string kind) {
        if(patch == null) {
            return;
        }
        if(!patch.IsStatic) {
            issues.Add($"{kind} must be static.");
        }

        var targetParams = target.GetParameters();
        foreach(var p in patch.GetParameters()) {
            var problem = CheckParam(target, targetParams, p);
            if(problem != null) {
                issues.Add($"{kind} parameter '{p.Name}': {problem} Target is {Signature(target)}");
            }
        }
    }

    // Mirrors how Harmony binds patch parameters; returns null when the parameter binds fine.
    private static string CheckParam(MethodBase target, ParameterInfo[] targetParams, ParameterInfo p) {
        var name = p.Name;
        if(SpecialParams.Contains(name)) {
            return null;
        }
        if(name == "__result") {
            if(target is not MethodInfo mi || mi.ReturnType == typeof(void)) {
                return "target has no return value.";
            }
            var elem = Strip(p.ParameterType);
            return elem.IsAssignableFrom(mi.ReturnType) || mi.ReturnType.IsAssignableFrom(elem)
                ? null
                : $"type {TypeName(elem)} does not match the target return type {TypeName(mi.ReturnType)}.";
        }
        if(name.StartsWith("___")) {
            var field = name.Substring(3);
            return int.TryParse(field, out _) || AccessTools.Field(target.DeclaringType, field) != null
                ? null
                : $"field '{field}' not found on {target.DeclaringType?.Name}.";
        }
        if(name.StartsWith("__") && int.TryParse(name.Substring(2), out var index)) {
            return index >= 0 && index < targetParams.Length ? null : $"index {index} is out of range.";
        }

        var match = targetParams.FirstOrDefault(tp => tp.Name == name);
        if(match == null) {
            return "no target parameter has this name.";
        }
        var patchType = Strip(p.ParameterType);
        var targetType = Strip(match.ParameterType);
        return patchType == targetType || patchType.IsAssignableFrom(targetType)
            ? null
            : $"type {TypeName(patchType)} does not match target parameter type {TypeName(targetType)}.";
    }

    private static Type Strip(Type t) => t.IsByRef ? t.GetElementType() : t;

    private static string Signature(MethodBase m) {
        var ps = string.Join(", ", m.GetParameters().Select(p => $"{(p.ParameterType.IsByRef ? "ref " : "")}{TypeName(Strip(p.ParameterType))} {p.Name}"));
        return $"{m.DeclaringType?.Name}.{m.Name}({ps})";
    }

    private static string TypeName(Type t) => Aliases.TryGetValue(t, out var alias) ? alias : t.Name;
}
