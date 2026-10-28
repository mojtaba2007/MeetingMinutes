using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace MeetingMinutes.Infrastructure
{
    public static class Db
    {
        private static string ConnStr
        {
            get
            {
                var environmentConnectionString = Environment.GetEnvironmentVariable("MEETINGMINUTES_CONNECTION_STRING");
                if (!string.IsNullOrWhiteSpace(environmentConnectionString))
                    return environmentConnectionString;

                var cs = ConfigurationManager.ConnectionStrings["MeetingDb"];
                if (cs == null)
                    throw new ConfigurationErrorsException("رشته‌ی اتصال را در متغیر محیطی MEETINGMINUTES_CONNECTION_STRING یا بخش MeetingDb در Web.config تعریف کنید.");
                return cs.ConnectionString;
            }
        }

        public static SqlConnection Open()
        {
            var con = new SqlConnection(ConnStr);
            con.Open();
            return con;
        }

        // مقدار null به DBNull تبدیل می‌شود. عمداً پارامتر دوم از نوع object است
        // تا new SqlParameter("@x", 0) به‌اشتباه به overload مربوط به SqlDbType نرود.
        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        /// <summary>الگوی LIKE امن (با ESCAPE '\\'): نویسه‌های % _ [ در متن جست‌وجو معنای ویژه نمی‌گیرند.</summary>
        public static string LikePattern(string s)
        {
            if (string.IsNullOrEmpty(s)) return "%";
            return "%" + s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
        }

        public static DataTable Query(string sql, params SqlParameter[] ps)
        {
            using (var con = Open())
            using (var cmd = new SqlCommand(sql, con))
            using (var da = new SqlDataAdapter(cmd))
            {
                if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
                var dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        public static object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var con = Open())
            using (var cmd = new SqlCommand(sql, con))
            {
                if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
                return cmd.ExecuteScalar();
            }
        }

        public static int Execute(string sql, params SqlParameter[] ps)
        {
            using (var con = Open())
            using (var cmd = new SqlCommand(sql, con))
            {
                if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
