using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace MeetingMinutes.Infrastructure
{
    public class PersonInput
    {
        public int? PersonId { get; set; }
        public string Prefix { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
        public int? OrgUnitId { get; set; }
        public bool IsBoardMember { get; set; }
        public bool IsActive { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
    }

    public static class PeopleService
    {
        private static readonly Regex EmailRx = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex MobileRx = new Regex(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled);

        /// <param name="active">"" = همه، "1" = فعال، "0" = غیرفعال</param>
        /// <param name="board">"" = همه، "1" = عضو هیأت، "0" = غیرعضو</param>
        public static DataTable Search(string query, string active, string board)
        {
            var q = TextNormalizer.Normalize(query);
            return Db.Query(
                @"SELECT p.PersonId, p.FullName, p.Position, p.IsBoardMember, p.IsActive, o.Title AS UnitTitle,
                         CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Users u WHERE u.PersonId = p.PersonId) THEN 1 ELSE 0 END AS bit) AS HasUser
                  FROM dbo.People p
                  LEFT JOIN dbo.OrgUnits o ON o.OrgUnitId = p.OrgUnitId
                  WHERE (@q = ''
                         OR p.SearchName LIKE @like ESCAPE '\'
                         OR EXISTS (SELECT 1 FROM dbo.PersonAliases a WHERE a.PersonId = p.PersonId AND a.AliasName LIKE @like ESCAPE '\'))
                    AND (@active = '' OR (@active = '1' AND p.IsActive = 1) OR (@active = '0' AND p.IsActive = 0))
                    AND (@board = ''  OR (@board = '1' AND p.IsBoardMember = 1) OR (@board = '0' AND p.IsBoardMember = 0))
                  ORDER BY p.IsActive DESC, p.LastName, p.FirstName",
                Db.P("@q", q), Db.P("@like", Db.LikePattern(q)),
                Db.P("@active", active ?? string.Empty), Db.P("@board", board ?? string.Empty));
        }

        public static DataRow Get(int personId)
        {
            var dt = Db.Query(
                @"SELECT PersonId, Prefix, FirstName, LastName, FullName, Position, OrgUnitId,
                         IsBoardMember, IsActive, Email, Mobile
                  FROM dbo.People WHERE PersonId = @id", Db.P("@id", personId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static bool Save(PersonInput p, out int personId, out string error)
        {
            personId = p.PersonId ?? 0;
            error = null;

            var last = TextNormalizer.Clean(p.LastName);
            var first = TextNormalizer.CleanOrNull(p.FirstName);
            var prefix = TextNormalizer.CleanOrNull(p.Prefix);
            var position = TextNormalizer.CleanOrNull(p.Position);
            var email = TextNormalizer.CleanOrNull(p.Email);
            var mobile = TextNormalizer.CleanOrNull(p.Mobile);

            if (last.Length == 0) { error = "نام خانوادگی الزامی است (اگر فرد فقط یک نام دارد، همان را وارد کنید)."; return false; }
            if (email != null && !EmailRx.IsMatch(email)) { error = "نشانی ایمیل معتبر نیست."; return false; }
            if (mobile != null)
            {
                mobile = Regex.Replace(TextNormalizer.Normalize(mobile), @"[\s\-]", string.Empty);
                if (!MobileRx.IsMatch(mobile)) { error = "شماره‌ی تلفن همراه معتبر نیست."; return false; }
            }

            var fullName = string.Join(" ", new[] { prefix, first, last }.Where(s => !string.IsNullOrEmpty(s)));
            var search = TextNormalizer.Normalize((first ?? string.Empty) + " " + last);

            var owner = FindNameOwner(search, p.PersonId);
            if (owner != null)
            {
                error = "این نام قبلاً برای «" + owner + "» ثبت شده است. اگر همان فرد است، املای دیگر را در «نام‌های مستعار» او اضافه کنید؛ اگر فرد دیگری است، نام کوچک را وارد کنید تا متمایز شوند.";
                return false;
            }

            if (p.PersonId.HasValue && !p.IsActive && HasActiveUser(p.PersonId.Value))
            {
                error = "این فرد حساب کاربری فعال دارد. ابتدا حساب کاربری او را غیرفعال کنید.";
                return false;
            }

            if (p.PersonId.HasValue)
            {
                Db.Execute(
                    @"UPDATE dbo.People
                      SET Prefix = @pre, FirstName = @fn, LastName = @ln, FullName = @full, SearchName = @s,
                          Position = @pos, OrgUnitId = @unit, IsBoardMember = @board, IsActive = @act,
                          Email = @em, Mobile = @mob
                      WHERE PersonId = @id",
                    Db.P("@pre", prefix), Db.P("@fn", first), Db.P("@ln", last), Db.P("@full", fullName), Db.P("@s", search),
                    Db.P("@pos", position), Db.P("@unit", p.OrgUnitId), Db.P("@board", p.IsBoardMember), Db.P("@act", p.IsActive),
                    Db.P("@em", email), Db.P("@mob", mobile), Db.P("@id", p.PersonId.Value));
            }
            else
            {
                personId = (int)Db.Scalar(
                    @"INSERT dbo.People (Prefix, FirstName, LastName, FullName, SearchName, Position, OrgUnitId,
                                         IsBoardMember, IsActive, Email, Mobile)
                      OUTPUT INSERTED.PersonId
                      VALUES (@pre, @fn, @ln, @full, @s, @pos, @unit, @board, @act, @em, @mob)",
                    Db.P("@pre", prefix), Db.P("@fn", first), Db.P("@ln", last), Db.P("@full", fullName), Db.P("@s", search),
                    Db.P("@pos", position), Db.P("@unit", p.OrgUnitId), Db.P("@board", p.IsBoardMember), Db.P("@act", p.IsActive),
                    Db.P("@em", email), Db.P("@mob", mobile));
            }
            return true;
        }

        public static bool SetActive(int personId, bool active, out string error)
        {
            error = null;
            if (!active && HasActiveUser(personId))
            {
                error = "این فرد حساب کاربری فعال دارد. ابتدا حساب کاربری او را غیرفعال کنید.";
                return false;
            }
            Db.Execute("UPDATE dbo.People SET IsActive = @a WHERE PersonId = @id", Db.P("@a", active), Db.P("@id", personId));
            return true;
        }

        // نام‌های مستعار
        public static DataTable Aliases(int personId)
        {
            return Db.Query("SELECT AliasId, AliasName FROM dbo.PersonAliases WHERE PersonId = @id ORDER BY AliasName", Db.P("@id", personId));
        }

        public static bool AddAlias(int personId, string alias, out string error)
        {
            error = null;
            var a = TextNormalizer.Normalize(alias);
            if (a.Length < 2) { error = "نام مستعار باید حداقل ۲ نویسه باشد."; return false; }
            if (a.Length > 200) { error = "نام مستعار بیش از حد طولانی است."; return false; }

            var owner = FindNameOwner(a, null);
            if (owner != null) { error = "این نام قبلاً برای «" + owner + "» ثبت شده است."; return false; }

            Db.Execute("INSERT dbo.PersonAliases (PersonId, AliasName) VALUES (@p, @a)", Db.P("@p", personId), Db.P("@a", a));
            return true;
        }

        public static void RemoveAlias(int personId, int aliasId)
        {
            Db.Execute("DELETE dbo.PersonAliases WHERE AliasId = @a AND PersonId = @p", Db.P("@a", aliasId), Db.P("@p", personId));
        }

        /// <summary>اگر این کلید نرمال‌شده قبلاً نامِ اصلی یا مستعارِ فرد دیگری باشد، نام آن فرد را برمی‌گرداند.</summary>
        private static string FindNameOwner(string normalized, int? exceptPersonId)
        {
            var dt = Db.Query(
                @"SELECT TOP 1 FullName FROM dbo.People
                  WHERE SearchName = @s AND (@ex IS NULL OR PersonId <> @ex)
                  UNION ALL
                  SELECT TOP 1 p.FullName FROM dbo.PersonAliases a
                  JOIN dbo.People p ON p.PersonId = a.PersonId
                  WHERE a.AliasName = @s AND (@ex IS NULL OR p.PersonId <> @ex)",
                Db.P("@s", normalized), Db.P("@ex", exceptPersonId));
            return dt.Rows.Count == 0 ? null : (string)dt.Rows[0][0];
        }

        private static bool HasActiveUser(int personId)
        {
            return (int)Db.Scalar("SELECT COUNT(*) FROM dbo.Users WHERE PersonId = @id AND IsActive = 1", Db.P("@id", personId)) > 0;
        }

        // حذف
        /// <summary>حذف واقعی فقط وقتی مجاز است که این فرد در هیچ‌جای دیگر دیتابیس استفاده نشده باشد؛
        /// در غیر این صورت دلیل (یا دلایل) دقیق را برمی‌گرداند تا کاربر به‌جای حذف، غیرفعالش کند.</summary>
        public static bool CanDelete(int personId, out string reason)
        {
            reason = null;
            var reasons = new List<string>();

            if ((int)Db.Scalar("SELECT COUNT(*) FROM dbo.Users WHERE PersonId = @id", Db.P("@id", personId)) > 0)
                reasons.Add("حساب کاربری دارد");

            int att = (int)Db.Scalar("SELECT COUNT(*) FROM dbo.MeetingAttendees WHERE PersonId = @id", Db.P("@id", personId));
            if (att > 0) reasons.Add("در " + Fa(att) + " جلسه به‌عنوان حاضر یا غایب ثبت شده");

            int chair = (int)Db.Scalar("SELECT COUNT(*) FROM dbo.Meetings WHERE ChairPersonId = @id OR SecretaryPersonId = @id", Db.P("@id", personId));
            if (chair > 0) reasons.Add("رئیس یا دبیرِ " + Fa(chair) + " جلسه بوده");

            int act = (int)Db.Scalar("SELECT COUNT(*) FROM dbo.ActionItems WHERE ResponsiblePersonId = @id", Db.P("@id", personId));
            if (act > 0) reasons.Add("مسئولِ " + Fa(act) + " اقدام است");

            if (reasons.Count == 0) return true;
            reason = "این فرد قابل حذف نیست، چون " + string.Join("، ", reasons) + ". به‌جای حذف، غیرفعالش کنید.";
            return false;
        }

        public static bool Delete(int personId, out string error)
        {
            if (!CanDelete(personId, out error)) return false;
            // نام‌های مستعار با CASCADE در دیتابیس خودشان پاک می‌شوند
            int n = Db.Execute("DELETE dbo.People WHERE PersonId = @id", Db.P("@id", personId));
            if (n == 0) { error = "فرد یافت نشد."; return false; }
            return true;
        }

        private static string Fa(object v)
        {
            return TextNormalizer.ToPersianDigits(v);
        }
    }
}
