namespace LoopCastsWeb.Models;

public class Invoice
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Number { get; set; } = "";
    public DateOnly IssuedOn { get; set; }
    public string Description { get; set; } = "";   // snapshot of the product name at that moment
    public string BillingName { get; set; } = "";
    public string? BillingCompany { get; set; }
    public string? BillingVatNumber { get; set; }
    public string? BillingAddress { get; set; }
    public string BillingCountry { get; set; } = "";
    public decimal AmountExclVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public bool ReverseCharge { get; set; }
    // Proof for reverse charge (#33): copy of the order's VIES check at that moment
    public VatCheck? VatCheckResult { get; set; }
    public DateTime? VatCheckedAt { get; set; }
    public string? VatCheckName { get; set; }
}