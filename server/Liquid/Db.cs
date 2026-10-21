using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Thrown for anything the user did wrong (bad input, a rule of the ledger). The message is shown
    /// on the page as is, so it is written in Swedish.
    /// </summary>
    public class LiquidException : Exception {
        public LiquidException(string message) : base(message) { }
    }

    /// <summary>
    /// Data access for the liquid app. Every class except membership carries a companyId, and every
    /// lookup from a page goes through forCompany/getOwned so one company never sees another's data.
    /// The server has no indexes: lookups are LINQ over the in-memory objects.
    /// </summary>
    public static class Db {
        public const string AppId = "liquid";

        /// <summary>
        /// One lock for everything that numbers or books (counters, vouchers, invoice state). The server
        /// has no transactions, so these steps must never interleave. Monitor locks are reentrant.
        /// </summary>
        public static readonly object Sync = new object();

        public static SwecialApplication app => SwecialData.current.getApp(AppId);

        public static List<SwecialObject> all(string className) {
            return app?.getObjects(className) ?? new List<SwecialObject>();
        }

        public static List<SwecialObject> forCompany(string className, string companyId) {
            return all(className).Where(o => o.get<string>("companyId") == companyId).ToList();
        }

        public static SwecialObject get(string className, string id) {
            if (string.IsNullOrEmpty(id)) return null;
            return app?.getObject(className, id);
        }

        /// <summary>The object only if it belongs to the company, so ids in URLs can't reach other companies.</summary>
        public static SwecialObject getOwned(string className, string id, string companyId) {
            var o = get(className, id);
            return o != null && o.get<string>("companyId") == companyId ? o : null;
        }

        public static SwecialObject create(string className, string companyId) {
            var o = SwecialObject.create(app, className);
            if (companyId != null) o.set("companyId", companyId);
            return o;
        }

        /// <summary>Saves and writes to disk right away instead of waiting for the 1 s save queue.</summary>
        public static void commit(SwecialObject o) {
            o.save();
            o.saveNow();
        }

        public static string str(SwecialObject o, string key) {
            return o?.get<string>(key) ?? "";
        }

        public static long num(SwecialObject o, string key) {
            object v = o?.get(key);
            if (v == null) return 0;
            try {
                return Convert.ToInt64(v);
            } catch {
                return 0;
            }
        }

        /// <summary>Next number in a series (invoice numbers, voucher numbers), gap-free per key.</summary>
        public static long nextNumber(string companyId, string key) {
            lock (Sync) {
                var counter = forCompany("counter", companyId).FirstOrDefault(c => str(c, "key") == key);
                if (counter == null) {
                    counter = create("counter", companyId);
                    counter.set("key", key);
                }
                long next = num(counter, "value") + 1;
                counter.set("value", next);
                commit(counter);
                return next;
            }
        }
    }
}
