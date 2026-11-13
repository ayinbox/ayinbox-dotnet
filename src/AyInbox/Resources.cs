using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AyInbox
{
    internal static class Q
    {
        public static List<KeyValuePair<string, string?>> Of(params (string Key, object? Value)[] items)
        {
            var list = new List<KeyValuePair<string, string?>>();
            foreach (var (k, v) in items)
            {
                if (v == null) continue;
                var s = v switch
                {
                    bool b => b ? "true" : "false",
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    _ => v.ToString(),
                };
                list.Add(new KeyValuePair<string, string?>(k, s));
            }
            return list;
        }
    }

    /// <summary><c>GET /me</c>.</summary>
    public sealed class MeResource
    {
        private readonly AyInboxClient _c;
        internal MeResource(AyInboxClient c) { _c = c; }

        /// <summary>Returns the caller's identity and scopes. Needs no scope.</summary>
        public Task<Identity> GetAsync(RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetAsync<Identity>("GET", "/me", null, null, options, false, cancellationToken);
    }

    /// <summary>Campaigns. Other campaign routes are reachable through <see cref="AyInboxClient.RequestAsync"/>.</summary>
    public sealed class CampaignsResource
    {
        private readonly AyInboxClient _c;
        internal CampaignsResource(AyInboxClient c) { _c = c; }

        private static List<KeyValuePair<string, string?>> Query(CampaignListParams? p) =>
            Q.Of(("q", p?.Q), ("status", p?.Status), ("folder", p?.Folder), ("cursor", p?.Cursor), ("limit", p?.Limit));

        /// <summary><c>GET /campaigns</c>: one page. Needs READ_CAMPAIGNS.</summary>
        public Task<Page<Campaign>> ListAsync(CampaignListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetPageAsync<Campaign>("GET", "/campaigns", Query(parameters), null, options, false, cancellationToken);

        /// <summary>Every campaign, following cursors.</summary>
        public IAsyncEnumerable<Campaign> ListAllAsync(CampaignListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            AyInboxClient.WalkAsync(cursor => _c.GetPageAsync<Campaign>("GET", "/campaigns",
                AyInboxClient.WithCursor(Query(parameters), cursor), null, options, false, cancellationToken), cancellationToken);

        /// <summary><c>GET /campaigns/{id}</c>. Needs READ_CAMPAIGNS.</summary>
        public Task<Campaign> GetAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetAsync<Campaign>("GET", "/campaigns/" + AyInboxClient.Seg(id), null, null, options, false, cancellationToken);

        /// <summary><c>POST /campaigns</c>: creates a draft. Never sends mail. Needs WRITE_CAMPAIGNS.</summary>
        public Task<Campaign> CreateAsync(CampaignCreate campaign, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            return _c.GetAsync<Campaign>("POST", "/campaigns", null, campaign, options, false, cancellationToken);
        }

        /// <summary><c>POST /campaigns/{id}/stop</c>: pauses an active campaign. Sends nothing. Needs WRITE_CAMPAIGNS, START_CAMPAIGNS or SEND_CAMPAIGNS.</summary>
        public Task<CampaignPauseResult> PauseAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetAsync<CampaignPauseResult>("POST", "/campaigns/" + AyInboxClient.Seg(id) + "/stop", null, null, options, false, cancellationToken);
    }

    /// <summary>Contacts.</summary>
    public sealed class ContactsResource
    {
        private readonly AyInboxClient _c;
        internal ContactsResource(AyInboxClient c) { _c = c; }

        private static List<KeyValuePair<string, string?>> Query(ContactListParams? p) =>
            Q.Of(("cursor", p?.Cursor), ("limit", p?.Limit), ("category", p?.Category));

        /// <summary><c>POST /contacts/search</c>: one page. A read, retried like a GET. Needs READ_CONTACTS.</summary>
        public Task<Page<Contact>> ListAsync(ContactSearch? search = null, ContactListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetPageAsync<Contact>("POST", "/contacts/search", Query(parameters), search ?? new ContactSearch(), options, true, cancellationToken);

        /// <summary>Every matching contact, resending the same search on each page.</summary>
        public IAsyncEnumerable<Contact> ListAllAsync(ContactSearch? search = null, ContactListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var body = search ?? new ContactSearch();
            return AyInboxClient.WalkAsync(cursor => _c.GetPageAsync<Contact>("POST", "/contacts/search",
                AyInboxClient.WithCursor(Query(parameters), cursor), body, options, true, cancellationToken), cancellationToken);
        }

        /// <summary><c>GET /contacts/{id}</c>. Needs READ_CONTACTS.</summary>
        public Task<ContactDetail> GetAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetAsync<ContactDetail>("GET", "/contacts/" + AyInboxClient.Seg(id), null, null, options, false, cancellationToken);

        /// <summary>
        /// <c>POST /contacts</c>: upserts contacts by email (up to 10,240). Needs WRITE_CONTACTS.
        /// Adding contacts to an active campaign can send mail.
        /// </summary>
        public Task<List<Contact>> CreateAsync(IEnumerable<ContactInput> contacts, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (contacts == null) throw new ArgumentNullException(nameof(contacts));
            return _c.GetAsync<List<Contact>>("POST", "/contacts", null, new List<ContactInput>(contacts), options, false, cancellationToken);
        }

        /// <summary><c>POST /contacts</c> with one contact. The API still answers with an array.</summary>
        public Task<List<Contact>> CreateAsync(ContactInput contact, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            return CreateAsync(new[] { contact }, options, cancellationToken);
        }

        /// <summary><c>PATCH /contacts/{id}</c>. Needs WRITE_CONTACTS.</summary>
        public Task<Contact> UpdateAsync(string id, ContactUpdate update, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            return _c.GetAsync<Contact>("PATCH", "/contacts/" + AyInboxClient.Seg(id), null, update, options, false, cancellationToken);
        }

        /// <summary><c>DELETE /contacts/{id}</c>. Needs WRITE_CONTACTS.</summary>
        public Task DeleteAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.SendAsync("DELETE", "/contacts/" + AyInboxClient.Seg(id), null, null, options, false, cancellationToken);
    }

    /// <summary>Mailboxes (<c>/emails</c>).</summary>
    public sealed class MailboxesResource
    {
        private readonly AyInboxClient _c;
        internal MailboxesResource(AyInboxClient c) { _c = c; }

        private static List<KeyValuePair<string, string?>> Query(MailboxListParams? p) =>
            Q.Of(("q", p?.Q), ("tag", p?.Tag), ("cursor", p?.Cursor), ("limit", p?.Limit));

        /// <summary><c>GET /emails</c>: one page. Needs READ_EMAILS.</summary>
        public Task<Page<Mailbox>> ListAsync(MailboxListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetPageAsync<Mailbox>("GET", "/emails", Query(parameters), null, options, false, cancellationToken);

        /// <summary>Every mailbox, following cursors.</summary>
        public IAsyncEnumerable<Mailbox> ListAllAsync(MailboxListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            AyInboxClient.WalkAsync(cursor => _c.GetPageAsync<Mailbox>("GET", "/emails",
                AyInboxClient.WithCursor(Query(parameters), cursor), null, options, false, cancellationToken), cancellationToken);
    }

    /// <summary>Unibox threads.</summary>
    public sealed class UniboxResource
    {
        private readonly AyInboxClient _c;
        internal UniboxResource(AyInboxClient c) { _c = c; }

        private static List<KeyValuePair<string, string?>> Query(ThreadListParams? p)
        {
            var q = Q.Of(("cursor", p?.Cursor), ("limit", p?.Limit), ("folder", p?.Folder), ("unseen", p?.Unseen),
                ("campaign_id", p?.CampaignId), ("contact_id", p?.ContactId), ("email_id", p?.EmailId), ("subject", p?.Subject));
            if (p?.Extra != null)
                foreach (var kv in p.Extra) q.Add(new KeyValuePair<string, string?>(kv.Key, kv.Value));
            return q;
        }

        private static List<KeyValuePair<string, string?>> ThreadQuery(string threadId, ThreadGetParams? p)
        {
            if (string.IsNullOrEmpty(threadId)) throw new ArgumentException("A thread id is required.", nameof(threadId));
            return Q.Of(("thread_id", threadId), ("email_id", p?.EmailId), ("limit", p?.Limit), ("cursor", p?.Cursor));
        }

        /// <summary><c>GET /unibox</c>: one row per conversation. Needs READ_UNIBOX.</summary>
        public Task<Page<MessagePreview>> ListThreadsAsync(ThreadListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetPageAsync<MessagePreview>("GET", "/unibox", Query(parameters), null, options, false, cancellationToken);

        /// <summary>Every conversation, following cursors.</summary>
        public IAsyncEnumerable<MessagePreview> ListAllThreadsAsync(ThreadListParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            AyInboxClient.WalkAsync(cursor => _c.GetPageAsync<MessagePreview>("GET", "/unibox",
                AyInboxClient.WithCursor(Query(parameters), cursor), null, options, false, cancellationToken), cancellationToken);

        /// <summary><c>GET /unibox/thread</c>: one page of a thread's message previews. Needs READ_UNIBOX.</summary>
        public Task<Page<MessagePreview>> GetThreadAsync(string threadId, ThreadGetParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default) =>
            _c.GetPageAsync<MessagePreview>("GET", "/unibox/thread", ThreadQuery(threadId, parameters), null, options, false, cancellationToken);

        /// <summary>Every message preview in a thread, following cursors.</summary>
        public IAsyncEnumerable<MessagePreview> GetThreadAllAsync(string threadId, ThreadGetParams? parameters = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
        {
            var q = ThreadQuery(threadId, parameters);
            return AyInboxClient.WalkAsync(cursor => _c.GetPageAsync<MessagePreview>("GET", "/unibox/thread",
                AyInboxClient.WithCursor(q, cursor), null, options, false, cancellationToken), cancellationToken);
        }
    }
}
