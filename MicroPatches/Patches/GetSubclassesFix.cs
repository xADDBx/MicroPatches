using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

using Kingmaker;

using Owlcat.Runtime.Core.Utility;

namespace MicroPatches.Patches;

[MicroPatch(
    $"Fix {nameof(TypeExtensions.GetSubclasses)}",
    Description = $"Changes {nameof(TypeExtensions.GetSubclasses)} to look in all loaded assemblies instead of just Code.dll",
    Experimental = true)]
[HarmonyPatch]
static class GetSubclassesFix
{
    static readonly MethodInfo Type_get_Assembly = AccessTools.PropertyGetter(typeof(Type), nameof(Type.Assembly));
    static readonly MethodInfo Assembly_GetTypes = AccessTools.Method(typeof(Assembly), nameof(Assembly.GetTypes));
    
    static Type[] GetAllTypes() => AppDomain.CurrentDomain.GetAssemblies().SelectMany(ass =>
    {
        PFLog.System.Log("");

        try
        {
            return ass.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types;
        }
        catch
        {
            return [];
        }
    })
    .ToArray();

    [HarmonyPatch(typeof(TypeExtensions), nameof(TypeExtensions.GetSubclasses), [typeof(Type), typeof(bool)])]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> GetSubclasses_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        Main.PatchLog(nameof(GetSubclassesFix), $"patching GetSubclasses");
        return TranspilerImpl(instructions);
    }

    [HarmonyPatch(typeof(TypeExtensions), nameof(TypeExtensions.GetSubclasses), [typeof(Type), typeof(bool)])]
    [HarmonyPrefix]
    static void GetSubclasses_Prefix(Type type) => Main.PatchLog(nameof(GetSubclassesFix), $"{nameof(TypeExtensions.GetSubclasses)}({type})");

    [HarmonyPatch(typeof(TypeExtensions), nameof(TypeExtensions.GetSubclassesGeneric), methodType: MethodType.Enumerator)]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> GetSubclassesGeneric_Transpiler(IEnumerable<CodeInstruction> instructions) => TranspilerImpl(instructions);

    [HarmonyPatch(typeof(TypeExtensions), nameof(TypeExtensions.GetSubclassesGeneric), [typeof(Type), typeof(bool)])]
    [HarmonyPrefix]
    static void GetSubclassesGeneric_Prefix(Type type) => Main.PatchLog(nameof(GetSubclassesFix), $"{nameof(TypeExtensions.GetSubclassesGeneric)}({type})");

    static IEnumerable<CodeInstruction> TranspilerImpl(IEnumerable<CodeInstruction> instructions)
        => new CodeMatcher(instructions)
            .MatchStartForward(
                new(ci => true),
                new(ci => true),
                new(ci => ci.Calls(Type_get_Assembly)),
                new(ci => ci.Calls(Assembly_GetTypes)))
            .SetAndAdvance(OpCodes.Nop, null)
            .SetAndAdvance(OpCodes.Nop, null)
            .SetAndAdvance(OpCodes.Nop, null)
            .SetInstructionAndAdvance(CodeInstruction.Call(() => GetAllTypes()))
            .InstructionEnumeration();
}
