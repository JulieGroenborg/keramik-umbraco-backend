using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;


namespace server.Controllers
{
    [ApiController]
    // Full endpoint bliver: /stripe-api/verify-session
    [Route("stripe-api")] 
    public class StripeVerificationController : ControllerBase
    {
        // GET endpoint bruges efter returning fra Stripe Checkout
        [HttpGet("verify-session")]
        public ActionResult VerifySession([FromQuery] string sessionId)
        {
            var sessionService = new SessionService();

            // Fetch the Checkout Session directly fra Stripe
            var session = sessionService.Get(sessionId);

            // If the session does not exist, something went wrong
            if (session == null)
                return BadRequest("Session not found");

            // Verify that the payment was actually completed. This is important so users cannot fake successful payment
            if (session.PaymentStatus != "paid")
            {
                return BadRequest("Payment not completed");
            }

            // If payment is valid, return relevant order information. This data can be shown on the confirmation page
            return Ok(new { });
        }
    }
}
