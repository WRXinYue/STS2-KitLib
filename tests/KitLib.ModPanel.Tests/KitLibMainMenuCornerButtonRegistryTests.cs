using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibMainMenuCornerButtonRegistryTests {
    [Fact]
    public void GetOrderedButtons_sorts_by_order_then_mod_then_id() {
        KitLibMainMenuCornerButtonRegistry.ClearForTests();
        try {
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "Beta",
                ButtonId = "b",
                SortOrder = 0,
                OnPressed = _ => { },
            });
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "Alpha",
                ButtonId = "z",
                SortOrder = 0,
                OnPressed = _ => { },
            });
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "Alpha",
                ButtonId = "a",
                SortOrder = 0,
                OnPressed = _ => { },
            });

            var buttons = KitLibMainMenuCornerButtonRegistry.GetOrderedButtons();
            Assert.Equal(3, buttons.Count);
            Assert.Equal("a", buttons[0].ButtonId);
            Assert.Equal("z", buttons[1].ButtonId);
            Assert.Equal("b", buttons[2].ButtonId);
        }
        finally {
            KitLibMainMenuCornerButtonRegistry.ClearForTests();
        }
    }

    [Fact]
    public void Register_replaces_same_mod_and_button_id() {
        KitLibMainMenuCornerButtonRegistry.ClearForTests();
        try {
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "KitDevTools",
                ButtonId = "dev-mode",
                Tooltip = "Old",
                OnPressed = _ => { },
            });
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "KitDevTools",
                ButtonId = "dev-mode",
                Tooltip = "New",
                OnPressed = _ => { },
            });

            var buttons = KitLibMainMenuCornerButtonRegistry.GetOrderedButtons();
            Assert.Single(buttons);
            Assert.Equal("New", buttons[0].Tooltip);
        }
        finally {
            KitLibMainMenuCornerButtonRegistry.ClearForTests();
        }
    }

    [Fact]
    public void ResolveIconPath_defaults_to_mod_image() {
        var button = new KitLibMainMenuCornerButtonRegistration {
            ModId = "MyMod",
            ButtonId = "action",
            OnPressed = _ => { },
        };
        Assert.Equal("res://MyMod/mod_image.png", KitLibMainMenuCornerButtonRegistry.ResolveIconPath(button));
        Assert.Equal(
            "res://MyMod/custom.png",
            KitLibMainMenuCornerButtonRegistry.ResolveIconPath(new KitLibMainMenuCornerButtonRegistration {
                ModId = "MyMod",
                ButtonId = "action",
                IconPath = "res://MyMod/custom.png",
                OnPressed = _ => { },
            }));
    }

    [Fact]
    public void Unregister_and_Contains_roundtrip() {
        KitLibMainMenuCornerButtonRegistry.ClearForTests();
        try {
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "MyMod",
                ButtonId = "action",
                OnPressed = _ => { },
            });
            Assert.True(KitLibMainMenuCornerButtonRegistry.Contains("mymod", "ACTION"));
            Assert.True(KitLibMainMenuCornerButtonRegistry.Unregister("MyMod", "action"));
            Assert.False(KitLibMainMenuCornerButtonRegistry.Contains("MyMod", "action"));
        }
        finally {
            KitLibMainMenuCornerButtonRegistry.ClearForTests();
        }
    }
}
