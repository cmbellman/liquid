using com.swecial.server;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Companies and who may use them. A user has one company for now (the first membership); the
    /// membership class is there so more users and companies per user can come later.
    /// </summary>
    public static class Companies {
        public static SwecialObject forUser(string userId) {
            var membership = Db.all("membership").FirstOrDefault(m => Db.str(m, "userId") == userId);
            return membership == null ? null : Db.get("company", Db.str(membership, "companyId"));
        }

        public static SwecialObject create(string userId, string name) {
            lock (Db.Sync) {
                if (forUser(userId) != null) throw new LiquidException("Du har redan ett bolag.");
                var company = Db.create("company", null);
                company.set("name", name);
                company.set("paymentTerms", 30);
                company.set("createdBy", userId);
                Db.commit(company);

                var membership = Db.create("membership", null);
                membership.set("companyId", company.id);
                membership.set("userId", userId);
                membership.set("role", "owner");
                Db.commit(membership);

                Bas.seed(company.id);
                return company;
            }
        }
    }
}
