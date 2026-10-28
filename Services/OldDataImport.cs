using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LoopCastsWeb.Data;
using LoopCastsWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Services;

/// <summary>
/// One-time import of the old Excel exports: orders-2026.csv + invoice-register.csv (spec: docs/specs/7-…).
/// Parse() only reads and cleans (no database) → a list of rows + a report of every fix, like CourseCsvImporter.
/// ApplyAsync() writes the rows. Invoices keep their old number and amounts: they were already sent to customers.
/// </summary>
public class OldDataImport
{
    public enum Level { Fixed, Note, Warning, Skipped }
    public record Line(Level Level, string Where, string Message);
    public record Row(Order Order, Invoice? Invoice);

    public List<Row> Rows { get; } = [];
    public List<Line> Report { get; } = [];

    private void Add(Level level, string where, string message) => Report.Add(new(level, where, message));

    // A CSV line: its line number in the file (for the report) + values by column name
    private record CsvRow(int Line, Dictionary<string, string> Col)
    {
        public string this[string name] => Col.GetValueOrDefault(name, "");
    }

    private record RegisterRow(int Line, string Number, DateOnly Date, string Customer,
        decimal Amount, decimal Vat, decimal Total, string Paid, string Remark);

    public static OldDataImport Parse(string ordersCsv, string registerCsv)
    {
        var import = new OldDataImport();
        import.Run(ReadCsv(ordersCsv), ReadCsv(registerCsv));
        return import;
    }

