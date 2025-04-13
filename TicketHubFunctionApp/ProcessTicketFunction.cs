using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TicketHubFunctionApp.Models;
using System.Text.Json.Serialization;

namespace TicketHubFunctionApp
{
    public class ProcessTicketFunction
    {
        private readonly ILogger<ProcessTicketFunction> _logger;
        private readonly IConfiguration _configuration;

        public ProcessTicketFunction(ILogger<ProcessTicketFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [Function("ProcessTicketFunction")]
        public async Task Run(
            [QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] string messageText)
        {
            Console.WriteLine("---------- FUNCTION START ----------");
            _logger.LogInformation("---------- FUNCTION START ----------");

            try
            {
                Console.WriteLine("Raw queue message:");
                Console.WriteLine(messageText);
                _logger.LogInformation("Raw queue message:");
                _logger.LogInformation(messageText);

                var purchase = JsonSerializer.Deserialize<TicketPurchase>(
                    messageText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement
                    });

                if (purchase == null)
                {
                    Console.WriteLine("ERROR: Deserialization failed: message content could not be parsed into TicketPurchase.");
                    _logger.LogError("Deserialization failed: message content could not be parsed into TicketPurchase.");
                    return;
                }

                // Log the deserialized object to verify all properties
                Console.WriteLine($"Deserialized purchase object:");
                Console.WriteLine($"ConcertId: {purchase.ConcertId}");
                Console.WriteLine($"Email: {purchase.Email}");
                Console.WriteLine($"Name: {purchase.Name}");
                Console.WriteLine($"Phone: {purchase.Phone}");
                Console.WriteLine($"Quantity: {purchase.Quantity}");
                Console.WriteLine($"CreditCard: {purchase.CreditCard?.Substring(Math.Max(0, purchase.CreditCard.Length - 4))}"); // Log only last 4
                Console.WriteLine($"Expiration: {purchase.Expiration}");
                Console.WriteLine($"SecurityCode length: {purchase.SecurityCode?.Length ?? 0}");
                Console.WriteLine($"Address: {purchase.Address}");
                Console.WriteLine($"City: {purchase.City}");
                Console.WriteLine($"Province: {purchase.Province}");
                Console.WriteLine($"PostalCode: {purchase.PostalCode}");
                Console.WriteLine($"Country: {purchase.Country}");

                purchase.CreatedAt = DateTime.UtcNow;
                Console.WriteLine($"CreatedAt: {purchase.CreatedAt}");

                // Implement trimming credit card to last 4 digits as mentioned in model comment
                if (purchase.CreditCard?.Length > 4)
                {
                    purchase.CreditCard = purchase.CreditCard.Substring(purchase.CreditCard.Length - 4);
                    Console.WriteLine($"Trimmed credit card to last 4 digits: {purchase.CreditCard}");
                }

                string? connString = _configuration["SqlConnection"];
                if (string.IsNullOrEmpty(connString))
                {
                    Console.WriteLine("ERROR: SQL connection string is missing or null.");
                    _logger.LogError("SQL connection string is missing or null.");
                    return;
                }

                Console.WriteLine("Opening SQL connection...");
                _logger.LogInformation("Opening SQL connection...");
                using SqlConnection conn = new(connString);
                await conn.OpenAsync();
                Console.WriteLine("SQL connection opened successfully.");
                _logger.LogInformation("SQL connection opened successfully.");

                Console.WriteLine("Building SQL command...");
                _logger.LogInformation("Building SQL command...");

                string query = @"
                    INSERT INTO TicketPurchases 
                    (ConcertId, Email, Name, Phone, Quantity, CreditCard, SecurityCode, Expiration, Address, City, Province, PostalCode, Country, CreatedAt)
                    VALUES
                    (@ConcertId, @Email, @Name, @Phone, @Quantity, @CreditCard, @SecurityCode, @Expiration, @Address, @City, @Province, @PostalCode, @Country, @CreatedAt);
                ";

                using SqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@ConcertId", purchase.ConcertId);
                cmd.Parameters.AddWithValue("@Email", purchase.Email);
                cmd.Parameters.AddWithValue("@Name", purchase.Name);
                cmd.Parameters.AddWithValue("@Phone", purchase.Phone);
                cmd.Parameters.AddWithValue("@Quantity", purchase.Quantity);
                cmd.Parameters.AddWithValue("@CreditCard", purchase.CreditCard);
                cmd.Parameters.AddWithValue("@SecurityCode", purchase.SecurityCode);
                cmd.Parameters.AddWithValue("@Expiration", purchase.Expiration);
                cmd.Parameters.AddWithValue("@Address", purchase.Address);
                cmd.Parameters.AddWithValue("@City", purchase.City);
                cmd.Parameters.AddWithValue("@Province", purchase.Province);
                cmd.Parameters.AddWithValue("@PostalCode", purchase.PostalCode);
                cmd.Parameters.AddWithValue("@Country", purchase.Country);
                cmd.Parameters.AddWithValue("@CreatedAt", purchase.CreatedAt);

                Console.WriteLine("SQL command built with all parameters.");
                _logger.LogInformation("SQL command built with all parameters.");
                Console.WriteLine("Executing SQL command...");
                _logger.LogInformation("Executing SQL command...");

                await cmd.ExecuteNonQueryAsync();

                Console.WriteLine("Ticket purchase inserted successfully.");
                _logger.LogInformation("Ticket purchase inserted successfully.");
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON Deserialization error: {jsonEx.Message}");
                _logger.LogError($"JSON Deserialization error: {jsonEx.Message}");
                Console.WriteLine($"Stack Trace: {jsonEx.StackTrace}");
                _logger.LogError($"Stack Trace: {jsonEx.StackTrace}");
                throw; // Rethrow to send to poison queue
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"SQL Exception: {sqlEx.Message}");
                _logger.LogError($"SQL Exception: {sqlEx.Message}");
                Console.WriteLine($"Error Number: {sqlEx.Number}");
                _logger.LogError($"Error Number: {sqlEx.Number}");

                // Log all SQL errors
                if (sqlEx.Errors != null)
                {
                    foreach (SqlError error in sqlEx.Errors)
                    {
                        Console.WriteLine($"SQL Error {error.Number}: {error.Message}, Line: {error.LineNumber}, State: {error.State}");
                        _logger.LogError($"SQL Error {error.Number}: {error.Message}, Line: {error.LineNumber}, State: {error.State}");
                    }
                }

                Console.WriteLine($"Stack Trace: {sqlEx.StackTrace}");
                _logger.LogError($"Stack Trace: {sqlEx.StackTrace}");
                throw; // Rethrow to send to poison queue
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception type: {ex.GetType().FullName}");
                _logger.LogError($"Unhandled exception type: {ex.GetType().FullName}");
                Console.WriteLine($"Unhandled exception: {ex.Message}");
                _logger.LogError($"Unhandled exception: {ex.Message}");

                // Log inner exception if available
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner exception type: {ex.InnerException.GetType().FullName}");
                    _logger.LogError($"Inner exception type: {ex.InnerException.GetType().FullName}");
                }

                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                throw; // Rethrow to send to poison queue
            }
            finally
            {
                Console.WriteLine("---------- FUNCTION END ----------");
                _logger.LogInformation("---------- FUNCTION END ----------");
            }
        }
    }
}