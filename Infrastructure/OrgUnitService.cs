using System;
using System.Collections.Generic;
using System.Data;

namespace MeetingMinutes.Infrastructure
{
    public static class OrgUnitService
    {
        public static DataTable List()
        {
            return Db.Query(
                @"SELECT o.OrgUnitId, o.Title, o.IsActive, p.Title AS ParentTitle,
                         (SELECT COUNT(*) FROM dbo.People x WHERE x.OrgUnitId = o.OrgUnitId) AS PeopleCount
                  FROM dbo.OrgUnits o
                  LEFT JOIN dbo.OrgUnits p ON p.OrgUnitId = o.ParentUnitId
                  ORDER BY o.IsActive DESC, COALESCE(p.Title, o.Title),
                           CASE WHEN p.OrgUnitId IS NULL THEN 0 ELSE 1 END, o.Title");
        }

        public static DataRow Get(int id)
        {
            var dt = Db.Query("SELECT OrgUnitId, ParentUnitId, Title, IsActive FROM dbo.OrgUnits WHERE OrgUnitId = @id", Db.P("@id", id));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        /// <summary>واحدهای فعال برای لیست‌های کشویی؛ واحد جاری (حتی اگر غیرفعال) و/یا حذف یک واحد قابل تعیین است.</summary>
        public static DataTable Options(int? includeId, int? excludeId)
        {
            return Db.Query(
                @"SELECT OrgUnitId, Title FROM dbo.OrgUnits
                  WHERE (IsActive = 1 OR OrgUnitId = @inc) AND (@exc IS NULL OR OrgUnitId <> @exc)
                  ORDER BY Title", Db.P("@inc", includeId), Db.P("@exc", excludeId));
        }

        public static bool Save(int? id, string title, int? parentId, bool isActive, out int unitId, out string error)
        {
            error = null;
            unitId = id ?? 0;
            title = TextNormalizer.Clean(title);
            if (title.Length == 0) { error = "عنوان واحد الزامی است."; return false; }

            if (id.HasValue && parentId.HasValue)
            {
                // والد نباید خودِ واحد یا یکی از زیرمجموعه‌هایش باشد
                var cycle = (int)Db.Scalar(
                    @"WITH d AS (
                          SELECT OrgUnitId FROM dbo.OrgUnits WHERE ParentUnitId = @id
                          UNION ALL
                          SELECT o.OrgUnitId FROM dbo.OrgUnits o JOIN d ON o.ParentUnitId = d.OrgUnitId)
                      SELECT COUNT(*) FROM d WHERE OrgUnitId = @p", Db.P("@id", id.Value), Db.P("@p", parentId.Value));
                if (cycle > 0 || parentId.Value == id.Value) { error = "واحد بالادستی نامعتبر است (چرخه ایجاد می‌شود)."; return false; }
            }

            var dup = (int)Db.Scalar(
                @"SELECT COUNT(*) FROM dbo.OrgUnits
                  WHERE Title = @t AND ISNULL(ParentUnitId, 0) = ISNULL(@p, 0) AND (@id IS NULL OR OrgUnitId <> @id)",
                Db.P("@t", title), Db.P("@p", parentId), Db.P("@id", id));
            if (dup > 0) { error = "واحدی با همین عنوان و همین والد قبلاً ثبت شده است."; return false; }

            if (id.HasValue)
            {
                Db.Execute("UPDATE dbo.OrgUnits SET Title = @t, ParentUnitId = @p, IsActive = @a WHERE OrgUnitId = @id",
                           Db.P("@t", title), Db.P("@p", parentId), Db.P("@a", isActive), Db.P("@id", id.Value));
            }
            else
            {
                unitId = (int)Db.Scalar(
                    "INSERT dbo.OrgUnits (Title, ParentUnitId, IsActive) OUTPUT INSERTED.OrgUnitId VALUES (@t, @p, @a)",
                    Db.P("@t", title), Db.P("@p", parentId), Db.P("@a", isActive));
            }
            return true;
        }

        public static void SetActive(int id, bool active)
        {
            Db.Execute("UPDATE dbo.OrgUnits SET IsActive = @a WHERE OrgUnitId = @id", Db.P("@a", active), Db.P("@id", id));
        }

        // حذف
        /// <summary>حذف واقعی فقط وقتی مجاز است که این واحد در هیچ‌جای دیگر دیتابیس استفاده نشده باشد.</summary>
        public static bool CanDelete(int unitId, out string reason)
        {
            reason = null;
            var reasons = new List<string>();

            int people = (int)Db.Scalar("SELECT COUNT(*) FROM dbo.People WHERE OrgUnitId = @id", Db.P("@id", unitId));
            if (people > 0) reasons.Add(Fa(people) + " نفر عضو این واحدند");

            int children = (int)Db.Scalar("SELECT COUNT(*) FROM dbo.OrgUnits WHERE ParentUnitId = @id", Db.P("@id", unitId));
            if (children > 0) reasons.Add(Fa(children) + " واحد زیرمجموعه دارد");

            int act = (int)Db.Scalar("SELECT COUNT(*) FROM dbo.ActionItems WHERE ResponsibleUnitId = @id", Db.P("@id", unitId));
            if (act > 0) reasons.Add("مسئولِ " + Fa(act) + " اقدام است");

            if (reasons.Count == 0) return true;
            reason = "این واحد قابل حذف نیست، چون " + string.Join("، ", reasons) + ". به‌جای حذف، غیرفعالش کنید.";
            return false;
        }

        public static bool Delete(int unitId, out string error)
        {
            if (!CanDelete(unitId, out error)) return false;
            int n = Db.Execute("DELETE dbo.OrgUnits WHERE OrgUnitId = @id", Db.P("@id", unitId));
            if (n == 0) { error = "واحد یافت نشد."; return false; }
            return true;
        }

        private static string Fa(object v)
        {
            return TextNormalizer.ToPersianDigits(v);
        }
    }
}
