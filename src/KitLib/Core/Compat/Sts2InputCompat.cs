using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Compat;

internal static class Sts2InputCompat {
    internal static bool IsUsingController(NControllerManager? mgr) {
#if STS2_STABLE_PROFILE
        return mgr?.IsUsingController == true;
#else
        return mgr?.InputType == MegaCrit.Sts2.Core.ControllerInput.InputType.Controller;
#endif
    }
}
