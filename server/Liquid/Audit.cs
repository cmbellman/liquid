using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// The change log (behandlingshistorik): what was changed outside the vouchers, by whom and when.
    /// The vouchers themselves are the log of the books (they are never changed, and corrections point
    /// at the original); this keeps the rest: settings and company details, accounts, receipts that
    /// were replaced, the closing, imports, accepted terms and an administrator's deletions. Entries
    /// are only ever added (class audit_entry; nothing in Liquid changes or removes them). The text
    /// is Swedish, like the books.
    /// </summary>
    public static class Audit {
        public static void log(string companyId, string userId, string action, string text) {
            var e = Db.create("audit_entry", companyId);
            e.set("at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            e.set("seq", Db.nextNumber(companyId, "audit"));
            e.set("userId", userId ?? "");
            e.set("action", action);
            e.set("text", text);
            Db.commit(e);
        }

        /// <summary>The fields that changed, as "name: before → after", for a log text.</summary>
        public static string changes(IEnumerable<(string name, string before, string after)> fields) {
            return string.Join("; ", fields.Where(f => (f.before ?? "") != (f.after ?? ""))
                .Select(f => f.name + ": " + show(f.before) + " → " + show(f.after)));
        }

        static string show(string value) => string.IsNullOrEmpty(value) ? "(tomt)" : value;

        /// <summary>The company's entries, newest first.</summary>
        public static List<SwecialObject> entries(string companyId) {
            return Db.forCompany("audit_entry", companyId).OrderByDescending(e => Db.num(e, "seq")).ToList();
        }
    }
}
