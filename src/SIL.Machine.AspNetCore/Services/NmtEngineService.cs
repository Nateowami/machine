﻿namespace SIL.Machine.AspNetCore.Services;

public static class NmtBuildStages
{
    public const string Preprocess = "preprocess";
    public const string Train = "train";
    public const string Postprocess = "postprocess";
}

public class NmtEngineService(
    IPlatformService platformService,
    IDistributedReaderWriterLockFactory lockFactory,
    IDataAccessContext dataAccessContext,
    IRepository<TranslationEngine> engines,
    IBuildJobService buildJobService,
    ILanguageTagService languageTagService,
    ClearMLMonitorService clearMLMonitorService
) : ITranslationEngineService
{
    private readonly IDistributedReaderWriterLockFactory _lockFactory = lockFactory;
    private readonly IPlatformService _platformService = platformService;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<TranslationEngine> _engines = engines;
    private readonly IBuildJobService _buildJobService = buildJobService;
    private readonly ILanguageTagService _languageTagService = languageTagService;
    private readonly ClearMLMonitorService _clearMLMonitorService = clearMLMonitorService;

    public TranslationEngineType Type => TranslationEngineType.Nmt;

    public async Task CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        await _engines.InsertAsync(
            new TranslationEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage
            },
            cancellationToken
        );
        await _buildJobService.CreateEngineAsync(
            new[] { BuildJobType.Cpu, BuildJobType.Gpu },
            engineId,
            engineName,
            cancellationToken
        );
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        await using (await @lock.WriterLockAsync(cancellationToken: cancellationToken))
        {
            await CancelBuildJobAsync(engineId, cancellationToken);

            await _engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
            await _buildJobService.DeleteEngineAsync(
                new[] { BuildJobType.Cpu, BuildJobType.Gpu },
                engineId,
                CancellationToken.None
            );
        }
        await _lockFactory.DeleteAsync(engineId, CancellationToken.None);
    }

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        string? buildOptions,
        IReadOnlyList<Corpus> corpora,
        CancellationToken cancellationToken = default
    )
    {
        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        await using (await @lock.WriterLockAsync(cancellationToken: cancellationToken))
        {
            // If there is a pending/running build, then no need to start a new one.
            if (await _buildJobService.IsEngineBuilding(engineId, cancellationToken))
                throw new InvalidOperationException("The engine is already building or in the process of canceling.");

            await _buildJobService.StartBuildJobAsync(
                BuildJobType.Cpu,
                TranslationEngineType.Nmt,
                engineId,
                buildId,
                NmtBuildStages.Preprocess,
                corpora,
                buildOptions,
                cancellationToken
            );
        }
    }

    public async Task CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        IDistributedReaderWriterLock @lock = await _lockFactory.CreateAsync(engineId, cancellationToken);
        await using (await @lock.WriterLockAsync(cancellationToken: cancellationToken))
        {
            if (!await CancelBuildJobAsync(engineId, cancellationToken))
                throw new InvalidOperationException("The engine is not currently building.");
        }
    }

    public Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task<WordGraph> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clearMLMonitorService.QueueSize);
    }

    public bool IsLanguageNativeToModel(string language, out string internalCode)
    {
        return _languageTagService.ConvertToFlores200Code(language, out internalCode);
    }

    private async Task<bool> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        (string? buildId, BuildJobState jobState) = await _buildJobService.CancelBuildJobAsync(
            engineId,
            cancellationToken
        );
        if (buildId is not null && jobState is BuildJobState.None)
            await _platformService.BuildCanceledAsync(buildId, CancellationToken.None);
        return buildId is not null;
    }
}
