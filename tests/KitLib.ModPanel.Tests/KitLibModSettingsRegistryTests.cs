using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibModSettingsRegistryTests {
    [Fact]
    public void GetPages_returns_sorted_by_order_then_id() {
        KitLibModSettingsRegistry.ClearForTests();
        try {
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "KitLib",
                PageId = "z",
                Title = "Z",
                SortOrder = 10,
                BuildBody = () => new object(),
            });
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "KitLib",
                PageId = "a",
                Title = "A",
                SortOrder = 0,
                BuildBody = () => new object(),
            });
            var pages = KitLibModSettingsRegistry.GetPages("KitLib");
            Assert.Equal(2, pages.Count);
            Assert.Equal("a", pages[0].PageId);
            Assert.Equal("z", pages[1].PageId);
        }
        finally {
            KitLibModSettingsRegistry.ClearForTests();
        }
    }

    [Fact]
    public void Register_replaces_same_mod_and_page_id() {
        KitLibModSettingsRegistry.ClearForTests();
        try {
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "KitLib",
                PageId = "general",
                Title = "Old",
                BuildBody = () => "old",
            });
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "KitLib",
                PageId = "general",
                Title = "New",
                BuildBody = () => "new",
            });
            var pages = KitLibModSettingsRegistry.GetPages("KitLib");
            Assert.Single(pages);
            Assert.Equal("New", pages[0].Title);
        }
        finally {
            KitLibModSettingsRegistry.ClearForTests();
        }
    }

    [Fact]
    public void TryGetPage_Contains_and_Unregister_roundtrip() {
        KitLibModSettingsRegistry.ClearForTests();
        try {
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "MyMod",
                PageId = "general",
                Title = "General",
                BuildBody = () => new object(),
            });
            Assert.True(KitLibModSettingsRegistry.Contains("mymod", "GENERAL"));
            Assert.True(KitLibModSettingsRegistry.TryGetPage("MyMod", "general", out var page));
            Assert.Equal("General", page!.Title);

            Assert.True(KitLibModSettingsRegistry.Unregister("MyMod", "general"));
            Assert.False(KitLibModSettingsRegistry.Contains("MyMod", "general"));
            Assert.False(KitLibModSettingsRegistry.TryGetPage("MyMod", "general", out _));
        }
        finally {
            KitLibModSettingsRegistry.ClearForTests();
        }
    }

    [Fact]
    public void UnregisterAll_and_GetRegisteredModIds() {
        KitLibModSettingsRegistry.ClearForTests();
        try {
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "Alpha",
                PageId = "a",
                Title = "A",
                BuildBody = () => new object(),
            });
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "Beta",
                PageId = "b",
                Title = "B",
                BuildBody = () => new object(),
            });
            KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
                ModId = "Alpha",
                PageId = "c",
                Title = "C",
                BuildBody = () => new object(),
            });

            var ids = KitLibModSettingsRegistry.GetRegisteredModIds();
            Assert.Equal(new[] { "Alpha", "Beta" }, ids);

            Assert.Equal(2, KitLibModSettingsRegistry.UnregisterAll("alpha"));
            Assert.False(KitLibModSettingsRegistry.HasPages("Alpha"));
            Assert.True(KitLibModSettingsRegistry.HasPages("Beta"));
            Assert.Equal(new[] { "Beta" }, KitLibModSettingsRegistry.GetRegisteredModIds());
        }
        finally {
            KitLibModSettingsRegistry.ClearForTests();
        }
    }

    [Fact]
    public void ResolveTitle_uses_translate_when_TitleKey_set() {
        var page = new KitLibModSettingsPageRegistration {
            ModId = "KitLib",
            PageId = "general",
            Title = "General",
            TitleKey = "modpanel.kitlib.page.general",
            BuildBody = () => new object(),
        };
        Assert.Equal("General", KitLibModSettingsRegistry.ResolveTitle(page));
        Assert.Equal("一般", KitLibModSettingsRegistry.ResolveTitle(page, (key, fallback) =>
            key == "modpanel.kitlib.page.general" ? "一般" : fallback));
    }
}
