using KitLib.Models;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Cheat;

/// <summary>Registers card-test models from the Cheat satellite assembly (not scanned by ModelDb.Init).</summary>
internal static class CardTestModelRegistration {
    internal static void Register() {
        ModelDb.Inject(typeof(KitLibCardTestDummy));
        ModelDb.Inject(typeof(KitLibCardTestEncounter));
    }
}
