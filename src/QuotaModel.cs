using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace QuotaOrb
{
    public sealed class QuotaWindow
    {
        public int Minutes;
        public double Remaining;
        public DateTimeOffset? ResetAt;
        public string Label { get { return Minutes == 300 ? "5h" : "周"; } }
        public string Percentage { get { return Math.Floor(Remaining).ToString("0", CultureInfo.InvariantCulture) + "%"; } }
        public string ResetText(DateTimeOffset now)
        {
            if (!ResetAt.HasValue) return "重置时间暂不可用";
            DateTime local = ResetAt.Value.LocalDateTime;
            if (ResetAt <= now) return "等待额度重置";
            return (local.Date == now.LocalDateTime.Date ? "今天 " : local.ToString("M 月 d 日 ")) + local.ToString("HH:mm") + " 重置";
        }
    }

    public sealed class QuotaSnapshot
    {
        public QuotaWindow FiveHour;
        public QuotaWindow Week;
        public int? ResetCredits;
        public DateTimeOffset? CreditExpiry;
        public bool PartialCreditDetails;
        public DateTimeOffset ObservedAt;

        public static QuotaSnapshot Parse(string json, DateTimeOffset observedAt)
        {
            var root = Json.Object(new JavaScriptSerializer().DeserializeObject(json));
            var buckets = Json.Child(root, "rateLimitsByLimitId");
            Dictionary<string, object> selected = null;
            if (buckets != null && buckets.ContainsKey("codex")) selected = Json.Child(buckets, "codex");
            if (selected == null)
            {
                var legacy = Json.Child(root, "rateLimits");
                string id = Json.Text(legacy, "limitId");
                if (String.IsNullOrEmpty(id) || id == "codex") selected = legacy;
            }
            if (selected == null) throw new FormatException("No Codex quota bucket.");
            var snapshot = new QuotaSnapshot { ObservedAt = observedAt };
            foreach (string name in new[] { "primary", "secondary" })
            {
                var item = Json.Child(selected, name);
                double? minutes = Json.Number(item, "windowDurationMins");
                double? used = Json.Number(item, "usedPercent");
                if (!minutes.HasValue || (minutes != 300 && minutes != 10080)) continue;
                if (!used.HasValue || Double.IsNaN(used.Value) || Double.IsInfinity(used.Value) || used < 0)
                    throw new FormatException("Invalid quota percentage.");
                var window = new QuotaWindow { Minutes = (int)minutes.Value,
                    Remaining = Math.Max(0, Math.Min(100, 100 - used.Value)), ResetAt = Json.Timestamp(item, "resetsAt") };
                if (window.Minutes == 300) snapshot.FiveHour = window; else snapshot.Week = window;
            }
            if (snapshot.FiveHour == null && snapshot.Week == null) throw new FormatException("No supported quota windows.");
            var credits = Json.Child(root, "rateLimitResetCredits");
            double? count = Json.Number(credits, "availableCount");
            if (count.HasValue && count >= 0 && count <= Int32.MaxValue) snapshot.ResetCredits = (int)count.Value;
            int details = 0;
            object raw;
            if (credits != null && credits.TryGetValue("credits", out raw) && raw is IEnumerable && !(raw is string))
            {
                foreach (object entry in (IEnumerable)raw)
                {
                    var credit = Json.Object(entry);
                    if (Json.Text(credit, "status") != "available") continue;
                    DateTimeOffset? expiry = Json.Timestamp(credit, "expiresAt");
                    if (expiry.HasValue && expiry <= observedAt) continue;
                    details++;
                    if (expiry.HasValue && (!snapshot.CreditExpiry.HasValue || expiry < snapshot.CreditExpiry)) snapshot.CreditExpiry = expiry;
                }
            }
            snapshot.PartialCreditDetails = snapshot.ResetCredits.HasValue && details < snapshot.ResetCredits.Value;
            return snapshot;
        }

        public static QuotaSnapshot Demo(bool both)
        {
            var now = DateTimeOffset.Now;
            return new QuotaSnapshot { ObservedAt = now, Week = new QuotaWindow { Minutes = 10080, Remaining = 78, ResetAt = now.AddDays(5).AddHours(4) },
                FiveHour = both ? new QuotaWindow { Minutes = 300, Remaining = 62, ResetAt = now.AddHours(3) } : null,
                ResetCredits = 2, CreditExpiry = now.AddDays(2) };
        }
    }

    internal static class Json
    {
        public static Dictionary<string, object> Object(object value) { return value as Dictionary<string, object>; }
        public static Dictionary<string, object> Child(Dictionary<string, object> obj, string key)
        { object value; return obj != null && obj.TryGetValue(key, out value) ? Object(value) : null; }
        public static string Text(Dictionary<string, object> obj, string key)
        { object value; return obj != null && obj.TryGetValue(key, out value) && value is string ? (string)value : null; }
        public static double? Number(Dictionary<string, object> obj, string key)
        {
            object value; double number;
            if (obj == null || !obj.TryGetValue(key, out value) || value == null || value is bool || value is string) return null;
            return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? (double?)number : null;
        }
        public static DateTimeOffset? Timestamp(Dictionary<string, object> obj, string key)
        {
            double? seconds = Number(obj, key);
            if (!seconds.HasValue) return null;
            try { return new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(seconds.Value); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
    }
}
