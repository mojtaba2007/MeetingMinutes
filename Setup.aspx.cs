using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using MeetingMinutes.Infrastructure;

namespace MeetingMinutes
{
    public partial class Setup : System.Web.UI.Page
    {
        private static readonly Regex UsernameRx = new Regex(@"^[A-Za-z0-9_.@-]{3,50}$", RegexOptions.Compiled);

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            try
            {
                if (AuthService.HasAnyUser()) GoToLogin(null);
            }
            catch (Exception ex) when (ex is SqlException || ex is System.Configuration.ConfigurationErrorsException)
            {
                System.Diagnostics.Debug.WriteLine("[Setup Error] " + ex.Message);
                ShowMessage("اتصال به دیتابیس برقرار نشد. متغیر MEETINGMINUTES_CONNECTION_STRING یا رشته‌ی اتصال MeetingDb در Web.config را بررسی کنید.");
                btnCreate.Enabled = false;
            }
        }

        protected void btnCreate_Click(object sender, EventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var fullName = TextNormalizer.Clean(txtFullName.Text);
            var password = txtPassword.Text;

            if (!UsernameRx.IsMatch(username))
            {
                ShowMessage("نام کاربری باید ۳ تا ۵۰ نویسه و فقط شامل حروف انگلیسی، عدد و . _ @ - باشد.");
                return;
            }
            string error;
            if (!PasswordHasher.TryValidate(username, password, out error))
            {
                ShowMessage(error);
                return;
            }
            if (password != txtConfirm.Text)
            {
                ShowMessage("رمز عبور و تکرار آن یکسان نیستند.");
                return;
            }

            try
            {
                // اگر بین همین لحظه کاربری ساخته شده باشد false برمی‌گرداند
                if (!AuthService.CreateFirstAdmin(username, fullName, password))
                {
                    GoToLogin(null);
                    return;
                }
            }
            catch (SqlException ex)
            {
                System.Diagnostics.Debug.WriteLine("[Setup Error] " + ex.Message);
                ShowMessage("ساخت کاربر با خطای دیتابیس روبه‌رو شد. مطمئن شوید اسکریپت دیتابیس کامل اجرا شده است.");
                return;
            }

            GoToLogin("?setup=1");
        }

        private void GoToLogin(string query)
        {
            Response.Redirect("~/Login.aspx" + query, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void ShowMessage(string text)
        {
            litMessage.Text = "<div class=\"alert alert-error\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
