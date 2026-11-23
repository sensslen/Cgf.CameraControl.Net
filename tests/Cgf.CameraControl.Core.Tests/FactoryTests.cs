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
        _factory.AddBuilder(new ThingBuilder<KnownThing>());

        Assert.Null(await Parse(new KnownThing { Instance = 7 }));

        Assert.Equal(7, _factory.Get(7)?.Instance);
        Assert.Null(_factory.Get(8));
    }

    [Fact]
    public async Task TheFirstBuilderToClaimTheEntryBuildsIt()
    {
        var other = new ThingBuilder<OtherThing>();
        var known = new ThingBuilder<KnownThing>();
        _factory.AddBuilder(other);
        _factory.AddBuilder(known);

        Assert.Null(await Parse(new KnownThing { Instance = 1 }));

        Assert.Equal(0, other.Built);
        Assert.Equal(1, known.Built);
    }

    [Fact]
    public async Task ReportsAnEntryNoBuilderClaims()
    {
        _factory.AddBuilder(new ThingBuilder<KnownThing>());

        var issue = await Parse(new OtherThing { Instance = 1 });

        Assert.NotNull(issue);
        Assert.Contains(OtherThing.TypeName, issue.Message, StringComparison.Ordinal);
        Assert.Empty(_factory.Instances);
    }

    [Fact]
    public async Task RefusesASecondEntryForTheSameInstance()
    {
        _factory.AddBuilder(new ThingBuilder<KnownThing>());
        await Parse(new KnownThing { Instance = 1 });

        var issue = await Parse(new KnownThing { Instance = 1 });

        Assert.NotNull(issue);
        Assert.Contains("already configured", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailingBuilderIsReportedAndTheNextEntryStillBuilds()
    {
        _factory.AddBuilder(new ThingBuilder<OtherThing> { Failure = "no route to host" });
        _factory.AddBuilder(new ThingBuilder<KnownThing>());

        var issue = await Parse(new OtherThing { Instance = 1 });

        Assert.Equal("no route to host", issue?.Message);
        Assert.Null(await Parse(new KnownThing { Instance = 2 }));
        Assert.NotNull(_factory.Get(2));
    }

    [Fact]
    public async Task DisposeDropsInstancesButKeepsBuildersSoAReloadWorks()
    {
        _factory.AddBuilder(new ThingBuilder<KnownThing>());
        await Parse(new KnownThing { Instance = 1 });
        var built = _factory.Get(1);

        await _factory.DisposeAsync();

        Assert.True(built!.Disposed);
        Assert.Null(_factory.Get(1));
        Assert.Null(await Parse(new KnownThing { Instance = 1 }));
        Assert.NotNull(_factory.Get(1));
    }

    [Fact]
    public async Task DisposeKeepsGoingWhenOneInstanceThrows()
    {
        _factory.AddBuilder(new ThingBuilder<KnownThing>());
        await Parse(new KnownThing { Instance = 1 });
        await Parse(new KnownThing { Instance = 2 });
        _factory.Get(1)!.ThrowOnDispose = true;

        await _factory.DisposeAsync();

        Assert.Empty(_factory.Instances);
        _logger.Received(1).Error("cams", Arg.Is<string>(m => m.StartsWith("disposing Thing failed")));
    }

    private Task<LoadIssue?> Parse(ThingConfiguration entry) =>
        _factory.ParseConfigAsync(entry, _logger, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AnEntryTheLoaderCouldNotReadIsNotBuiltOrReportedAgain()
    {
        _factory.AddBuilder(new ThingBuilder<KnownThing>());

        var issue = await _factory.ParseConfigAsync(
            new InvalidThingConfiguration(KnownThing.TypeName, "{}", "cams[0] is missing a numeric 'instance'"),
            _logger,
            TestContext.Current.CancellationToken);

        Assert.Null(issue);
        Assert.Empty(_factory.Instances);
    }

    private abstract record ThingConfiguration(string Type) : CameraConfiguration(Type)
    {
        public override IEnumerable<ValueProblem> Validate() => [];
    }

    private sealed record KnownThing() : ThingConfiguration(TypeName)
    {
        public const string TypeName = "known";
    }

    private sealed record OtherThing() : ThingConfiguration(TypeName)
    {
        public const string TypeName = "other";
    }

    private sealed record InvalidThingConfiguration(string Type, string Json, string Problem)
        : ThingConfiguration(Type), IInvalidConfiguration;

    private sealed class ThingFactory() : Factory<Thing, ThingConfiguration>("cams");

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

    private sealed class ThingBuilder<TRecord> : IBuilder<Thing, ThingConfiguration>
        where TRecord : ThingConfiguration
    {
        public string? Failure { get; init; }

        public int Built { get; private set; }

        public Task<Thing>? BuildAsync(ThingConfiguration entry, CancellationToken cancellationToken)
        {
            if (entry is not TRecord)
            {
                return null;
            }

            Built++;
            return Failure is null
                ? Task.FromResult(new Thing(entry.Instance))
                : Task.FromException<Thing>(new InvalidOperationException(Failure));
        }
    }
}
