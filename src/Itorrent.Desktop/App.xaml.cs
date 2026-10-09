using System.IO;
using System.Windows;
using System.Windows.Threading;
using Itorrent.Core.Engine;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;
using Itorrent.Desktop.Platform;
using Itorrent.Desktop.ViewModels;
using Itorrent.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Itorrent.Desktop;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Descartados em ExitAsync; a vida do App é a do processo.")]
public partial class App : Application
{
    private SingleInstance? _instance;
    private IHost? _host;
    private ITorrentService? _service;
    private TrayService? _tray;
    private MainWindow? _window;
    private bool _exiting;

    /// <summary>Dados, banco e logs em %LocalAppData%\Itorrent (nunca em Program Files).</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Itorrent");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var input = e.Args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        var startInTray = e.Args.Contains("--tray");

        // Instância única: se já há um Itorrent aberto, repassa o link e sai.
        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            SingleInstance.SendToPrimary(input ?? SingleInstance.ShowCommand);
            _instance.Dispose();
            Shutdown();
            return;
        }

        Directory.CreateDirectory(DataDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(DataDirectory, "logs", "itorrent-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
        Log.Information("Itorrent iniciando");

        DispatcherUnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Log.Error(ex.Exception, "Exceção de tarefa não observada");
            ex.SetObserved();
        };

        try
        {
            _host = BuildHost();
            _service = _host.Services.GetRequiredService<ITorrentService>();
            await _service.StartAsync();
            Log.Information("Motor iniciado");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Falha ao iniciar o motor");
            SmallDialog.Message(null, "Itorrent",
                "Não foi possível iniciar o Itorrent.\n\n" + ex.Message +
                "\n\nSe a porta estiver em uso, troque-a em Opções > Configurações.", "Warning");
            await ExitAsync();
            return;
        }

        var settings = _host.Services.GetRequiredService<ISettingsService>();
        _window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = _window;
        var vm = _window.ViewModel;

        _tray = new TrayService(
            open: () => _window.ShowFromTray(),
            pauseAll: () => _ = vm.PauseAllCommand.ExecuteAsync(null),
            resumeAll: () => _ = vm.ResumeAllCommand.ExecuteAsync(null),
            exit: () => _ = ExitAsync());

        _window.ExitRequested += (_, _) => _ = ExitAsync();

        // Encerramento automático: só com a janela fora de vista e nenhum diálogo aberto.
        vm.CanAutoExit = () =>
            (!_window.IsVisible || _window.WindowState == WindowState.Minimized)
            && Windows.OfType<Window>().All(w => w == _window || !w.IsVisible);
        vm.AutoExitRequested += (_, _) => _ = ExitAsync();
        _window.HiddenToTray += (_, _) =>
            _tray.Notify("Itorrent continua rodando", "Os downloads seguem em segundo plano. Use o ícone da bandeja para abrir ou sair.");

        _service.TorrentCompleted += (_, c) => Dispatcher.InvokeAsync(() =>
        {
            if (settings.Current.NotifyOnComplete)
                _tray?.Notify("Download concluído", c.Name);
        });

        var trayTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            if (_service is null || _exiting)
                return;
            var stats = _service.GetGlobalStats();
            _tray?.SetToolTip($"Itorrent\n↓ {Format.Rate(stats.DownloadRate)}  ↑ {Format.Rate(stats.UploadRate)}");
        }, Dispatcher);
        trayTimer.Start();

        _instance.Listen(message => Dispatcher.InvokeAsync(() =>
        {
            _window.ShowFromTray();
            if (message != SingleInstance.ShowCommand)
                vm.HandleExternalInput(message);
        }));

        vm.Start();
        if (!startInTray)
            _window.Show();
        if (input is not null)
        {
            _window.ShowFromTray();
            vm.HandleExternalInput(input);
        }
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        var s = builder.Services;
        s.AddSingleton(_ => new Database(Path.Combine(DataDirectory, "itorrent.db")));
        s.AddSingleton<ISettingsService, SettingsRepository>();
        s.AddSingleton<TorrentRepository>();
        s.AddSingleton<ITorrentService>(sp => new TorrentService(
            sp.GetRequiredService<ISettingsService>(), sp.GetRequiredService<TorrentRepository>(), DataDirectory));
        s.AddSingleton<IDialogs, Dialogs>();
        s.AddSingleton(sp => new MainViewModel(
            sp.GetRequiredService<ITorrentService>(), sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IDialogs>(), Dialogs.PickFolder));
        s.AddSingleton<MainWindow>();
        return builder.Build();
    }

    /// <summary>Só "Sair" encerra o processo, salvando o estado antes.</summary>
    private async Task ExitAsync()
    {
        if (_exiting)
            return;
        _exiting = true;
        Log.Information("Itorrent encerrando");
        try
        {
            if (_window is not null)
            {
                _window.ViewModel.Stop();
                _window.AllowClose = true;
                _window.Hide();
            }
            _tray?.Dispose();
            if (_service is not null)
                await _service.DisposeAsync();
            KeepAwake.Set(false);
            _host?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao encerrar");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
            _instance?.Dispose();
            Shutdown();
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exceção não tratada na interface");
        e.Handled = true;
        SmallDialog.Message(MainWindow, "Itorrent", "Ocorreu um erro inesperado:\n" + e.Exception.Message, "Warning");
    }
}
