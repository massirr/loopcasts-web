using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;


namespace LoopCastsWeb.Models;

public class Order
{
    // [BindNever] = the form can never set this (no "IsPaid=true" sent by a crafted POST)
    [BindNever] public int Id { get; set; }
    [Required(ErrorMessage = "Enter your name.")]
    [StringLength(100, ErrorMessage = "Keep your name under 100 characters.")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Enter your e-mail address.")]
    [EmailAddress(ErrorMessage = "Enter an e-mail address like name@company.com.")]
    [StringLength(254, ErrorMessage = "This e-mail address is too long.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Choose your country.")]
    public string Country { get; set; } = "";

    // Company details: only required when IsCompany is true (checked in Order.cshtml.cs)
    [Display(Name = "I'm ordering for a company")]
    public bool IsCompany { get; set; }
    [StringLength(100, ErrorMessage = "Keep the company name under 100 characters.")]
    public string? Company { get; set; }
    [Display(Name = "VAT number")]
    [StringLength(30, ErrorMessage = "Enter a VAT number like BE0123456789.")]
    public string? VatNumber { get; set; }
    [StringLength(200, ErrorMessage = "Keep the address under 200 characters.")]
    public string? Address { get; set; }

    // VIES check of the VAT number (#33). Null = no check needed (private, Belgium, outside the EU).
    // NotVerified = VIES was down: 21% charged, Marieke checks later (Admin → Orders).
    [BindNever] public VatCheck? VatCheckResult { get; set; }
    [BindNever] public DateTime? VatCheckedAt { get; set; }
    [BindNever] public string? VatCheckName { get; set; }     // company name VIES returned, if the country shares it
    public string PlanCode { get; set; } = "";
    [BindNever] public decimal Price { get; set; }
    [BindNever] public DateTime CreatedAt { get; set; }
    [BindNever] public bool IsPaid { get; set; }
    [BindNever] public DateTime? PaidAt { get; set; }

    // Secret part of the /Pay and /OrderThanks URLs: ids are 1, 2, 3..., so without it anyone could open other orders
    [BindNever] public string AccessKey { get; set; } = "";

    // Mollie's id for the payment of this order ("tr_..."), set when the customer goes to Mollie's checkout
    [BindNever] public string? MolliePaymentId { get; set; }

    public bool Opens(string? key) => AccessKey.Length > 0 && key == AccessKey;

}

public enum VatCheck { Valid, Invalid, NotVerified }
