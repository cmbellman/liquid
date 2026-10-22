using com.swecial.code;
using com.swecial.server;
using System;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/securities.html : Källaren, the company's funds. What it holds (units, cost and the
    /// value at the latest event), a form for a purchase, sale or switch (Securities), and the events
    /// so far; POST action=remove takes back the latest one.
    /// </summary>
    public class WebSecurities : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("securities.title"));
            if (isPost) {
                if (form("action") == "remove") {
                    var e = Db.getOwned("security_event", form("id"), companyId) ?? throw new LiquidException("Händelsen finns inte.", "EVENT NOT FOUND.");
                    Securities.remove(e, account.id);
                    redirect(BasePath + "/securities.html?ok=deleted");
                    return;
                }
                string kind = form("kind");
                Securities.book(companyId, kind, form("date"), form("fromFund"), Securities.parseUnits(form("fromUnits")),
                    form("toFund"), Securities.parseUnits(form("toUnits")), Money.parse(form("amount")), form("note"), account.id);
                redirect(BasePath + "/securities.html?ok=saved");
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        void setValues() {
            var holdings = Securities.holdings(companyId);
            rows("holdings", holdings.Select(x => row(("name", h(x.name)), ("units", Securities.formatUnits(x.units)), ("cost", Money.format(x.cost)),
                ("value", Money.format(value(x))), ("valueDate", h(x.lastDate)))));
            set("holdingsCost", Money.format(holdings.Sum(x => x.cost)));
            set("holdingsValue", Money.format(holdings.Sum(x => value(x))));

            int year = DateTime.Today.Year;
            var (gain, loss) = Securities.realised(companyId, year);
            set("realisedAny", gain != 0 || loss != 0);
            set("realised", T("securities.realised", year, Money.format(gain), Money.format(loss)));

            // The form
            string kind = isPost ? form("kind") : Securities.Switch;
            set("kinds", Securities.Kinds.Select(k => row(("value", k), ("text", T("securities.kind." + k + ".name")), ("hint", T("securities.kind." + k + ".hint")),
                ("amountLabel", T("securities.amount." + k)), ("selected", selected(kind == k)))).ToList());
            set("fromFunds", holdings.Select(x => row(("name", h(x.name)), ("units", Securities.inputUnits(x.units)), ("cost", x.cost.ToString()),
                ("selected", selected(form("fromFund") == x.name)))).ToList());
            set("fundNames", Securities.events(companyId).SelectMany(e => new[] { Db.str(e, "fromFund"), Db.str(e, "toFund") })
                .Where(n => n.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).Select(n => row(("name", h(n)))).ToList());
            val("date", isPost ? form("date") : Dates.today());
            val("fromUnits", form("fromUnits"));
            val("toFund", form("toFund"));
            val("toUnits", form("toUnits"));
            val("amount", form("amount"));
            val("note", form("note"));

            // What has happened, newest first; only the latest can be removed
            var events = Securities.events(companyId);
            string latest = events.Count > 0 ? events.Last().id : "";
            rows("events", Enumerable.Reverse(events).Select(e => {
                string k = Db.str(e, "kind");
                long gainOrLoss = Db.num(e, "gain");
                bool sells = k == Securities.Sell || k == Securities.Switch;
                string units = (sells ? "−" + Securities.formatUnits(Db.num(e, "fromUnits")) : "")
                    + (k == Securities.Switch ? " / " : "")
                    + (k != Securities.Sell ? "+" + Securities.formatUnits(Db.num(e, "toUnits")) : "");
                var voucher = Db.getOwned("voucher", Db.str(e, "voucherId"), companyId);
                return row(("id", h(e.id)), ("date", h(Db.str(e, "date"))),
                    ("text", T("securities.what." + k, h(Db.str(e, "fromFund")), h(Db.str(e, "toFund"))) + (Db.str(e, "note").Length > 0 ? " · " + h(Db.str(e, "note")) : "")),
                    ("units", units), ("amount", Money.format(Db.num(e, "amount"))),
                    ("result", sells ? (gainOrLoss > 0 ? "+" : "") + Money.format(gainOrLoss) : ""), ("loss", gainOrLoss < 0 ? "loss" : ""),
                    ("hasVoucher", voucher != null), ("voucherHref", BasePath + "/voucher.html?id=" + u(voucher?.id)), ("voucher", h(voucher != null ? Ledger.label(voucher) : "")),
                    ("removable", e.id == latest));
            }));
        }

        /// <summary>The holding at the price of its latest event (Liquid doesn't know today's price).</summary>
        static long value(Holding x) {
            return (long)Math.Round(x.units * x.lastPrice / Securities.UnitScale, 0, MidpointRounding.AwayFromZero);
        }
    }
}
