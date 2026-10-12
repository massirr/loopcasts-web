using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;
using static LoopCastsWeb.Services.OldDataImport;

namespace LoopCastsWeb.Services;

/// <summary>
/// One-time import of the old website's members table (phpMyAdmin export of `users`, spec docs/specs/20-…).
/// The password column (MD5) is skipped character by character while reading: its value is never copied into a
/// string, so it can't be stored, shown or logged. After reading, only e-mail, product, dates and active remain.
/// Parse() cleans and reports (no database); ApplyAsync() writes an Account + a Member login without password.
/// </summary>
public class OldMemberImport
{
    /// What we keep of one users row (Row = the old table's id, to find it in phpMyAdmin). Never a password.
    public record Raw(int Row, string Email, string Product, string? Start, string? End, string Active);
    public record Member(int Row, string Email, Plan Plan, DateOnly StartsOn, DateOnly? EndsOn, bool WasActive, bool IsActive);
    /// "Active in the old system but end date passed": for Lotte, with what a year of that plan costs today.
    public record Overdue(string Email, string PlanName, DateOnly EndedOn, decimal YearlyValue);

    public List<Member> Members { get; } = [];
    public List<Line> Report { get; } = [];
    public List<Overdue> ActiveButEnded { get; } = [];
    public List<Raw> Rows { get; private set; } = [];
    public DateOnly Today { get; private init; }

    public int Expired => Members.Count(m => m.EndsOn < Today);
    public decimal OverdueYearlyValue => ActiveButEnded.Sum(o => o.YearlyValue);

    private void Add(Level level, string where, string message) => Report.Add(new(level, where, message));

    // Any column that looks like a secret is skipped while reading
    private static bool IsSecret(string column) => Regex.IsMatch(column, "pass|hash|salt|token", RegexOptions.IgnoreCase);

    public static OldMemberImport ParseSql(string sql, DateOnly today) => Parse(ReadUsersSql(sql), today);
    public static OldMemberImport ParseCsv(string csv, DateOnly today) => Parse(ReadCsv(csv), today);

    private static OldMemberImport Parse(List<Raw> rows, DateOnly today)
    {
        var import = new OldMemberImport { Rows = rows, Today = today };
        import.Run();
        return import;
    }

    private void Run()
    {
        var byEmail = new Dictionary<string, Member>();
        int monthlyRenewal = 0, singleEndIgnored = 0;

        foreach (var r in Rows)
        {
            var where = $"users row {r.Row}";
            var email = CleanEmail(r.Email);
            if (email is null) { Add(Level.Skipped, where, $"e-mail '{r.Email}' not valid"); continue; }

            var plan = Plans.Find(r.Product.Trim().ToLowerInvariant())
                ?? Plans.All.FirstOrDefault(p => string.Equals(p.Name, r.Product.Trim(), StringComparison.OrdinalIgnoreCase));
            if (plan is null) { Add(Level.Skipped, where, $"{email}: product '{r.Product}' unknown"); continue; }

            if (!TryDate(r.Start, out var start) || start is null) { Add(Level.Skipped, where, $"{email}: start date '{r.Start}' not readable"); continue; }
            if (!TryDate(r.End, out var oldEnd)) { Add(Level.Skipped, where, $"{email}: end date '{r.End}' not readable"); continue; }
            var wasActive = r.Active.Trim() == "1";

            // Our end date: single = lifetime; monthly without end = rolling, so access runs to the next monthly renewal
            DateOnly? ends = oldEnd;
            if (plan.AccessMonths is null)
            {
                if (oldEnd is not null) singleEndIgnored++;
                ends = null;
            }
            else if (oldEnd is null)
            {
                ends = wasActive && plan.AccessMonths == 1 ? NextRenewal(start.Value, Today) : start.Value.AddMonths(plan.AccessMonths.Value);
                if (wasActive && plan.AccessMonths == 1) monthlyRenewal++;
                else Add(Level.Fixed, where, $"{email}: no end date → {ends:dd/MM/yyyy} (start + {plan.AccessMonths} months)");
            }

            var member = new Member(r.Row, email, plan, start.Value, ends, wasActive, wasActive && (ends is null || ends >= Today));

            // The same e-mail twice (mostly team rows): keep the active one with the latest end date
            if (byEmail.TryGetValue(email, out var other))
            {
                var keep = Better(member, other) ? member : other;
                var drop = keep == member ? other : member;
                byEmail[email] = keep;
                Add(Level.Skipped, $"users row {drop.Row}", $"{email}: same e-mail as row {keep.Row} ({keep.Plan.Code}, {(keep.WasActive ? "active" : "inactive")}) → that one is kept");
                continue;
            }
            byEmail[email] = member;
        }

        Members.AddRange(byEmail.Values.OrderBy(m => m.Row));
        foreach (var m in Members.Where(m => m.WasActive && m.EndsOn < Today))
            ActiveButEnded.Add(new(m.Email, m.Plan.Name, m.EndsOn!.Value, YearlyValue(m.Plan)));

        if (monthlyRenewal > 0)
            Add(Level.Fixed, "users", $"{monthlyRenewal} active monthly member(s) without end date → access until their next monthly renewal date");
        if (singleEndIgnored > 0)
            Add(Level.Note, "users", $"{singleEndIgnored} single-course member(s) had an end date: single courses are lifetime here, end date ignored");
        if (ActiveButEnded.Count > 0)
            Add(Level.Warning, "users", $"{ActiveButEnded.Count} member(s) active in the old system but their end date passed → imported as ended (list for Lotte)");
    }

