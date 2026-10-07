using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;


namespace LoopCastsWeb.Models;

public class Order
{
    // [BindNever] = the form can never set this (no "IsPaid=true" sent by a crafted POST)
    [BindNever] public int Id { get; set; }
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