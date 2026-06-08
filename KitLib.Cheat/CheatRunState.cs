namespace KitLib.Cheat;

public static class CheatRunState {
    public static RuntimeStatModifiers? StatModifiers { get; set; }

    public static void ClearRunState() => StatModifiers = null;
}
