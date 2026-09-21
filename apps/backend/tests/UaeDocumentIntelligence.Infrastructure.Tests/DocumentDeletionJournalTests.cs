using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class DocumentDeletionJournalTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "p6-delete-journal-" + Guid.NewGuid().ToString("N"));
    private readonly Guid journalId = Guid.NewGuid();
    private DocumentDeletionOptions OptionsValue => new() { Enabled = true, JournalId = journalId, JournalDirectory = Path.Combine(root, "journal") };
    private StorageOptions Storage => new() { SecurityMode = "Enforced", UploadDirectory = Path.Combine(root, "uploads"), QuarantineDirectory = Path.Combine(root, "quarantine") };
    private DocumentDeletionJournal Journal() { DocumentDeletionJournal.Initialize(OptionsValue.JournalDirectory, journalId); return new(Options.Create(OptionsValue), Options.Create(Storage)); }
    private static DeletionIntent Intent()
    {
        var org = Guid.NewGuid(); var doc = Guid.NewGuid();
        return new(Guid.NewGuid(), org, doc, $"organizations/{org:N}/documents/{doc:N}/source.txt",
            Convert.ToHexString(SHA256.HashData("source"u8.ToArray())).ToLowerInvariant(), 6, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
    }
    [Fact]
    public void Journal_is_append_only_and_rejects_conflicting_retry()
    {
        var journal = Journal(); var intent = Intent(); journal.Append(intent); journal.Append(intent);
        Assert.Equal(intent, Assert.Single(journal.ReadAll()));
        Assert.Throws<InvalidOperationException>(() => journal.Append(intent with { PolicyVersion = Guid.NewGuid() }));
        Assert.Throws<InvalidOperationException>(() => DocumentDeletionJournal.Initialize(OptionsValue.JournalDirectory, journalId));
    }
    [Theory]
    [InlineData("disabled")]
    [InlineData("identity")]
    [InlineData("inside-uploads")]
    [InlineData("locked-storage")]
    public void Deletion_requires_independent_pinned_journal(string scenario)
    {
        _ = Journal(); var options = OptionsValue; var storage = Storage;
        if (scenario == "disabled") options.Enabled = false;
        if (scenario == "identity") options.JournalId = Guid.NewGuid();
        if (scenario == "inside-uploads") storage.UploadDirectory = root;
        if (scenario == "locked-storage") storage.SecurityMode = "Locked";
        Assert.Throws<InvalidOperationException>(() => new DocumentDeletionJournal(Options.Create(options), Options.Create(storage)).RequireRoot());
    }
    [Theory]
    [InlineData("malformed")]
    [InlineData("foreign-path")]
    [InlineData("oversize")]
    public void Untrusted_journal_entries_fail_closed(string scenario)
    {
        var journal = Journal(); var intent = Intent();
        if (scenario == "foreign-path") { Assert.Throws<InvalidDataException>(() => journal.Append(intent with { StorageKey = "../source.txt" })); return; }
        File.WriteAllText(Path.Combine(OptionsValue.JournalDirectory, "unknown.json"), scenario == "oversize" ? new string('x', 4097) : "{}");
        Assert.ThrowsAny<Exception>(() => journal.ReadAll());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_removal_requires_matching_bytes_and_never_deletes_a_changed_file(bool changed)
    {
        var journal = Journal(); var intent = Intent(); journal.Append(intent); var path = journal.SourcePath(intent);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, changed ? "CHANGED" : "source");
        if (changed) { await Assert.ThrowsAsync<InvalidDataException>(() => journal.RemoveSourceAsync(intent, default)); Assert.True(File.Exists(path)); }
        else { await journal.RemoveSourceAsync(intent, default); await journal.RemoveSourceAsync(intent, default); Assert.False(File.Exists(path)); }
        Assert.Single(journal.ReadAll());
    }
    public void Dispose()
    {
        var target = Path.GetFullPath(root);
        if (!target.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("p6-delete-journal-")) throw new InvalidOperationException();
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}
