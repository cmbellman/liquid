using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Thrown for anything the user did wrong (bad input, a rule of the ledger). The message is shown
    /// on the page as is: Swedish, plus the English text for the themes that speak English
    /// (Nostromo). Without an English text the Swedish one is shown everywhere.
    /// </summary>
    public class LiquidException : Exception {
        public readonly string english;

        public LiquidException(string message, string english = null) : base(message) {
            this.english = english;
        }

        public string text(bool inEnglish) {
            return inEnglish && english != null ? english : Message;
        }
    }

    /// <summary>
    /// Data access for the liquid app. Every class except company links to its company through the
    /// SwecialObject reference "company" (stored as "__company": "liquid|company|&lt;id&gt;"), and every
    /// lookup from a page goes through forCompany/getOwned/linked so one company never sees another's data.
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

        public static SwecialObject company(SwecialObject o) {
            if (o == null) return null;
            if (o.get("company") is SwecialObject c && !c.isDeleted) return c;
            return get("company", str(o, "companyId"));
        }

        public static string companyId(SwecialObject o) {
            if (o == null) return "";
            if (o.get("company") is SwecialObject c) return c.isDeleted ? "" : (c.id ?? "");
            return str(o, "companyId");
        }

        public static List<SwecialObject> forCompany(string className, string companyId) {
            if (string.IsNullOrEmpty(companyId)) return new List<SwecialObject>();
            return all(className).Where(o => Db.companyId(o) == companyId).ToList();
        }

        public static SwecialObject get(string className, string id) {
            if (string.IsNullOrEmpty(id)) return null;
            return app?.getObject(className, id);
        }

        public static SwecialUser user(string userId) {
            if (string.IsNullOrEmpty(userId)) return null;
            return SwecialData.current.getUser(userId);
        }

        /// <summary>The object only if it belongs to the company, so ids in URLs can't reach other companies.</summary>
        public static SwecialObject getOwned(string className, string id, string companyId) {
            var o = get(className, id);
            return o != null && Db.companyId(o) == companyId ? o : null;
        }

        public static SwecialObject create(string className, string companyId) {
            var o = SwecialObject.create(app, className);
            if (!string.IsNullOrEmpty(companyId)) {
                var c = get("company", companyId);
                if (c != null) o.set("company", c);
                else o.set("companyId", companyId);
            }
            return o;
        }

        /// <summary>Links a SwecialObject reference (serialized as "__&lt;key&gt;": "&lt;app&gt;|&lt;class&gt;|&lt;id&gt;").</summary>
        public static void link(SwecialObject o, string key, SwecialObject target) {
            if (o == null) return;
            o.remove(key + "Id");
            o.remove("__" + key);
            if (target != null && !target.isDeleted) {
                o.set(key, target);
            } else {
                o.remove(key);
            }
        }

        /// <summary>Links a _user reference (serialized as "__&lt;key&gt;": "_users|_user|&lt;id&gt;").</summary>
        public static void linkUser(SwecialObject o, string key, string userId) {
            if (o == null) return;
            o.remove(key + "Id");
            o.remove("__" + key);
            var u = user(userId);
            if (u != null && !u.isDeleted) {
                o.set(key, u);
            } else {
                o.remove(key);
            }
        }

        /// <summary>Removes an object reference from memory and JSON.</summary>
        public static void unlink(SwecialObject o, string key) {
            if (o == null) return;
            o.remove(key);
            o.remove("__" + key);
            o.remove(key + "Id");
        }

        /// <summary>The linked SwecialObject for key, checked against the owner's company when both belong to one.</summary>
        public static SwecialObject linked(SwecialObject o, string key, string targetClass = null) {
            if (o == null) return null;
            object raw = o.get(key);
            if (raw is SwecialObject target) {
                if (target.isDeleted) return null;
                string ownerComp = companyId(o);
                string targetComp = companyId(target);
                if (ownerComp.Length > 0 && targetComp.Length > 0 && ownerComp != targetComp) return null;
                return target;
            }
            string fallbackId = str(o, key + "Id");
            if (fallbackId.Length == 0 && raw is string s) fallbackId = s;
            if (fallbackId.Length == 0) return null;
            string cls = targetClass ?? key;
            if (cls == "_user") return user(fallbackId);
            string compId = companyId(o);
            return compId.Length > 0 ? getOwned(cls, fallbackId, compId) : get(cls, fallbackId);
        }

        /// <summary>The id of the linked SwecialObject for key (or "" if none).</summary>
        public static string linkedId(SwecialObject o, string key) {
            if (o == null) return "";
            object raw = o.get(key);
            if (raw is SwecialObject target) return target.isDeleted ? "" : (target.id ?? "");
            string id = str(o, key + "Id");
            if (id.Length > 0) return id;
            return raw is string s ? s : "";
        }

        /// <summary>The linked SwecialUser for key (or null if none).</summary>
        public static SwecialUser linkedUser(SwecialObject o, string key) {
            if (o == null) return null;
            if (o.get(key) is SwecialUser u) return u.isDeleted ? null : u;
            string id = linkedId(o, key);
            return id.Length > 0 ? user(id) : null;
        }

        /// <summary>Saves and writes to disk right away instead of waiting for the 1 s save queue.</summary>
        public static void commit(SwecialObject o) {
            o.save();
            o.saveNow();
        }

        /// <summary>Deletes an object permanently from memory and disk.</summary>
        public static void delete(SwecialObject o) {
            if (o == null) return;
            lock (Sync) {
                o.delete();
            }
        }

        public static string str(SwecialObject o, string key) {
            object v = o?.get(key);
            if (v == null) return "";
            if (v is SwecialObject so) return so.isDeleted ? "" : (so.id ?? "");
            return v as string ?? Convert.ToString(v) ?? "";
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

        /// <summary>Sets the sequence counter value directly (used when renumbering a series).</summary>
        public static void setCounter(string companyId, string key, long value) {
            lock (Sync) {
                var counter = forCompany("counter", companyId).FirstOrDefault(c => str(c, "key") == key);
                if (counter == null) {
                    counter = create("counter", companyId);
                    counter.set("key", key);
                }
                counter.set("value", value);
                commit(counter);
            }
        }

        /// <summary>
        /// Upgrades any legacy string ID fields (companyId, userId, customerId, voucherId, etc.) to
        /// SwecialObject references (__company, __user, __customer, __voucher, etc.). Runs at startup.
        /// </summary>
        public static void migrateLinks() {
            lock (Sync) {
                bool migrateCompanyField(SwecialObject o) {
                    if (o.get("companyId") == null) return false;
                    string cId = str(o, "companyId");
                    var c = get("company", cId);
                    o.remove("companyId");
                    if (c != null && !(o.get("company") is SwecialObject)) o.set("company", c);
                    return true;
                }
                bool migrateUserField(SwecialObject o, string key, string legacyKey = null) {
                    bool changed = false;
                    string id = "";
                    if (legacyKey != null && o.get(legacyKey) != null) {
                        id = str(o, legacyKey);
                        o.remove(legacyKey);
                        changed = true;
                    } else if (o.get(key) is string s) {
                        id = s;
                        o.remove(key);
                        changed = true;
                    }
                    if (id.Length > 0 && !(o.get(key) is SwecialObject)) {
                        var u = user(id);
                        if (u != null) { o.set(key, u); changed = true; }
                    }
                    return changed;
                }
                bool migrateObjField(SwecialObject o, string key, string legacyKey, string targetClass) {
                    if (o.get(legacyKey) == null) return false;
                    string id = str(o, legacyKey);
                    o.remove(legacyKey);
                    if (id.Length > 0 && !(o.get(key) is SwecialObject) && !string.IsNullOrEmpty(targetClass)) {
                        string cId = companyId(o);
                        var target = targetClass == "company" ? get("company", id)
                            : cId.Length > 0 ? getOwned(targetClass, id, cId) : get(targetClass, id);
                        if (target != null) o.set(key, target);
                    }
                    return true;
                }

                foreach (var c in all("company")) {
                    if (migrateUserField(c, "createdBy")) commit(c);
                }
                foreach (var m in all("membership")) {
                    bool ch = migrateCompanyField(m);
                    ch |= migrateUserField(m, "user", "userId");
                    if (ch) commit(m);
                }
                foreach (var cls in new[] { "account", "counter", "customer" }) {
                    foreach (var o in all(cls)) {
                        if (migrateCompanyField(o)) commit(o);
                    }
                }
                foreach (var f in all("_file")) {
                    if (migrateCompanyField(f)) commit(f);
                }
                foreach (var e in all("audit_entry")) {
                    bool ch = migrateCompanyField(e);
                    ch |= migrateUserField(e, "user", "userId");
                    if (ch) commit(e);
                }
                foreach (var c in all("closing")) {
                    bool ch = migrateCompanyField(c);
                    ch |= migrateUserField(c, "closedBy");
                    if (ch) commit(c);
                }
                foreach (var i in all("sie_import")) {
                    bool ch = migrateCompanyField(i);
                    ch |= migrateUserField(i, "importedBy");
                    if (ch) commit(i);
                }
                foreach (var v in all("voucher")) {
                    bool ch = migrateCompanyField(v);
                    ch |= migrateUserField(v, "createdBy");
                    ch |= migrateObjField(v, "reverses", "reversesId", "voucher");
                    ch |= migrateObjField(v, "reversedBy", "reversedById", "voucher");
                    ch |= migrateObjField(v, "source", "sourceId", str(v, "sourceType"));
                    if (ch) commit(v);
                }
                foreach (var inv in all("invoice")) {
                    bool ch = migrateCompanyField(inv);
                    ch |= migrateUserField(inv, "sentBy");
                    ch |= migrateObjField(inv, "customer", "customerId", "customer");
                    ch |= migrateObjField(inv, "voucher", "voucherId", "voucher");
                    ch |= migrateObjField(inv, "paymentVoucher", "paymentVoucherId", "voucher");
                    ch |= migrateObjField(inv, "closingVoucher", "closingVoucherId", "voucher");
                    ch |= migrateObjField(inv, "documentFile", "documentFileId", "_file");
                    if (ch) commit(inv);
                }
                foreach (var exp in all("expense")) {
                    bool ch = migrateCompanyField(exp);
                    ch |= migrateUserField(exp, "createdBy");
                    ch |= migrateObjField(exp, "voucher", "voucherId", "voucher");
                    ch |= migrateObjField(exp, "receiptFile", "receiptFileId", "_file");
                    ch |= migrateObjField(exp, "invoice", "invoiceId", "invoice");
                    if (ch) commit(exp);
                }
                foreach (var be in all("bank_event")) {
                    bool ch = migrateCompanyField(be);
                    ch |= migrateUserField(be, "createdBy");
                    ch |= migrateObjField(be, "voucher", "voucherId", "voucher");
                    ch |= migrateObjField(be, "file", "fileId", "_file");
                    if (ch) commit(be);
                }
                foreach (var se in all("security_event")) {
                    bool ch = migrateCompanyField(se);
                    ch |= migrateUserField(se, "createdBy");
                    ch |= migrateObjField(se, "voucher", "voucherId", "voucher");
                    if (ch) commit(se);
                }
            }
        }
    }
}
