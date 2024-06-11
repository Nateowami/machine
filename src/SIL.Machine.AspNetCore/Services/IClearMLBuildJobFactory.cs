﻿namespace SIL.Machine.AspNetCore.Services;

public interface IClearMLBuildJobFactory
{
    TranslationEngineType EngineType { get; }

    Task<string> CreateJobScriptAsync(
        string engineId,
        string buildId,
        string modelType,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    );
}