    private static bool Better(Member a, Member b) =>
        a.WasActive != b.WasActive ? a.WasActive : (a.EndsOn ?? DateOnly.MaxValue) > (b.EndsOn ?? DateOnly.MaxValue);

    /// A rolling monthly subscription that started on 14 March renews on the 14th: the first renewal on or after today.
    public static DateOnly NextRenewal(DateOnly start, DateOnly today)
    {
        var months = Math.Max(1, (today.Year - start.Year) * 12 + today.Month - start.Month);
        var next = start.AddMonths(months);
        return next < today ? start.AddMonths(months + 1) : next;
    }

    /// What a year of this plan costs at today's prices (monthly × 12). Lifetime plans: 0.
    /// The old site has one row per team seat, so a team row is worth its share: €599 / 5 seats.
    public static decimal YearlyValue(Plan plan) => plan.AccessMonths is int m ? Math.Round(plan.Price * 12 / m / plan.Seats, 2) : 0;

    private static bool TryDate(string? raw, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "0000-00-00") return true;
        if (!DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        date = d;
        return true;
    }

    // ---------- reading ----------

    /// Reads every `INSERT INTO `users` (...) VALUES (...),(...);` statement of a phpMyAdmin dump.
    /// Columns are found by name; secret columns are walked over without keeping a single character.
    public static List<Raw> ReadUsersSql(string sql)
    {
        var rows = new List<Raw>();
        foreach (Match insert in Regex.Matches(sql, @"INSERT\s+INTO\s+`?users`?\s*\(([^)]*)\)\s*VALUES", RegexOptions.IgnoreCase))
        {
            var columns = insert.Groups[1].Value.Split(',').Select(c => c.Trim().Trim('`').ToLowerInvariant()).ToArray();
            int Col(string name) => Array.IndexOf(columns, name);
            var (id, email, product, start, end, active) = (Col("id"), Col("email"), Col("product"), Col("start_date"), Col("end_date"), Col("active"));
            if (email < 0 || product < 0) continue;
            var skip = columns.Select(IsSecret).ToArray();

            var i = insert.Index + insert.Length;
            while (i < sql.Length && sql[i] != ';')
            {
                if (sql[i] != '(') { i++; continue; }
                var values = ReadTuple(sql, ref i, skip);
                var row = int.TryParse(Get(values, id), out var n) ? n : rows.Count + 1;
                rows.Add(new Raw(row, Get(values, email) ?? "", Get(values, product) ?? "",
                    Get(values, start), Get(values, end), Get(values, active) ?? "1"));
            }
        }
        return rows;
    }

