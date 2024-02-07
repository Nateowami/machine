﻿namespace SIL.Machine.AspNetCore.Models;

public class Corpus
{
    public string Id { get; set; } = default!;
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public bool TrainOnAll { get; set; }
    public bool PretranslateAll { get; set; }
    public Dictionary<string, HashSet<int>>? TrainOnChapters { get; set; }
    public Dictionary<string, HashSet<int>>? PretranslateChapters { get; set; }
    public HashSet<string> TrainOnTextIds { get; set; } = default!;
    public HashSet<string> PretranslateTextIds { get; set; } = default!;
    public List<CorpusFile> SourceFiles { get; set; } = default!;
    public List<CorpusFile> TargetFiles { get; set; } = default!;
}
