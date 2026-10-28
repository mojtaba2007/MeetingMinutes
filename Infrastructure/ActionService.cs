using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;

namespace MeetingMinutes.Infrastructure
{
    public class ActionFilter
    {
        public string Query { get; set; }
        public int? PersonId { get; set; }
        public int? UnitId { get; set; }
        public int? StatusId { get; set; }
        public bool OnlyOpen { get; set; }
        public bool OnlyOverdue { get; set; }
        public DateTime? DueFrom { get; set; }
        public DateTime? DueTo { get; set; }
        public bool CanSeeDrafts { get; set; }
    }

    /// <summary>پیگیری اقدام‌ها در همه‌ی جلسه‌ها. تغییر وضعیت اقدام «ویرایش صورت‌جلسه» نیست؛
    /// پس حتی بعد از نهایی‌شدن جلسه هم ممکن است و در جدول ActionFollowUps ثبت می‌شود.</summary>
    public static class ActionService
    {
        private const string BaseSelect =
            @"SELECT a.ActionId, a.Title, d.DecisionText, a.ResponsiblePersonId, a.ResponsibleUnitId,
                     p.FullName AS PersonName, u.Title AS UnitTitle, a.ResponsibleNote,
                     a.DueDate, a.DueText, a.StatusId, s.Title AS StatusTitle, s.CssClass, s.IsClosed, a.CompletedAt,
                     CAST(CASE WHEN s.IsClosed = 0 AND a.DueDate < CAST(GETDATE() AS date) THEN 1 ELSE 0 END AS bit) AS IsOverdue,
                     m.MeetingId, m.MeetingNo, m.MeetingDate, m.StatusId AS MeetingStatusId,
                     COALESCE(NULLIF(m.Title, N''), mt.Title) AS MeetingTitle
              FROM dbo.ActionItems a
              JOIN dbo.Decisions d ON d.DecisionId = a.DecisionId
              JOIN dbo.Meetings m ON m.MeetingId = d.MeetingId AND m.IsDeleted = 0
              JOIN dbo.MeetingTypes mt ON mt.MeetingTypeId = m.MeetingTypeId
              JOIN dbo.ActionStatuses s ON s.StatusId = a.StatusId
              LEFT JOIN dbo.People p ON p.PersonId = a.ResponsiblePersonId
              LEFT JOIN dbo.OrgUnits u ON u.OrgUnitId = a.ResponsibleUnitId ";

