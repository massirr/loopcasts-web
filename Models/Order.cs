using System.ComponentModel.DataAnnotations;


namespace LoopCastsWeb.Models;

public class Order
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = "";
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    [Required]
    public string Country { get; set; } = "";

    // Company details: only required when IsCompany is true (checked in Order.cshtml.cs)
    [Display(Name = "I'm ordering for a company")]
    public bool IsCompany { get; set; }
    public string? Company { get; set; }
    [Display(Name = "VAT number")]
    public string? VatNumber { get; set; }
    public string? Address { get; set; }
    public string PlanCode { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

}