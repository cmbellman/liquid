using com.swecial.server;

namespace com.swecial.code {
    /// <summary>
    /// Liquid has no game client: everything goes through the web pages in www/. The SDK protocol
    /// still reaches the app, so the hooks below keep it away from the books.
    /// </summary>
    public class Cloud : CustomCode {

        /// <summary>
        /// Runs before every SDK save. The server stores the save whatever this hook does, so it
        /// turns it into a harmless empty object of a class Liquid never reads.
        /// </summary>
        public void beforeSave() {
            if (req?.o == null) return;
            log.write("liquid: refused SDK save of " + req.o.className + " " + req.o.id);
            req.o.clear();
            req.o.set("_className", "refused_sdk_save");
        }

        /// <summary>Gives every company the accounts added to the starter chart since it was created.</summary>
        public override void onStartUp() {
            foreach (var company in Db.all("company")) {
                Bas.seed(company.id);
            }
            log.write("liquid: started");
        }
    }
}
