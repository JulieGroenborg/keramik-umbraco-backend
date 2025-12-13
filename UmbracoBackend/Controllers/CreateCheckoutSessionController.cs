using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace server.Controllers
{
    [Route("stripe-api/create-checkout-session")]
    [ApiController]
    public class CreateCheckoutSessionController : ControllerBase
    {
        // ✅ Create a Stripe Checkout session
        [HttpPost]
        public ActionResult Create([FromBody] List<CartItem> items)
        {
            // Log the incoming cart items to the terminal
            Console.WriteLine("Received items from frontend:");
            if (items != null)
            {
                foreach (var item in items)
                {
                    Console.WriteLine($"Name: {item.Name}, Price: {item.Price}, Quantity: {item.Quantity}");
                }
            }

            if (items == null || items.Count == 0)
                return BadRequest(new { error = "Cart is empty" });

            var domain = "http://localhost:3000"; // Your frontend domain

            var lineItems = new List<SessionLineItemOptions>();

            foreach (var item in items)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100), // Stripe expects smallest currency unit
                        Currency = "dkk",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Name,
                            Description = item.Description,
                        },
                    },
                    Quantity = item.Quantity,
                });
            }

            var options = new SessionCreateOptions
            {
                LineItems = lineItems,
                Mode = "payment",
                SuccessUrl = domain + "/success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = domain + "/kurv",
            };

            var service = new SessionService();
            var session = service.Create(options);

            // Log the generated Stripe session URL to the terminal
            Console.WriteLine("Stripe session URL: " + session.Url);

            return Ok(new { url = session.Url });
        }
    }

    // ✅ Model for frontend cart items
    public class CartItem
    {
    public string Name { get; set; }
    public string? Description { get; set; } // nullable
    public decimal Price { get; set; }
    public int Quantity { get; set; } 
    }
}
