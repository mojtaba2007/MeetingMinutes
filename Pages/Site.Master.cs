using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using MeetingMinutes.Infrastructure;

namespace MeetingMinutes
{
    public class NavItem
    {
        public string Section { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Icon { get; set; }
        public string[] Roles { get; set; }
    }

    public partial class SiteMaster : System.Web.UI.MasterPage
    {
        private static readonly string[] AdminSecretary = { RoleCodes.Admin, RoleCodes.Secretary };
        private static readonly string[] AdminOnly = { RoleCodes.Admin };

        // منوی کناری مطابق دمو. آیتمی که صفحه‌اش هنوز ساخته نشده کمرنگ و غیرفعال نمایش داده می‌شود
        // و به‌محض افزودن فایل صفحه، خودکار فعال می‌شود.
        private static readonly NavItem[] Items =
        {
            new NavItem { Section = "مدیریت", Title = "داشبورد",       Url = "~/Pages/Default.aspx",   Icon = "ti-layout-dashboard" },
            new NavItem { Section = "مدیریت", Title = "جلسات",         Url = "~/Pages/Meetings.aspx",  Icon = "ti-users" },
            new NavItem { Section = "مدیریت", Title = "اقدامات",       Url = "~/Pages/Actions.aspx",   Icon = "ti-checklist" },
            new NavItem { Section = "مدیریت", Title = "گزارش‌ها",      Url = "~/Pages/Reports.aspx",   Icon = "ti-chart-bar" },

            new NavItem { Section = "اطلاعات پایه", Title = "افراد",    Url = "~/Pages/People.aspx",   Icon = "ti-user-circle", Roles = AdminSecretary },
            new NavItem { Section = "اطلاعات پایه", Title = "واحدها",   Url = "~/Pages/OrgUnits.aspx", Icon = "ti-building",    Roles = AdminSecretary },
            new NavItem { Section = "اطلاعات پایه", Title = "کاربران",  Url = "~/Pages/Admin/Users.aspx",    Icon = "ti-lock",        Roles = AdminOnly },
            new NavItem { Section = "اطلاعات پایه", Title = "اعلان‌ها",  Url = "~/Pages/Admin/Notifications.aspx", Icon = "ti-speakerphone", Roles = AdminOnly },
            new NavItem { Section = "اطلاعات پایه", Title = "تنظیمات",  Url = "~/Pages/Admin/Settings.aspx", Icon = "ti-settings",    Roles = AdminOnly }
        };

        protected void Page_Load(object sender, EventArgs e)
        {
            var user = Context.User as AppPrincipal;
            if (user != null)
            {
                litName.Text = user.DisplayName;
                litRole.Text = RoleCodes.Title(user.RoleCode);
                int? pid; int ver;
                ProfileService.GetAvatar(user.UserId, out pid, out ver);
                litAvatar.Text = AvatarHtml.Render(pid, ver, litName.Text, "purple");

                // با هر درخواست (حتی postback) دوباره خوانده می‌شود تا بلافاصله بعد از تکمیل پروفایل
                // یا مشاهده‌ی یک اعلان، وضعیت درست نشان داده شود
                BindNotifications(user.UserId);
            }
            if (!IsPostBack)
            {
                BindNav(user);
                var org = SettingsService.OrgName();
                litOrgName.Text = org.Length == 0 ? string.Empty : org;
            }
        }

        private void BindNotifications(int userId)
        {
            // هشدار «تکمیل پروفایل»: چون بر اساس داده‌ی واقعی محاسبه می‌شود (نه یک ردیف قابل‌بستن)،
            // تا وقتی فرد/ایمیل/تلفن کامل نشود، هر بار دوباره نشان داده می‌شود — کاربر فقط می‌تواند
            // با «نمایش کامل» به صفحه‌ی پروفایل برود، نه اینکه آن را کنار بزند.
            var onProfilePage = string.Equals(Request.AppRelativeCurrentExecutionFilePath, "~/Pages/Profile.aspx", StringComparison.OrdinalIgnoreCase);
            pnlProfileNotif.Visible = !onProfilePage && ProfileService.IsIncomplete(userId);

            // فهرست شامل خوانده‌شده‌ها هم هست (تا مدیر حذفشان نکند، از فهرست کاربر کنار نمی‌روند)؛
            // شمارنده‌ی زنگوله جدا محاسبه می‌شود چون فقط نخوانده‌ها را می‌شمارد
            var dt = NotificationService.ActiveForUser(userId);
            rptNotifications.DataSource = dt;
            rptNotifications.DataBind();
            pnlNoNotif.Visible = dt.Rows.Count == 0 && !pnlProfileNotif.Visible;

            int unread = NotificationService.CountUnread(userId) + (pnlProfileNotif.Visible ? 1 : 0);
            litNotifBadge.Text = unread == 0 ? string.Empty
                : "<span class=\"notif-badge\">" + TextNormalizer.ToPersianDigits(Math.Min(unread, 9)) + (unread > 9 ? "+" : "") + "</span>";
        }

        protected static string FaDate(object v)
        {
            return v is DateTime ? PersianDate.ToDisplay((DateTime)v, true) : string.Empty;
        }

        protected static string NotifTitle(object title, object body)
        {
            if (!(title is DBNull) && !string.IsNullOrWhiteSpace((string)title)) return (string)title;
            var b = (string)body;
            return b.Length > 30 ? b.Substring(0, 30) + "…" : b;
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            var t = Page.Title;
            Page.Title = string.IsNullOrEmpty(t) ? "سامانه مدیریت جلسات" : t + " — سامانه مدیریت جلسات";
        }

        private void BindNav(AppPrincipal user)
        {
            var current = Request.AppRelativeCurrentExecutionFilePath;
            var sb = new StringBuilder();
            string lastSection = null;

            foreach (var it in Items)
            {
                if (it.Roles != null && (user == null || Array.IndexOf(it.Roles, user.RoleCode) < 0)) continue;

                if (it.Section != lastSection)
                {
                    sb.Append("<div class=\"nav-title\">").Append(HttpUtility.HtmlEncode(it.Section)).Append("</div>");
                    lastSection = it.Section;
                }

                bool exists = File.Exists(Server.MapPath(it.Url));
                bool active = string.Equals(current, it.Url, StringComparison.OrdinalIgnoreCase);
                var css = active ? "active" : (exists ? string.Empty : "disabled");
                var href = exists ? ResolveUrl(it.Url) : "#";

                sb.Append("<a class=\"nav-item ").Append(css).Append("\" href=\"")
                  .Append(HttpUtility.HtmlAttributeEncode(href)).Append("\"><i class=\"ti ")
                  .Append(HttpUtility.HtmlAttributeEncode(it.Icon)).Append("\"></i>")
                  .Append(HttpUtility.HtmlEncode(it.Title)).Append("</a>");
            }
            litNav.Text = sb.ToString();
        }

        private static string Initial(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "؟";
            var last = name.Trim().Split(' ').Last();
            return last.Length > 0 ? last.Substring(0, 1) : "؟";
        }

        protected void lnkLogout_Click(object sender, EventArgs e)
        {
            var user = Context.User as AppPrincipal;
            if (user != null) Audit.Write(user.UserId, "Logout", "User", user.UserId, "خروج از سامانه");
            FormsAuthentication.SignOut();
            Response.Redirect(FormsAuthentication.LoginUrl, false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
