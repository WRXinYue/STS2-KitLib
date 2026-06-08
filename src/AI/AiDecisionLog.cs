using System;
using System.Collections.Generic;
using System.Globalization;

namespace KitLib.AI;

/// <summary>Ring buffer of recent AI decision lines for the in-game AI Host panel.</summary>
public static class AiDecisionLog {
    const int MaxLines = 200;

    static readonly Queue<string> Lines = new();
    static readonly object Gate = new();

    public static void Record(string source, string message) {
        MainFile.Logger.Info($"[{source}] {message}");
        var line = $"[{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] [{source}] {message}";
        lock (Gate) {
            Lines.Enqueue(line);
            while (Lines.Count > MaxLines)
                Lines.Dequeue();
        }
    }

    public static IReadOnlyList<string> Snapshot() {
        lock (Gate)
            return Lines.ToArray();
    }

    public static void Clear() {
        lock (Gate)
            Lines.Clear();
    }
}
