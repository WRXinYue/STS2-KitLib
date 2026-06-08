using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace KitLib.Patches;

/// <summary>
/// The game forcibly downgrades <see cref="FastModeType.Instant"/> to <see cref="FastModeType.Fast"/>
/// on non-editor builds. This transpiler removes that check so DevMode can freely toggle Instant mode.
///
/// GameStartup is async, so the actual IL lives in the compiler-generated state machine's MoveNext().
/// We find that via reflection to avoid hard-coding the generated class name.
/// </summary>
[HarmonyPatch]
public static class InstantModeUnlockPatch {
    [HarmonyTargetMethod]
    public static MethodBase FindTarget() {
        // async method body is in the nested state-machine type (name contains "GameStartup")
        foreach (var nested in typeof(NGame).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) {
            if (!nested.Name.Contains("GameStartup")) continue;
            var moveNext = nested.GetMethod("MoveNext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (moveNext != null) return moveNext;
        }

        // Fallback for older game versions that used a synchronous InitializeGame method
        var legacy = AccessTools.Method(typeof(NGame), "InitializeGame");
        if (legacy != null) return legacy;

        // Target not found — return null so Harmony silently skips this patch
        MainFile.Logger.Warn("InstantModeUnlockPatch: could not find NGame.GameStartup state machine or InitializeGame. Patch skipped.");
        return null;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var codes = new List<CodeInstruction>(instructions);
        var fastModeSetter = AccessTools.PropertySetter(typeof(PrefsSave), nameof(PrefsSave.FastMode));

        // Find the pattern: ldc.i4 FastModeType.Fast → call set_FastMode
        // Then walk backwards to find the branch start and NOP the whole block.
        for (int i = 0; i < codes.Count; i++) {
            if (codes[i].opcode == OpCodes.Call
                && codes[i].operand is MethodInfo mi
                && mi == fastModeSetter
                && i >= 1
                && codes[i - 1].LoadsConstant((int)FastModeType.Fast)) {
                // Walk backwards to find the conditional branch that guards this block.
                // The pattern is: ... brtrue/brfalse LABEL ... ldc.i4 Fast ... call set_FastMode
                int blockStart = -1;
                for (int j = i - 2; j >= 0; j--) {
                    if (codes[j].opcode == OpCodes.Brtrue || codes[j].opcode == OpCodes.Brtrue_S
                        || codes[j].opcode == OpCodes.Brfalse || codes[j].opcode == OpCodes.Brfalse_S) {
                        blockStart = j;
                        break;
                    }
                }

                if (blockStart < 0) {
                    // Fallback: just NOP the setter and its arg
                    MainFile.Logger.Warn("InstantModeUnlockPatch: Could not find branch, NOP-ing setter only.");
                    codes[i - 1] = new CodeInstruction(OpCodes.Nop);
                    codes[i] = new CodeInstruction(OpCodes.Nop);
                }
                else {
                    // NOP from the branch instruction through the setter call
                    for (int j = blockStart; j <= i; j++) {
                        // Preserve labels so other jumps still land correctly
                        var nop = new CodeInstruction(OpCodes.Nop);
                        nop.labels.AddRange(codes[j].labels);
                        codes[j] = nop;
                    }
                    MainFile.Logger.Info($"InstantModeUnlockPatch: NOP'd Instant→Fast downgrade (IL {blockStart}..{i})");
                }

                break;
            }
        }

        return codes;
    }
}
