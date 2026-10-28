using System;
using MeetingMinutes.Infrastructure;

namespace MeetingMinutes
{
    public partial class Settings : BasePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RequireRole(RoleCodes.Admin);
            if (IsPostBack) return;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = SettingsService.Get();
            txtOrgName.Text = s.OrgName;
            txtPrefix.Text = s.NumberPrefix;
            chkOverdue.Checked = s.OverdueReminder;
            ShowSample();
        }

        private void ShowSample()
        {
            var sample = (txtPrefix.Text ?? string.Empty).Replace("{YYYY}", TextNormalizer.ToPersianDigits(1405)) + "۰۰۱";
            litSample.Text = sample;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            string error;
            if (SettingsService.Save(txtOrgName.Text, txtPrefix.Text, chkOverdue.Checked, CurrentUser.UserId, out error))
            {
                litMessage.Text = Alert("تنظیمات ذخیره شد.", "alert-success");
                LoadSettings();
            }
            else
            {
                litMessage.Text = Alert(error, "alert-error");
                ShowSample();
            }
        }
    }
}
