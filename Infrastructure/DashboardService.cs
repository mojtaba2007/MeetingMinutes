using System.Data;

namespace MeetingMinutes.Infrastructure
{
    public static class DashboardService
    {
        public static DataRow Stats(bool canSeeDrafts)
        {
            var dt = Db.Query(
                @"SELECT
                    (SELECT COUNT(*) FROM dbo.Meetings WHERE IsDeleted = 0 AND (@drafts = 1 OR StatusId <> 1)) AS TotalMeetings,
                    (SELECT COUNT(*) FROM dbo.Meetings WHERE IsDeleted = 0 AND StatusId IN (3, 5)) AS HeldMeetings,
                    (SELECT COUNT(*) FROM dbo.Decisions d JOIN dbo.Meetings m ON m.MeetingId = d.MeetingId
                       WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1)) AS TotalDecisions,
                    (SELECT COUNT(*) FROM dbo.ActionItems a
                       JOIN dbo.ActionStatuses s ON s.StatusId = a.StatusId
                       JOIN dbo.Decisions d ON d.DecisionId = a.DecisionId
                       JOIN dbo.Meetings m ON m.MeetingId = d.MeetingId
                      WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1) AND s.IsClosed = 0) AS OpenActions,
                    (SELECT COUNT(*) FROM dbo.ActionItems a
                       JOIN dbo.ActionStatuses s ON s.StatusId = a.StatusId
                       JOIN dbo.Decisions d ON d.DecisionId = a.DecisionId
                       JOIN dbo.Meetings m ON m.MeetingId = d.MeetingId
                      WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1)
                        AND s.IsClosed = 0 AND a.DueDate < CAST(GETDATE() AS date)) AS OverdueActions",
                Db.P("@drafts", canSeeDrafts));
            return dt.Rows[0];
        }

        public static DataTable RecentMeetings(bool canSeeDrafts, int take)
        {
            return Db.Query(
                @"SELECT TOP (@n) m.MeetingId, m.MeetingNo, m.MeetingDate, m.StatusId, s.Title AS StatusTitle, s.CssClass,
                         COALESCE(NULLIF(m.Title, N''), mt.Title) AS DisplayTitle,
                         (SELECT COUNT(*) FROM dbo.Decisions x WHERE x.MeetingId = m.MeetingId) AS DecisionCount
                  FROM dbo.Meetings m
                  JOIN dbo.MeetingStatuses s ON s.StatusId = m.StatusId
                  JOIN dbo.MeetingTypes mt ON mt.MeetingTypeId = m.MeetingTypeId
                  WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1)
                  ORDER BY m.MeetingDate DESC, m.MeetingId DESC", Db.P("@n", take), Db.P("@drafts", canSeeDrafts));
        }

        public static DataTable RecentDecisions(bool canSeeDrafts, int take)
        {
            return Db.Query(
                @"SELECT TOP (@n) d.DecisionId, d.DecisionText, m.MeetingDate, m.MeetingDateFa,
                         COALESCE(NULLIF(m.Title, N''), mt.Title) AS MeetingTitle, m.MeetingId
                  FROM dbo.Decisions d
                  JOIN dbo.Meetings m ON m.MeetingId = d.MeetingId
                  JOIN dbo.MeetingTypes mt ON mt.MeetingTypeId = m.MeetingTypeId
                  WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1)
                  ORDER BY m.MeetingDate DESC, d.DecisionId DESC", Db.P("@n", take), Db.P("@drafts", canSeeDrafts));
        }

        /// <summary>اقدام‌های باز، اول معوق‌ها بر اساس نزدیک‌ترین مهلت.</summary>
        public static DataTable PendingActions(bool canSeeDrafts, int take)
        {
            return Db.Query(
                @"SELECT TOP (@n) a.ActionId, a.Title, a.DueDate, a.DueText, a.StatusId, s.Title AS StatusTitle, s.CssClass,
                         COALESCE(p.FullName, u.Title, a.ResponsibleNote) AS ResponsibleName,
                         CAST(CASE WHEN a.DueDate < CAST(GETDATE() AS date) THEN 1 ELSE 0 END AS bit) AS IsOverdue
                  FROM dbo.ActionItems a
                  JOIN dbo.ActionStatuses s ON s.StatusId = a.StatusId
                  JOIN dbo.Decisions d ON d.DecisionId = a.DecisionId
                  JOIN dbo.Meetings m ON m.MeetingId = d.MeetingId
                  LEFT JOIN dbo.People p ON p.PersonId = a.ResponsiblePersonId
                  LEFT JOIN dbo.OrgUnits u ON u.OrgUnitId = a.ResponsibleUnitId
                  WHERE m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1) AND s.IsClosed = 0
                  ORDER BY CASE WHEN a.DueDate < CAST(GETDATE() AS date) THEN 0 ELSE 1 END,
                           CASE WHEN a.DueDate IS NULL THEN 1 ELSE 0 END, a.DueDate, a.ActionId",
                Db.P("@n", take), Db.P("@drafts", canSeeDrafts));
        }

        public static DataTable MeetingStatusCounts(bool canSeeDrafts)
        {
            return Db.Query(
                @"SELECT s.StatusId, s.Title, COUNT(m.MeetingId) AS Cnt
                  FROM dbo.MeetingStatuses s
                  LEFT JOIN dbo.Meetings m ON m.StatusId = s.StatusId AND m.IsDeleted = 0 AND (@drafts = 1 OR m.StatusId <> 1)
                  WHERE @drafts = 1 OR s.StatusId <> 1
                  GROUP BY s.StatusId, s.Title
                  ORDER BY s.StatusId", Db.P("@drafts", canSeeDrafts));
        }
    }
}
