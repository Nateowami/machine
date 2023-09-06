﻿namespace SIL.Machine.AspNetCore.Services;

public abstract class HangfireBuildJob : HangfireBuildJob<object?>
{
    protected HangfireBuildJob(
        IPlatformService platformService,
        IRepository<TranslationEngine> engines,
        IDistributedReaderWriterLockFactory lockFactory,
        IBuildJobService buildJobService,
        ILogger<HangfireBuildJob> logger
    )
        : base(platformService, engines, lockFactory, buildJobService, logger) { }

    public virtual Task RunAsync(string engineId, string buildId, CancellationToken cancellationToken)
    {
        return RunAsync(engineId, buildId, null, cancellationToken);
    }
}

public abstract class HangfireBuildJob<T>
{
    protected HangfireBuildJob(
        IPlatformService platformService,
        IRepository<TranslationEngine> engines,
        IDistributedReaderWriterLockFactory lockFactory,
        IBuildJobService buildJobService,
        ILogger<HangfireBuildJob<T>> logger
    )
    {
        PlatformService = platformService;
        Engines = engines;
        LockFactory = lockFactory;
        BuildJobService = buildJobService;
        Logger = logger;
    }

    protected IPlatformService PlatformService { get; }
    protected IRepository<TranslationEngine> Engines { get; }
    protected IDistributedReaderWriterLockFactory LockFactory { get; }
    protected IBuildJobService BuildJobService { get; }
    protected ILogger<HangfireBuildJob<T>> Logger { get; }

    public virtual async Task RunAsync(string engineId, string buildId, T data, CancellationToken cancellationToken)
    {
        IDistributedReaderWriterLock @lock = await LockFactory.CreateAsync(engineId, cancellationToken);
        JobCompletionStatus completionStatus = JobCompletionStatus.Completed;
        try
        {
            await InitializeAsync(engineId, buildId, data, @lock, cancellationToken);
            await using (await @lock.WriterLockAsync(cancellationToken: cancellationToken))
            {
                if (!await BuildJobService.BuildJobStartedAsync(engineId, buildId, cancellationToken))
                {
                    completionStatus = JobCompletionStatus.Canceled;
                    return;
                }
            }

            await DoWorkAsync(engineId, buildId, data, @lock, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Check if the cancellation was initiated by an API call or a shutdown.
            TranslationEngine? engine = await Engines.GetAsync(
                e => e.EngineId == engineId && e.CurrentBuild != null && e.CurrentBuild.BuildId == buildId,
                CancellationToken.None
            );
            if (engine?.CurrentBuild?.JobState is BuildJobState.Canceling)
            {
                completionStatus = JobCompletionStatus.Canceled;
                await using (await @lock.WriterLockAsync(cancellationToken: CancellationToken.None))
                {
                    await PlatformService.BuildCanceledAsync(buildId, CancellationToken.None);
                    await BuildJobService.BuildJobFinishedAsync(
                        engineId,
                        buildId,
                        buildComplete: false,
                        CancellationToken.None
                    );
                }
                Logger.LogInformation("Build canceled ({0})", buildId);
            }
            else if (engine is not null)
            {
                // the build was canceled, because of a server shutdown
                // switch state back to pending
                completionStatus = JobCompletionStatus.Restarting;
                await using (await @lock.WriterLockAsync(cancellationToken: CancellationToken.None))
                {
                    await PlatformService.BuildRestartingAsync(buildId, CancellationToken.None);
                    await BuildJobService.BuildJobRestartingAsync(engineId, buildId, CancellationToken.None);
                }
                throw;
            }
            else
            {
                completionStatus = JobCompletionStatus.Canceled;
            }
        }
        catch (Exception e)
        {
            completionStatus = JobCompletionStatus.Faulted;
            await using (await @lock.WriterLockAsync(cancellationToken: CancellationToken.None))
            {
                await PlatformService.BuildFaultedAsync(buildId, e.Message, CancellationToken.None);
                await BuildJobService.BuildJobFinishedAsync(
                    engineId,
                    buildId,
                    buildComplete: false,
                    CancellationToken.None
                );
            }
            Logger.LogError(0, e, "Build faulted ({0})", buildId);
            throw;
        }
        finally
        {
            await CleanupAsync(engineId, buildId, data, @lock, completionStatus);
        }
    }

    protected virtual Task InitializeAsync(
        string engineId,
        string buildId,
        T data,
        IDistributedReaderWriterLock @lock,
        CancellationToken cancellationToken
    )
    {
        return Task.CompletedTask;
    }

    protected abstract Task DoWorkAsync(
        string engineId,
        string buildId,
        T data,
        IDistributedReaderWriterLock @lock,
        CancellationToken cancellationToken
    );

    protected virtual Task CleanupAsync(
        string engineId,
        string buildId,
        T data,
        IDistributedReaderWriterLock @lock,
        JobCompletionStatus completionStatus
    )
    {
        return Task.CompletedTask;
    }

    protected enum JobCompletionStatus
    {
        Completed,
        Faulted,
        Canceled,
        Restarting
    }
}
