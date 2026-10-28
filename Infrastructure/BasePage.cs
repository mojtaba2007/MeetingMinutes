using System;
using System.Web;
using System.Web.Security;
using System.Web.UI;

namespace MeetingMinutes.Infrastructure
{
    /// <summary>همه صفحات نیازمند ورودبه‌جز Login و Setup</summary>
    public class BasePage : Page
    {
        protected AppPrincipal CurrentUser
        {
            get { return Context.User as AppPrincipal; }
        }

        /// <summary>فقط صفحه‌ی تغییر رمز true برمی‌گرداند تا کاربرِ دارای رمز موقت بتواند به آن برسد.</summary>
        protected virtual bool AllowPendingPasswordChange
        {
            get { return false; }
        }

        protected override void OnInit(EventArgs e)
        {
            // جلوگیری از نمایش صفحات با دکمه‌ی Back بعد از خروج
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            var u = CurrentUser;
            if (u != null)
            {
                // محافظت در برابر CSRF روی ViewState
                ViewStateUserKey = u.UserId.ToString();

                // بررسی وضعیت جاری حساب در هر درخواست (یک جست‌وجوی ساده روی کلید اصلی)
                var dt = Db.Query(
                    @"SELECT u.IsActive, u.MustChangePassword, r.Code AS RoleCode
                      FROM dbo.Users u JOIN dbo.Roles r ON r.RoleId = u.RoleId
                      WHERE u.UserId = @id", Db.P("@id", u.UserId));

                // کاربر حذف/غیرفعال شده یا نقشش عوض شده: خروج و ورود دوباره
                if (dt.Rows.Count == 0 || !(bool)dt.Rows[0]["IsActive"] || (string)dt.Rows[0]["RoleCode"] != u.RoleCode)
                {
                    FormsAuthentication.SignOut();
                    Response.Redirect(FormsAuthentication.LoginUrl, true);
                    return;
                }
                if ((bool)dt.Rows[0]["MustChangePassword"] && !AllowPendingPasswordChange)
                {
                    Response.Redirect("~/Pages/ChangePassword.aspx?forced=1", true);
                    return;
                }
            }

            base.OnInit(e);
        }

        /// <summary>اگر کاربر هیچ‌کدام از نقش‌های داده‌شده را نداشت، به داشبورد با پیام «عدم دسترسی» برمی‌گردد.</summary>
        protected void RequireRole(params string[] roleCodes)
        {
            var u = CurrentUser;
            if (u == null || Array.IndexOf(roleCodes, u.RoleCode) < 0)
                Response.Redirect("~/Pages/Default.aspx?denied=1", true);
        }

        /// <summary> alert-error / alert-success / alert-info</summary>
        protected static string Alert(string text, string cssClass)
        {
            return "<div class=\"alert " + cssClass + "\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
