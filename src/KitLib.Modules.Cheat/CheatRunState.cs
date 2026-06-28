namespace KitLib.Cheat;

public static class CheatRunState {
    public static RuntimeStatModifiers? StatModifiers { get; set; }

    public static RuntimeStatModifiers Ensure() {
        StatModifiers ??= new RuntimeStatModifiers();
        return StatModifiers;
    }

    public static void ClearRunState() => StatModifiers = null;
}
