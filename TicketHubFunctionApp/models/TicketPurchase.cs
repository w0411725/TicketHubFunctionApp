namespace TicketHubFunctionApp.Models
{
    public class TicketPurchase
    {
        public int ConcertId { get; set; }
        public string Email { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public int Quantity { get; set; }
        //Still named CreditCard to match JSON, but we only store the last 4
        public string CreditCard { get; set; } = default!;
        public string Expiration { get; set; } = default!;
        public string SecurityCode { get; set; } = default!;
        public string Address { get; set; } = default!;
        public string City { get; set; } = default!;
        public string Province { get; set; } = default!;
        public string PostalCode { get; set; } = default!;
        public string Country { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}