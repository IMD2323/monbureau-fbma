using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MonBureau.Core.Entities;
using MonBureau.Core.Interfaces;
using MonBureau.Core.Services;
using MonBureau.Infrastructure.Data;
using MonBureau.Infrastructure.Repositories;
using MonBureau.Infrastructure.Services;
using MonBureau.Infrastructure.Services.Firebase;
using MonBureau.UI.Services;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;
        private readonly string _logPath;
        private AutoBackupService? _autoBackupService;

        public App()
        {
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonBureau", "Logs");
            Directory.CreateDirectory(_logPath);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Setup global exception handlers
            SetupExceptionHandlers();

            // ✅ INITIALIZE FIREBASE FIRST
            InitializeFirebase();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            InitializeDatabase();
            InitializeAutoBackup();
        }

        /// <summary>
        /// ✅ NEW: Initialize Firebase before everything else
        /// </summary>
        private void InitializeFirebase()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App] Initializing Firebase...");

                // Chemin vers le fichier de credentials
                var credentialsPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "monbureau-licenses-firebase-adminsdk.json"
                );

                System.Diagnostics.Debug.WriteLine($"[App] Looking for credentials at: {credentialsPath}");

                if (!File.Exists(credentialsPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[App] ⚠️ Firebase credentials not found!");
                    System.Diagnostics.Debug.WriteLine($"[App] Expected path: {credentialsPath}");
                    System.Diagnostics.Debug.WriteLine($"[App] App will run in OFFLINE MODE");

                    MessageBox.Show(
                        "Fichier de configuration Firebase manquant.\n\n" +
                        $"Chemin attendu: {credentialsPath}\n\n" +
                        "L'application fonctionnera en mode dégradé.\n" +
                        "La validation des licences sera limitée.",
                        "Configuration Firebase manquante",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[App] Credentials file found, initializing...");

                // Initialiser Firebase
                FirebaseConfig.Initialize(credentialsPath);

                System.Diagnostics.Debug.WriteLine("[App] ✅ Firebase initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] ❌ Firebase initialization failed: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[App] Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] StackTrace: {ex.StackTrace}");

                LogError(ex);

                MessageBox.Show(
                    $"Erreur d'initialisation Firebase:\n\n{ex.Message}\n\n" +
                    "L'application fonctionnera en mode dégradé.\n" +
                    "La validation des licences sera limitée.",
                    "Erreur Firebase",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void SetupExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            ShowErrorDialog("Une erreur s'est produite", e.Exception);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogError(ex);
                ShowErrorDialog("Une erreur critique s'est produite", ex);
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError(e.Exception);
            e.SetObserved();
        }

        private void LogError(Exception? ex)
        {
            if (ex == null) return;

            try
            {
                var logFile = Path.Combine(_logPath, $"error_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"""
                    
                    ==========================================
                    Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Exception: {ex.GetType().FullName}
                    Message: {ex.Message}
                    StackTrace:
                    {ex.StackTrace}
                    {(ex.InnerException != null ? $"Inner Exception: {ex.InnerException.Message}" : "")}
                    ==========================================
                    
                    """;

                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // Swallow logging errors
            }
        }

        private void ShowErrorDialog(string message, Exception? ex = null)
        {
            var details = ex != null ? $"\n\nDétails: {ex.Message}" : "";
            MessageBox.Show(
                $"{message}{details}\n\nLes détails de l'erreur ont été enregistrés dans les logs.",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void ConfigureServices(ServiceCollection services)
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonBureau",
                "monbureau.db"
            );

            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // ========================================
            // Database
            // ========================================
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging(false);
                options.EnableDetailedErrors(true);
            });

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }, ServiceLifetime.Scoped);

            // ========================================
            // Repositories
            // ========================================
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // ========================================
            // Infrastructure Services - Singleton
            // ========================================
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
                return new BackupService(dbPath, factory);
            });

            services.AddSingleton<DpapiService>();
            services.AddSingleton<DeviceIdentifier>();

            // ✅ UPDATED: Firebase License Service
            services.AddSingleton<FirestoreLicenseService>(sp =>
            {
                return new FirestoreLicenseService("monbureau-licenses");
            });

            services.AddSingleton<SecureLicenseStorage>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<CacheService>();
            services.AddSingleton<AutoBackupService>();

            // ========================================
            // Business Services - Scoped
            // ========================================
            services.AddScoped<ICaseService, CaseService>();

            // ========================================
            // ViewModels - Transient
            // ========================================
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ClientsViewModel>();
            services.AddTransient<CasesViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DocumentsViewModel>();
        }

        private void InitializeDatabase()
        {
            try
            {
                using var scope = _serviceProvider?.CreateScope();
                if (scope != null)
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    context.Database.EnsureCreated();
                    SeedDatabaseIfEmpty(context);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show($"Erreur d'initialisation de la base de données: {ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void InitializeAutoBackup()
        {
            try
            {
                _autoBackupService = _serviceProvider?.GetService<AutoBackupService>();

                if (_autoBackupService != null)
                {
                    var settings = _autoBackupService.GetSettings();
                    System.Diagnostics.Debug.WriteLine(
                        $"Auto-backup initialized. Enabled: {settings.Enabled}, " +
                        $"Interval: {settings.IntervalType}, " +
                        $"Last backup: {settings.LastBackupDate:yyyy-MM-dd HH:mm}");
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                System.Diagnostics.Debug.WriteLine($"Failed to initialize auto-backup: {ex.Message}");
            }
        }

        private void SeedDatabaseIfEmpty(AppDbContext context)
        {
            if (!context.Clients.Any())
            {
                var client1 = new Client
                {
                    FirstName = "Ahmed",
                    LastName = "Benali",
                    ContactEmail = "ahmed.benali@email.com",
                    ContactPhone = "0555123456",
                    Address = "Rue de la République, El Oued",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var client2 = new Client
                {
                    FirstName = "Fatima",
                    LastName = "Mansour",
                    ContactEmail = "f.mansour@email.com",
                    ContactPhone = "0666789012",
                    Address = "Boulevard du 1er Novembre, El Oued",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Clients.AddRange(client1, client2);
                context.SaveChanges();

                var case1 = new Case
                {
                    Number = "DOSS-2025-0001",
                    Title = "Contentieux Commercial",
                    Description = "Litige commercial avec un fournisseur",
                    Status = Core.Enums.CaseStatus.InProgress,
                    ClientId = client1.Id,
                    CourtName = "Tribunal d'El Oued",
                    CourtAddress = "Place de la Justice, El Oued",
                    OpeningDate = DateTime.Today.AddDays(-30),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var case2 = new Case
                {
                    Number = "DOSS-2025-0002",
                    Title = "Affaire Familiale",
                    Description = "Procédure de divorce à l'amiable",
                    Status = Core.Enums.CaseStatus.Open,
                    ClientId = client2.Id,
                    CourtName = "Tribunal d'El Oued",
                    OpeningDate = DateTime.Today.AddDays(-15),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Cases.AddRange(case1, case2);
                context.SaveChanges();

                var doc1 = new CaseItem
                {
                    CaseId = case1.Id,
                    Type = Core.Enums.ItemType.Document,
                    Name = "Contrat Commercial",
                    Description = "Contrat signé avec le fournisseur",
                    Date = DateTime.Today.AddDays(-35),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var expense1 = new CaseItem
                {
                    CaseId = case1.Id,
                    Type = Core.Enums.ItemType.Expense,
                    Name = "Frais d'huissier",
                    Description = "Constatation des faits",
                    Amount = 15000,
                    Date = DateTime.Today.AddDays(-25),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.CaseItems.AddRange(doc1, expense1);
                context.SaveChanges();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _autoBackupService?.Dispose();
            _autoBackupService = null;

            if (_serviceProvider != null)
            {
                _serviceProvider.Dispose();
                _serviceProvider = null;
            }

            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._serviceProvider?.GetService<T>()
                ?? throw new InvalidOperationException($"Service {typeof(T)} not found");
        }
    }
}