    private void Run(List<CsvRow> orders, List<CsvRow> registerRows)
    {
        var register = ReadRegister(registerRows, out var credited);

        // Which invoice numbers do the orders point to? (needed to find the replacement of a credited invoice)
        var referenced = orders.Select(o => o["invoice_nr"]).Where(register.ContainsKey).ToHashSet();
        var usedNumbers = new HashSet<string>();
        var invoiced = new List<(string Email, string Plan, int Line)>();
        var year = 2026;   // for dates without a year ("6 jan"): the year of the previous row

        foreach (var r in orders)
        {
            var where = $"orders line {r.Line}";
            var name = r["name"];

            var date = ParseDate(r["date"], ref year, out var dateFix);
            if (date is null) { Add(Level.Skipped, where, $"{name}: date '{r["date"]}' not readable"); continue; }
            if (dateFix is not null) Add(Level.Fixed, where, dateFix);

            var email = CleanEmail(r["email"]);
            if (email is null) { Add(Level.Skipped, where, $"{name}: e-mail '{r["email"]}' not valid"); continue; }
            if (email != r["email"]) Add(Level.Fixed, where, $"e-mail '{r["email"]}' → '{email}'");

            var country = FindCountry(r["country"]);
            if (country is null) { Add(Level.Skipped, where, $"{name}: country '{r["country"]}' unknown"); continue; }
            if (country.Code != r["country"]) Add(Level.Fixed, where, $"country '{r["country"]}' → {country.Code}");

            var plan = Plans.All.FirstOrDefault(p => string.Equals(p.Name, r["product"], StringComparison.OrdinalIgnoreCase));
            if (plan is null) { Add(Level.Skipped, where, $"{name}: product '{r["product"]}' unknown"); continue; }

            var price = Money(r["price_excl"]);
            if (price is null) { Add(Level.Skipped, where, $"{name}: price '{r["price_excl"]}' not readable"); continue; }
            if (r["price_excl"] != price.Value.ToString("0.##", CultureInfo.InvariantCulture))
                Add(Level.Fixed, where, $"price '{r["price_excl"]}' → {price:0.00}");
            if (price != plan.Price) Add(Level.Warning, where, $"price {price:0.00} ≠ {plan.Name} price {plan.Price:0.00} (kept)");

            var company = r["company"] is { Length: > 0 } c ? c : null;
            var vatNumber = r["vat_number"] is { Length: > 0 } v ? Regex.Replace(v, @"[\s.]", "").ToUpperInvariant() : null;
            if (vatNumber is not null && vatNumber != r["vat_number"]) Add(Level.Fixed, where, $"VAT number '{r["vat_number"]}' → {vatNumber}");

            if (r["remark"] is { Length: > 0 } remark) Add(Level.Note, where, $"{name}: \"{remark}\"");

            var order = new Order
            {
                Name = name, Email = email, Country = country.Code,
                IsCompany = company is not null, Company = company, VatNumber = vatNumber,
                PlanCode = plan.Code, Price = price.Value,
                CreatedAt = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                AccessKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            };

            // No invoice number: a duplicate of an invoiced row, or an order that was never paid
            var number = r["invoice_nr"];
            if (!register.ContainsKey(number))
            {
                if (Regex.IsMatch(number, @"^\d{4}-\d{4}$"))
                {
                    Add(Level.Skipped, where, $"{name}: invoice {number} is not in the invoice register");
                    continue;
                }
                var twin = invoiced.FirstOrDefault(i => i.Email == email && i.Plan == plan.Code);
                if (twin != default)
                {
                    Add(Level.Skipped, where, $"{name}: duplicate of orders line {twin.Line} (same e-mail and product, no invoice)");
                    continue;
                }
                Add(Level.Note, where, $"{name}: no invoice ('{number}') → imported as unpaid order");
                Rows.Add(new Row(order, null));
                continue;
            }

            // Credited invoice (credit note in the register) → use the customer's later invoice instead
            if (credited.Contains(number))
            {
                var customer = company ?? name;
                var replacement = register.Values.FirstOrDefault(x =>
                    !referenced.Contains(x.Number) && !usedNumbers.Contains(x.Number) && x.Customer == customer);
                if (replacement is not null)
                {
                    Add(Level.Fixed, where, $"invoice {number} was credited → using the replacement invoice {replacement.Number}");
                    number = replacement.Number;
                }
                else
                {
                    Add(Level.Warning, where, $"invoice {number} was credited but no replacement found (imported as registered)");
                }
            }

            usedNumbers.Add(number);
            invoiced.Add((email, plan.Code, r.Line));
            order.IsPaid = true;
            Rows.Add(new Row(order, BuildInvoice(order, plan, register[number], where)));
        }

        foreach (var x in register.Values.Where(x => !usedNumbers.Contains(x.Number) && !credited.Contains(x.Number)))
            Add(Level.Warning, $"register line {x.Line}", $"invoice {x.Number} ({x.Customer}) has no order: not imported");

        ReportGaps(register.Keys.Concat(credited));
    }

    private Invoice BuildInvoice(Order order, Plan plan, RegisterRow reg, string where)
    {
        var at = $"{where} / invoice {reg.Number}";
        // ponytail: old orders are history, not re-checked at VIES: a VAT number on the sheet counts as accepted back then
        var oldCheck = string.IsNullOrWhiteSpace(order.VatNumber) ? (VatCheck?)null : VatCheck.Valid;
        var rules = VatCalculator.Calculate(reg.Amount, order.Country, order.IsCompany, oldCheck);

        if (reg.Customer != (order.Company ?? order.Name))
            Add(Level.Warning, at, $"register says '{reg.Customer}', order says '{order.Company ?? order.Name}'");
        if (reg.Vat != rules.VatAmount)
            Add(Level.Warning, at, $"VAT {reg.Vat:0.00} in register, rules say {rules.VatAmount:0.00} ({rules.Rate:0%}{(rules.ReverseCharge ? ", reverse charge" : "")}) — check with the accountant");
        if (reg.Total != reg.Amount + reg.Vat)
            Add(Level.Warning, at, $"total {reg.Total:0.00} ≠ amount + VAT {reg.Amount + reg.Vat:0.00}");

        var paid = reg.Paid.ToLowerInvariant();
        if (paid == "")
            Add(Level.Note, at, "paid column empty: imported as paid (invoice exists), check the bank");
        else if (paid is not ("green" or "ja" or "yes" or "x") && !paid.StartsWith("betaald"))
            Add(Level.Warning, at, $"paid column '{reg.Paid}'{(reg.Remark is { Length: > 0 } ? $": \"{reg.Remark}\"" : "")}");
        else if (reg.Remark is { Length: > 0 })
            Add(Level.Note, at, $"\"{reg.Remark}\"");

