using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System;
using System.Linq;

namespace server.Controllers
{
    [ApiController]
    // Endpoint: /stripe-api/verify-session
    [Route("stripe-api")]
    public class StripeVerificationController : ControllerBase
    {
        // GET endpoint som frontenden kalder på succes-siden for at få den endelige status
        [HttpGet("verify-session")]
        public ActionResult VerifySession([FromQuery] string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest(new { status = "error", message = "Session ID is required" });
            }

            try
            {
                var sessionService = new SessionService();

                // 1. Hent Checkout Sessionen fra Stripe
                var session = sessionService.Get(sessionId);

                if (session == null)
                {
                    return NotFound(new { status = "error", message = "Session not found" });
                }

                // 2. Grundlæggende tjek: Er betalingen gennemført hos Stripe?
                // Hvis status er 'unpaid', betyder det, at kunden f.eks. lukkede vinduet før tid.
                if (session.PaymentStatus != "paid")
                {
                    return Ok(new { status = "pending", message = "Betalingen er ikke registreret endnu." });
                }

                // 3. Tjek om vores Webhook har refunderet betalingen pga. oversalg.
                var paymentIntentService = new PaymentIntentService();
                // Vi beder Stripe om at inkludere (expand) 'latest_charge', så vi kan se refund-status
                var piOptions = new PaymentIntentGetOptions();
                piOptions.AddExpand("latest_charge");

                var paymentIntent = paymentIntentService.Get(session.PaymentIntentId, piOptions);

                // Vi tjekker nu på LatestCharge objektet
                bool isRefunded = paymentIntent.LatestCharge?.Refunded ?? false;

                if (isRefunded)
                {
                    return Ok(new
                    {
                        status = "refunded",
                        message = "Oversalg konstateret. Beløbet er refunderet."
                    });
                }
                // 4. Alt er i orden! 
                // Betalingen er 'paid' og den er IKKE blevet refunderet.
                return Ok(new
                {
                    status = "success",
                });
            }
            catch (StripeException e)
            {
                // Håndterer fejl fra Stripes API (f.eks. netværksfejl eller ugyldige API-nøgler)
                Console.WriteLine($"Stripe Error: {e.Message}");
                return StatusCode(500, new { status = "error", message = "Kunne ikke verificere med Stripe." });
            }
            catch (Exception ex)
            {
                // Generel fejlhåndtering
                Console.WriteLine($"General Error: {ex.Message}");
                return StatusCode(500, new { status = "error", message = "Der skete en uventet fejl." });
            }
        }
    }
}