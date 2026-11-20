using Microsoft.Extensions.Logging;
using SoulCore.Memory;

namespace SoulCore.Inference.Presence;

/// <summary>
/// PROP-18.7 residue writer: non-generic activities log + thoughts topic journal (depth law).
/// </summary>
public sealed class ActivityResidueWriter : IActivityResidueWriter
{
    private readonly IVictoriaActivitiesLogStore _activities;
    private readonly IVictoriaJournalStore _journals;
    private readonly ILogger<ActivityResidueWriter> _logger;

    public ActivityResidueWriter(
        IVictoriaActivitiesLogStore activities,
        IVictoriaJournalStore journals,
        ILogger<ActivityResidueWriter> logger)
    {
        _activities = activities ?? throw new ArgumentNullException(nameof(activities));
        _journals = journals ?? throw new ArgumentNullException(nameof(journals));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ActivityResidueWriteResult?> TryWriteAsync(
        string activity,
        string subject,
        string findings,
        string? occurredAt = null,
        CancellationToken cancellationToken = default)
    {
        if (!ActivityResidueComposer.IsAllowedActivity(activity))
        {
            _logger.LogDebug("Activity residue skipped — unknown activity kind {Activity}", activity);
            return null;
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            _logger.LogDebug("Activity residue skipped — empty subject");
            return null;
        }

        if (!ActivityResidueComposer.HasSubstance(findings))
        {
            _logger.LogDebug("Activity residue skipped — findings too thin for subject {Subject}", subject);
            return null;
        }

        var kind = ActivityResidueComposer.NormalizeActivity(activity);
        var topic = subject.Trim();

        string desc;
        string journalBody;
        try
        {
            desc = ActivityResidueComposer.BuildSessionDesc(topic, findings);
            journalBody = ActivityResidueComposer.BuildTopicJournalBody(topic, findings, kind);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity residue compose refused for subject {Subject}", topic);
            return null;
        }

        // Q4 depth law: never one-liner theater (reuse Composer split — PROP-18.29 F2).
        var paraCount = ActivityResidueComposer.CountParagraphs(journalBody);
        if (paraCount < ActivityResidueComposer.MinJournalParagraphs)
        {
            _logger.LogWarning(
                "Activity residue refused — journal below {Min}¶ floor (got {Count})",
                ActivityResidueComposer.MinJournalParagraphs,
                paraCount);
            return null;
        }

        var tagsJson = ActivityResidueComposer.BuildTopicTagsJson(topic, kind);
        long journalId;
        try
        {
            journalId = await _journals.WriteEntryAsync(
                    ActivityResidueComposer.TopicJournalBookId,
                    journalBody,
                    moodJson: null,
                    tagsJson: tagsJson,
                    source: "self",
                    occurredAt: occurredAt,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity residue journal append failed for {Subject}", topic);
            return null;
        }

        long entryNo;
        try
        {
            entryNo = await _activities.AppendAsync(
                    kind,
                    topic,
                    desc,
                    occurredAt: occurredAt,
                    journalBookId: ActivityResidueComposer.TopicJournalBookId,
                    journalEntryId: journalId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity residue log append failed for {Subject}", topic);
            return null;
        }

        var row = await _activities.GetAsync(entryNo, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            _logger.LogWarning("Activity residue wrote entry_no={EntryNo} but GetAsync returned null", entryNo);
            return null;
        }

        _logger.LogInformation(
            "Activity residue written activity={Activity} subject={Subject} entry={EntryNo} journal={JournalId}",
            kind,
            topic,
            entryNo,
            journalId);

        return new ActivityResidueWriteResult(
            ActivityEntryNo: entryNo,
            JournalEntryId: journalId,
            JournalBookId: ActivityResidueComposer.TopicJournalBookId,
            SessionDesc: desc,
            JournalBody: journalBody,
            LogEntry: row);
    }
}
