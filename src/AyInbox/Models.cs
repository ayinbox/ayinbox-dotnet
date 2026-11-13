using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AyInbox
{
    /// <summary>Base of every response model: fields this library does not model land in <see cref="Extra"/>.</summary>
    public abstract class ApiObject
    {
        /// <summary>Response fields without a typed property.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>One page of a list response.</summary>
    /// <typeparam name="T">Row type.</typeparam>
    public sealed class Page<T>
    {
        /// <summary>Rows on this page.</summary>
        public IReadOnlyList<T> Data { get; private set; } = Array.Empty<T>();
        /// <summary>Opaque cursor for the next page, or null.</summary>
        public string? NextCursor { get; private set; }
        /// <summary>Whether another page exists.</summary>
        public bool HasMore { get; private set; }
        /// <summary>Total count, on the lists that send one.</summary>
        public long? Total { get; private set; }
        /// <summary>The whole response, including extras such as <c>counts</c> on contact search.</summary>
        public JsonElement Raw { get; private set; }

        internal static Page<T> FromJson(JsonElement root)
        {
            var page = new Page<T> { Raw = root };
            if (root.ValueKind != JsonValueKind.Object) return page;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                page.Data = JsonSerializer.Deserialize<List<T>>(data.GetRawText(), AyInboxClient.Json) ?? new List<T>();
            if (root.TryGetProperty("pagination", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                if (p.TryGetProperty("next_cursor", out var c) && c.ValueKind == JsonValueKind.String) page.NextCursor = c.GetString();
                if (p.TryGetProperty("has_more", out var h)) page.HasMore = h.ValueKind == JsonValueKind.True;
                if (p.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number) page.Total = t.GetInt64();
            }
            return page;
        }
    }

#pragma warning disable CS1591
    /// <summary>The caller's identity from <c>GET /me</c>.</summary>
    public sealed class Identity : ApiObject
    {
        [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("first_name")] public string FirstName { get; set; } = "";
        [JsonPropertyName("last_name")] public string LastName { get; set; } = "";
        [JsonPropertyName("organization_id")] public string? OrganizationId { get; set; }
        [JsonPropertyName("organization_name")] public string? OrganizationName { get; set; }
        /// <summary><c>api_key</c>, <c>oauth</c> or <c>jwt</c>.</summary>
        [JsonPropertyName("auth_type")] public string AuthType { get; set; } = "";
        /// <summary>Lowercased permission names.</summary>
        [JsonPropertyName("scopes")] public List<string> Scopes { get; set; } = new List<string>();
    }

    /// <summary>A sender on a campaign.</summary>
    public sealed class CampaignSender : ApiObject
    {
        [JsonPropertyName("email_account_id")] public string EmailAccountId { get; set; } = "";
        [JsonPropertyName("weight")] public int? Weight { get; set; }
        [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
        [JsonPropertyName("last_sent_at")] public DateTimeOffset? LastSentAt { get; set; }
    }

    /// <summary>A campaign.</summary>
    public sealed class Campaign : ApiObject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("user_id")] public string? UserId { get; set; }
        [JsonPropertyName("organization_id")] public string? OrganizationId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        /// <summary>Campaign status; treat unknown values as opaque.</summary>
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("stop_on_reply")] public bool StopOnReply { get; set; }
        [JsonPropertyName("stop_on_meeting")] public bool StopOnMeeting { get; set; }
        [JsonPropertyName("open_tracking")] public bool OpenTracking { get; set; }
        [JsonPropertyName("link_tracking")] public bool LinkTracking { get; set; }
        [JsonPropertyName("text_only")] public bool TextOnly { get; set; }
        [JsonPropertyName("unsubscribe_header")] public bool UnsubscribeHeader { get; set; }
        [JsonPropertyName("risky_emails")] public bool RiskyEmails { get; set; }
        [JsonPropertyName("daily_limit")] public int DailyLimit { get; set; }
        [JsonPropertyName("unsubscribe_mode")] public string? UnsubscribeMode { get; set; }
        [JsonPropertyName("cc")] public List<string>? Cc { get; set; }
        [JsonPropertyName("bcc")] public List<string>? Bcc { get; set; }
        [JsonPropertyName("start_date")] public DateTimeOffset? StartDate { get; set; }
        [JsonPropertyName("end_date")] public DateTimeOffset? EndDate { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
        [JsonPropertyName("effective_timezone")] public string? EffectiveTimezone { get; set; }
        [JsonPropertyName("days")] public int Days { get; set; }
        [JsonPropertyName("start_time")] public string? StartTime { get; set; }
        [JsonPropertyName("end_time")] public string? EndTime { get; set; }
        /// <summary>Seven arrays of <c>{start, end}</c> minute windows, Sunday first.</summary>
        [JsonPropertyName("schedule_windows")] public JsonElement ScheduleWindows { get; set; }
        [JsonPropertyName("email_tags")] public List<string>? EmailTags { get; set; }
        [JsonPropertyName("folders")] public List<string>? Folders { get; set; }
        [JsonPropertyName("sender_strategy")] public string? SenderStrategy { get; set; }
        [JsonPropertyName("rotation_mode")] public string? RotationMode { get; set; }
        [JsonPropertyName("senders")] public List<CampaignSender>? Senders { get; set; }
        [JsonPropertyName("max_new_leads_per_day")] public int MaxNewLeadsPerDay { get; set; }
        [JsonPropertyName("continuous")] public bool Continuous { get; set; }
        [JsonPropertyName("review_first_emails")] public bool ReviewFirstEmails { get; set; }
        [JsonPropertyName("entry_delay_minutes")] public int EntryDelayMinutes { get; set; }
        [JsonPropertyName("idle_since")] public DateTimeOffset? IdleSince { get; set; }
        [JsonPropertyName("guardrail_enabled")] public bool GuardrailEnabled { get; set; }
        [JsonPropertyName("guardrail_tripped_at")] public DateTimeOffset? GuardrailTrippedAt { get; set; }
        [JsonPropertyName("guardrail_reason")] public string? GuardrailReason { get; set; }
        [JsonPropertyName("tracking_domain")] public string? TrackingDomain { get; set; }
        [JsonPropertyName("last_status_change_at")] public DateTimeOffset? LastStatusChangeAt { get; set; }
        [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    }

    /// <summary>A step created inside <c>POST /campaigns</c>.</summary>
    public sealed class StepInput
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("subject")] public string? Subject { get; set; }
        [JsonPropertyName("body_plain")] public string? BodyPlain { get; set; }
        [JsonPropertyName("body_html")] public string? BodyHtml { get; set; }
        [JsonPropertyName("body_sync")] public bool? BodySync { get; set; }
        [JsonPropertyName("body_code")] public bool? BodyCode { get; set; }
        /// <summary>Days to wait after this step.</summary>
        [JsonPropertyName("wait_after")] public int? WaitAfter { get; set; }
        [JsonPropertyName("thread_reply")] public bool? ThreadReply { get; set; }
    }

    /// <summary>Body of <c>POST /campaigns</c>. Only <see cref="Name"/> is required; null fields are not sent.</summary>
    public sealed class CampaignCreate
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("stop_on_reply")] public bool? StopOnReply { get; set; }
        [JsonPropertyName("stop_on_meeting")] public bool? StopOnMeeting { get; set; }
        [JsonPropertyName("open_tracking")] public bool? OpenTracking { get; set; }
        [JsonPropertyName("link_tracking")] public bool? LinkTracking { get; set; }
        [JsonPropertyName("text_only")] public bool? TextOnly { get; set; }
        [JsonPropertyName("unsubscribe_header")] public bool? UnsubscribeHeader { get; set; }
        [JsonPropertyName("risky_emails")] public bool? RiskyEmails { get; set; }
        [JsonPropertyName("daily_limit")] public int? DailyLimit { get; set; }
        [JsonPropertyName("unsubscribe_mode")] public string? UnsubscribeMode { get; set; }
        [JsonPropertyName("cc")] public List<string>? Cc { get; set; }
        [JsonPropertyName("bcc")] public List<string>? Bcc { get; set; }
        [JsonPropertyName("start_date")] public DateTimeOffset? StartDate { get; set; }
        [JsonPropertyName("end_date")] public DateTimeOffset? EndDate { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
        [JsonPropertyName("days")] public int? Days { get; set; }
        [JsonPropertyName("start_time")] public string? StartTime { get; set; }
        [JsonPropertyName("end_time")] public string? EndTime { get; set; }
        [JsonPropertyName("schedule_windows")] public JsonElement? ScheduleWindows { get; set; }
        [JsonPropertyName("email_tag_ids")] public List<string>? EmailTagIds { get; set; }
        [JsonPropertyName("folder_ids")] public List<string>? FolderIds { get; set; }
        /// <summary><c>tags</c> or <c>explicit</c>.</summary>
        [JsonPropertyName("sender_strategy")] public string? SenderStrategy { get; set; }
        [JsonPropertyName("rotation_mode")] public string? RotationMode { get; set; }
        [JsonPropertyName("senders")] public List<CampaignSender>? Senders { get; set; }
        [JsonPropertyName("ramp_enabled")] public bool? RampEnabled { get; set; }
        [JsonPropertyName("ramp_start")] public int? RampStart { get; set; }
        [JsonPropertyName("ramp_increment")] public int? RampIncrement { get; set; }
        [JsonPropertyName("ramp_ceiling")] public int? RampCeiling { get; set; }
        [JsonPropertyName("esp_match_mode")] public string? EspMatchMode { get; set; }
        [JsonPropertyName("max_new_leads_per_day")] public int? MaxNewLeadsPerDay { get; set; }
        [JsonPropertyName("prioritize_new_leads")] public bool? PrioritizeNewLeads { get; set; }
        [JsonPropertyName("entry_delay_minutes")] public int? EntryDelayMinutes { get; set; }
        [JsonPropertyName("continuous")] public bool? Continuous { get; set; }
        [JsonPropertyName("review_first_emails")] public bool? ReviewFirstEmails { get; set; }
        [JsonPropertyName("tracking_domain")] public string? TrackingDomain { get; set; }
        [JsonPropertyName("utm_tracking")] public bool? UtmTracking { get; set; }
        [JsonPropertyName("utm_source")] public string? UtmSource { get; set; }
        [JsonPropertyName("utm_medium")] public string? UtmMedium { get; set; }
        [JsonPropertyName("utm_campaign")] public string? UtmCampaign { get; set; }
        /// <summary>Steps, created and connected in order.</summary>
        [JsonPropertyName("steps")] public List<StepInput>? Steps { get; set; }
        /// <summary>A/B variants of the first step; shape not fixed by the API reference.</summary>
        [JsonPropertyName("variants")] public JsonElement? Variants { get; set; }
        /// <summary>Advanced overrides; shape not fixed by the API reference.</summary>
        [JsonPropertyName("advanced_overrides")] public JsonElement? AdvancedOverrides { get; set; }
    }

    /// <summary>Answer of <c>POST /campaigns/{id}/stop</c>.</summary>
    public sealed class CampaignPauseResult : ApiObject
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "";
    }

    /// <summary>Query of <c>GET /campaigns</c>.</summary>
    public sealed class CampaignListParams
    {
        /// <summary>Name search.</summary>
        public string? Q { get; set; }
        /// <summary><c>draft</c>, <c>active</c>, <c>paused</c> or <c>completed</c>.</summary>
        public string? Status { get; set; }
        /// <summary>Folder uuid.</summary>
        public string? Folder { get; set; }
        public string? Cursor { get; set; }
        /// <summary>Page size; 1 to 100 recommended.</summary>
        public int? Limit { get; set; }
    }

    /// <summary>A reference to a campaign on a contact.</summary>
    public sealed class CampaignRef : ApiObject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }

    /// <summary>A contact label.</summary>
    public sealed class Category : ApiObject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("color")] public string? Color { get; set; }
    }

    /// <summary>A lead's hold in one campaign.</summary>
    public sealed class LeadHold : ApiObject
    {
        [JsonPropertyName("since")] public DateTimeOffset? Since { get; set; }
        [JsonPropertyName("until")] public DateTimeOffset? Until { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
        [JsonPropertyName("source")] public string? Source { get; set; }
    }

    /// <summary>A contact's progress in one campaign (contact search with one campaign id).</summary>
    public sealed class ContactCampaignProgress : ApiObject
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("sent")] public int Sent { get; set; }
        [JsonPropertyName("opened")] public int Opened { get; set; }
        [JsonPropertyName("machine_opened")] public int MachineOpened { get; set; }
        [JsonPropertyName("clicked")] public int Clicked { get; set; }
        [JsonPropertyName("replied")] public int Replied { get; set; }
        [JsonPropertyName("bounced")] public int Bounced { get; set; }
        [JsonPropertyName("last_activity_at")] public DateTimeOffset? LastActivityAt { get; set; }
        [JsonPropertyName("current_step")] public string? CurrentStep { get; set; }
        [JsonPropertyName("failure_reason")] public string? FailureReason { get; set; }
        [JsonPropertyName("sender")] public string? Sender { get; set; }
        [JsonPropertyName("hold")] public LeadHold? Hold { get; set; }
    }

    /// <summary>A contact (list rows, create and update responses).</summary>
    public class Contact : ApiObject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("custom_fields")] public Dictionary<string, string>? CustomFields { get; set; }
        [JsonPropertyName("subscribed")] public bool Subscribed { get; set; }
        [JsonPropertyName("campaigns")] public List<CampaignRef>? Campaigns { get; set; }
        [JsonPropertyName("categories")] public List<Category>? Categories { get; set; }
        /// <summary><c>valid</c>, <c>risky</c>, <c>invalid</c> or <c>unknown</c>.</summary>
        [JsonPropertyName("verification_status")] public string? VerificationStatus { get; set; }
        [JsonPropertyName("verification_reason")] public string? VerificationReason { get; set; }
        [JsonPropertyName("is_catch_all")] public bool IsCatchAll { get; set; }
        [JsonPropertyName("verification_checked_at")] public DateTimeOffset? VerificationCheckedAt { get; set; }
        [JsonPropertyName("verification_confidence")] public int VerificationConfidence { get; set; }
        [JsonPropertyName("mail_host")] public string? MailHost { get; set; }
        [JsonPropertyName("esp_provider")] public string? EspProvider { get; set; }
        [JsonPropertyName("consent_given_at")] public DateTimeOffset? ConsentGivenAt { get; set; }
        [JsonPropertyName("consent_source")] public string? ConsentSource { get; set; }
        /// <summary>Lead score; fields not fixed by the API reference.</summary>
        [JsonPropertyName("lead_score")] public JsonElement? LeadScore { get; set; }
        [JsonPropertyName("campaign_lead")] public ContactCampaignProgress? CampaignLead { get; set; }
        /// <summary>Outreach summary on search rows; fields not fixed by the API reference.</summary>
        [JsonPropertyName("outreach")] public JsonElement? Outreach { get; set; }
        [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    }

    /// <summary>A contact read with <c>GET /contacts/{id}</c>.</summary>
    public sealed class ContactDetail : Contact
    {
        [JsonPropertyName("engagement")] public JsonElement? Engagement { get; set; }
        [JsonPropertyName("suppression")] public JsonElement? Suppression { get; set; }
        [JsonPropertyName("verification")] public JsonElement? Verification { get; set; }
        [JsonPropertyName("source")] public string? Source { get; set; }
        [JsonPropertyName("source_detail")] public string? SourceDetail { get; set; }
        [JsonPropertyName("first_seen_at")] public DateTimeOffset? FirstSeenAt { get; set; }
        [JsonPropertyName("consent_country")] public string? ConsentCountry { get; set; }
        [JsonPropertyName("consent_required")] public bool ConsentRequired { get; set; }
    }

    /// <summary>One contact for <c>POST /contacts</c> (an upsert by email). Null fields are not sent.</summary>
    public sealed class ContactInput
    {
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        /// <summary>Campaign ids. Adding to an active campaign can send mail.</summary>
        [JsonPropertyName("campaigns")] public List<string>? Campaigns { get; set; }
        [JsonPropertyName("categories")] public List<string>? Categories { get; set; }
        [JsonPropertyName("segments")] public List<string>? Segments { get; set; }
        [JsonPropertyName("custom_fields")] public Dictionary<string, string>? CustomFields { get; set; }
        [JsonPropertyName("subscribed")] public bool? Subscribed { get; set; }
        [JsonPropertyName("verification_status")] public string? VerificationStatus { get; set; }
        [JsonPropertyName("verification_provider")] public string? VerificationProvider { get; set; }
    }

    /// <summary>Body of <c>PATCH /contacts/{id}</c>. Null fields are left unchanged.</summary>
    public sealed class ContactUpdate
    {
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        /// <summary>Replaces the custom fields.</summary>
        [JsonPropertyName("custom_fields")] public Dictionary<string, string>? CustomFields { get; set; }
        [JsonPropertyName("subscribed")] public bool? Subscribed { get; set; }
        /// <summary>Replaces the contact's campaign list.</summary>
        [JsonPropertyName("campaigns")] public List<string>? Campaigns { get; set; }
        /// <summary>Replaces the labels.</summary>
        [JsonPropertyName("categories")] public List<string>? Categories { get; set; }
        [JsonPropertyName("add_categories")] public List<string>? AddCategories { get; set; }
        [JsonPropertyName("remove_categories")] public List<string>? RemoveCategories { get; set; }
    }

    /// <summary>A custom field filter in a contact search.</summary>
    public sealed class CustomFieldFilter
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("value")] public string Value { get; set; } = "";
        /// <summary><c>equal</c>, <c>starts_with</c>, <c>ends_with</c> or <c>contains</c>.</summary>
        [JsonPropertyName("type")] public string Type { get; set; } = "equal";
    }

    /// <summary>Body of <c>POST /contacts/search</c>. Null fields are not sent; an empty search matches everything.</summary>
    public sealed class ContactSearch
    {
        [JsonPropertyName("query")] public string? Query { get; set; }
        [JsonPropertyName("custom_field_filters")] public List<CustomFieldFilter>? CustomFieldFilters { get; set; }
        [JsonPropertyName("campaign_ids")] public List<string>? CampaignIds { get; set; }
        [JsonPropertyName("lead_status")] public string? LeadStatus { get; set; }
        [JsonPropertyName("engagement")] public string? Engagement { get; set; }
        [JsonPropertyName("category_ids")] public List<string>? CategoryIds { get; set; }
        [JsonPropertyName("segment_ids")] public List<string>? SegmentIds { get; set; }
        [JsonPropertyName("min_campaigns")] public int? MinCampaigns { get; set; }
        [JsonPropertyName("max_campaigns")] public int? MaxCampaigns { get; set; }
        [JsonPropertyName("subscribed")] public bool? Subscribed { get; set; }
        [JsonPropertyName("verification_status")] public string? VerificationStatus { get; set; }
        [JsonPropertyName("mail_hosts")] public List<string>? MailHosts { get; set; }
        [JsonPropertyName("lead_grades")] public List<string>? LeadGrades { get; set; }
        [JsonPropertyName("created_after")] public DateTimeOffset? CreatedAfter { get; set; }
        [JsonPropertyName("created_before")] public DateTimeOffset? CreatedBefore { get; set; }
        [JsonPropertyName("updated_after")] public DateTimeOffset? UpdatedAfter { get; set; }
        [JsonPropertyName("updated_before")] public DateTimeOffset? UpdatedBefore { get; set; }
        [JsonPropertyName("sort_by")] public string? SortBy { get; set; }
        /// <summary>Ascending when true.</summary>
        [JsonPropertyName("reverse")] public bool? Reverse { get; set; }
    }

    /// <summary>Query of <c>POST /contacts/search</c>.</summary>
    public sealed class ContactListParams
    {
        public string? Cursor { get; set; }
        /// <summary>Page size; 1 to 100 recommended.</summary>
        public int? Limit { get; set; }
        /// <summary>One label uuid.</summary>
        public string? Category { get; set; }
    }

    /// <summary>A connected mailbox.</summary>
    public sealed class Mailbox : ApiObject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("user_id")] public string? UserId { get; set; }
        [JsonPropertyName("organization_id")] public string? OrganizationId { get; set; }
        [JsonPropertyName("worker_id")] public string? WorkerId { get; set; }
        [JsonPropertyName("email")] public string Email { get; set; } = "";
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("send_as_email")] public string? SendAsEmail { get; set; }
        /// <summary><c>gmail</c>, <c>outlook</c> or <c>smtp_imap</c>.</summary>
        [JsonPropertyName("provider")] public string? Provider { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("mail_host")] public string? MailHost { get; set; }
        [JsonPropertyName("auth_method")] public string? AuthMethod { get; set; }
        [JsonPropertyName("last_synced_at")] public DateTimeOffset? LastSyncedAt { get; set; }
        [JsonPropertyName("campaign_limit")] public int CampaignLimit { get; set; }
        /// <summary>Seconds between sends.</summary>
        [JsonPropertyName("min_wait_time")] public int MinWaitTime { get; set; }
        [JsonPropertyName("reply_to")] public string? ReplyTo { get; set; }
        [JsonPropertyName("tracking_domain")] public string? TrackingDomain { get; set; }
        [JsonPropertyName("auth_state")] public string? AuthState { get; set; }
        [JsonPropertyName("auth_spf")] public bool AuthSpf { get; set; }
        [JsonPropertyName("auth_dkim")] public bool AuthDkim { get; set; }
        [JsonPropertyName("auth_dmarc")] public bool AuthDmarc { get; set; }
        /// <summary>Set while warmup is on.</summary>
        [JsonPropertyName("warmup")] public DateTimeOffset? Warmup { get; set; }
        [JsonPropertyName("warmup_paused_at")] public DateTimeOffset? WarmupPausedAt { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
        [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    }

    /// <summary>Query of <c>GET /emails</c>.</summary>
    public sealed class MailboxListParams
    {
        public string? Q { get; set; }
        /// <summary>Tag uuid.</summary>
        public string? Tag { get; set; }
        public string? Cursor { get; set; }
        /// <summary>Page size; 1 to 100 recommended.</summary>
        public int? Limit { get; set; }
    }

    /// <summary>A message preview (unibox list and thread rows).</summary>
    public sealed class MessagePreview : ApiObject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        /// <summary>Mailbox id.</summary>
        [JsonPropertyName("email_id")] public string? EmailId { get; set; }
        /// <summary>Provider thread string (not a uuid).</summary>
        [JsonPropertyName("thread_id")] public string ThreadId { get; set; } = "";
        [JsonPropertyName("from_addr")] public List<string>? FromAddr { get; set; }
        [JsonPropertyName("to_addr")] public List<string>? ToAddr { get; set; }
        [JsonPropertyName("subject")] public string? Subject { get; set; }
        [JsonPropertyName("snippet")] public string? Snippet { get; set; }
        [JsonPropertyName("internal_date")] public DateTimeOffset? InternalDate { get; set; }
        [JsonPropertyName("seen")] public bool Seen { get; set; }
        [JsonPropertyName("message_count")] public int MessageCount { get; set; }
        [JsonPropertyName("has_unread")] public bool HasUnread { get; set; }
        [JsonPropertyName("labels")] public List<Category>? Labels { get; set; }
        [JsonPropertyName("answers_mailbox_id")] public string? AnswersMailboxId { get; set; }
        [JsonPropertyName("campaign_id")] public string? CampaignId { get; set; }
    }

    /// <summary>Query of <c>GET /unibox</c>. Other filters go in <see cref="Extra"/>.</summary>
    public sealed class ThreadListParams
    {
        public string? Cursor { get; set; }
        /// <summary>1 to 100, default 50.</summary>
        public int? Limit { get; set; }
        /// <summary><c>inbox</c>, <c>sent</c>, <c>drafts</c>, <c>archive</c>, <c>spam</c> or <c>trash</c>.</summary>
        public string? Folder { get; set; }
        public bool? Unseen { get; set; }
        public string? CampaignId { get; set; }
        public string? ContactId { get; set; }
        /// <summary>Mailbox id.</summary>
        public string? EmailId { get; set; }
        public string? Subject { get; set; }
        /// <summary>Any other query parameter from the API reference, for example <c>awaiting_reply</c>.</summary>
        public IDictionary<string, string>? Extra { get; set; }
    }

    /// <summary>Query of <c>GET /unibox/thread</c>.</summary>
    public sealed class ThreadGetParams
    {
        /// <summary>Mailbox id; omit to read across every mailbox.</summary>
        public string? EmailId { get; set; }
        /// <summary>1 to 500, default 500.</summary>
        public int? Limit { get; set; }
        public string? Cursor { get; set; }
    }
#pragma warning restore CS1591
}
