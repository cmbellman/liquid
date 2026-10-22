using com.swecial.code;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// Kranen: money in (cyan) and out (amber) per bucket as smooth glowing lines, drawn as inline SVG
    /// on the server (no chart library: the server doesn't serve .js). Forecast buckets are dashed.
    /// The curves use monotone cubic interpolation, so they never swing below zero or above a peak.
    /// Each bucket has a hover area with a tooltip (SVG title).
    /// </summary>
    public static class FlowChart {
        const double W = 760, H = 250, Left = 8, Right = 8, Top = 22, Bottom = 30;

        static string f(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>The chart. The texts (aria label, "now", "expected", "in", "out") come from the theme.</summary>
        public static string svg(List<FlowBucket> buckets, string label, string now, string expected, string inText, string outText, string currency) {
            int n = buckets.Count;
            if (n < 2) return "";
            long max = Math.Max(1, buckets.Max(b => Math.Max(b.moneyIn, b.moneyOut)));
            double step = (W - Left - Right) / (n - 1);
            double x(int i) => Left + i * step;
            double y(long v) => Top + (H - Top - Bottom) * (1 - (double)v / max);

            var inPts = buckets.Select((b, i) => (x(i), y(b.moneyIn))).ToList();
            var outPts = buckets.Select((b, i) => (x(i), y(b.moneyOut))).ToList();
            int firstForecast = buckets.FindIndex(b => b.forecast);
            int lastPast = firstForecast < 0 ? n - 1 : Math.Max(0, firstForecast - 1);

            var sb = new StringBuilder();
            sb.Append("<svg class='flow' viewBox='0 0 " + f(W) + " " + f(H) + "' role='img' aria-label='" + LiquidPage.h(label) + "'>");
            sb.Append("<defs>");
            sb.Append("<linearGradient id='gradIn' x1='0' y1='0' x2='0' y2='1'><stop class='s-in' offset='0' stop-opacity='0.28'/><stop class='s-in' offset='1' stop-opacity='0'/></linearGradient>");
            sb.Append("<linearGradient id='gradOut' x1='0' y1='0' x2='0' y2='1'><stop class='s-out' offset='0' stop-opacity='0.18'/><stop class='s-out' offset='1' stop-opacity='0'/></linearGradient>");
            sb.Append("</defs>");

            double baseY = y(0);
            sb.Append("<line class='zero' x1='" + f(Left) + "' x2='" + f(W - Right) + "' y1='" + f(baseY) + "' y2='" + f(baseY) + "'/>");

            // Today, between the last real bucket and the forecast
            if (firstForecast > 0) {
                double nowX = (x(lastPast) + x(firstForecast)) / 2;
                sb.Append("<line class='now' x1='" + f(nowX) + "' x2='" + f(nowX) + "' y1='" + f(Top - 8) + "' y2='" + f(baseY) + "'/>");
                sb.Append("<text x='" + f(nowX + 6) + "' y='" + f(Top - 2) + "'>" + LiquidPage.h(now) + "</text>");
            }

            // Areas and lines for what happened, dashed lines for the forecast
            var pastIn = inPts.Take(lastPast + 1).ToList();
            var pastOut = outPts.Take(lastPast + 1).ToList();
            if (pastIn.Count > 1) {
                sb.Append("<path class='area-out' d='" + curve(pastOut) + " L" + f(pastOut.Last().Item1) + "," + f(baseY) + " L" + f(pastOut[0].Item1) + "," + f(baseY) + " Z'/>");
                sb.Append("<path class='area-in' d='" + curve(pastIn) + " L" + f(pastIn.Last().Item1) + "," + f(baseY) + " L" + f(pastIn[0].Item1) + "," + f(baseY) + " Z'/>");
                sb.Append("<path class='out' d='" + curve(pastOut) + "'/>");
                sb.Append("<path class='in' d='" + curve(pastIn) + "'/>");
            }
            if (firstForecast >= 0) {
                int from = Math.Max(0, firstForecast - 1);
                sb.Append("<path class='in forecast' d='" + curve(inPts.Skip(from).ToList()) + "'/>");
            }

            for (int i = 0; i < n; i++) {
                var b = buckets[i];
                // Labels: every bucket when few, otherwise every other one
                if (n <= 16 || i % 2 == 0) {
                    sb.Append("<text x='" + f(x(i)) + "' y='" + f(H - 8) + "' text-anchor='" + (i == 0 ? "start" : i == n - 1 ? "end" : "middle") + "'>" + LiquidPage.h(b.label) + "</text>");
                }
                if (!b.forecast && (b.moneyIn > 0 || b.moneyOut > 0) && i <= lastPast) {
                    if (b.moneyIn > 0) sb.Append("<circle class='in' cx='" + f(x(i)) + "' cy='" + f(y(b.moneyIn)) + "' r='3.5'/>");
                    if (b.moneyOut > 0) sb.Append("<circle class='out' cx='" + f(x(i)) + "' cy='" + f(y(b.moneyOut)) + "' r='3.5'/>");
                }
                double hx = Math.Max(0, x(i) - step / 2);
                string tip = b.label + (b.forecast ? " (" + expected + ")" : "") + ": " + inText + " " + Money.format(b.moneyIn) + " " + currency + (b.forecast ? "" : ", " + outText + " " + Money.format(b.moneyOut) + " " + currency);
                sb.Append("<rect class='hit' x='" + f(hx) + "' y='0' width='" + f(Math.Min(step, W - hx)) + "' height='" + f(H - Bottom) + "'><title>" + LiquidPage.h(tip) + "</title></rect>");
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        /// <summary>A smooth SVG path through the points (monotone cubic, Fritsch–Carlson).</summary>
        static string curve(List<(double x, double y)> p) {
            int n = p.Count;
            if (n == 0) return "";
            var sb = new StringBuilder("M" + f(p[0].x) + "," + f(p[0].y));
            if (n == 1) return sb.ToString();
            var d = new double[n - 1];
            for (int i = 0; i < n - 1; i++) d[i] = (p[i + 1].y - p[i].y) / (p[i + 1].x - p[i].x);
            var m = new double[n];
            m[0] = d[0];
            m[n - 1] = d[n - 2];
            for (int i = 1; i < n - 1; i++) m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2;
            for (int i = 0; i < n - 1; i++) {
                if (d[i] == 0) { m[i] = 0; m[i + 1] = 0; continue; }
                double a = m[i] / d[i], b = m[i + 1] / d[i], s = a * a + b * b;
                if (s > 9) {
                    double t = 3 / Math.Sqrt(s);
                    m[i] = t * a * d[i];
                    m[i + 1] = t * b * d[i];
                }
            }
            for (int i = 0; i < n - 1; i++) {
                double h = (p[i + 1].x - p[i].x) / 3;
                sb.Append(" C" + f(p[i].x + h) + "," + f(p[i].y + m[i] * h) + " " + f(p[i + 1].x - h) + "," + f(p[i + 1].y - m[i + 1] * h) + " " + f(p[i + 1].x) + "," + f(p[i + 1].y));
            }
            return sb.ToString();
        }

        /// <summary>Glaset in the theme's style: "beer" (default) or "tank".</summary>
        public static string gauge(Runway r, string style) {
            return style == "tank" ? tank(r) : glass(r);
        }

        /// <summary>How full the gauge is: full at six months of money or more.</summary>
        static double fill(Runway r) {
            return r.months == null ? (r.balance > 0 ? 1 : 0) : Math.Min(1, (double)r.months.Value / 6);
        }

        /// <summary>
        /// A reserve tank: ten segments lit from the bottom, in a frame with tick marks, like a ship's
        /// fuel gauge. Lit segments use the theme's colour (currentColor via CSS).
        /// </summary>
        static string tank(Runway r) {
            int lit = (int)Math.Round(fill(r) * 10);
            var sb = new StringBuilder("<svg class='tank' viewBox='0 0 74 96' aria-hidden='true'>");
            sb.Append("<rect class='tank-frame' x='18' y='4' width='38' height='88'/>");
            for (int i = 0; i < 10; i++) {
                double y = 84 - i * 8.2;
                sb.Append("<rect class='tank-cell" + (i < lit ? " on" : "") + "' x='22' y='" + f(y) + "' width='30' height='6'/>");
            }
            for (int i = 0; i <= 4; i++) {
                double y = 6 + i * 21;
                sb.Append("<line class='tank-tick' x1='8' x2='15' y1='" + f(y) + "' y2='" + f(y) + "'/><line class='tank-tick' x1='59' x2='66' y1='" + f(y) + "' y2='" + f(y) + "'/>");
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// A beer glass filled to how long the money lasts (full at six months or more), with
        /// a gently sloshing surface, rolling foam and rising streams of bubbles.
        /// </summary>
        static string glass(Runway r) {
            // Full is a little below the rim, so the foam shows
            double level = 0.88 * fill(r);
            const double top = 10, bottom = 90, height = bottom - top;
            double liquidTop = bottom - height * level;
            // 74-unit periodic frothy curve (dx = 74, dy = 0) repeated twice for seamless horizontal wave motion
            const string Wave = "q6 -6 12 -1 q6 -6 13 1 q7 -7 12 -1 q6 -5 13 1 q6 -6 12 -1 q6 -5 12 1";
            var sb = new StringBuilder("<svg viewBox='0 0 74 96' aria-hidden='true'>");
            sb.Append("<defs><linearGradient id='beer' x1='0' y1='0' x2='0' y2='1'><stop class='s-beer-top' offset='0'/><stop class='s-beer-bottom' offset='1'/></linearGradient>");
            sb.Append("<clipPath id='glassClip'><path d='M10 8 L64 8 L58 90 Q57 93 54 93 L20 93 Q17 93 16 90 Z'/></clipPath></defs>");
            sb.Append("<g clip-path='url(#glassClip)'>");
            if (level > 0) {
                sb.Append("<g class='beer-slosh' style='transform-origin:37px " + f(liquidTop) + "px'>");
                sb.Append("<rect class='beer' x='-20' y='" + f(liquidTop) + "' width='114' height='" + f(102 - liquidTop) + "'/>");
                sb.Append("<path class='foam-wave' d='M-74 " + f(liquidTop + 3) + " " + Wave + " " + Wave + " V" + f(liquidTop + 8) + " H-74 Z'/>");
                sb.Append("<path class='foam' d='M-74 " + f(liquidTop + 1.5) + " " + Wave + " " + Wave + " V" + f(liquidTop + 5.5) + " H-74 Z'/>");
                sb.Append("</g>");
                if (level > 0.15) {
                    sb.Append("<circle class='bubble' cx='22' cy='89' r='1.2'/>");
                    sb.Append("<circle class='bubble' cx='27' cy='86' r='1.6'/>");
                    sb.Append("<circle class='bubble' cx='32' cy='90' r='1.0'/>");
                    sb.Append("<circle class='bubble' cx='37' cy='85' r='1.5'/>");
                    sb.Append("<circle class='bubble' cx='41' cy='89' r='1.3'/>");
                    sb.Append("<circle class='bubble' cx='46' cy='87' r='1.7'/>");
                    sb.Append("<circle class='bubble' cx='50' cy='90' r='1.1'/>");
                    sb.Append("<circle class='bubble' cx='54' cy='86' r='1.4'/>");
                }
            }
            sb.Append("</g><path class='glass-shine' d='M15 14 L20 86'/><path class='cup' d='M10 8 L64 8 L58 90 Q57 93 54 93 L20 93 Q17 93 16 90 Z'/></svg>");
            return sb.ToString();
        }
    }
}
