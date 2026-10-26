// SPDX-License-Identifier: EUPL-1.2
using System.Text.Json.Nodes;
using Xfir.Mail;
using Xunit;

namespace Xfir.Tests;

public sealed class ReadFlagTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xfirmail-flags-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(SendResult.Sent, true)]
    [InlineData(SendResult.Retry, false)]
    [InlineData(SendResult.Rejected, false)]
    [InlineData(SendResult.Uncertain, false)]
    public async Task MarksReadOnlyAfterSuccessfulReplyAndDoesNotReapplyFlag(SendResult result, bool expected)
    {
        var settings = MailTests.Config(root);
        using var message = MailTests.Message();
        await using var inbox = new MailTests.FakeInbox(MailTests.Bytes(message));
        using var store = new RequestStore(settings.StatePath);
        var sender = new MailTests.FakeSender { Result = result };
        var worker = new MailWorker(settings, store, new MailTests.FakeConverter(), sender, _ => { });
        await worker.PollAsync(inbox, default);
        await worker.PollAsync(inbox, default);
        Assert.Equal(expected, Assert.Single(store.All()).ReadFlagApplied);
        Assert.Equal(expected ? new uint[] { 1 } : [], inbox.MarkedRead);
        Assert.Equal(1, sender.Calls);
    }

    [Fact]
    public async Task FailedFlagDoesNotBlockNewMailAndRestartRetriesOnlyFlagWithoutResending()
    {
        var settings = MailTests.Config(root);
        using var message = MailTests.Message();
        await using var inbox = new MailTests.FakeInbox(MailTests.Bytes(message)) { FailMarkRead = true };
        var sender = new MailTests.FakeSender();
        var converter = new MailTests.FakeConverter();
        using (var store = new RequestStore(settings.StatePath))
        {
            var worker = new MailWorker(settings, store, converter, sender, _ => { });
            await worker.PollAsync(inbox, default);
            using var second = MailTests.Message(); second.Subject = "Seconda richiesta sintetica";
            inbox.Add(MailTests.Bytes(second));
            await worker.PollAsync(inbox, default);
            Assert.Equal(2, sender.Calls);
            Assert.All(store.All(), r => { Assert.Equal(RequestStage.Sent, r.Stage); Assert.False(r.ReadFlagApplied); });
        }
        inbox.FailMarkRead = false;
        using (var reopened = new RequestStore(settings.StatePath))
        {
            reopened.Recover();
            await new MailWorker(settings, reopened, converter, sender, _ => { }).PollAsync(inbox, default);
            Assert.All(reopened.All(), r => { Assert.Equal(RequestStage.Sent, r.Stage); Assert.True(r.ReadFlagApplied); Assert.Equal(1, r.SendAttempts); });
            Assert.Equal(new uint[] { 1, 2 }, inbox.MarkedRead.Order());
            Assert.Equal(2, sender.Calls); Assert.Equal(2, converter.Calls); Assert.Equal(2, inbox.Reads);
        }
    }

    [Fact]
    public async Task OldSentRecordsAcquireFlagButOtherMailboxIdentityAndRejectedRecordsDoNot()
    {
        var settings = MailTests.Config(root);
        using var message = MailTests.Message();
        await using var inbox = new MailTests.FakeInbox(MailTests.Bytes(message));
        var sent = new RequestRecord(RequestStore.Hash(inbox.Scope + ":1"), inbox.Scope, 1, DateTimeOffset.UtcNow) { Stage = RequestStage.Sent };
        using var store = new RequestStore(settings.StatePath);
        store.Save(sent); store.Advance(inbox.Scope, 1);
        // Simulate an existing record written by the previous application version.
        var path = Path.Combine(settings.StatePath, "requests", sent.Id + ".json");
        var old = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); old.Remove("ReadFlagApplied");
        File.WriteAllText(path, old.ToJsonString());
        store.Save(new(RequestStore.Hash("other-mailbox"), RequestStore.Hash("other-scope"), 2, DateTimeOffset.UtcNow) { Stage = RequestStage.Sent });
        store.Save(new(RequestStore.Hash("rejected"), inbox.Scope, 3, DateTimeOffset.UtcNow) { Stage = RequestStage.Rejected });
        var sender = new MailTests.FakeSender();
        await new MailWorker(settings, store, new MailTests.FakeConverter(), sender, _ => { }).PollAsync(inbox, default);
        Assert.Equal(new uint[] { 1 }, inbox.MarkedRead);
        Assert.True(store.Find(sent.Id)!.ReadFlagApplied);
        Assert.Equal(0, sender.Calls); Assert.Equal(0, inbox.Reads);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