        public static DataTable Search(ActionFilter f)
        {
            var q = TextNormalizer.Clean(f.Query);
            var qn = TextNormalizer.Normalize(f.Query);
            return Db.Query(BaseSelect +
                @"WHERE (@drafts = 1 OR m.StatusId <> 1)
                    AND (@q = ''
                         OR a.Title LIKE @like ESCAPE '\'
                         OR d.DecisionText LIKE @like ESCAPE '\'
                         OR p.FullName LIKE @like ESCAPE '\'
                         OR p.SearchName LIKE @likeN ESCAPE '\'
                         OR EXISTS (SELECT 1 FROM dbo.PersonAliases al WHERE al.PersonId = p.PersonId AND al.AliasName LIKE @likeN ESCAPE '\')
                         OR u.Title LIKE @like ESCAPE '\'
                         OR a.ResponsibleNote LIKE @like ESCAPE '\')
                    AND (@person IS NULL OR a.ResponsiblePersonId = @person)
                    AND (@unit IS NULL OR a.ResponsibleUnitId = @unit)
                    AND (@status IS NULL OR a.StatusId = @status)
                    AND (@open = 0 OR s.IsClosed = 0)
                    AND (@overdue = 0 OR (s.IsClosed = 0 AND a.DueDate < CAST(GETDATE() AS date)))
                    AND (@dfrom IS NULL OR a.DueDate >= @dfrom)
                    AND (@dto IS NULL OR a.DueDate <= @dto)
                  ORDER BY CASE WHEN s.IsClosed = 0 AND a.DueDate < CAST(GETDATE() AS date) THEN 0 ELSE 1 END,
                           s.IsClosed,
                           CASE WHEN a.DueDate IS NULL THEN 1 ELSE 0 END, a.DueDate,
                           m.MeetingDate DESC, a.ActionId",
                Db.P("@drafts", f.CanSeeDrafts), Db.P("@q", q),
                Db.P("@like", Db.LikePattern(q)), Db.P("@likeN", Db.LikePattern(qn)),
                Db.P("@person", f.PersonId), Db.P("@unit", f.UnitId), Db.P("@status", f.StatusId),
                Db.P("@open", f.OnlyOpen), Db.P("@overdue", f.OnlyOverdue),
                Db.P("@dfrom", f.DueFrom), Db.P("@dto", f.DueTo));
        }

        public static DataRow Get(int actionId, bool canSeeDrafts)
        {
            var dt = Db.Query(BaseSelect + "WHERE a.ActionId = @a AND (@drafts = 1 OR m.StatusId <> 1)",
                              Db.P("@a", actionId), Db.P("@drafts", canSeeDrafts));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataTable ResponsiblePeople()
        {
            return Db.Query(
                @"SELECT DISTINCT p.PersonId, p.FullName, p.LastName, p.FirstName
                  FROM dbo.ActionItems a JOIN dbo.People p ON p.PersonId = a.ResponsiblePersonId
                  ORDER BY p.LastName, p.FirstName");
        }

        public static DataTable ResponsibleUnits()
        {
            return Db.Query(
                @"SELECT DISTINCT u.OrgUnitId, u.Title
                  FROM dbo.ActionItems a JOIN dbo.OrgUnits u ON u.OrgUnitId = a.ResponsibleUnitId
                  ORDER BY u.Title");
        }

        public static DataTable Statuses()
        {
            return Db.Query("SELECT StatusId, Title, IsClosed FROM dbo.ActionStatuses ORDER BY StatusId");
        }

        /// <summary>جلسه‌های اخیر برای انتخاب بررسی‌شده در جلسه‌ی...</summary>
        public static DataTable RecentMeetings(bool canSeeDrafts, int take)
        {
            return Db.Query(
                @"SELECT TOP (@n) m.MeetingId, m.MeetingDate, m.MeetingDateFa, COALESCE(NULLIF(m.Title, N''), mt.Title) AS Title
                  FROM dbo.Meetings m JOIN dbo.MeetingTypes mt ON mt.MeetingTypeId = m.MeetingTypeId
                  WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1)
                  ORDER BY m.MeetingDate DESC, m.MeetingId DESC", Db.P("@n", take), Db.P("@drafts", canSeeDrafts));
        }

        public static int? PersonOfUser(int userId)
        {
            var v = Db.Scalar("SELECT PersonId FROM dbo.Users WHERE UserId = @u", Db.P("@u", userId));
            return v == null || v is DBNull ? (int?)null : (int)v;
        }

        public static DataTable FollowUps(int actionId)
        {
            return Db.Query(
                @"SELECT f.FollowUpId, f.FollowUpDate, ost.Title AS OldTitle, nst.Title AS NewTitle, f.OldStatusId, f.NewStatusId, f.Comment,
                         m.MeetingId, m.MeetingNo, m.MeetingDate, COALESCE(pe.FullName, us.Username) AS ByName
                  FROM dbo.ActionFollowUps f
                  JOIN dbo.ActionStatuses nst ON nst.StatusId = f.NewStatusId
                  LEFT JOIN dbo.ActionStatuses ost ON ost.StatusId = f.OldStatusId
                  LEFT JOIN dbo.Meetings m ON m.MeetingId = f.MeetingId
                  LEFT JOIN dbo.Users us ON us.UserId = f.CreatedBy
                  LEFT JOIN dbo.People pe ON pe.PersonId = us.PersonId
                  WHERE f.ActionId = @a
                  ORDER BY f.FollowUpDate DESC, f.FollowUpId DESC", Db.P("@a", actionId));
        }

        /// <summary>کاربر می‌تواند برای این اقدام پیگیری ثبت کند؟ مدیر و دبیر: همه؛ عضو: فقط اقدام‌های خودش</summary>
        public static bool CanFollow(bool isEditor, int? userPersonId, object responsiblePersonId)
        {
            if (isEditor) return true;
            return userPersonId.HasValue && !(responsiblePersonId is DBNull) && responsiblePersonId != null
                   && (int)responsiblePersonId == userPersonId.Value;
        }

        public static bool AddFollowUp(int actionId, int newStatusId, string comment, int? reviewMeetingId,
                                       int userId, bool isEditor, bool canSeeDrafts, out string error)
        {
            error = null;
            var row = Get(actionId, canSeeDrafts);
            if (row == null) { error = "اقدام یافت نشد."; return false; }

            var personId = PersonOfUser(userId);
            if (!CanFollow(isEditor, personId, row["ResponsiblePersonId"])) { error = "فقط مسئول همین اقدام (یا دبیر و مدیر) می‌تواند پیگیری ثبت کند."; return false; }

            comment = TextNormalizer.CleanOrNull(comment);
            if (comment != null && comment.Length > 1000) { error = "توضیح حداکثر ۱۰۰۰ نویسه است."; return false; }

            int oldStatus = Convert.ToInt32(row["StatusId"]);
            if (newStatusId == oldStatus && comment == null && !reviewMeetingId.HasValue)
            { error = "برای ثبت پیگیری، وضعیت را تغییر دهید یا توضیح بنویسید."; return false; }
            if ((int)Db.Scalar("SELECT COUNT(*) FROM dbo.ActionStatuses WHERE StatusId = @s", Db.P("@s", newStatusId)) == 0) { error = "وضعیت نامعتبر است."; return false; }
            if (reviewMeetingId.HasValue &&
                (int)Db.Scalar("SELECT COUNT(*) FROM dbo.Meetings WHERE MeetingId = @m AND IsDeleted = 0", Db.P("@m", reviewMeetingId.Value)) == 0)
            { error = "جلسه‌ی انتخاب‌شده نامعتبر است."; return false; }

            using (var con = Db.Open())
            using (var tx = con.BeginTransaction())
            {
                using (var c = new SqlCommand(
                    @"UPDATE dbo.ActionItems
                      SET StatusId = @s,
                          CompletedAt = CASE WHEN @s = 3 THEN COALESCE(CompletedAt, CAST(GETDATE() AS date)) ELSE NULL END,
                          UpdatedBy = @u, UpdatedAt = SYSDATETIME()
                      WHERE ActionId = @a", con, tx))
                {
                    c.Parameters.Add(Db.P("@s", newStatusId)); c.Parameters.Add(Db.P("@u", userId)); c.Parameters.Add(Db.P("@a", actionId));
                    c.ExecuteNonQuery();
                }
                using (var c = new SqlCommand(
                    @"INSERT dbo.ActionFollowUps (ActionId, MeetingId, OldStatusId, NewStatusId, Comment, CreatedBy)
                      VALUES (@a, @m, @o, @n, @c, @u)", con, tx))
                {
                    c.Parameters.Add(Db.P("@a", actionId)); c.Parameters.Add(Db.P("@m", reviewMeetingId));
                    c.Parameters.Add(Db.P("@o", oldStatus)); c.Parameters.Add(Db.P("@n", newStatusId));
                    c.Parameters.Add(Db.P("@c", comment)); c.Parameters.Add(Db.P("@u", userId));
                    c.ExecuteNonQuery();
                }
                tx.Commit();
            }
            Audit.Write(userId, "Update", "Action", actionId, newStatusId == oldStatus ? "ثبت پیگیری" : "تغییر وضعیت اقدام");
            return true;
        }

        // ---------- خروجی CSV ----------
        public static string ToCsv(DataTable dt)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join(",", new[] { "ردیف", "شرح اقدام", "مسئول", "مهلت", "وضعیت", "معوق", "تاریخ انجام", "جلسه", "شماره جلسه", "تاریخ جلسه" }.Select(Csv)));
            int i = 0;
            foreach (DataRow r in dt.Rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv((++i).ToString()), Csv((string)r["Title"]), Csv(ResponsibleText(r)), Csv(DueText(r)),
                    Csv((string)r["StatusTitle"]), Csv((bool)r["IsOverdue"] ? "بله" : ""),
                    Csv(r["CompletedAt"] is DateTime ? PersianDate.ToJalali((DateTime)r["CompletedAt"]) : ""),
                    Csv((string)r["MeetingTitle"]), Csv(r["MeetingNo"] is DBNull ? "" : (string)r["MeetingNo"]),
                    Csv(PersianDate.ToJalali((DateTime)r["MeetingDate"]))
                }));
            }
            return sb.ToString();
        }

        private static string Csv(string s)
        {
            s = s ?? string.Empty;
            // جلوگیری از اجرای فرمول هنگام باز کردن در Excel
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public static string ResponsibleText(DataRow r)
        {
            var parts = new List<string>();
            foreach (var col in new[] { "PersonName", "UnitTitle", "ResponsibleNote" })
                if (!(r[col] is DBNull) && !string.IsNullOrWhiteSpace((string)r[col])) parts.Add((string)r[col]);
            return parts.Count == 0 ? "—" : string.Join(" / ", parts);
        }

        public static string DueText(DataRow r)
        {
            if (r["DueDate"] is DateTime) return PersianDate.ToJalali((DateTime)r["DueDate"]);
            if (!(r["DueText"] is DBNull) && !string.IsNullOrWhiteSpace((string)r["DueText"])) return (string)r["DueText"];
            return "—";
        }
    }
}
