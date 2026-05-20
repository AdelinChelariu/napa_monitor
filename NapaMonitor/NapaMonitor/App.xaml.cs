using System.Windows;
using Microsoft.Extensions.Configuration;
using NapaMonitor.ViewModels;
using NapaMonitor.Views;

namespace NapaMonitor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = config["Database:ConnectionString"]!;
        var geminiApiKey = config["Groq:ApiKey"]!;

        var viewModel = new MainViewModel(connectionString, geminiApiKey);

        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }
}