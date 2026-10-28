<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Setup.aspx.cs" Inherits="MeetingMinutes.Setup" %>
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>ساخت مدیر اولیه — سامانه مدیریت جلسات</title>
    <link runat="server" rel="stylesheet" href="~/Content/assets/font/Vazirmatn-font-face.css" />
    <link runat="server" rel="stylesheet" href="~/Content/assets/tabler-icons/tabler-icons.min.css" />
    <link runat="server" rel="stylesheet" href="~/Content/Site.css" />
</head>
<body>
<form id="form1" runat="server" defaultbutton="btnCreate" class="login-wrap">
    <div class="login-card">
        <div class="login-brand"><i class="ti ti-shield-lock"></i><span>راه‌اندازی اولیه</span></div>
        <h1>ساخت مدیر سامانه</h1>
        <p class="sub">این صفحه فقط تا زمانی که هیچ کاربری وجود ندارد کار می‌کند. پس از ساخت مدیر، فایل Setup.aspx را حذف کنید.</p>

        <asp:Literal ID="litMessage" runat="server" />

        <div class="field">
            <label for="txtFullName">نام و نام خانوادگی (اختیاری)</label>
            <asp:TextBox ID="txtFullName" runat="server" MaxLength="200" />
        </div>
        <div class="field">
            <label for="txtUsername">نام کاربری (حروف انگلیسی، عدد، نقطه و خط‌تیره)</label>
            <asp:TextBox ID="txtUsername" runat="server" CssClass="ltr" MaxLength="50" autocomplete="username" />
        </div>
        <div class="field">
            <label for="txtPassword">رمز عبور (حداقل ۸ نویسه)</label>
            <asp:TextBox ID="txtPassword" runat="server" TextMode="Password" CssClass="ltr" MaxLength="100" autocomplete="new-password" />
        </div>
        <div class="field">
            <label for="txtConfirm">تکرار رمز عبور</label>
            <asp:TextBox ID="txtConfirm" runat="server" TextMode="Password" CssClass="ltr" MaxLength="100" autocomplete="new-password" />
        </div>
        <asp:Button ID="btnCreate" runat="server" Text="ساخت مدیر سامانه" CssClass="btn btn-primary btn-block" OnClick="btnCreate_Click" />
    </div>
</form>
</body>
</html>
