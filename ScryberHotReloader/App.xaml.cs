using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScryberHotReloader.Services;
using ScryberHotReloader.ViewModels;
using System;
using System.IO;
using System.Windows;

namespace ScryberHotReloader {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            // Build configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            // Setup dependency injection
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Show main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services) {
            // Register configuration
            services.AddSingleton(Configuration);

            // Register services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IExternalPackageService, ExternalPackageService>();
            services.AddSingleton<ICompilationService, CompilationService>();
            services.AddSingleton<IPdfService, PdfService>();

            // Register ViewModels
            services.AddSingleton<MainViewModel>();

            // Register MainWindow
            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e) {
            if (ServiceProvider is IDisposable disposable) {
                disposable.Dispose();
            }
            base.OnExit(e);
        }
    }

}
