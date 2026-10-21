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

        public static string svg(List<FlowBucket> buckets) {
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
            sb.Append("<svg class='flow' viewBox='0 0 " + f(W) + " " + f(H) + "' role='img' aria-label='Pengar in och ut över tid'>");
            sb.Append("<defs>");
            sb.Append("<linearGradient id='gradIn' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#2fe0ff' stop-opacity='0.28'/><stop offset='1' stop-color='#2fe0ff' stop-opacity='0'/></linearGradient>");
            sb.Append("<linearGradient id='gradOut' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#ffc23d' stop-opacity='0.18'/><stop offset='1' stop-color='#ffc23d' stop-opacity='0'/></linearGradient>");
            sb.Append("</defs>");

            double baseY = y(0);
            sb.Append("<line class='zero' x1='" + f(Left) + "' x2='" + f(W - Right) + "' y1='" + f(baseY) + "' y2='" + f(baseY) + "'/>");

            // Today, between the last real bucket and the forecast
            if (firstForecast > 0) {
                double nowX = (x(lastPast) + x(firstForecast)) / 2;
                sb.Append("<line class='now' x1='" + f(nowX) + "' x2='" + f(nowX) + "' y1='" + f(Top - 8) + "' y2='" + f(baseY) + "'/>");
                sb.Append("<text x='" + f(nowX + 6) + "' y='" + f(Top - 2) + "'>nu</text>");
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
                string tip = b.label + (b.forecast ? " (väntas)" : "") + ": in " + Money.format(b.moneyIn) + " kr" + (b.forecast ? "" : ", ut " + Money.format(b.moneyOut) + " kr");
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

        /// <summary>
        /// Glaset: a beer glass filled to how long the money lasts (full at six months or more), with
        /// foam and a few rising bubbles.
        /// </summary>
        public static string glass(Runway r) {
            // Full is a little below the rim, so the foam shows
            double level = 0.88 * (r.months == null ? (r.balance > 0 ? 1 : 0) : Math.Min(1, (double)r.months.Value / 6));
            const double top = 10, bottom = 90, height = bottom - top;
            double liquidTop = bottom - height * level;
            var sb = new StringBuilder("<svg viewBox='0 0 74 96' aria-hidden='true'>");
            sb.Append("<defs><linearGradient id='beer' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#ffd35c'/><stop offset='1' stop-color='#f59e0b'/></linearGradient>");
            sb.Append("<clipPath id='glassClip'><path d='M10 8 L64 8 L58 90 Q57 93 54 93 L20 93 Q17 93 16 90 Z'/></clipPath></defs>");
            sb.Append("<g clip-path='url(#glassClip)'>");
            if (level > 0) {
                sb.Append("<rect class='beer' x='0' y='" + f(liquidTop) + "' width='74' height='" + f(96 - liquidTop) + "'/>");
                sb.Append("<path class='foam' d='M0 " + f(liquidTop + 2) + " q6 -7 12 -2 q6 -7 13 -1 q7 -7 13 0 q6 -6 12 -1 q7 -6 12 1 q6 -4 12 -1 V" + f(liquidTop + 4) + " H0 Z'/>");
                if (level > 0.15) {
                    sb.Append("<circle class='bubble' cx='28' cy='86' r='1.6'/><circle class='bubble' cx='41' cy='84' r='1.2'/><circle class='bubble' cx='48' cy='88' r='1.4'/>");
                }
            }
            sb.Append("</g><path class='cup' d='M10 8 L64 8 L58 90 Q57 93 54 93 L20 93 Q17 93 16 90 Z'/></svg>");
            return sb.ToString();
        }
    }
}
