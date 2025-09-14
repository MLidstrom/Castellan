using Microsoft.Extensions.Hosting;

namespace Castellan.Worker.Services;

/// <summary>
/// Generic hosted service that executes a single startup task then completes.
/// </summary>
public class StartupImporterHostedService : IHostedService
{
    private readonly Func<IServiceProvider, CancellationToken, Task> _startupAction;

    public StartupImporterHostedService(Func<IServiceProvider, CancellationToken, Task> startupAction)
    {
        _startupAction = startupAction;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _startupAction.Invoke(ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // We need access to the root provider when StartAsync is called
    public IServiceProvider ServiceProvider { get; set; } = default!;
}
