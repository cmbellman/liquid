using com.swecial.server;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Companies and who may use them. A user has one company for now (the first membership); the
    /// membership class is there so more users and companies per user can come later.
    /// </summary>
    public static class Companies {
        public static SwecialObject forUser(string userId) {
            if (string.IsNullOrEmpty(userId)) return null;
            var membership = Db.all("membership").FirstOrDefault(m => Db.linkedId(m, "user") == userId);
            return Db.company(membership);
        }

        public static SwecialObject create(string userId, string name) {
            lock (Db.Sync) {
                if (forUser(userId) != null) throw new LiquidException("Du har redan ett bolag.", "A COMPANY IS ALREADY REGISTERED TO THIS USER.");
                var company = Db.create("company", null);
                company.set("name", name);
                company.set("paymentTerms", 30);
                Db.linkUser(company, "createdBy", userId);
                Db.commit(company);

                var membership = Db.create("membership", company.id);
                Db.linkUser(membership, "user", userId);
                membership.set("role", "owner");
                Db.commit(membership);

                Bas.seed(company.id);
                return company;
            }
        }
    }
}
