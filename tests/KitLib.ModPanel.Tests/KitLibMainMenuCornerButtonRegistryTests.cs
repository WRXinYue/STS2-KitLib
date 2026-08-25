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
    public void ResolveTitle_defaults_to_mod_name_then_mod_id() {
        var button = new KitLibMainMenuCornerButtonRegistration {
            ModId = "MyMod",
            ButtonId = "action",
            OnPressed = _ => { },
        };
        Assert.Equal("My Mod", KitLibMainMenuCornerButtonRegistry.ResolveTitle(button, fallbackName: "My Mod"));
        Assert.Equal("MyMod", KitLibMainMenuCornerButtonRegistry.ResolveTitle(button));
        Assert.Equal("Custom", KitLibMainMenuCornerButtonRegistry.ResolveTitle(
            new KitLibMainMenuCornerButtonRegistration {
                ModId = "MyMod",
                ButtonId = "action",
                Title = "Custom",
                OnPressed = _ => { },
            },
            fallbackName: "My Mod"));
    }

    [Fact]
    public void ResolveDescription_defaults_to_v_version() {
        var button = new KitLibMainMenuCornerButtonRegistration {
            ModId = "MyMod",
            ButtonId = "action",
            OnPressed = _ => { },
        };
        Assert.Equal("v1.2.3", KitLibMainMenuCornerButtonRegistry.ResolveDescription(button, "1.2.3"));
        Assert.Equal("v1.2.3", KitLibMainMenuCornerButtonRegistry.ResolveDescription(button, "v1.2.3"));
        Assert.Equal("", KitLibMainMenuCornerButtonRegistry.ResolveDescription(button));
        Assert.Equal("v9.0", KitLibMainMenuCornerButtonRegistry.ResolveDescription(
            new KitLibMainMenuCornerButtonRegistration {
                ModId = "MyMod",
                ButtonId = "action",
                Version = "9.0",
                OnPressed = _ => { },
            },
            "1.0"));
        Assert.Equal("custom note", KitLibMainMenuCornerButtonRegistry.ResolveDescription(
            new KitLibMainMenuCornerButtonRegistration {
                ModId = "MyMod",
                ButtonId = "action",
                Description = "custom note",
                Version = "9.0",
                OnPressed = _ => { },
            },
            "1.0"));
    }

    [Fact]
    public void ResolveInfoLabelText_joins_title_and_description() {
        var button = new KitLibMainMenuCornerButtonRegistration {
            ModId = "LustTravel2",
            ButtonId = "patch-notes",
            OnPressed = _ => { },
        };
        Assert.Equal(
            "LustTravel2\nv0.12.0",
            KitLibMainMenuCornerButtonRegistry.ResolveInfoLabelText(
                button, fallbackName: "LustTravel2", fallbackVersion: "0.12.0"));
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

    [Fact]
    public void ResolveActiveIconPath_is_optional() {
        var idle = new KitLibMainMenuCornerButtonRegistration {
            ModId = "MyMod",
            ButtonId = "action",
            OnPressed = _ => { },
        };
        Assert.Null(KitLibMainMenuCornerButtonRegistry.ResolveActiveIconPath(idle));
        Assert.Equal(
            "res://MyMod/open.png",
            KitLibMainMenuCornerButtonRegistry.ResolveActiveIconPath(new KitLibMainMenuCornerButtonRegistration {
                ModId = "MyMod",
                ButtonId = "action",
                ActiveIconPath = "res://MyMod/open.png",
                OnPressed = _ => { },
            }));
    }

    [Fact]
    public void Register_roundtrips_active_icon_and_is_open() {
        KitLibMainMenuCornerButtonRegistry.ClearForTests();
        try {
            Func<object, bool> isOpen = _ => true;
            Action<object> onMenuReady = _ => { };
            KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
                ModId = "LustTravel2",
                ButtonId = "patch-notes",
                ActiveIconPath = "res://LustTravel2/open.png",
                IsOpen = isOpen,
                OnMenuReady = onMenuReady,
                OnPressed = _ => { },
            });

            var buttons = KitLibMainMenuCornerButtonRegistry.GetOrderedButtons();
            Assert.Single(buttons);
            Assert.Equal("res://LustTravel2/open.png", buttons[0].ActiveIconPath);
            Assert.Same(isOpen, buttons[0].IsOpen);
            Assert.Same(onMenuReady, buttons[0].OnMenuReady);
        }
        finally {
            KitLibMainMenuCornerButtonRegistry.ClearForTests();
        }
    }
}
