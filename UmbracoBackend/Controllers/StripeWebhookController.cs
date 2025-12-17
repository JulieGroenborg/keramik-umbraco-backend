using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace server.Controllers
{
    [Route("stripe/webhook")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly string endpointSecret = "whsec_XXXXXXXXXXXXXXXX";

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    endpointSecret
                );
            }
            catch (StripeException e)
            {
                return BadRequest($"Webhook Error: {e.Message}");
            }

            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                var paymentIntentService = new PaymentIntentService();
                paymentIntentService.Update(session.PaymentIntentId, new PaymentIntentUpdateOptions
                {
                    ReceiptEmail = session.CustomerDetails.Email
                });
            }

            return Ok();
        }
    }
}
