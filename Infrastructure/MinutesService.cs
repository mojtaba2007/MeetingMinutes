using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace MeetingMinutes.Infrastructure
{
    public static class MinutesService
    {

        /// <summary>موضوعات به‌صورت فهرست تخت و مرتب (والد، سپس زیرموضوع‌هایش) با شماره‌ی 3 یا 3-1.</summary>
        public static DataTable TopicsFlat(int meetingId, bool includeLoose)
        {
            var src = Db.Query(
                @"SELECT TopicId, ParentTopicId, SortOrder, Title, DiscussionText
                  FROM dbo.MeetingTopics WHERE MeetingId = @m ORDER BY SortOrder, TopicId", Db.P("@m", meetingId));

            var res = new DataTable();
            res.Columns.Add("TopicId", typeof(int));
            res.Columns.Add("ParentTopicId", typeof(int));
            res.Columns.Add("Level", typeof(int));
            res.Columns.Add("Number", typeof(string));
            res.Columns.Add("Title", typeof(string));
            res.Columns.Add("DiscussionText", typeof(string));
            res.Columns.Add("IsLoose", typeof(bool));

            int n = 0;
            foreach (DataRow t in src.Select("ParentTopicId IS NULL", "SortOrder, TopicId"))
            {
                n++;
                AddTopicRow(res, t, 0, n.ToString());
                int k = 0;
                foreach (DataRow c in src.Select("ParentTopicId = " + (int)t["TopicId"], "SortOrder, TopicId"))
                    AddTopicRow(res, c, 1, n + "-" + (++k));
            }
            if (includeLoose)
            {
                var r = res.NewRow();
                r["TopicId"] = 0; r["Level"] = 0; r["Number"] = ""; r["Title"] = "مصوبات بدون موضوع";
                r["DiscussionText"] = DBNull.Value; r["IsLoose"] = true;
                res.Rows.Add(r);
            }
            return res;
        }

        private static void AddTopicRow(DataTable res, DataRow t, int level, string number)
        {
            var r = res.NewRow();
            r["TopicId"] = t["TopicId"];
            r["ParentTopicId"] = t["ParentTopicId"];
            r["Level"] = level;
            r["Number"] = number;
            r["Title"] = t["Title"];
            r["DiscussionText"] = t["DiscussionText"];
            r["IsLoose"] = false;
            res.Rows.Add(r);
        }

        public static DataTable Decisions(int meetingId)
        {
            return Db.Query("SELECT DecisionId, TopicId, SortOrder, DecisionText FROM dbo.Decisions WHERE MeetingId = @m ORDER BY SortOrder, DecisionId",
                            Db.P("@m", meetingId));
        }

        public static DataTable Actions(int meetingId)
        {
            return Db.Query(
                @"SELECT a.ActionId, a.DecisionId, a.Title, p.FullName AS PersonName, u.Title AS UnitTitle, a.ResponsibleNote,
                         a.DueDate, a.DueText, a.StatusId, s.Title AS StatusTitle, s.CssClass
                  FROM dbo.ActionItems a
                  JOIN dbo.Decisions d ON d.DecisionId = a.DecisionId
                  JOIN dbo.ActionStatuses s ON s.StatusId = a.StatusId
                  LEFT JOIN dbo.People p ON p.PersonId = a.ResponsiblePersonId
                  LEFT JOIN dbo.OrgUnits u ON u.OrgUnitId = a.ResponsibleUnitId
                  WHERE d.MeetingId = @m ORDER BY a.ActionId", Db.P("@m", meetingId));
        }

        public static DataRow GetTopic(int meetingId, int topicId)
        {
            var dt = Db.Query("SELECT TopicId, ParentTopicId, Title, DiscussionText FROM dbo.MeetingTopics WHERE TopicId = @t AND MeetingId = @m",
                              Db.P("@t", topicId), Db.P("@m", meetingId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataRow GetDecision(int meetingId, int decisionId)
        {
            var dt = Db.Query("SELECT DecisionId, TopicId, DecisionText FROM dbo.Decisions WHERE DecisionId = @d AND MeetingId = @m",
                              Db.P("@d", decisionId), Db.P("@m", meetingId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        public static DataRow GetAction(int meetingId, int actionId)
        {
            var dt = Db.Query(
                @"SELECT a.ActionId, a.DecisionId, a.Title, a.ResponsiblePersonId, a.ResponsibleUnitId, a.ResponsibleNote,
                         a.DueDate, a.DueText, a.StatusId
                  FROM dbo.ActionItems a JOIN dbo.Decisions d ON d.DecisionId = a.DecisionId
                  WHERE a.ActionId = @a AND d.MeetingId = @m", Db.P("@a", actionId), Db.P("@m", meetingId));
            return dt.Rows.Count == 0 ? null : dt.Rows[0];
        }

        /// <summary>موضوعات سطح اول (برای انتخاب والد یک زیرموضوع)، به‌جز خود موضوع.</summary>
        public static DataTable TopLevelTopics(int meetingId, int? excludeId)
        {
            return Db.Query(
                @"SELECT TopicId, Title FROM dbo.MeetingTopics
                  WHERE MeetingId = @m AND ParentTopicId IS NULL AND (@ex IS NULL OR TopicId <> @ex)
                  ORDER BY SortOrder, TopicId", Db.P("@m", meetingId), Db.P("@ex", excludeId));
        }

        public static bool TopicHasChildren(int topicId)
        {
            return (int)Db.Scalar("SELECT COUNT(*) FROM dbo.MeetingTopics WHERE ParentTopicId = @t", Db.P("@t", topicId)) > 0;
        }

        public static DataTable ActionStatuses()
        {
            return Db.Query("SELECT StatusId, Title FROM dbo.ActionStatuses ORDER BY StatusId");
        }

        /// <summary>افراد فعال (و فرد جاری) همراه با متن جست‌وجو شامل نام‌های مستعار.</summary>
        public static DataTable PeopleForPicker(int? includePersonId)
        {
            var people = Db.Query(
                @"SELECT PersonId, FullName, SearchName FROM dbo.People
                  WHERE IsActive = 1 OR PersonId = @inc
                  ORDER BY IsBoardMember DESC, LastName, FirstName", Db.P("@inc", includePersonId));
            var aliases = Db.Query("SELECT PersonId, AliasName FROM dbo.PersonAliases");
            var byPerson = new Dictionary<int, List<string>>();
            foreach (DataRow a in aliases.Rows)
            {
                int pid = (int)a["PersonId"];
                if (!byPerson.ContainsKey(pid)) byPerson[pid] = new List<string>();
                byPerson[pid].Add((string)a["AliasName"]);
            }
            people.Columns.Add("SearchText", typeof(string));
            foreach (DataRow p in people.Rows)
            {
                var s = (string)p["SearchName"];
                List<string> al;
                if (byPerson.TryGetValue((int)p["PersonId"], out al)) s += " " + string.Join(" ", al);
                p["SearchText"] = s;
            }
            return people;
        }

        // جمع‌بندی رئیس جلسه
        public static bool SaveChairSummary(int meetingId, string text, int userId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            Db.Execute("UPDATE dbo.Meetings SET ChairSummary = @t, UpdatedBy = @u, UpdatedAt = SYSDATETIME() WHERE MeetingId = @m",
                       Db.P("@t", TextNormalizer.CleanOrNull(text)), Db.P("@u", userId), Db.P("@m", meetingId));
            return true;
        }

        // موضوعات
        public static bool SaveTopic(int meetingId, int? topicId, string title, string discussion, int? parentId,
                                     out int savedId, out string error)
        {
            savedId = topicId ?? 0;
            if (!MeetingService.IsEditable(meetingId, out error)) return false;

            title = TextNormalizer.Clean(title);
            if (title.Length == 0) { error = "عنوان موضوع را وارد کنید."; return false; }
            if (title.Length > 300) { error = "عنوان موضوع حداکثر ۳۰۰ نویسه است."; return false; }
            var text = TextNormalizer.CleanOrNull(discussion);

            if (parentId.HasValue)
            {
                if (topicId.HasValue && parentId.Value == topicId.Value) { error = "یک موضوع نمی‌تواند زیرموضوعِ خودش باشد."; return false; }
                if ((int)Db.Scalar("SELECT COUNT(*) FROM dbo.MeetingTopics WHERE TopicId = @p AND MeetingId = @m AND ParentTopicId IS NULL",
                                   Db.P("@p", parentId.Value), Db.P("@m", meetingId)) == 0)
                { error = "موضوع بالادست نامعتبر است (فقط موضوع سطح اول می‌تواند والد باشد)."; return false; }
                if (topicId.HasValue && TopicHasChildren(topicId.Value)) { error = "این موضوع خودش زیرموضوع دارد و نمی‌تواند زیرموضوعِ دیگری شود."; return false; }
            }

            if (topicId.HasValue)
            {
                var cur = GetTopic(meetingId, topicId.Value);
                if (cur == null) { error = "موضوع یافت نشد."; return false; }
                int? oldParent = cur["ParentTopicId"] is DBNull ? (int?)null : (int)cur["ParentTopicId"];
                bool moved = oldParent != parentId;
                Db.Execute(
                    @"UPDATE dbo.MeetingTopics
                      SET Title = @t, DiscussionText = @d, ParentTopicId = @p,
                          SortOrder = CASE WHEN @moved = 1
                                           THEN (SELECT ISNULL(MAX(SortOrder), 0) + 1 FROM dbo.MeetingTopics WHERE MeetingId = @m AND ISNULL(ParentTopicId, 0) = ISNULL(@p, 0))
                                           ELSE SortOrder END
                      WHERE TopicId = @id AND MeetingId = @m",
                    Db.P("@t", title), Db.P("@d", text), Db.P("@p", parentId), Db.P("@moved", moved),
                    Db.P("@m", meetingId), Db.P("@id", topicId.Value));
            }
            else
            {
                savedId = (int)Db.Scalar(
                    @"INSERT dbo.MeetingTopics (MeetingId, ParentTopicId, SortOrder, Title, DiscussionText)
                      OUTPUT INSERTED.TopicId
                      SELECT @m, @p, ISNULL(MAX(SortOrder), 0) + 1, @t, @d FROM dbo.MeetingTopics
                      WHERE MeetingId = @m AND ISNULL(ParentTopicId, 0) = ISNULL(@p, 0)",
                    Db.P("@m", meetingId), Db.P("@p", parentId), Db.P("@t", title), Db.P("@d", text));
            }
            return true;
        }

        /// <summary>حذف موضوع: زیرموضوع‌ها به سطح اول می‌آیند و مصوبات آن «بدون موضوع» می‌شوند (چیزی پاک نمی‌شود).</summary>
        public static bool DeleteTopic(int meetingId, int topicId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            using (var con = Db.Open())
            using (var tx = con.BeginTransaction())
            {
                Exec(con, tx, "UPDATE dbo.MeetingTopics SET ParentTopicId = NULL WHERE ParentTopicId = @t AND MeetingId = @m", Db.P("@t", topicId), Db.P("@m", meetingId));
                Exec(con, tx, "UPDATE dbo.Decisions SET TopicId = NULL WHERE TopicId = @t AND MeetingId = @m", Db.P("@t", topicId), Db.P("@m", meetingId));
                int n = Exec(con, tx, "DELETE dbo.MeetingTopics WHERE TopicId = @t AND MeetingId = @m", Db.P("@t", topicId), Db.P("@m", meetingId));
                if (n == 0) { error = "موضوع یافت نشد."; return false; }
                tx.Commit();
            }
            return true;
        }

        public static bool MoveTopic(int meetingId, int topicId, int direction, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            var cur = GetTopic(meetingId, topicId);
            if (cur == null) { error = "موضوع یافت نشد."; return false; }
            int parent = cur["ParentTopicId"] is DBNull ? 0 : (int)cur["ParentTopicId"];
            Move("MeetingTopics", "TopicId", "MeetingId = @m AND ISNULL(ParentTopicId, 0) = @g",
                 new Dictionary<string, object> { { "@m", meetingId }, { "@g", parent } }, topicId, direction);
            return true;
        }

        // مصوبات
        public static bool SaveDecision(int meetingId, int? decisionId, int? topicId, string text, out int savedId, out string error)
        {
            savedId = decisionId ?? 0;
            if (!MeetingService.IsEditable(meetingId, out error)) return false;

            text = TextNormalizer.Clean(text);
            if (text.Length == 0) { error = "متن مصوبه را وارد کنید."; return false; }
            if (topicId.HasValue &&
                (int)Db.Scalar("SELECT COUNT(*) FROM dbo.MeetingTopics WHERE TopicId = @t AND MeetingId = @m", Db.P("@t", topicId.Value), Db.P("@m", meetingId)) == 0)
            { error = "موضوع انتخاب‌شده نامعتبر است."; return false; }

            if (decisionId.HasValue)
            {
                var cur = GetDecision(meetingId, decisionId.Value);
                if (cur == null) { error = "مصوبه یافت نشد."; return false; }
                int? oldTopic = cur["TopicId"] is DBNull ? (int?)null : (int)cur["TopicId"];
                bool moved = oldTopic != topicId;
                Db.Execute(
                    @"UPDATE dbo.Decisions
                      SET DecisionText = @x, TopicId = @t,
                          SortOrder = CASE WHEN @moved = 1
                                           THEN (SELECT ISNULL(MAX(SortOrder), 0) + 1 FROM dbo.Decisions WHERE MeetingId = @m AND ISNULL(TopicId, 0) = ISNULL(@t, 0))
                                           ELSE SortOrder END
                      WHERE DecisionId = @id AND MeetingId = @m",
                    Db.P("@x", text), Db.P("@t", topicId), Db.P("@moved", moved), Db.P("@m", meetingId), Db.P("@id", decisionId.Value));
            }
            else
            {
                savedId = (int)Db.Scalar(
                    @"INSERT dbo.Decisions (MeetingId, TopicId, SortOrder, DecisionText)
                      OUTPUT INSERTED.DecisionId
                      SELECT @m, @t, ISNULL(MAX(SortOrder), 0) + 1, @x FROM dbo.Decisions
                      WHERE MeetingId = @m AND ISNULL(TopicId, 0) = ISNULL(@t, 0)",
                    Db.P("@m", meetingId), Db.P("@t", topicId), Db.P("@x", text));
            }
            return true;
        }

        public static bool DeleteDecision(int meetingId, int decisionId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            int n = Db.Execute("DELETE dbo.Decisions WHERE DecisionId = @d AND MeetingId = @m", Db.P("@d", decisionId), Db.P("@m", meetingId));
            if (n == 0) { error = "مصوبه یافت نشد."; return false; }
            return true;
        }

        /// <summary>تصحیح تشخیص اشتباه: متن مصوبه به «شرح مذاکرات» همان موضوع منتقل و مصوبه (و اقدام‌هایش) حذف می‌شود.
        /// اگر مصوبه موضوع نداشت، موضوعی به نام «مشروح مذاکرات» پیدا/ساخته می‌شود.</summary>
        public static bool DecisionToDiscussion(int meetingId, int decisionId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            using (var con = Db.Open())
            using (var tx = con.BeginTransaction())
            {
                string text;
                object topicObj;
                using (var c = new SqlCommand("SELECT DecisionText, TopicId FROM dbo.Decisions WHERE DecisionId = @d AND MeetingId = @m", con, tx))
                {
                    c.Parameters.Add(Db.P("@d", decisionId)); c.Parameters.Add(Db.P("@m", meetingId));
                    using (var r = c.ExecuteReader())
                    {
                        if (!r.Read()) { error = "مصوبه یافت نشد."; return false; }
                        text = r.GetString(0);
                        topicObj = r.IsDBNull(1) ? null : (object)r.GetInt32(1);
                    }
                }

                int topicId;
                if (topicObj != null)
                {
                    topicId = (int)topicObj;
                }
                else
                {
                    const string title = "مشروح مذاکرات";
                    object found;
                    using (var c = new SqlCommand("SELECT TOP 1 TopicId FROM dbo.MeetingTopics WHERE MeetingId = @m AND Title = @t AND ParentTopicId IS NULL ORDER BY TopicId", con, tx))
                    {
                        c.Parameters.Add(Db.P("@m", meetingId)); c.Parameters.Add(Db.P("@t", title));
                        found = c.ExecuteScalar();
                    }
                    if (found != null)
                    {
                        topicId = (int)found;
                    }
                    else
                    {
                        using (var c = new SqlCommand(
                            @"INSERT dbo.MeetingTopics (MeetingId, ParentTopicId, SortOrder, Title)
                              OUTPUT INSERTED.TopicId
                              SELECT @m, NULL, ISNULL(MAX(SortOrder), 0) + 1, @t FROM dbo.MeetingTopics WHERE MeetingId = @m AND ParentTopicId IS NULL", con, tx))
                        {
                            c.Parameters.Add(Db.P("@m", meetingId)); c.Parameters.Add(Db.P("@t", title));
                            topicId = (int)c.ExecuteScalar();
                        }
                    }
                }

                Exec(con, tx,
                    @"UPDATE dbo.MeetingTopics
                      SET DiscussionText = CASE WHEN DiscussionText IS NULL OR DiscussionText = N'' THEN @x ELSE DiscussionText + NCHAR(10) + @x END
                      WHERE TopicId = @t AND MeetingId = @m",
                    Db.P("@x", text), Db.P("@t", topicId), Db.P("@m", meetingId));
                Exec(con, tx, "DELETE dbo.Decisions WHERE DecisionId = @d AND MeetingId = @m", Db.P("@d", decisionId), Db.P("@m", meetingId));
                tx.Commit();
            }
            return true;
        }

        public static bool MoveDecision(int meetingId, int decisionId, int direction, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            var cur = GetDecision(meetingId, decisionId);
            if (cur == null) { error = "مصوبه یافت نشد."; return false; }
            int topic = cur["TopicId"] is DBNull ? 0 : (int)cur["TopicId"];
            Move("Decisions", "DecisionId", "MeetingId = @m AND ISNULL(TopicId, 0) = @g",
                 new Dictionary<string, object> { { "@m", meetingId }, { "@g", topic } }, decisionId, direction);
            return true;
        }

        // اقدام‌ها
        public static bool SaveAction(int meetingId, int? actionId, int decisionId, string title, int? personId, int? unitId,
                                      string note, DateTime? dueDate, string dueText, int statusId, int userId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;

            title = TextNormalizer.Clean(title);
            note = TextNormalizer.CleanOrNull(note);
            dueText = TextNormalizer.CleanOrNull(dueText);

            if (title.Length == 0) { error = "شرح اقدام را وارد کنید."; return false; }
            if (title.Length > 400) { error = "شرح اقدام حداکثر ۴۰۰ نویسه است."; return false; }
            if (!personId.HasValue && !unitId.HasValue && note == null) { error = "مسئول اقدام را مشخص کنید (فرد، واحد یا توضیح)."; return false; }
            if (note != null && note.Length > 200) { error = "توضیح مسئول حداکثر ۲۰۰ نویسه است."; return false; }
            if (dueText != null && dueText.Length > 100) { error = "متن مهلت حداکثر ۱۰۰ نویسه است."; return false; }
            if ((int)Db.Scalar("SELECT COUNT(*) FROM dbo.ActionStatuses WHERE StatusId = @s", Db.P("@s", statusId)) == 0) { error = "وضعیت نامعتبر است."; return false; }
            if ((int)Db.Scalar("SELECT COUNT(*) FROM dbo.Decisions WHERE DecisionId = @d AND MeetingId = @m", Db.P("@d", decisionId), Db.P("@m", meetingId)) == 0)
            { error = "مصوبه یافت نشد."; return false; }

            if (actionId.HasValue)
            {
                int n = Db.Execute(
                    @"UPDATE dbo.ActionItems
                      SET Title = @t, ResponsiblePersonId = @p, ResponsibleUnitId = @u, ResponsibleNote = @n,
                          DueDate = @dd, DueText = @dt, StatusId = @s,
                          CompletedAt = CASE WHEN @s = 3 THEN COALESCE(CompletedAt, CAST(GETDATE() AS date)) ELSE NULL END,
                          UpdatedBy = @uid, UpdatedAt = SYSDATETIME()
                      WHERE ActionId = @id AND DecisionId IN (SELECT DecisionId FROM dbo.Decisions WHERE MeetingId = @m)",
                    Db.P("@t", title), Db.P("@p", personId), Db.P("@u", unitId), Db.P("@n", note),
                    Db.P("@dd", dueDate), Db.P("@dt", dueText), Db.P("@s", statusId), Db.P("@uid", userId),
                    Db.P("@id", actionId.Value), Db.P("@m", meetingId));
                if (n == 0) { error = "اقدام یافت نشد."; return false; }
            }
            else
            {
                Db.Execute(
                    @"INSERT dbo.ActionItems (DecisionId, Title, ResponsiblePersonId, ResponsibleUnitId, ResponsibleNote,
                                              DueDate, DueText, StatusId, CompletedAt, UpdatedBy, UpdatedAt)
                      VALUES (@d, @t, @p, @u, @n, @dd, @dt, @s,
                              CASE WHEN @s = 3 THEN CAST(GETDATE() AS date) ELSE NULL END, @uid, SYSDATETIME())",
                    Db.P("@d", decisionId), Db.P("@t", title), Db.P("@p", personId), Db.P("@u", unitId), Db.P("@n", note),
                    Db.P("@dd", dueDate), Db.P("@dt", dueText), Db.P("@s", statusId), Db.P("@uid", userId));
            }
            return true;
        }

        public static bool DeleteAction(int meetingId, int actionId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            int n = Db.Execute(
                @"DELETE dbo.ActionItems
                  WHERE ActionId = @a AND DecisionId IN (SELECT DecisionId FROM dbo.Decisions WHERE MeetingId = @m)",
                Db.P("@a", actionId), Db.P("@m", meetingId));
            if (n == 0) { error = "اقدام یافت نشد."; return false; }
            return true;
        }

        public static bool MarkActionDone(int meetingId, int actionId, int userId, out string error)
        {
            if (!MeetingService.IsEditable(meetingId, out error)) return false;
            int n = Db.Execute(
                @"UPDATE dbo.ActionItems
                  SET StatusId = 3, CompletedAt = COALESCE(CompletedAt, CAST(GETDATE() AS date)), UpdatedBy = @u, UpdatedAt = SYSDATETIME()
                  WHERE ActionId = @a AND DecisionId IN (SELECT DecisionId FROM dbo.Decisions WHERE MeetingId = @m)",
                Db.P("@u", userId), Db.P("@a", actionId), Db.P("@m", meetingId));
            if (n == 0) { error = "اقدام یافت نشد."; return false; }
            return true;
        }

        // کمکی
        private static int Exec(SqlConnection con, SqlTransaction tx, string sql, params SqlParameter[] ps)
        {
            using (var c = new SqlCommand(sql, con, tx))
            {
                if (ps != null && ps.Length > 0) c.Parameters.AddRange(ps);
                return c.ExecuteNonQuery();
            }
        }

        /// <summary>جابه‌جایی یک ردیف با هم‌گروهی قبلی/بعدی و یکدست‌سازی SortOrder گروه (۱، ۲، ۳، ...).</summary>
        private static void Move(string table, string idCol, string where, Dictionary<string, object> args, int id, int direction)
        {
            using (var con = Db.Open())
            using (var tx = con.BeginTransaction())
            {
                var ids = new List<int>();
                using (var c = new SqlCommand("SELECT " + idCol + " FROM dbo." + table + " WHERE " + where + " ORDER BY SortOrder, " + idCol, con, tx))
                {
                    foreach (var kv in args) c.Parameters.Add(Db.P(kv.Key, kv.Value));
                    using (var r = c.ExecuteReader())
                        while (r.Read()) ids.Add(r.GetInt32(0));
                }
                int i = ids.IndexOf(id), j = i + direction;
                if (i < 0 || j < 0 || j >= ids.Count) return;

                int tmp = ids[i]; ids[i] = ids[j]; ids[j] = tmp;
                for (int k = 0; k < ids.Count; k++)
                    Exec(con, tx, "UPDATE dbo." + table + " SET SortOrder = @s WHERE " + idCol + " = @id", Db.P("@s", k + 1), Db.P("@id", ids[k]));
                tx.Commit();
            }
        }
    }
}
