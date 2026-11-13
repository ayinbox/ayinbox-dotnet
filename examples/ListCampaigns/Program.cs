using AyInbox;

// Needs AYINBOX_API_KEY with READ_CAMPAIGNS.
using var client = new AyInboxClient();

var me = await client.Me.GetAsync();
Console.WriteLine($"Signed in as {me.Email}");

await foreach (var campaign in client.Campaigns.ListAllAsync(new CampaignListParams { Limit = 50 }))
{
    Console.WriteLine($"{campaign.Status,-10} {campaign.Name}");
}
