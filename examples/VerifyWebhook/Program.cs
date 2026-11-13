using System.Text;
using AyInbox;

// Runs offline against the published test vector.
const string secret = "whsec_test_secret";
const string body = "{\"id\":\"00000000-0000-4000-8000-000000000001\",\"event_type\":\"campaign.reply_received\",\"organization_id\":\"00000000-0000-4000-8000-0000000000aa\",\"created_at\":\"2023-11-14T22:13:20Z\",\"data\":{}}";
const string header = "t=1700000000,v1=d1efe3010ae2523566985158ab4852f8f3569365a06941ef87e3a641e3171342";
var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

try
{
    var ev = WebhookSignature.ConstructEvent(Encoding.UTF8.GetBytes(body), header, secret, now: now);
    Console.WriteLine($"Verified {ev.EventType} {ev.Id}");
}
catch (SignatureVerificationException e)
{
    Console.WriteLine($"Rejected: {e.Message}");
    return 1;
}

var tampered = WebhookSignature.Verify(body.Replace("{}", "{\"x\":1}"), header, secret, now: now);
Console.WriteLine($"Tampered body verifies: {tampered}");
return tampered ? 1 : 0;
