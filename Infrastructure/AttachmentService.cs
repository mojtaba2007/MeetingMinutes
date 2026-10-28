using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace MeetingMinutes.Infrastructure
{
    public static class AttachmentKinds
    {
        public const int SignedScan = 1;   // اسکن صورت‌جلسه امضاشده
        public const int General = 2;      // ضمیمه
        public const int GeneratedWord = 3; // فایل Word (رزرو برای آینده)

        public static string Title(int kind)
        {
            switch (kind)
            {
                case SignedScan: return "اسکن صورت‌جلسه امضاشده";
                case GeneratedWord: return "فایل Word";
                default: return "ضمیمه";
            }
        }
    }

    /// <summary>
    /// آپلود، فهرست و حذف ضمائم جلسه. فایل‌ها زیر App_Data ذخیره می‌شوند (IIS این پوشه را مستقیماً سرو نمی‌کند)
    /// و فقط از طریق AttachmentDownload.ashx —با بررسی دسترسی— در دسترس‌اند.
    /// ضمائم برخلاف مصوبات و مشروح، حتی بعد از نهایی‌شدن جلسه هم قابل افزودن‌اند (مثل اسکن نسخه‌ی امضاشده).
    /// </summary>
    public static class AttachmentService
    {
        // پسوندهای مجاز؛ هرگز فایل اجرایی یا اسکریپت پذیرفته نمی‌شود
        private static readonly Dictionary<string, string> AllowedExt = new Dictionary<string, string>
        {
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".tif", "image/tiff" }, { ".tiff", "image/tiff" }
        };
        private const long MaxSizeBytes = 15 * 1024 * 1024;

        public static string AllowedExtensionsText
        {
            get { return string.Join("، ", AllowedExt.Keys); }
        }

        public static DataTable List(int meetingId)
        {
            return Db.Query(
                @"SELECT a.AttachmentId, a.Kind, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAt,
                         COALESCE(p.FullName, u.Username) AS UploaderName
                  FROM dbo.Attachments a
                  LEFT JOIN dbo.Users u ON u.UserId = a.UploadedBy
                  LEFT JOIN dbo.People p ON p.PersonId = u.PersonId
                  WHERE a.MeetingId = @m
                  ORDER BY a.UploadedAt DESC", Db.P("@m", meetingId));
        }

        /// <summary>برای دانلود: مسیر فیزیکی، نام نمایشی و نوع محتوا، به‌همراه اطلاعات لازم برای بررسی دسترسی.</summary>
        public static DataRow GetForDownload(int attachmentId)
        {
            var dt = Db.Query(
                @"SELECT a.AttachmentId, a.MeetingId, a.FileName, a.StoragePath, a.ContentType,
                         m.StatusId AS MeetingStatusId, m.IsDeleted AS MeetingDeleted
                  FROM dbo.Attachments a JOIN dbo.Meetings m ON m.MeetingId = a.MeetingId
                  WHERE a.AttachmentId = @id", Db.P("@id", attachmentId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        /// <summary>اعتبارسنجی، ذخیره‌ی فیزیکی و ثبت رکورد. فایل تا اعتبارسنجی کامل روی دیسک نوشته نمی‌شود.</summary>
        public static bool Upload(int meetingId, int kind, HttpPostedFile file, int userId, out string error)
        {
            error = null;
            if (file == null || file.ContentLength == 0) { error = "فایلی انتخاب نشده است."; return false; }
            if (file.ContentLength > MaxSizeBytes) { error = "حجم فایل بیش از حد مجاز (۱۵ مگابایت) است."; return false; }

            var originalName = Path.GetFileName(file.FileName);
            var ext = Path.GetExtension(originalName).ToLowerInvariant();
            string contentType;
            if (!AllowedExt.TryGetValue(ext, out contentType))
            { error = "این نوع فایل مجاز نیست. فرمت‌های مجاز: " + AllowedExtensionsText; return false; }
            if (kind != AttachmentKinds.SignedScan && kind != AttachmentKinds.General) { error = "نوع ضمیمه نامعتبر است."; return false; }
            if ((int)Db.Scalar("SELECT COUNT(*) FROM dbo.Meetings WHERE MeetingId = @m AND IsDeleted = 0", Db.P("@m", meetingId)) == 0)
            { error = "جلسه یافت نشد."; return false; }

            var root = HttpContext.Current.Server.MapPath("~/App_Data/Attachments/" + meetingId);
            Directory.CreateDirectory(root);
            var storedName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(root, storedName);

            try
            {
                file.SaveAs(fullPath);
            }
            catch (IOException ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                error = "ذخیره‌ی فایل روی سرور ممکن نشد.";
                return false;
            }

            var relativePath = "App_Data/Attachments/" + meetingId + "/" + storedName;
            var displayName = Sanitize(originalName);
            Db.Execute(
                @"INSERT dbo.Attachments (MeetingId, Kind, FileName, StoragePath, ContentType, SizeBytes, UploadedBy)
                  VALUES (@m, @k, @f, @p, @c, @s, @u)",
                Db.P("@m", meetingId), Db.P("@k", kind), Db.P("@f", displayName), Db.P("@p", relativePath),
                Db.P("@c", contentType), Db.P("@s", (long)file.ContentLength), Db.P("@u", userId));

            Audit.Write(userId, "Create", "Attachment", meetingId, "بارگذاری ضمیمه: " + displayName);
            return true;
        }

        public static bool Delete(int attachmentId, int userId, out string error)
        {
            error = null;
            var dt = Db.Query("SELECT MeetingId, FileName, StoragePath FROM dbo.Attachments WHERE AttachmentId = @id", Db.P("@id", attachmentId));
            if (dt.Rows.Count == 0) { error = "ضمیمه یافت نشد."; return false; }
            var row = dt.Rows[0];

            Db.Execute("DELETE dbo.Attachments WHERE AttachmentId = @id", Db.P("@id", attachmentId));
            try
            {
                var full = HttpContext.Current.Server.MapPath("~/" + (string)row["StoragePath"]);
                if (File.Exists(full)) File.Delete(full);
            }
            catch (IOException ex)
            {
                // رکورد دیتابیس حذف شد؛ نبودِ فایل فیزیکی روی دیسک را فقط لاگ می‌کنیم
                System.Diagnostics.Trace.TraceError(ex.ToString());
            }

            Audit.Write(userId, "Delete", "Attachment", (int)row["MeetingId"], "حذف ضمیمه: " + row["FileName"]);
            return true;
        }

        public static string FormatSize(object bytesObj)
        {
            if (bytesObj is DBNull || bytesObj == null) return "—";
            long bytes = Convert.ToInt64(bytesObj);
            if (bytes >= 1024 * 1024) return TextNormalizer.ToPersianDigits(Math.Round(bytes / 1024.0 / 1024.0, 1)) + " مگابایت";
            return TextNormalizer.ToPersianDigits(Math.Max(1, bytes / 1024)) + " کیلوبایت";
        }

        /// <summary>نام فایل را برای نمایش امن می‌کند (کاراکترهای کنترلی/جداکننده‌ی مسیر را حذف می‌کند)، طول را محدود می‌کند.</summary>
        private static string Sanitize(string name)
        {
            name = Regex.Replace(name, "[\\/:*?\"<>|\\x00-\\x1F]", "_").Trim();
            if (name.Length > 200)
            {
                var ext = Path.GetExtension(name);
                name = name.Substring(0, 200 - ext.Length) + ext;
            }
            return name.Length == 0 ? "file" : name;
        }
    }
}
