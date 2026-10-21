using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/pant.html : Pantpåsen. Throw receipts in (drop, pick or take a photo, several at once)
    /// without filling in anything; they wait here until they're sorted on the expense page, where
    /// they are booked. POST action=bag adds receipts, action=remove throws one away.
    /// </summary>
    public class WebPant : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Pantpåsen");
            if (isPost) {
                if (form("action") == "remove") {
                    var item = Db.getOwned("expense", form("id"), companyId);
                    if (item == null || Db.str(item, "status") != Expenses.Inbox) throw new LiquidException("Kvittot finns inte i påsen.");
                    Expenses.remove(item, account.id);
                    redirect(BasePath + "/pant.html?ok=deleted");
                    return;
                }
                bag(req, companyId, account.id);
                redirect(BasePath + "/pant.html?ok=bagged");
                return;
            }
            renderPage();
        }

        protected override void renderAfterError() {
            renderPage();
        }

        /// <summary>Puts every uploaded receipt in the bag. All are checked first, so it's all or nothing.</summary>
        public static int bag(HttpRequest req, string companyId, string userId) {
            var files = req.body.fileList.Where(f => f.data != null && f.data.Length > 0).ToList();
            if (files.Count == 0) throw new LiquidException("Välj eller dra in minst ett kvitto.");
            foreach (var file in files) {
                try {
                    Expenses.checkReceipt(file);
                } catch (LiquidException ex) {
                    throw new LiquidException(file.fileName + ": " + ex.Message);
                }
            }
            foreach (var file in files) Expenses.addToInbox(companyId, file, userId);
            return files.Count;
        }

        void renderPage() {
            var items = Expenses.inbox(companyId);
            var sb = new StringBuilder(heading("Pantpåsen", "Släng in kvitton direkt, sortera dem när det passar"));
            sb.Append(dropZone(items.Count, BasePath + "/pant.html", false));
            sb.Append("<section class='panel'>");
            if (items.Count == 0) {
                sb.Append("<p class='empty'>Påsen är tom. Allt är sorterat.</p>");
            } else {
                sb.Append("<div class='heading'><div><h2>" + items.Count + (items.Count == 1 ? " kvitto" : " kvitton") + " att sortera</h2>");
                sb.Append("<p class='sub'>Ange vad det var och beloppet, så bokförs det.</p></div>");
                sb.Append("<div class='heading-actions'><a class='button' href='" + BasePath + "/expense.html?id=" + u(items[0].id) + "'>Börja sortera</a></div></div>");
                sb.Append("<div class='bag'>");
                foreach (var item in items) {
                    string fileId = Db.str(item, "receiptFileId");
                    var file = Expenses.receipt(fileId, companyId);
                    string src = BasePath + "/receipt.html?id=" + u(item.id) + "&v=" + u(fileId);
                    string type = file?.fileType ?? "";
                    sb.Append("<div class='bag-item'><a class='thumb' href='" + BasePath + "/expense.html?id=" + u(item.id) + "'>");
                    sb.Append(type == "pdf" ? "PDF" : type == "heic" ? "HEIC-bild" : "<img src='" + src + "' alt='Kvitto' loading='lazy'>");
                    sb.Append("</a><div class='meta'><span>" + h(Db.str(file, "originalName")) + "<br>Inlagt " + item.createdAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + "</span>");
                    sb.Append("<div class='actions'><a class='button' href='" + BasePath + "/expense.html?id=" + u(item.id) + "'>Sortera</a>");
                    sb.Append("<form method='post' class='row-action'>" + input("id", item.id, "hidden") + "<button type='submit' name='action' value='remove' class='link danger'>Släng</button></form>");
                    sb.Append("</div></div></div>");
                }
                sb.Append("</div>");
            }
            sb.Append("</section>");
            sb.Append(DropScript);
            render(sb.ToString());
        }

        /// <summary>
        /// The drop zone: click to pick files, drop them on it, or take a photo (phones). Submits as
        /// soon as files are chosen. Posts action=bag to the given page.
        /// </summary>
        public static string dropZone(int waiting, string action, bool withHeading) {
            var sb = new StringBuilder("<section class='panel'>");
            if (withHeading) {
                sb.Append("<div class='heading'><div><h2>Pantpåsen</h2><p class='sub'>Släng in dina kvitton här</p></div>");
                if (waiting > 0) sb.Append("<div class='heading-actions'><a href='" + BasePath + "/pant.html'>" + waiting + " i påsen</a></div>");
                sb.Append("</div>");
            }
            sb.Append("<form method='post' enctype='multipart/form-data' action='" + action + "' class='bag-form'>" + input("action", "bag", "hidden"));
            sb.Append("<label class='drop'>" + Icons.Upload + "<div><strong>Dra in kvitton eller klicka</strong><span>Bilder eller PDF, flera på en gång</span></div>");
            sb.Append("<input type='file' name='receipts' accept='image/*,application/pdf' multiple></label>");
            sb.Append("<label class='button secondary camera'>" + Icons.Camera + "Fota kvitto<input type='file' name='photo' accept='image/*' capture='environment'></label>");
            sb.Append("</form></section>");
            return sb.ToString();
        }

        /// <summary>Submits the bag form when files are picked or dropped. Inline: no .js files on the server.</summary>
        public const string DropScript = @"<script>
(function () {
  document.querySelectorAll('.bag-form').forEach(function (f) {
    var drop = f.querySelector('.drop'), input = drop.querySelector('input');
    f.querySelectorAll('input[type=file]').forEach(function (i) { i.addEventListener('change', function () { if (i.files.length) f.submit(); }); });
    ['dragenter', 'dragover'].forEach(function (e) { drop.addEventListener(e, function (ev) { ev.preventDefault(); drop.classList.add('over'); }); });
    ['dragleave', 'drop'].forEach(function (e) { drop.addEventListener(e, function (ev) { ev.preventDefault(); drop.classList.remove('over'); }); });
    drop.addEventListener('drop', function (ev) { if (ev.dataTransfer.files.length) { input.files = ev.dataTransfer.files; f.submit(); } });
  });
})();
</script>";
    }
}
