using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AyInbox.Tests
{
    public class ResourceTests
    {
        private const string CampaignId = "11111111-1111-4111-8111-111111111111";
        private const string ContactId = "22222222-2222-4222-8222-111111111111";

        [Fact]
        public async Task CampaignsListGetCreatePause()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("campaigns_list.json").EnqueueFixture("campaign.json").EnqueueFixture("campaign.json").EnqueueFixture("campaign_stop.json");

            var page = await client.Campaigns.ListAsync(new CampaignListParams { Status = "active", Q = "Q4" });
            Assert.Equal(2, page.Data.Count);
            Assert.True(page.HasMore);
            Assert.Equal("c1_ZmFrZS1jdXJzb3ItcGFnZS0y", page.NextCursor);
            Assert.Equal(3, page.Total);
            Assert.Equal("?q=Q4&status=active", stub.Requests[0].Uri.Query);

            var c = await client.Campaigns.GetAsync(CampaignId);
            Assert.False(string.IsNullOrEmpty(c.Name));
            Assert.Equal("/v1/campaigns/" + CampaignId, stub.Requests[1].Uri.AbsolutePath);

            await client.Campaigns.CreateAsync(new CampaignCreate { Name = "New", DailyLimit = 30, Steps = new() { new StepInput { Subject = "Hi", WaitAfter = 3 } } },
                new RequestOptions { IdempotencyKey = "create-1" });
            var create = stub.Requests[2];
            Assert.Equal("POST", create.Method);
            Assert.Equal("create-1", create.Headers["Idempotency-Key"]);
            using (var doc = JsonDocument.Parse(create.Body!))
            {
                Assert.Equal("New", doc.RootElement.GetProperty("name").GetString());
                Assert.Equal(30, doc.RootElement.GetProperty("daily_limit").GetInt32());
                Assert.Equal(3, doc.RootElement.GetProperty("steps")[0].GetProperty("wait_after").GetInt32());
                Assert.False(doc.RootElement.TryGetProperty("description", out _));
            }

            var paused = await client.Campaigns.PauseAsync(CampaignId);
            Assert.Equal("stopped", paused.Status);
            Assert.Equal("/v1/campaigns/" + CampaignId + "/stop", stub.Requests[3].Uri.AbsolutePath);
        }

        [Fact]
        public async Task ContactsListGetCreateUpdateDelete()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("contacts_list.json").EnqueueFixture("contact.json").EnqueueFixture("contacts_create.json")
                .EnqueueFixture("contact.json").Enqueue(204, null);

            var page = await client.Contacts.ListAsync(new ContactSearch { CampaignIds = new() { CampaignId }, SortBy = "email" }, new ContactListParams { Limit = 10 });
            Assert.Equal(2, page.Data.Count);
            Assert.True(page.Raw.TryGetProperty("counts", out _));
            var search = stub.Requests[0];
            Assert.Equal("POST", search.Method);
            Assert.Equal("/v1/contacts/search", search.Uri.AbsolutePath);
            Assert.Equal("?limit=10", search.Uri.Query);
            Assert.Contains("\"campaign_ids\":[\"" + CampaignId + "\"]", search.Body);

            var detail = await client.Contacts.GetAsync(ContactId);
            Assert.Equal("lee@example.com", detail.Email);
            Assert.Equal("Head of Ops", detail.CustomFields!["title"]);
            Assert.Equal("api", detail.Source);

            var created = await client.Contacts.CreateAsync(new[] { new ContactInput { Email = "lee@example.com" } });
            Assert.NotEmpty(created);

            var updated = await client.Contacts.UpdateAsync(ContactId, new ContactUpdate { Company = "New Co" }, new RequestOptions { IdempotencyKey = "upd-1" });
            Assert.Equal(ContactId, updated.Id);
            Assert.Equal("PATCH", stub.Requests[3].Method);
            Assert.Equal("{\"company\":\"New Co\"}", stub.Requests[3].Body);

            await client.Contacts.DeleteAsync(ContactId);
            Assert.Equal("DELETE", stub.Requests[4].Method);
            Assert.Equal("/v1/contacts/" + ContactId, stub.Requests[4].Uri.AbsolutePath);
        }

        [Fact]
        public async Task ContactSearchRetriesLikeARead()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("errors/503_service_unavailable.json", 503).EnqueueFixture("contacts_list.json");
            var page = await client.Contacts.ListAsync();
            Assert.Equal(2, page.Data.Count);
            Assert.Equal("{}", stub.Requests[0].Body);
            Assert.Equal(2, stub.Requests.Count);
        }

        [Fact]
        public async Task MailboxesList()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("mailboxes_list.json");
            var page = await client.Mailboxes.ListAsync(new MailboxListParams { Q = "sales" });
            Assert.Equal(2, page.Data.Count);
            Assert.Equal("sales@example.com", page.Data[0].Email);
            Assert.Null(page.Data[0].WorkerId);
            Assert.False(page.HasMore);
            Assert.Equal("/v1/emails", stub.Requests[0].Uri.AbsolutePath);
        }

        [Fact]
        public async Task UniboxListThreadsAndGetThread()
        {
            var (client, stub, _) = TestClient.Create();
            stub.EnqueueFixture("unibox_threads.json").EnqueueFixture("unibox_thread.json");

            var threads = await client.Unibox.ListThreadsAsync(new ThreadListParams { Unseen = true, Folder = "inbox" });
            Assert.Single(threads.Data);
            Assert.Equal("thread-example-001", threads.Data[0].ThreadId);
            Assert.True(threads.HasMore);
            Assert.Null(threads.Total);
            Assert.Equal("?folder=inbox&unseen=true", stub.Requests[0].Uri.Query);

            var thread = await client.Unibox.GetThreadAsync("thread-example-001");
            Assert.Equal(2, thread.Data.Count);
            Assert.Equal("?thread_id=thread-example-001", stub.Requests[1].Uri.Query);
            Assert.All(thread.Data, m => Assert.Equal("thread-example-001", m.ThreadId));
            Assert.Equal(2, stub.Requests.Count(r => r.Uri.AbsolutePath.StartsWith("/v1/unibox")));
        }
    }
}
