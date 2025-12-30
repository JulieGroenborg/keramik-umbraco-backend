using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Text.Json.Serialization;

namespace server.Controllers
{
    // Denne controller exposer et API endpoint som vores frontend kan kalde, når brugeren klikker "Gå til betaling".
    // Denne controller håndterer oprettelsen af en Stripe Checkout Session og returnerer Stripes checkout URL. Den indeholder også validering, så en bruger ikke kan ændre produkt-data i frontenden
    [Route("stripe-api/create-checkout-session")]
    [ApiController]
    public class CreateCheckoutSessionController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CreateCheckoutSessionController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost]
        public async Task<ActionResult> Create([FromBody] List<CartRequestItem> cartRequests)
        {
            // 1. Basic validation: throw an error, hvis kurven er tom:
            if (cartRequests == null || cartRequests.Count == 0)
                return BadRequest(new { error = "Cart is empty" });

            var client = _httpClientFactory.CreateClient();
            var lineItems = new List<SessionLineItemOptions>();

            // 2. Loop igennem alle modtagne ID's og hent den "ægte" data fra Umbraco. Dette er vores primære sikkerhed: vi stoler kun på priser, der hentes direkte fra vores CMS
            foreach (var item in cartRequests)
            {
                // Vi bruger Umbraco Delivery API til at hente produktet via dets GUID
                var umbracoUrl = $"http://localhost:51857/umbraco/delivery/api/v2/content/item/{item.Id}";

                try
                {
                    // Fetch the product from Umbraco
                    var product = await client.GetFromJsonAsync<UmbracoProductResponse>(umbracoUrl);

                    // --- HER SKAL DU TESTE ---
                    Console.WriteLine($"--- DEBUG START for {product?.Name} ---");
                    Console.WriteLine($"Lager fundet i JSON: {product?.Properties?.StockQuantity}");
                    Console.WriteLine($"Kunde vil købe: {item.Quantity}");
                    Console.WriteLine($"Pris fundet: {product?.Properties?.Price}");
                    // ------------

                    if (product.Properties.StockQuantity < item.Quantity)
                    {
                        Console.WriteLine("LOGIK: Lager for lavt! Sender fejl til frontend.");
                        return BadRequest(new { error = $"Der er ikke nok på lager af {product.Name}. Der er {product.Properties.StockQuantity} stk. tilbage." });
                    }


                    if (product != null && product.Properties != null)
                    {
                        // Successfully found product, add it to the Stripe list
                        lineItems.Add(new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                // Stripe bruger mindste enhed (øre), så vi ganger prisen fra Umbraco med 100
                                UnitAmount = (long)(product.Properties.Price * 100),
                                Currency = "dkk",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = product.Name,
                                    Description = product.Properties.Description,
                                    // Vi gemmer Umbraco GUID i Metadata, så vores Webhook senere ved, hvilken vare der er købt, så vi kan updatere stockQuantity i backend
                                    Metadata = new Dictionary<string, string> { { "umbracoId", item.Id.ToString() } }
                                },
                            },
                            Quantity = item.Quantity,
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log the error to your terminal so you can see if a specific ID fails
                    Console.WriteLine($"Error fetching product {item.Id}: {ex.Message}");
                }
            }

            // IMPORTANT: If we didn't find any valid products, don't call Stripe
            if (lineItems.Count == 0)
            {
                return BadRequest(new { error = "No valid products found in CMS. Check backend logs." });
            }

            // 3. Konfiguration af Stripe Session
            var domain = "http://localhost:3000";
            var options = new SessionCreateOptions
            {
                LineItems = lineItems,
                Mode = "payment",
                ShippingAddressCollection = new
                // Tillad kun forsendelse til specifikke lande
                SessionShippingAddressCollectionOptions
                {
                    AllowedCountries = new List<string> { "DK", "SE", "DE" }
                },
                SuccessUrl = domain + "/success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = domain + "/kurv",
            };

            // Opret sessinen hos Stripe og returner URL'en til vores frontend
            var service = new SessionService();
            var session = service.Create(options);

            return Ok(new { url = session.Url });
        }
    }

    // --- Helper Models with lowercase JSON mapping ---

    // Modtager kun ID og Quantity fra frontenden for at minimere data-manipulation
    public class CartRequestItem
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
    }

    // Mapper Umbracos JSON struktur. Vi bruger [JsonProptertyName] da Umbravo API returnerer lowercase
    public class UmbracoProductResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public ProductProperties Properties { get; set; } = new();
    }

    public class ProductProperties
    {
        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("stockQuantity")]
        public int StockQuantity { get; set; }
    }
}