        order.PaidAt = reg.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new Invoice
        {
            Number = reg.Number,
            IssuedOn = reg.Date,
            Description = plan.Name,
            BillingName = order.Name,
            BillingCompany = order.Company,
            BillingVatNumber = order.VatNumber,
            BillingAddress = order.Address,
            BillingCountry = order.Country,
            AmountExclVat = reg.Amount,
            VatRate = reg.Amount == 0 ? 0 : Math.Round(reg.Vat / reg.Amount, 2),
            VatAmount = reg.Vat,
            Total = reg.Total,
            ReverseCharge = reg.Vat == 0 && rules.ReverseCharge,
        };
    }

    private Dictionary<string, RegisterRow> ReadRegister(List<CsvRow> rows, out HashSet<string> credited)
    {
        var register = new Dictionary<string, RegisterRow>();
        credited = [];
        var year = 2026;

        foreach (var r in rows)
        {
            var where = $"register line {r.Line}";
            var number = r["invoice_nr"];
            var date = ParseDate(r["date"], ref year, out var dateFix);
            var amount = Money(r["amount_excl"]);
            var vat = Money(r["vat"]);
            var total = Money(r["total"]);
            if (date is null || amount is null || vat is null || total is null)
            {
                Add(Level.Skipped, where, $"invoice {number}: date or amounts not readable");
                continue;
            }
            if (dateFix is not null) Add(Level.Fixed, where, dateFix);

            if (amount < 0)   // credit note: not imported (no credit-note model yet), but remember what it cancels
            {
                var cancels = Regex.Match(r["remark"], @"\d{4}-\d{4}");
                if (cancels.Success) credited.Add(cancels.Value);
                Add(Level.Warning, where, $"credit note {number} ({amount:0.00}){(cancels.Success ? $" for {cancels.Value}" : "")}: not imported, handle manually");
                if (register.ContainsKey(number))
                    Add(Level.Warning, where, $"number {number} is used twice in the register (invoice + credit note)");
                continue;
            }
            if (register.ContainsKey(number))
            {
                Add(Level.Skipped, where, $"number {number} is used twice in the register");
                continue;
            }
            register[number] = new RegisterRow(r.Line, number, date.Value, r["customer"],
                amount.Value, vat.Value, total.Value, r["paid"], r["remark"]);
        }
        return register;
    }

    private void ReportGaps(IEnumerable<string> numbers)
    {
        foreach (var year in numbers.GroupBy(n => n[..4]))
        {
            var seq = year.Select(n => int.Parse(n[5..])).ToHashSet();
            for (var i = 1; i < seq.Max(); i++)
                if (!seq.Contains(i)) Add(Level.Note, "register", $"invoice number {year.Key}-{i:0000} does not exist (gap)");
        }
    }

    // ---------- cleaning helpers (public static → unit-tested one by one) ----------

    public static DateOnly? ParseDate(string raw, ref int year, out string? fix)
    {
        fix = null;
        var inv = CultureInfo.InvariantCulture;
        if (DateOnly.TryParseExact(raw, "dd/MM/yyyy", inv, DateTimeStyles.None, out var d)) { year = d.Year; return d; }

        if (DateOnly.TryParseExact(raw, ["d/M/yyyy", "yyyy-MM-dd", "d-M-yy", "d-M-yyyy"], inv, DateTimeStyles.None, out d))
        {
            fix = $"date '{raw}' → {d:dd/MM/yyyy}";
        }
        else if (DateOnly.TryParseExact(raw, "M/d/yyyy", inv, DateTimeStyles.None, out d))
        {
            fix = $"date '{raw}' is month/day (US) → {d:dd/MM/yyyy}";
        }
        else if (DateOnly.TryParseExact($"{raw} {year}", "d MMM yyyy", inv, DateTimeStyles.AllowWhiteSpaces, out d))
        {
            fix = $"date '{raw}' has no year → {d:dd/MM/yyyy}";
        }
        else
        {
            return null;
        }
        year = d.Year;
        return d;
    }

    /// "149", "€ 149", "149,00", "149.00", "-180,29" → decimal
    public static decimal? Money(string raw)
    {
        var s = raw.Replace("€", "").Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m)
            ? m : null;
    }

    /// Trim, lower-case, "example,com" → "example.com". Null if still not an e-mail address.
    public static string? CleanEmail(string raw)
    {
        var e = raw.Trim().ToLowerInvariant().Replace(',', '.');
        return Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[a-z]{2,}$") ? e : null;
    }

    private static readonly Dictionary<string, string> CountryAliases = new()
    {
        ["belgie"] = "BE", ["belgique"] = "BE", ["nederland"] = "NL", ["deutschland"] = "DE",
    };

    /// "BE", "Belgium", "België", "belgie" → BE. Accents and capitals are ignored.
    public static Country? FindCountry(string raw)
    {
        var key = RemoveAccents(raw.Trim()).ToLowerInvariant();
        return Countries.Find(raw)
            ?? Countries.All.FirstOrDefault(c => c.Name.ToLowerInvariant() == key)
            ?? (CountryAliases.TryGetValue(key, out var code) ? Countries.Find(code) : null);
    }

    private static string RemoveAccents(string s) =>
        new(s.Normalize(NormalizationForm.FormD).Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());

    // ponytail: plain split on ';' — the old exports have no quoted fields; use a CSV library if they ever do
    private static List<CsvRow> ReadCsv(string csv)
    {
        var lines = csv.Replace("\r", "").Split('\n');
        var header = lines[0].Trim().TrimStart('﻿').Split(';').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var rows = new List<CsvRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = lines[i].Split(';');
            var col = new Dictionary<string, string>();
            for (var c = 0; c < header.Length; c++)
                col[header[c]] = c < values.Length ? values[c].Trim() : "";
            rows.Add(new CsvRow(i + 1, col));
        }
        return rows;
    }

    // ---------- writing ----------

    /// Writes the rows in one transaction. Already imported (same invoice number / same unpaid order) → skipped.
    /// Returns (imported, alreadyThere).
    public async Task<(int Imported, int Existing)> ApplyAsync(AppDbContext db, MemberService members)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var existingNumbers = (await db.Invoices.Select(i => i.Number).ToListAsync()).ToHashSet();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int imported = 0, existing = 0;

        foreach (var (order, invoice) in Rows)
        {
            var already = invoice is not null
                ? existingNumbers.Contains(invoice.Number)
                : await db.Orders.AnyAsync(o => o.Email == order.Email && o.PlanCode == order.PlanCode && o.CreatedAt == order.CreatedAt);
            if (already) { existing++; continue; }

            db.Orders.Add(order);
            await db.SaveChangesAsync();   // the order gets its Id

            if (invoice is not null)
            {
                invoice.OrderId = order.Id;
                db.Invoices.Add(invoice);
                var start = DateOnly.FromDateTime(order.CreatedAt);
                var months = Plans.Find(order.PlanCode)?.AccessMonths;
                var ends = months is int m ? start.AddMonths(m) : (DateOnly?)null;
                db.Accounts.Add(new Account
                {
                    OrderId = order.Id, Email = order.Email, StartsOn = start, EndsOn = ends,
                    IsActive = ends is null || ends >= today,
                });
                await db.SaveChangesAsync();
                await members.EnsureMemberAsync(order.Email);   // login without password, nobody is e-mailed
            }
            imported++;
        }

        await tx.CommitAsync();
        return (imported, existing);
    }
}
