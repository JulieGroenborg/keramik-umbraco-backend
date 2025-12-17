using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;


namespace server.Controllers
{
    [ApiController]
    [Route("stripe-api")]
    public class StripeVerificationController : ControllerBase
    {
        [HttpGet("verify-session")]
        public ActionResult VerifySession([FromQuery] string sessionId)
        {
            var sessionService = new SessionService();
            var session = sessionService.Get(sessionId);

            if (session == null)
                return BadRequest("Session not found");

            // Check the session's payment status directly
            if (session.PaymentStatus != "paid")
            {
                return BadRequest("Payment not completed");
            }

            return Ok(new
            {
                session.Id,
                AmountTotal = session.AmountTotal,
                Currency = session.Currency,
                CustomerDetails = session.CustomerDetails,
                ShippingDetails = session.CustomerDetails?.Address
            });
        }

    }
}
