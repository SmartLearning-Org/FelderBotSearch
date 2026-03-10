using FelderBot.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FelderBot.Services;

public class InstructionsLoader : IInstructionsLoader
{
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<OpenAIOptions> _options;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "Instructions";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public InstructionsLoader(IWebHostEnvironment env, IOptions<OpenAIOptions> options, IMemoryCache cache)
    {
        _env = env;
        _options = options;
        _cache = cache;
    }

    public Task<string> GetInstructionsAsync(CancellationToken cancellationToken = default)
    {
        var path = _options.Value.InstructionsPath;
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult("");

        if (_cache.TryGetValue(CacheKey, out string? cached) && cached != null)
            return Task.FromResult(cached);

        var fullPath = Path.Combine(_env.ContentRootPath, path.TrimStart('/', '\\'));

        if (!File.Exists(fullPath))
            return Task.FromResult("Du er en venlig og hjælpsom assistent.");

        var content = File.ReadAllText(fullPath);
        _cache.Set(CacheKey, content, CacheDuration);
        return Task.FromResult(content);
    }
}
