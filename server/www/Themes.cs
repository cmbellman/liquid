using com.swecial.code;
using com.swecial.server;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// A look and a voice for the whole app. Every theme is data, not code:
    /// - server/themes/&lt;id&gt;.json: name, description, language, the theme it builds on (extends),
    ///   gauge graphic, icon choices, fonts and all texts by key ("bar.title", "pant.drop", …);
    /// - server/www/theme-&lt;id&gt;.css: colours and typography (scoped to body.theme-&lt;id&gt;).
    /// A text missing in a theme comes from the theme it extends, then from the default theme (boring),
    /// so a new theme can override as little as it likes. Files are re-read when they change.
    /// </summary>
    public class Theme {
        public string id;
        public string name;
        public string description;
        /// <summary>"sv" or "en": which language the error messages from the books use (inherited).</summary>
        public string language;
        public string extends;
        /// <summary>"beer" (an ölglas) or "tank" (a segmented reserve gauge), for Glaset.</summary>
        public string gauge;
        /// <summary>The brand text in the sidebar.</summary>
        public string brand;
        /// <summary>Show a clock under the brand.</summary>
        public bool? clock;
        /// <summary>A Google Fonts stylesheet URL, or empty.</summary>
        public string fonts;
        public int order = 100;
        /// <summary>Icon name per place, e.g. "nav.bar": "console" (names in Icons.byName).</summary>
        public Dictionary<string, string> icons = new Dictionary<string, string>();
        public Dictionary<string, string> texts = new Dictionary<string, string>();
    }

    public static class Themes {
        public const string Default = "boring";
        const string Field = "liquidTheme";

        static readonly ConcurrentDictionary<string, (DateTime written, Theme theme)> cache = new ConcurrentDictionary<string, (DateTime, Theme)>();

        static string folder => Path.Combine(Db.app.getCodePath(), "themes");

        /// <summary>Every theme in the themes folder, in their order.</summary>
        public static List<Theme> all() {
            if (!Directory.Exists(folder)) return new List<Theme> { get(Default) };
            return Directory.GetFiles(folder, "*.json")
                .Select(f => load(Path.GetFileNameWithoutExtension(f)))
                .Where(t => t != null)
                .OrderBy(t => t.order).ThenBy(t => t.name)
                .ToList();
        }

        /// <summary>The theme, or the default one if it doesn't exist (any more).</summary>
        public static Theme get(string id) {
            return load(id) ?? load(Default) ?? new Theme { id = Default, name = Default };
        }

        static Theme load(string id) {
            if (string.IsNullOrEmpty(id) || id.Any(c => !(char.IsLetterOrDigit(c) || c == '-'))) return null;
            string path = Path.Combine(folder, id + ".json");
            if (!File.Exists(path)) return null;
            DateTime written = File.GetLastWriteTimeUtc(path);
            if (cache.TryGetValue(id, out var cached) && cached.written == written) return cached.theme;
            try {
                var json = JObject.Parse(File.ReadAllText(path));
                var theme = json.ToObject<Theme>();
                theme.id = id;
                cache[id] = (written, theme);
                return theme;
            } catch (Exception ex) {
                SwecialData.current.log.write("liquid: theme " + id + " could not be read: " + ex.Message);
                return cached.theme;
            }
        }

        /// <summary>A text by key, following extends and then the default theme. The key itself if missing.</summary>
        public static string text(Theme theme, string key) {
            var seen = new HashSet<string>();
            for (var t = theme; t != null && seen.Add(t.id); t = t.extends != null ? load(t.extends) : (t.id != Default ? load(Default) : null)) {
                if (t.texts != null && t.texts.TryGetValue(key, out string value)) return value;
            }
            return key;
        }

        static readonly ConcurrentDictionary<string, (string version, SwecialObject tree)> trees = new ConcurrentDictionary<string, (string, SwecialObject)>();

        /// <summary>
        /// All of the theme's texts as nested objects for the templates, HTML-encoded:
        /// "bar.tab.title" is &lt;%t.bar.tab.title%&gt; on a page (&lt;%current.t.…%&gt; inside a loop).
        /// A key can't be both a text and a group ("a.b" and "a.b.c"); use "a.b.label" for the text.
        /// Rebuilt when one of the theme files changes.
        /// </summary>
        public static SwecialObject tree(Theme theme) {
            var chain = new List<Theme>();
            var seen = new HashSet<string>();
            for (var t = theme; t != null && seen.Add(t.id); t = t.extends != null ? load(t.extends) : (t.id != Default ? load(Default) : null)) {
                chain.Add(t);
            }
            string version = string.Join("|", chain.Select(t => t.id + ":" + (cache.TryGetValue(t.id, out var c) ? c.written.Ticks : 0)));
            if (trees.TryGetValue(theme.id, out var cached) && cached.version == version) return cached.tree;

            var root = new SwecialObject();
            // Default theme first, the theme itself last, so its texts win
            for (int i = chain.Count - 1; i >= 0; i--) {
                if (chain[i].texts == null) continue;
                foreach (var pair in chain[i].texts) {
                    var parts = pair.Key.Split('.');
                    var node = root;
                    for (int p = 0; p < parts.Length - 1; p++) {
                        var child = node.get(parts[p]) as SwecialObject;
                        if (child == null) {
                            child = new SwecialObject();
                            node.set(parts[p], child);
                        }
                        node = child;
                    }
                    if (!(node.get(parts[parts.Length - 1]) is SwecialObject)) {
                        node.set(parts[parts.Length - 1], LiquidPage.h(pair.Value));
                    }
                }
            }
            trees[theme.id] = (version, root);
            return root;
        }

        /// <summary>A setting (gauge, brand, …) following extends and then the default theme.</summary>
        public static T setting<T>(Theme theme, Func<Theme, T> get) {
            var seen = new HashSet<string>();
            for (var t = theme; t != null && seen.Add(t.id); t = t.extends != null ? load(t.extends) : (t.id != Default ? load(Default) : null)) {
                T value = get(t);
                if (value != null) return value;
            }
            return default;
        }

        public static string icon(Theme theme, string place, string fallback) {
            string name = setting(theme, t => t.icons != null && t.icons.TryGetValue(place, out string n) ? n : null);
            return Icons.byName(name) ?? Icons.byName(fallback) ?? "";
        }

        public static string of(SwecialUser user) {
            string id = user?.get<string>(Field);
            return id != null && load(id) != null ? id : Default;
        }

        public static void set(SwecialUser user, string id) {
            string value = load(id) != null ? id : Default;
            if (user.get<string>(Field) == value) return;
            user.set(Field, value);
            user.save();
        }
    }
}
