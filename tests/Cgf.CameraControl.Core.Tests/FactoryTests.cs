using System.Text.Json;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Logger;
using NSubstitute;

namespace Cgf.CameraControl.Core.Tests;

public class FactoryTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ThingFactory _factory = new();

    [Fact]
    public async Task BuildsAnInstanceAndKeysItByInstanceNumber()
    {
        _factory.AddBuilder(new ThingBuilder("known"));

        Assert.Null(await Parse(7, "known"));

        Assert.Equal(7, _factory.Get(7)?.Instance);
        Assert.Null(_factory.Get(8));
    }

    [Fact]
    public async Task MatchesTheTypeStringCaseInsensitively()
    {
        _factory.AddBuilder(new ThingBuilder("Websocket.PtzLanc"));

        Assert.Null(await Parse(1, "websocket.ptzlanc"));
        Assert.NotNull(_factory.Get(1));
    }

    [Fact]
    public async Task ReportsAnUnknownTypeAndListsWhatItDoesKnow()
    {
        _factory.AddBuilder(new ThingBuilder("Signalr.PtzLanc"));

        var issue = await Parse(1, "Cgf.PtzCamera");

        Assert.NotNull(issue);
        Assert.Contains("Cgf.PtzCamera", issue.Message, StringComparison.Ordinal);
        Assert.Contains("Signalr.PtzLanc", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesASecondEntryForTheSameInstance()
    {
        _factory.AddBuilder(new ThingBuilder("known"));
        await Parse(1, "known");

        var issue = await Parse(1, "known");

        Assert.NotNull(issue);
        Assert.Contains("already configured", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailingBuilderIsReportedAndTheNextEntryStillBuilds()
    {
        _factory.AddBuilder(new ThingBuilder("boom") { Failure = "no route to host" });
        _factory.AddBuilder(new ThingBuilder("fine"));

        var issue = await Parse(1, "boom");

        Assert.Equal("no route to host", issue?.Message);
        Assert.Null(await Parse(2, "fine"));
        Assert.NotNull(_factory.Get(2));
    }

    [Fact]
    public void TwoBuildersClaimingOneTypeIsAProgrammingError()
    {
        _factory.AddBuilder(new ThingBuilder("shared"));

        Assert.Throws<InvalidOperationException>(() => _factory.AddBuilder(new ThingBuilder("shared")));
    }

    [Fact]
    public async Task DisposeDropsInstancesButKeepsBuildersSoAReloadWorks()
    {
        _factory.AddBuilder(new ThingBuilder("known"));
        await Parse(1, "known");
        var built = _factory.Get(1);

        await _factory.DisposeAsync();

        Assert.True(built!.Disposed);
        Assert.Null(_factory.Get(1));
        Assert.Null(await Parse(1, "known"));
        Assert.NotNull(_factory.Get(1));
    }

    [Fact]
    public async Task DisposeKeepsGoingWhenOneInstanceThrows()
    {
        _factory.AddBuilder(new ThingBuilder("known"));
        await Parse(1, "known");
        await Parse(2, "known");
        _factory.Get(1)!.ThrowOnDispose = true;

        await _factory.DisposeAsync();

        Assert.Empty(_factory.Instances);
    }

    private Task<LoadIssue?> Parse(int instance, string type) =>
        _factory.ParseConfigAsync(Entry(instance, type), _logger, TestContext.Current.CancellationToken);

    private static ConfigEntry Entry(int instance, string type)
    {
        using var document = JsonDocument.Parse($$"""{ "instance": {{instance}}, "type": "{{type}}" }""");
        return new ConfigEntry(instance, type, document.RootElement.Clone());
    }

    private sealed class ThingFactory() : Factory<Thing>("cams");

    private sealed class Thing(int instance) : IAsyncDisposable
    {
        public int Instance => instance;

        public bool Disposed { get; private set; }

        public bool ThrowOnDispose { get; set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ThrowOnDispose ? ValueTask.FromException(new IOException("socket already gone")) : ValueTask.CompletedTask;
        }
    }

    private sealed class ThingBuilder(params string[] types) : IBuilder<Thing>
    {
        public string? Failure { get; init; }

        public IReadOnlyCollection<string> SupportedTypes => types;

        public Task<Thing> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken) =>
            Failure is null
                ? Task.FromResult(new Thing(entry.Instance))
                : Task.FromException<Thing>(new InvalidOperationException(Failure));
    }
}
