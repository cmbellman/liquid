using com.swecial.server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace com.swecial.code {
    /// <summary>
    /// The company's logo, uploaded as SVG and kept as plain one-colour geometry so every theme and invoice can
    /// paint it in its own colour. An upload is read by a small parser of our own (no DTDs, entities or scripts:
    /// only shapes are read), every shape becomes an absolute path of M, L, C and Z with its transforms applied,
    /// and only those numbers are stored (`logoJson` on the company). So nothing from the file reaches a page:
    /// the SVG on pages (`svg`) and the PDF (`InvoicePdf`) are drawn from the stored paths.
    /// Fills and strokes take the theme's colour; near-white fills are "knockouts" painted in the background,
    /// and dark outlines drawn over lighter parts are "outlines" (black on the invoice, the background on pages).
    /// </summary>
    public static class Logo {
        public const int MaxBytes = 512 * 1024;
        const int MaxShapes = 2000;
        const int MaxPathChars = 400_000;

        public sealed class Shape {
            /// <summary>Absolute path data: only M, L, C and Z.</summary>
            public string d;
            /// <summary>Filled (in the logo's colour, or the background when knockout), else only stroked.</summary>
            public bool fill;
            public bool evenOdd;
            public bool knockout;
            /// <summary>A dark outline over lighter parts, painted in the outline colour (wins over knockout).</summary>
            public bool outline;
            /// <summary>Stroke width in the logo's units, 0 for no stroke.</summary>
            public double stroke;
            public bool strokeKnockout;
            public bool strokeOutline;
            public string cap;
            public string join;
        }

        public sealed class Drawing {
            public double x, y, w, h;
            public List<Shape> shapes = new List<Shape>();
        }

        /// <summary>The company's logo, null when it has none.</summary>
        public static Drawing of(SwecialObject company) {
            string json = company == null ? "" : Db.str(company, "logoJson");
            if (json.Length == 0) return null;
            try { return JsonConvert.DeserializeObject<Drawing>(json); } catch { return null; }
        }

        public static void set(SwecialObject company, Drawing drawing) {
            company.set("logoJson", drawing == null ? "" : JsonConvert.SerializeObject(drawing));
        }

        /// <summary>The logo as inline SVG: fills and strokes in currentColor, knockouts in --logo-knockout, outlines in --logo-outline (CSS in style.css).</summary>
        public static string svg(Drawing d, string cssClass, string label) {
            if (d == null) return "";
            var sb = new StringBuilder();
            sb.Append("<svg class='company-logo ").Append(cssClass).Append("' viewBox='")
              .Append(n(d.x)).Append(' ').Append(n(d.y)).Append(' ').Append(n(d.w)).Append(' ').Append(n(d.h))
              .Append("' role='img' aria-label='").Append(System.Net.WebUtility.HtmlEncode(label ?? "")).Append("'>");
            foreach (var s in d.shapes) {
                if (!isPathData(s.d)) continue;
                if (s.fill) {
                    sb.Append("<path class='").Append(s.outline ? "o" : s.knockout ? "k" : "f").Append("' d='").Append(s.d).Append('\'');
                    if (s.evenOdd) sb.Append(" fill-rule='evenodd'");
                    sb.Append("/>");
                }
                if (s.stroke > 0) {
                    sb.Append("<path class='").Append(s.strokeOutline ? "so" : s.strokeKnockout ? "sk" : "s").Append("' d='").Append(s.d).Append("' stroke-width='").Append(n(s.stroke)).Append('\'');
                    if (s.cap == "round" || s.cap == "square") sb.Append(" stroke-linecap='").Append(s.cap).Append('\'');
                    if (s.join == "round" || s.join == "bevel") sb.Append(" stroke-linejoin='").Append(s.join).Append('\'');
                    sb.Append("/>");
                }
            }
            return sb.Append("</svg>").ToString();
        }

        /// <summary>Stored path data is ours, but check it anyway before it goes into markup.</summary>
        static readonly Regex PathData = new Regex(@"^[MLCZ0-9 .\-e]*$", RegexOptions.Compiled);
        public static bool isPathData(string d) => !string.IsNullOrEmpty(d) && PathData.IsMatch(d);

        // ================= Reading an upload =================

        /// <summary>Reads an uploaded SVG into one-colour geometry. Throws LiquidException with what's wrong.</summary>
        public static Drawing parse(byte[] data) {
            if (data == null || data.Length == 0) throw new LiquidException("Välj en SVG-fil.", "SELECT AN SVG FILE.");
            if (data.Length > MaxBytes) throw new LiquidException("Logotypen är för stor (högst 512 kB).", "LOGO EXCEEDS 512 KB.");
            string text = Encoding.UTF8.GetString(data);
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            if (text.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0) {
                throw new LiquidException("Filen är ingen SVG. Spara logotypen som SVG (vektor) och försök igen.", "NOT AN SVG FILE. EXPORT THE LOGO AS SVG.");
            }
            var reader = new Reader();
            try {
                reader.read(text);
            } catch (LiquidException) {
                throw;
            } catch (Exception) {
                throw new LiquidException("SVG-filen gick inte att läsa.", "THE SVG FILE COULD NOT BE READ.");
            }
            var drawing = reader.drawing();
            if (drawing == null) {
                throw reader.sawText
                    ? new LiquidException("Logotypen innehåller text som inte är konverterad till konturer. Gör om texten till konturer (outlines) i ritprogrammet och spara igen.", "THE LOGO CONTAINS LIVE TEXT. CONVERT TEXT TO OUTLINES AND EXPORT AGAIN.")
                    : new LiquidException("Hittade inga former i SVG-filen.", "NO SHAPES FOUND IN THE SVG FILE.");
            }
            return drawing;
        }

        struct Matrix {
            public double a, b, c, d, e, f;
            public static readonly Matrix Identity = new Matrix { a = 1, d = 1 };
            public Matrix times(Matrix m) => new Matrix {
                a = a * m.a + c * m.b, b = b * m.a + d * m.b,
                c = a * m.c + c * m.d, d = b * m.c + d * m.d,
                e = a * m.e + c * m.f + e, f = b * m.e + d * m.f + f
            };
            public (double, double) apply(double x, double y) => (a * x + c * y + e, b * x + d * y + f);
            public double scale => Math.Sqrt(Math.Abs(a * d - b * c));
        }

        sealed class Style {
            public string fill = "black", stroke = "none", fillRule = "nonzero", cap = "butt", join = "miter";
            public double strokeWidth = 1, opacity = 1;
            public bool hidden;
            public Style copy() => (Style)MemberwiseClone();
        }

        sealed class Element {
            public string name;
            public Dictionary<string, string> attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public bool selfClosing;
        }

        static readonly HashSet<string> Skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "defs", "clipPath", "mask", "symbol", "pattern", "linearGradient", "radialGradient", "filter", "marker",
            "title", "desc", "metadata", "script", "foreignObject", "switch", "style", "text", "a"
        };

        sealed class Reader {
            readonly List<(string selector, Dictionary<string, string> props)> css = new List<(string, Dictionary<string, string>)>();
            readonly List<Shape> shapes = new List<Shape>();
            readonly List<(double x, double y)> points = new List<(double, double)>();
            double maxStroke;
            int pathChars;
            public bool sawText;

            /// <summary>The file's tags in order: an element, or the name of a closing tag. Comments, CDATA, &lt;?…?&gt; and DOCTYPEs are skipped.</summary>
            static List<(Element el, string closing)> tokens(string text) {
                var list = new List<(Element, string)>();
                int i = 0;
                while (i < text.Length) {
                    int lt = text.IndexOf('<', i);
                    if (lt < 0) break;
                    if (string.CompareOrdinal(text, lt, "<!--", 0, 4) == 0) { i = end(text, lt, "-->"); continue; }
                    if (string.CompareOrdinal(text, lt, "<![CDATA[", 0, 9) == 0) { i = end(text, lt, "]]>"); continue; }
                    if (string.CompareOrdinal(text, lt, "<!", 0, 2) == 0 || string.CompareOrdinal(text, lt, "<?", 0, 2) == 0) { i = skipDeclaration(text, lt); continue; }
                    int gt = tagEnd(text, lt);
                    string tag = text.Substring(lt + 1, gt - lt - 1);
                    i = gt + 1;
                    if (tag.StartsWith("/")) list.Add((null, localName(tag.Substring(1).Trim())));
                    else list.Add((parseTag(tag), null));
                }
                return list;
            }

            /// <summary>A gradient as one paint: its average colour, and whether it is visible at all (overlays that fade to nothing aren't).</summary>
            sealed class Gradient {
                public List<(double r, double g, double b, double opacity)> stops = new List<(double, double, double, double)>();
                public string href;
            }

            readonly Dictionary<string, Gradient> gradients = new Dictionary<string, Gradient>();
            /// <summary>clipPath id → its shapes, each with its transform inside the clipPath.</summary>
            readonly Dictionary<string, List<(Element el, Matrix m)>> clipPaths = new Dictionary<string, List<(Element, Matrix)>>();

            /// <summary>Reads gradients and clip paths, which live in defs and are used by reference.</summary>
            void collectDefinitions(List<(Element el, string closing)> list) {
                Gradient gradient = null;
                string clipId = null;
                var clipStack = new Stack<(string name, Matrix m)>();
                foreach (var (el, closing) in list) {
                    if (closing != null) {
                        if (closing == "linearGradient" || closing == "radialGradient") gradient = null;
                        if (clipId != null) {
                            if (clipStack.Count > 0 && clipStack.Peek().name == closing) clipStack.Pop();
                            if (clipStack.Count == 0) clipId = null;
                        }
                        continue;
                    }
                    el.attrs.TryGetValue("id", out string id);
                    if (el.name == "linearGradient" || el.name == "radialGradient") {
                        gradient = new Gradient();
                        el.attrs.TryGetValue("href", out gradient.href);
                        if (!string.IsNullOrEmpty(id)) gradients[id] = gradient;
                        if (el.selfClosing) gradient = null;
                        continue;
                    }
                    if (el.name == "stop" && gradient != null) {
                        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var key in new[] { "stop-color", "stop-opacity" }) if (el.attrs.TryGetValue(key, out string v)) props[key] = v;
                        if (el.attrs.TryGetValue("style", out string st)) foreach (var kv in declarations(st)) props[kv.Key] = kv.Value;
                        var rgb = color(props.TryGetValue("stop-color", out string c) ? c : "black");
                        if (rgb != null) gradient.stops.Add((rgb.Value.r, rgb.Value.g, rgb.Value.b, props.TryGetValue("stop-opacity", out string o) ? length(o, 1) : 1));
                        continue;
                    }
                    if (el.name == "clipPath" && !string.IsNullOrEmpty(id) && clipId == null) {
                        if (el.selfClosing) continue;
                        clipId = id;
                        clipPaths[id] = new List<(Element, Matrix)>();
                        clipStack.Push((el.name, el.attrs.TryGetValue("transform", out string ct) ? transform(ct) : Matrix.Identity));
                        continue;
                    }
                    if (clipId != null) {
                        var m = clipStack.Peek().m;
                        if (el.attrs.TryGetValue("transform", out string t)) m = m.times(transform(t));
                        clipPaths[clipId].Add((el, m));
                        if (!el.selfClosing) clipStack.Push((el.name, m));
                    }
                }
            }

            public void read(string text) {
                // CSS first, since Illustrator and Figma style shapes by class
                foreach (Match m in Regex.Matches(text, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase)) {
                    readCss(m.Groups[1].Value.Replace("<![CDATA[", "").Replace("]]>", ""));
                }
                var list = tokens(text);
                collectDefinitions(list);
                var stack = new Stack<(Matrix m, Style s, string name, bool skip)>();
                stack.Push((Matrix.Identity, new Style(), "", false));
                foreach (var (el, closing) in list) {
                    if (closing != null) {
                        // Pop to the matching element (tolerates sloppy files)
                        while (stack.Count > 1) {
                            var top = stack.Pop();
                            if (string.Equals(top.name, closing, StringComparison.OrdinalIgnoreCase)) break;
                        }
                        continue;
                    }
                    var parent = stack.Peek();
                    if (parent.skip || Skipped.Contains(el.name)) {
                        if (string.Equals(el.name, "text", StringComparison.OrdinalIgnoreCase) && !parent.skip) sawText = true;
                        if (!el.selfClosing) stack.Push((parent.m, parent.s, el.name, true));
                        continue;
                    }
                    var style = resolve(el, parent.s);
                    var matrix = parent.m;
                    if (el.attrs.TryGetValue("transform", out string tr)) matrix = matrix.times(transform(tr));
                    if (string.Equals(el.name, "svg", StringComparison.OrdinalIgnoreCase) && stack.Count > 1) {
                        // A nested svg: place it at its x and y
                        matrix = matrix.times(new Matrix { a = 1, d = 1, e = num(el, "x"), f = num(el, "y") });
                    }
                    if (!style.hidden) shape(el, matrix, style);
                    if (!el.selfClosing) stack.Push((matrix, style, el.name, false));
                    if (shapes.Count > MaxShapes || pathChars > MaxPathChars) {
                        throw new LiquidException("Logotypen är för detaljerad. Förenkla den och försök igen.", "THE LOGO IS TOO COMPLEX. SIMPLIFY IT AND TRY AGAIN.");
                    }
                }
            }

            public Drawing drawing() {
                if (shapes.Count == 0 || points.Count == 0) return null;
                double pad = maxStroke / 2;
                double x0 = points.Min(p => p.x) - pad, x1 = points.Max(p => p.x) + pad;
                double y0 = points.Min(p => p.y) - pad, y1 = points.Max(p => p.y) + pad;
                if (!(x1 - x0 > 0) || !(y1 - y0 > 0) || double.IsInfinity(x1 - x0) || double.IsInfinity(y1 - y0)) return null;
                return new Drawing { x = x0, y = y0, w = x1 - x0, h = y1 - y0, shapes = shapes };
            }

            static int end(string text, int from, string marker) {
                int e = text.IndexOf(marker, from, StringComparison.Ordinal);
                return e < 0 ? text.Length : e + marker.Length;
            }

            /// <summary>Skips &lt;?…?&gt; and &lt;!DOCTYPE …&gt;, including an internal subset in brackets (never read).</summary>
            static int skipDeclaration(string text, int from) {
                if (text[from + 1] == '?') return end(text, from, "?>");
                int depth = 0;
                for (int i = from; i < text.Length; i++) {
                    if (text[i] == '[') depth++;
                    else if (text[i] == ']') depth--;
                    else if (text[i] == '>' && depth <= 0) return i + 1;
                }
                return text.Length;
            }

            static int tagEnd(string text, int from) {
                char quote = '\0';
                for (int i = from + 1; i < text.Length; i++) {
                    char ch = text[i];
                    if (quote != '\0') { if (ch == quote) quote = '\0'; }
                    else if (ch == '"' || ch == '\'') quote = ch;
                    else if (ch == '>') return i;
                }
                throw new FormatException("unclosed tag");
            }

            static string localName(string name) {
                int colon = name.IndexOf(':');
                return colon >= 0 ? name.Substring(colon + 1) : name;
            }

            static readonly Regex Attr = new Regex(@"([^\s=/]+)\s*=\s*(""([^""]*)""|'([^']*)')", RegexOptions.Compiled);

            static Element parseTag(string tag) {
                var el = new Element();
                tag = tag.Trim();
                if (tag.EndsWith("/")) { el.selfClosing = true; tag = tag.Substring(0, tag.Length - 1); }
                int space = 0;
                while (space < tag.Length && !char.IsWhiteSpace(tag[space])) space++;
                el.name = localName(tag.Substring(0, space));
                foreach (Match m in Attr.Matches(tag.Substring(space))) {
                    string value = m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
                    el.attrs[localName(m.Groups[1].Value)] = decode(value);
                }
                return el;
            }

            /// <summary>Only the predefined entities and character references: nothing is ever expanded from a DTD.</summary>
            static string decode(string s) {
                if (s.IndexOf('&') < 0) return s;
                return Regex.Replace(s, @"&(#x[0-9a-fA-F]+|#[0-9]+|amp|lt|gt|quot|apos);", m => {
                    string v = m.Groups[1].Value;
                    switch (v) {
                        case "amp": return "&";
                        case "lt": return "<";
                        case "gt": return ">";
                        case "quot": return "\"";
                        case "apos": return "'";
                    }
                    try {
                        int code = v.StartsWith("#x") ? Convert.ToInt32(v.Substring(2), 16) : int.Parse(v.Substring(1));
                        return code > 0 && code < 0x10000 ? ((char)code).ToString() : "";
                    } catch { return ""; }
                });
            }

            void readCss(string text) {
                text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
                foreach (Match m in Regex.Matches(text, @"([^{}@]+)\{([^{}]*)\}")) {
                    var props = declarations(m.Groups[2].Value);
                    foreach (var sel in m.Groups[1].Value.Split(',')) {
                        string s = sel.Trim();
                        if (s.Length > 0) css.Add((s, props));
                    }
                }
            }

            static Dictionary<string, string> declarations(string text) {
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in text.Split(';')) {
                    int colon = part.IndexOf(':');
                    if (colon <= 0) continue;
                    props[part.Substring(0, colon).Trim()] = part.Substring(colon + 1).Replace("!important", "").Trim();
                }
                return props;
            }

            /// <summary>Simple selectors only: .class, #id, element, element.class.</summary>
            bool matches(string selector, Element el) {
                if (selector.IndexOfAny(new[] { ' ', '>', '+', '~', ':', '[' }) >= 0) return false;
                var m = Regex.Match(selector, @"^([a-zA-Z][\w-]*)?((?:[.#][\w-]+)*)$");
                if (!m.Success) return false;
                if (m.Groups[1].Success && m.Groups[1].Value.Length > 0 && !string.Equals(m.Groups[1].Value, el.name, StringComparison.OrdinalIgnoreCase)) return false;
                el.attrs.TryGetValue("class", out string cls);
                var classes = (cls ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                el.attrs.TryGetValue("id", out string id);
                foreach (Match part in Regex.Matches(m.Groups[2].Value, @"([.#])([\w-]+)")) {
                    if (part.Groups[1].Value == "." && !classes.Contains(part.Groups[2].Value)) return false;
                    if (part.Groups[1].Value == "#" && part.Groups[2].Value != id) return false;
                }
                return true;
            }

            Style resolve(Element el, Style parent) {
                var s = parent.copy();
                s.opacity = 1;
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in new[] { "fill", "stroke", "stroke-width", "fill-rule", "opacity", "fill-opacity", "stroke-opacity", "display", "visibility", "stroke-linecap", "stroke-linejoin" }) {
                    if (el.attrs.TryGetValue(key, out string v)) props[key] = v;
                }
                foreach (var rule in css) {
                    if (matches(rule.selector, el)) foreach (var p in rule.props) props[p.Key] = p.Value;
                }
                if (el.attrs.TryGetValue("style", out string inline)) foreach (var p in declarations(inline)) props[p.Key] = p.Value;

                foreach (var p in props) {
                    string v = p.Value.Trim();
                    if (v == "inherit") continue;
                    switch (p.Key.ToLowerInvariant()) {
                        case "fill": s.fill = v; break;
                        case "stroke": s.stroke = v; break;
                        case "stroke-width": s.strokeWidth = length(v, 1); break;
                        case "fill-rule": s.fillRule = v; break;
                        case "stroke-linecap": s.cap = v; break;
                        case "stroke-linejoin": s.join = v; break;
                        case "opacity": s.opacity = length(v, 1); break;
                        case "fill-opacity": if (length(v, 1) <= 0.01) s.fill = "none"; break;
                        case "stroke-opacity": if (length(v, 1) <= 0.01) s.stroke = "none"; break;
                        case "display": if (v == "none") s.hidden = true; break;
                        case "visibility": if (v == "hidden" || v == "collapse") s.hidden = true; break;
                    }
                }
                if (s.opacity <= 0.01) s.hidden = true;
                return s;
            }

            static double length(string v, double fallback) {
                var m = Regex.Match(v ?? "", @"^\s*([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)\s*(%|px|pt)?");
                if (!m.Success) return fallback;
                double x = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (m.Groups[2].Value == "%") x /= 100;
                if (m.Groups[2].Value == "pt") x *= 4.0 / 3;
                return x;
            }

            static double num(Element el, string key, double fallback = 0) {
                return el.attrs.TryGetValue(key, out string v) ? length(v, fallback) : fallback;
            }

            static Matrix transform(string text) {
                var result = Matrix.Identity;
                foreach (Match m in Regex.Matches(text, @"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)")) {
                    var a = numbers(m.Groups[2].Value);
                    double at(int i, double f = 0) => i < a.Count ? a[i] : f;
                    Matrix t;
                    switch (m.Groups[1].Value) {
                        case "matrix": t = new Matrix { a = at(0, 1), b = at(1), c = at(2), d = at(3, 1), e = at(4), f = at(5) }; break;
                        case "translate": t = new Matrix { a = 1, d = 1, e = at(0), f = at(1) }; break;
                        case "scale": t = new Matrix { a = at(0, 1), d = a.Count > 1 ? a[1] : at(0, 1) }; break;
                        case "rotate": {
                            double r = at(0) * Math.PI / 180, cos = Math.Cos(r), sin = Math.Sin(r), cx = at(1), cy = at(2);
                            t = new Matrix { a = 1, d = 1, e = cx, f = cy }
                                .times(new Matrix { a = cos, b = sin, c = -sin, d = cos })
                                .times(new Matrix { a = 1, d = 1, e = -cx, f = -cy });
                            break;
                        }
                        case "skewX": t = new Matrix { a = 1, d = 1, c = Math.Tan(at(0) * Math.PI / 180) }; break;
                        default: t = new Matrix { a = 1, d = 1, b = Math.Tan(at(0) * Math.PI / 180) }; break;
                    }
                    result = result.times(t);
                }
                return result;
            }

            static readonly Regex Number = new Regex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

            static List<double> numbers(string text) {
                var list = new List<double>();
                foreach (Match m in Number.Matches(text ?? "")) list.Add(double.Parse(m.Value, CultureInfo.InvariantCulture));
                return list;
            }

            static (double r, double g, double b)? color(string value) {
                string c = (value ?? "").Trim().ToLowerInvariant();
                if (c == "white") return (255, 255, 255);
                if (c == "black") return (0, 0, 0);
                var hex = Regex.Match(c, @"^#([0-9a-f]{3}|[0-9a-f]{6})$");
                if (hex.Success) {
                    string h = hex.Groups[1].Value;
                    if (h.Length == 3) h = string.Concat(h.Select(ch => new string(ch, 2)));
                    return (Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
                }
                var rgb = Regex.Match(c, @"^rgba?\(\s*([\d.]+%?)\s*,?\s*([\d.]+%?)\s*,?\s*([\d.]+%?)");
                if (rgb.Success) {
                    double part(string s) => s.EndsWith("%") ? double.Parse(s.TrimEnd('%'), CultureInfo.InvariantCulture) * 2.55 : double.Parse(s, CultureInfo.InvariantCulture);
                    return (part(rgb.Groups[1].Value), part(rgb.Groups[2].Value), part(rgb.Groups[3].Value));
                }
                return null;
            }

            /// <summary>
            /// Whether a paint shows, and whether it is near-white (a knockout). A gradient counts as its average colour;
            /// one that is mostly transparent (a gloss or glow overlay) doesn't show.
            /// </summary>
            (bool paints, bool light, double lum) paint(string value) {
                string c = (value ?? "").Trim();
                if (c.Length == 0 || c == "none" || c == "transparent") return (false, false, 0);
                var url = Regex.Match(c, @"^url\(\s*['""]?#([^'"")\s]+)['""]?\s*\)");
                if (url.Success) {
                    var g = gradients.TryGetValue(url.Groups[1].Value, out var found) ? found : null;
                    for (int hops = 0; g != null && g.stops.Count == 0 && !string.IsNullOrEmpty(g.href) && hops < 5; hops++) {
                        g = gradients.TryGetValue(g.href.TrimStart('#'), out var next) ? next : null;
                    }
                    if (g == null || g.stops.Count == 0) return (true, false, 0);
                    if (g.stops.Max(st => st.opacity) < 0.5) return (false, false, 0);
                    double l = lum(g.stops.Average(st => st.r), g.stops.Average(st => st.g), g.stops.Average(st => st.b));
                    return (true, l > 235, l);
                }
                var rgbValue = color(c);
                double v = rgbValue == null ? 0 : lum(rgbValue.Value.r, rgbValue.Value.g, rgbValue.Value.b);
                return (true, v > 235, v);
            }

            static double lum(double r, double g, double b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;

            /// <summary>Whether a lighter part has been drawn in ink: near-black drawn over it is an outline that separates parts.</summary>
            bool lighterInk;

            /// <summary>
            /// Near-white is a knockout. Near-black drawn on top of lighter coloured parts is an outline: in one colour,
            /// dark outlines between parts become gaps or stay dark, so the parts stay apart. A logo all in black stays ink.
            /// </summary>
            (bool knockout, bool outline) kind((bool paints, bool light, double lum) p) {
                if (p.light) return (true, false);
                if (p.lum < 40 && lighterInk) return (true, true);
                if (p.lum >= 80) lighterInk = true;
                return (false, false);
            }

            void shape(Element el, Matrix m, Style s) {
                var fillPaint = paint(s.fill);
                var strokePaint = paint(s.stroke);
                bool fill = fillPaint.paints;
                bool stroke = strokePaint.paints && s.strokeWidth > 0;
                if (!fill && !stroke) return;
                // A shape clipped by a clip path (a common way to paint a shape with a gradient: a big rect
                // clipped to the shape) is drawn as the clip path's shapes in its paint
                string clip = el.attrs.TryGetValue("clip-path", out string cp) ? cp : null;
                if (el.attrs.TryGetValue("style", out string inline) && declarations(inline).TryGetValue("clip-path", out string cps)) clip = cps;
                var clipRef = Regex.Match(clip ?? "", @"url\(\s*['""]?#([^'"")\s]+)");
                if (clipRef.Success && clipPaths.TryGetValue(clipRef.Groups[1].Value, out var clipShapes)) {
                    foreach (var (cel, cm) in clipShapes) {
                        var cpath = geometry(cel, m.times(cm), false);
                        if (cpath == null) continue;
                        string rule = cel.attrs.TryGetValue("clip-rule", out string cr) ? cr : cel.attrs.TryGetValue("fill-rule", out string fr) ? fr : "nonzero";
                        add(cpath, true, rule == "evenodd", kind(fillPaint), 0, (false, false), null, null);
                    }
                    return;
                }
                var path = geometry(el, m, stroke);
                if (path == null) return;
                double width = stroke ? s.strokeWidth * m.scale : 0;
                if (el.name.ToLowerInvariant() == "line") fill = false;
                add(path, fill, s.fillRule == "evenodd", fill ? kind(fillPaint) : (false, false), width, stroke ? kind(strokePaint) : (false, false),
                    s.cap == "butt" ? null : s.cap, s.join == "miter" ? null : s.join);
            }

            void add(PathBuilder path, bool fill, bool evenOdd, (bool knockout, bool outline) fillKind, double stroke, (bool knockout, bool outline) strokeKind, string cap, string join) {
                string data = path.result();
                if (data.Length == 0) return;
                pathChars += data.Length;
                points.AddRange(path.points);
                maxStroke = Math.Max(maxStroke, stroke);
                shapes.Add(new Shape { d = data, fill = fill, evenOdd = evenOdd, knockout = fillKind.knockout, outline = fillKind.outline, stroke = stroke,
                    strokeKnockout = strokeKind.knockout, strokeOutline = strokeKind.outline, cap = cap, join = join });
            }

            /// <summary>An element's outline as a path, null for elements that aren't shapes.</summary>
            PathBuilder geometry(Element el, Matrix m, bool stroke) {
                var path = new PathBuilder(m);
                switch (el.name.ToLowerInvariant()) {
                    case "path":
                        if (!el.attrs.TryGetValue("d", out string d)) return null;
                        path.parse(d);
                        break;
                    case "rect": {
                        double x = num(el, "x"), y = num(el, "y"), w = num(el, "width"), h = num(el, "height");
                        if (w <= 0 || h <= 0) return null;
                        double rx = num(el, "rx", -1), ry = num(el, "ry", -1);
                        if (rx < 0) rx = ry; if (ry < 0) ry = rx;
                        rx = Math.Min(Math.Max(rx, 0), w / 2); ry = Math.Min(Math.Max(ry, 0), h / 2);
                        if (rx > 0 && ry > 0) {
                            path.moveTo(x + rx, y); path.lineTo(x + w - rx, y); path.arcTo(rx, ry, 0, false, true, x + w, y + ry);
                            path.lineTo(x + w, y + h - ry); path.arcTo(rx, ry, 0, false, true, x + w - rx, y + h);
                            path.lineTo(x + rx, y + h); path.arcTo(rx, ry, 0, false, true, x, y + h - ry);
                            path.lineTo(x, y + ry); path.arcTo(rx, ry, 0, false, true, x + rx, y);
                        } else {
                            path.moveTo(x, y); path.lineTo(x + w, y); path.lineTo(x + w, y + h); path.lineTo(x, y + h);
                        }
                        path.close();
                        break;
                    }
                    case "circle":
                    case "ellipse": {
                        double cx = num(el, "cx"), cy = num(el, "cy");
                        double rx = el.name == "circle" ? num(el, "r") : num(el, "rx"), ry = el.name == "circle" ? rx : num(el, "ry");
                        if (rx <= 0 || ry <= 0) return null;
                        path.moveTo(cx + rx, cy);
                        path.arcTo(rx, ry, 0, false, true, cx, cy + ry);
                        path.arcTo(rx, ry, 0, false, true, cx - rx, cy);
                        path.arcTo(rx, ry, 0, false, true, cx, cy - ry);
                        path.arcTo(rx, ry, 0, false, true, cx + rx, cy);
                        path.close();
                        break;
                    }
                    case "line":
                        if (!stroke) return null;
                        path.moveTo(num(el, "x1"), num(el, "y1"));
                        path.lineTo(num(el, "x2"), num(el, "y2"));
                        break;
                    case "polyline":
                    case "polygon": {
                        var p = numbers(el.attrs.TryGetValue("points", out string pts) ? pts : "");
                        if (p.Count < 4) return null;
                        path.moveTo(p[0], p[1]);
                        for (int i = 2; i + 1 < p.Count; i += 2) path.lineTo(p[i], p[i + 1]);
                        if (el.name == "polygon") path.close();
                        break;
                    }
                    default:
                        return null;
                }
                return path;
            }
        }

        /// <summary>Builds absolute M/L/C/Z path data in the logo's coordinates, with the element's transform applied.</summary>
        sealed class PathBuilder {
            readonly Matrix m;
            readonly StringBuilder sb = new StringBuilder();
            public readonly List<(double x, double y)> points = new List<(double, double)>();
            double cx, cy, sx, sy;   // current point and subpath start, untransformed
            double lcx, lcy;         // last control point for S and T
            char last;

            public PathBuilder(Matrix m) { this.m = m; }

            void emit(char cmd, params double[] xy) {
                sb.Append(cmd);
                for (int i = 0; i + 1 < xy.Length; i += 2) {
                    var (x, y) = m.apply(xy[i], xy[i + 1]);
                    if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) throw new FormatException("bad number");
                    points.Add((x, y));
                    sb.Append(n(x)).Append(' ').Append(n(y)).Append(' ');
                }
            }

            public void moveTo(double x, double y) { emit('M', x, y); cx = sx = x; cy = sy = y; last = 'M'; }
            public void lineTo(double x, double y) { emit('L', x, y); cx = x; cy = y; last = 'L'; }
            public void close() { sb.Append('Z'); cx = sx; cy = sy; last = 'Z'; }

            public void curveTo(double x1, double y1, double x2, double y2, double x, double y) {
                emit('C', x1, y1, x2, y2, x, y);
                lcx = x2; lcy = y2; cx = x; cy = y; last = 'C';
            }

            void quadTo(double qx, double qy, double x, double y) {
                emit('C', cx + 2.0 / 3 * (qx - cx), cy + 2.0 / 3 * (qy - cy), x + 2.0 / 3 * (qx - x), y + 2.0 / 3 * (qy - y), x, y);
                lcx = qx; lcy = qy; cx = x; cy = y; last = 'Q';
            }

            /// <summary>An elliptical arc (SVG endpoint form) as cubic Béziers of at most 90° each.</summary>
            public void arcTo(double rx, double ry, double angle, bool large, bool sweep, double x, double y) {
                double x0 = cx, y0 = cy;
                if (x0 == x && y0 == y) return;
                rx = Math.Abs(rx); ry = Math.Abs(ry);
                if (rx == 0 || ry == 0) { lineTo(x, y); return; }
                double phi = angle * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
                double dx = (x0 - x) / 2, dy = (y0 - y) / 2;
                double x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
                double lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
                if (lambda > 1) { double sq = Math.Sqrt(lambda); rx *= sq; ry *= sq; }
                double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
                double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
                double coef = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / den));
                double cxp = coef * rx * y1p / ry, cyp = -coef * ry * x1p / rx;
                double ccx = cos * cxp - sin * cyp + (x0 + x) / 2, ccy = sin * cxp + cos * cyp + (y0 + y) / 2;
                double ang(double ux, double uy, double vx, double vy) {
                    double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
                    return a;
                }
                double t1 = ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
                double dt = ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
                if (!sweep && dt > 0) dt -= 2 * Math.PI;
                else if (sweep && dt < 0) dt += 2 * Math.PI;
                int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 2) - 1e-9));
                double step = dt / segments;
                double k = 4.0 / 3 * Math.Tan(step / 4);
                for (int i = 0; i < segments; i++) {
                    double a1 = t1 + i * step, a2 = a1 + step;
                    double c1 = Math.Cos(a1), s1 = Math.Sin(a1), c2 = Math.Cos(a2), s2 = Math.Sin(a2);
                    (double, double) pt(double ex, double ey) => (ccx + rx * ex * cos - ry * ey * sin, ccy + rx * ex * sin + ry * ey * cos);
                    var p1 = pt(c1 - k * s1, s1 + k * c1);
                    var p2 = pt(c2 + k * s2, s2 - k * c2);
                    var p = i == segments - 1 ? (x, y) : pt(c2, s2);
                    curveTo(p1.Item1, p1.Item2, p2.Item1, p2.Item2, p.Item1, p.Item2);
                }
                last = 'A';
            }

            public void parse(string d) {
                int i = 0;
                char cmd = ' ';
                bool first = true;
                while (true) {
                    skip(d, ref i);
                    if (i >= d.Length) break;
                    char ch = d[i];
                    if (char.IsLetter(ch) && ch != 'e' && ch != 'E') { cmd = ch; i++; first = true; }
                    else if (cmd == ' ') throw new FormatException("path must start with a command");
                    bool rel = char.IsLower(cmd);
                    double ox = rel ? cx : 0, oy = rel ? cy : 0;
                    switch (char.ToUpperInvariant(cmd)) {
                        case 'M': {
                            double x = number(d, ref i) + ox, y = number(d, ref i) + oy;
                            if (first) moveTo(x, y); else lineTo(x, y);
                            first = false;
                            continue;
                        }
                        case 'L': lineTo(number(d, ref i) + ox, number(d, ref i) + oy); break;
                        case 'H': lineTo(number(d, ref i) + ox, cy); break;
                        case 'V': lineTo(cx, number(d, ref i) + oy); break;
                        case 'C': {
                            double x1 = number(d, ref i) + ox, y1 = number(d, ref i) + oy, x2 = number(d, ref i) + ox, y2 = number(d, ref i) + oy;
                            curveTo(x1, y1, x2, y2, number(d, ref i) + ox, number(d, ref i) + oy);
                            break;
                        }
                        case 'S': {
                            double x1 = last == 'C' ? 2 * cx - lcx : cx, y1 = last == 'C' ? 2 * cy - lcy : cy;
                            double x2 = number(d, ref i) + ox, y2 = number(d, ref i) + oy;
                            curveTo(x1, y1, x2, y2, number(d, ref i) + ox, number(d, ref i) + oy);
                            break;
                        }
                        case 'Q': {
                            double qx = number(d, ref i) + ox, qy = number(d, ref i) + oy;
                            quadTo(qx, qy, number(d, ref i) + ox, number(d, ref i) + oy);
                            break;
                        }
                        case 'T': {
                            double qx = last == 'Q' ? 2 * cx - lcx : cx, qy = last == 'Q' ? 2 * cy - lcy : cy;
                            quadTo(qx, qy, number(d, ref i) + ox, number(d, ref i) + oy);
                            break;
                        }
                        case 'A': {
                            double rx = number(d, ref i), ry = number(d, ref i), angle = number(d, ref i);
                            bool large = flag(d, ref i), sweep = flag(d, ref i);
                            arcTo(rx, ry, angle, large, sweep, number(d, ref i) + ox, number(d, ref i) + oy);
                            break;
                        }
                        case 'Z': close(); cmd = ' '; continue;
                        default: throw new FormatException("unknown path command");
                    }
                    first = false;
                }
            }

            static void skip(string d, ref int i) {
                while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
            }

            static double number(string d, ref int i) {
                skip(d, ref i);
                int start = i;
                if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                bool dot = false;
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot))) { if (d[i] == '.') dot = true; i++; }
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) {
                    i++;
                    if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                    while (i < d.Length && char.IsDigit(d[i])) i++;
                }
                if (i == start) throw new FormatException("number expected");
                return double.Parse(d.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            /// <summary>Arc flags may be written without separators ("a1 1 0 01 2 2").</summary>
            static bool flag(string d, ref int i) {
                skip(d, ref i);
                if (i >= d.Length || (d[i] != '0' && d[i] != '1')) throw new FormatException("flag expected");
                return d[i++] == '1';
            }

            public string result() => sb.ToString().Trim();
        }

        static string n(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
