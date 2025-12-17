using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;

namespace server.Controllers
{
    // Denne controller exposer et API endpoint som vores frontend kan kalde, når brugeren klikker "Gå til betaling". Controllerens creater en Stripe Checkout Session og returnerer Stripes checkout URL.
    [Route("stripe-api/create-checkout-session")]
    [ApiController]
    public class CreateCheckoutSessionController : ControllerBase
    {

        // ✅ Create a Stripe Checkout session: dette endpoint modtager kurvens indhold fra frontenden. VIGTIGT: vi stoler ikke blindt på frontenden, så det er her server-side validation og price lookup vil ske, når jeg når dertil.
        [HttpPost]
        public ActionResult Create([FromBody] List<CartItem> items)
        {
            // Log the incoming cart items from the frontend to the terminal
            Console.WriteLine("Received items from frontend:");
            if (items != null)
            {
                foreach (var item in items)
                {
                    Console.WriteLine($"Name: {item.Name}, Price: {item.Price}, Quantity: {item.Quantity}");
                }
            }

            // Basic validation: preventer at der bliver created en Stripe session, hvis kurven mangler eller er tom
            if (items == null || items.Count == 0)
                return BadRequest(new { error = "Cart is empty" });

            var domain = "http://localhost:3000"; //Domænet som Stripe skal bruge til at redirect brugeren til efter checkout (succes or cancellation)


            // Stripe Checkout forventer line items i et specifikt format, så vi konverterer vores kurv items into Stripe line items her
            var lineItems = new List<SessionLineItemOptions>();

            foreach (var item in items)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        // Stripe arbejder med den mindste currency unit (øre i stedet for kroner), så derfor laver vi følgende udregning:
                        UnitAmount = (long)(item.Price * 100),
                        Currency = "dkk",
                        
                        // Product information shown on the Stripe Checkout page
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Name,
                            // Description is optional and must not be empty
                            Description = item.Description,
                        },
                    },
                    Quantity = item.Quantity,
                });
            }

            // Configuration object for creating the Stripe Checkout Session
            var options = new SessionCreateOptions
            {
                LineItems = lineItems,
                Mode = "payment",

                ShippingAddressCollection = new SessionShippingAddressCollectionOptions
                {
                    AllowedCountries = new List<string> { "DK", "SE", "DE" }
                },

                // Brugeren bliver redirected hertil efter succesfuld payment
                SuccessUrl = domain + "/success?session_id={CHECKOUT_SESSION_ID}",

                // Brugeren bliver redirected hertil, hvis de cancel'er checkout'en
                CancelUrl = domain + "/kurv",
            };

            // Create the Stripe Checkoute Session
            var service = new SessionService();
            var session = service.Create(options);

            // Log the generated Stripe session URL to the terminal
            Console.WriteLine("Stripe session URL: " + session.Url);

            // Send the Stripe-hosted checkout URL tilbage til frontenden. Frontenden vil så redirect browseren til denne URL
            return Ok(new { url = session.Url });
        }
    }

    // ✅ Model representing et kurv item modtaget fra frontenden. Dette skal matche JSON-strukturen, der er sent fra frontenden
    public class CartItem
    {
    public required string Name { get; set; }
    public string? Description { get; set; } // Description er optional (nogle produkter har måske ikke en description)
    public decimal Price { get; set; } // Price is in major currency units, so it is converted to øre before sending to Stripe
    public int Quantity { get; set; }
    }
}
