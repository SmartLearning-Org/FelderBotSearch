namespace FelderBot.Services;

public interface IInstructionsLoader
{
    Task<string> GetInstructionsAsync(CancellationToken cancellationToken = default);
}
