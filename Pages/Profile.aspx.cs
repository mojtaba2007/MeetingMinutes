using System;
using MeetingMinutes.Infrastructure;

namespace MeetingMinutes
{
    public partial class Profile : BasePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            LoadProfile();
            switch (Request.QueryString["saved"])
            {
                case "1": litMessage.Text = Alert("اطلاعات شما ذخیره شد.", "alert-success"); break;
                case "photo": litMessage.Text = Alert("عکس پروفایل ذخیره شد.", "alert-success"); break;
                case "photo-removed": litMessage.Text = Alert("عکس پروفایل حذف شد.", "alert-success"); break;
            }
        }

        private void LoadProfile()
        {
            var p = ProfileService.Get(CurrentUser.UserId);
            if (p == null) return;   // اتفاق نمی‌افتد مگر کاربر همین لحظه حذف شده باشد

            txtUsername.Text = p.Username;
            txtRole.Text = p.RoleTitle;
            txtUnit.Text = p.UnitTitle.Length == 0 ? "—" : p.UnitTitle;

            txtPrefix.Text = p.Prefix;
            txtFirstName.Text = p.FirstName;
            txtLastName.Text = p.LastName;
            txtPosition.Text = p.Position;
            txtEmail.Text = p.Email;
            txtMobile.Text = p.Mobile;

            pnlIncomplete.Visible = !p.PersonId.HasValue;
            LoadAvatar();
        }
        private void LoadAvatar()
        {
            var p = ProfileService.Get(CurrentUser.UserId);
            if (p == null) return;

            var name = (p.FirstName + p.LastName).Length > 0 ? p.FirstName + p.LastName : p.Username;
            litAvatar.Text = AvatarHtml.Render(p.PersonId, p.PhotoVersion, name, "avatar-xl");

            btnRemovePhoto.Visible = p.PhotoVersion > 0;
            btnPhoto.Enabled = p.PersonId.HasValue;
            pnlNoPerson.Visible = !p.PersonId.HasValue;
        }

        protected void btnPhoto_Click(object sender, EventArgs e)
        {
            if (!fuPhoto.HasFile)
            {
                LoadAvatar();
                litMessage.Text = Alert("ابتدا یک عکس انتخاب کنید.", "alert-error");
                return;
            }

            string error;
            if (ProfileService.SavePhoto(CurrentUser.UserId, fuPhoto.PostedFile, out error))
            {
                Response.Redirect(Request.Path + "?saved=photo", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            LoadAvatar();
            litMessage.Text = Alert(error, "alert-error");
        }

        protected void btnRemovePhoto_Click(object sender, EventArgs e)
        {
            ProfileService.RemovePhoto(CurrentUser.UserId);
            Response.Redirect(Request.Path + "?saved=photo-removed", false);
            Context.ApplicationInstance.CompleteRequest();
        }
        protected void btnSave_Click(object sender, EventArgs e)
        {
            var input = new ProfileInput
            {
                Prefix = txtPrefix.Text,
                FirstName = txtFirstName.Text,
                LastName = txtLastName.Text,
                Position = txtPosition.Text,
                Email = txtEmail.Text,
                Mobile = txtMobile.Text
            };

            string error;
            if (ProfileService.Save(CurrentUser.UserId, input, out error))
            {
                // کوکی ورود دوباره صادر می‌شود تا نام جدید همین حالا در نوار بالا نمایش داده شود
                // (نام نمایشی در کوکی ذخیره می‌شود و بدون این کار تا ورود بعدی به‌روز نمی‌شد)
                var lr = new LoginResult
                {
                    Success = true,
                    UserId = CurrentUser.UserId,
                    RoleCode = CurrentUser.RoleCode,
                    DisplayName = ProfileService.DisplayName(CurrentUser.UserId)
                };
                AuthService.SignIn(Context, lr, User.Identity.Name);
                Response.Redirect(Request.Path + "?saved=1", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            litMessage.Text = Alert(error, "alert-error");
        }
    }
}
