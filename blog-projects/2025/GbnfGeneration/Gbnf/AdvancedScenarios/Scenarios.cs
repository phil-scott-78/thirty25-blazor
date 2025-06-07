using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using RedPajama;

namespace Gbnf.AdvancedScenarios;

public class Product
{
    [Description("The unique identifier for this product5")]
    public required Guid Id { get; init; }
    
    [Description("The customer-facing name of the product")]
    public required string Name { get; init; }
    
    [Description("Current inventory count")]
    public required int StockLevel { get; init; }
}

public class User
{
    [MinLength(2)]
    [MaxLength(50)]
    public required string Username { get; init; }
    
    [MinLength(8)]
    [MaxLength(100)]
    public required string Password { get; init; }
    
    [MinLength(5)]
    [MaxLength(5)]
    public required string ZipCode { get; init; }
}

public class Order
{
    public required string CustomerName { get; init; }
    
    [AllowedValues("Pending", "Processing", "Shipped", "Delivered", "Cancelled")]
    public required string Status { get; init; }
    
    [AllowedValues("Standard", "Express", "Overnight")]
    public required string ShippingMethod { get; init; }
}

public class ShoppingCart
{
    public required string UserId { get; init; }
    
    public required CartItem[] Items { get; init; }
}

public class CartItem
{
    public required string ProductId { get; init; }
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal Price { get; init; }
}

public class Contact
{
    [Format("alpha-space")]
    public required string FullName { get; init; }
    
    [Format("email")]
    public required string Email { get; init; }
    
    [Format("(###) ###-####")]
    public required string PhoneNumber { get; init; }
    
    [Format("uppercase")]
    public required string CountryCode { get; init; }
    
    [Format("alphanumeric")]
    public required string ReferenceNumber { get; init; }
}

public class Document
{
    [Description("Alphanumeric code in the format XXX-XXX-XXXX.")]
    [Format("gbnf:[a-zA-Z0-9]{3}-[a-zA-Z0-9]{3}-[a-zA-Z0-9]{4}")]
    public required string ReferenceCode { get; init; }
    
    [Description("Serial number in the format AA999999999AA.")]
    [Format("gbnf:[A-Z]{2}[0-9]{9}[A-Z]{2}")]
    public required string SerialNumber { get; init; }
    
    public required string Title { get; init; }
    
    public required DateTime CreatedOn { get; init; }
}