    private static string? Get(List<string?> values, int index) => index >= 0 && index < values.Count ? values[index] : null;

    // One "(1, 'a@b.be', 'skip me', NULL, ...)" tuple. i starts at '(' and ends after ')'.
    private static List<string?> ReadTuple(string sql, ref int i, bool[] skip)
    {
        var values = new List<string?>();
        var cur = new StringBuilder();
        bool inQuote = false, quoted = false;
        i++;
        while (i < sql.Length)
        {
            var c = sql[i];
            var keep = values.Count >= skip.Length || !skip[values.Count];
            if (inQuote)
            {
                if (c == '\\' && i + 1 < sql.Length) { if (keep) cur.Append(sql[i + 1]); i += 2; continue; }
                if (c == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'') { if (keep) cur.Append('\''); i += 2; continue; }
                if (c == '\'') { inQuote = false; i++; continue; }
                if (keep) cur.Append(c);
                i++;
                continue;
            }
            if (c == '\'') { cur.Clear(); inQuote = quoted = true; i++; continue; }   // drop the space after the comma
            if (c is ',' or ')')
            {
                var text = cur.ToString().Trim();
                values.Add(!keep ? null : !quoted && text.Equals("NULL", StringComparison.OrdinalIgnoreCase) ? null : quoted ? cur.ToString() : text);
                cur.Clear();
                quoted = false;
                i++;
                if (c == ')') break;
                continue;
            }
            if (keep) cur.Append(c);
            i++;
        }
        return values;
    }

    /// The rows travel between Preview and Import as this CSV (hidden field): no password column exists any more.
    public string ToCsv()
    {
        var sb = new StringBuilder("id;email;product;start_date;end_date;active\n");
        foreach (var r in Rows) sb.Append($"{r.Row};{Clean(r.Email)};{Clean(r.Product)};{Clean(r.Start ?? "")};{Clean(r.End ?? "")};{Clean(r.Active)}\n");
        return sb.ToString();
        static string Clean(string s) => s.Replace(";", "").Replace("\n", "").Replace("\r", "");
    }

    private static List<Raw> ReadCsv(string csv) =>
        csv.Replace("\r", "").Split('\n').Skip(1).Where(l => l.Length > 0).Select((l, n) =>
        {
            var v = l.Split(';');
            string At(int k) => k < v.Length ? v[k] : "";
            return new Raw(int.TryParse(At(0), out var id) ? id : n + 1, At(1), At(2),
                At(3) is { Length: > 0 } s ? s : null, At(4) is { Length: > 0 } e ? e : null, At(5));
        }).ToList();

    // ---------- writing ----------

    /// One Account per member (plan on the account, no order) + a Member login without password. Nobody is e-mailed.
    /// E-mails that already have an account (e.g. from the imported orders, #7) are skipped. Returns (imported, existing).
    public async Task<(int Imported, int Existing)> ApplyAsync(AppDbContext db, MemberService members)
    {
        await using var tx = await db.Database.BeginTransactionAsync();   // one transaction: all or nothing, and fast on SQLite
        var known = (await db.Accounts.Select(a => a.Email.ToLower()).ToListAsync()).ToHashSet();
        int imported = 0, existing = 0;

        foreach (var m in Members)
        {
            if (known.Contains(m.Email)) { existing++; continue; }
            db.Accounts.Add(new Account
            {
                Email = m.Email, PlanCode = m.Plan.Code, StartsOn = m.StartsOn, EndsOn = m.EndsOn, IsActive = m.IsActive,
            });
            await db.SaveChangesAsync();
            await members.EnsureMemberAsync(m.Email);   // no password: they use "Forgot password"
            known.Add(m.Email);
            imported++;
        }

        await tx.CommitAsync();
        return (imported, existing);
    }
}
