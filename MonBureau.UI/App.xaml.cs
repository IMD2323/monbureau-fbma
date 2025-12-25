using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
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
using MonBureau.Infrastructure.Security;
using MonBureau.UI.Services;
using MonBureau.UI.ViewModels;
using MonBureau.UI.Features;
using MonBureau.UI.Features.Clients;
using MonBureau.UI.Features.Cases;
using MonBureau.UI.Features.Documents;
using MonBureau.UI.Features.Expenses;
using MonBureau.UI.Features.Backup;
using MonBureau.UI.Features.Rdvs;


namespace MonBureau.UI
{
    /// <summary>
    /// FIXED: Localization initialized before any windows
    /// </summary>
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

            // ✅ CRITICAL FIX: Initialize localization BEFORE any UI elements
            InitializeLocalization();
        }

        /// <summary>
        /// Initializes localization and applies saved language
        /// </summary>
        private void InitializeLocalization()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App] Initializing localization...");

                // Initialize the localization service
                var localizationService = LocalizationService.Instance;

                // Get current language
                var currentLanguage = localizationService.CurrentLanguage;
                System.Diagnostics.Debug.WriteLine($"[App] Current language: {currentLanguage}");

                // Apply culture
                var culture = new CultureInfo(currentLanguage);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                // Set thread culture
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

                // Set language for XAML
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name)));

                System.Diagnostics.Debug.WriteLine($"[App] ✅ Localization initialized: {culture.Name}");
                System.Diagnostics.Debug.WriteLine($"[App] CurrentUICulture: {CultureInfo.CurrentUICulture.Name}");
                System.Diagnostics.Debug.WriteLine($"[App] RTL: {localizationService.IsRightToLeft}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] ❌ Localization initialization error: {ex.Message}");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            System.Diagnostics.Debug.WriteLine("[App] OnStartup called");

            // Setup global exception handlers
            SetupExceptionHandlers();

            // Initialize security
            if (!InitializeSecurity())
            {
                Shutdown(1);
                return;
            }

            // Initialize Firebase Web SDK
            InitializeFirebase();

            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Initialize database
            InitializeDatabase();

            // Initialize auto-backup
            InitializeAutoBackup();

            System.Diagnostics.Debug.WriteLine("[App] ✅ Startup complete");
        }

        // [Rest of the App.xaml.cs code remains the same...]
        // [Include all other methods: InitializeSecurity, InitializeFirebase, ConfigureServices, etc.]

        private bool InitializeSecurity()
        {
            try
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var result = MessageBox.Show(
                        "Un débogueur a été détecté. Cela peut affecter la sécurité de l'application.\n\n" +
                        "Continuer quand même?",
                        "Débogueur Détecté",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return false;
                    }
                }

                if (!SecureCredentialManager.CredentialExists("Database_EncryptionKey"))
                {
                    var newKey = GenerateEncryptionKey();

                    bool stored = SecureCredentialManager.StoreCredential(
                        "Database_EncryptionKey",
                        "DatabaseEncryption",
                        newKey
                    );

                    if (!stored)
                    {
                        MessageBox.Show(
                            "Échec de la génération de la clé de chiffrement.\n\n" +
                            "L'application ne peut pas continuer.",
                            "Erreur Critique",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex);

                MessageBox.Show(
                    $"Échec de l'initialisation de la sécurité:\n\n{ex.Message}\n\n" +
                    "L'application ne peut pas continuer.",
                    "Erreur Critique",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        private static string GenerateEncryptionKey()
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                byte[] keyBytes = new byte[32];
                rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes);
            }
        }

        private void InitializeFirebase()
        {
            try
            {
                if (!FirebaseConfig.AreCredentialsConfigured())
                {
                    var result = MessageBox.Show(
                        "⚠️ Firebase non configuré\n\n" +
                        "Les identifiants Firebase Web SDK ne sont pas configurés.\n\n" +
                        "Fonctionnalités affectées:\n" +
                        "• Validation de licence en ligne\n" +
                        "• Synchronisation cloud\n\n" +
                        "L'application fonctionnera en mode hors ligne.\n\n" +
                        "Voulez-vous configurer Firebase maintenant?\n" +
                        "(Vous pouvez le faire plus tard dans les paramètres)",
                        "Configuration Firebase",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        ShowFirebaseSetupInstructions();
                    }

                    return;
                }

                bool initialized = FirebaseConfig.Initialize();

                if (!initialized)
                {
                    MessageBox.Show(
                        "Échec de l'initialisation Firebase.\n" +
                        "L'application fonctionnera en mode hors ligne.\n\n" +
                        $"Erreur: {FirebaseConfig.InitializationError}",
                        "Mode Hors Ligne",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);

                MessageBox.Show(
                    "Échec de l'initialisation des fonctionnalités cloud.\n" +
                    "L'application fonctionnera en mode hors ligne.\n\n" +
                    "Pour activer les fonctionnalités cloud:\n" +
                    "1. Exécutez SetupCredentials.ps1\n" +
                    "2. Redémarrez l'application",
                    "Mode Hors Ligne",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ShowFirebaseSetupInstructions()
        {
            MessageBox.Show(
                "Configuration Firebase Web SDK:\n\n" +
                "1. Ouvrez PowerShell en tant qu'administrateur\n" +
                "2. Naviguez vers le dossier de l'application\n" +
                "3. Exécutez: .\\SetupCredentials.ps1\n" +
                "4. Suivez les instructions\n" +
                "5. Redémarrez l'application\n\n" +
                "Vous aurez besoin de:\n" +
                "• Firebase API Key (Console Firebase)\n" +
                "• Firebase Project ID\n" +
                "• (Optionnel) Database URL\n\n" +
                "Documentation: https://firebase.google.com/docs/web/setup",
                "Instructions de Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
                return new BackupService(dbPath, factory);
            });

            services.AddSingleton<DpapiService>();
            services.AddSingleton<DeviceIdentifier>();
            services.AddSingleton<FirestoreLicenseService>();
            services.AddSingleton<SecureLicenseStorage>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<CacheService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<AutoBackupService>();
         
            services.AddScoped<ICaseService, CaseService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ClientsViewModel>();
            services.AddTransient<CasesViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DocumentsViewModel>();
            services.AddTransient<ExpensesViewModel>();
            services.AddTransient<AppointmentsViewModel>();
            services.AddTransient<ExpenseDialogViewModel>();
            services.AddTransient<AppointmentDialogViewModel>();
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

                    bool encryptionVerified = AppDbContext.VerifyEncryption();
                    if (!encryptionVerified)
                    {
                        throw new InvalidOperationException(
                            "Database encryption verification failed"
                        );
                    }

                    SeedDatabaseIfEmpty(context);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);

                MessageBox.Show(
                    $"Erreur d'initialisation de la base de données:\n\n{ex.Message}\n\n" +
                    "Vérifiez que:\n" +
                    "• L'application a les permissions d'écriture\n" +
                    "• Le dossier LocalApplicationData\\MonBureau existe\n" +
                    "• La base de données n'est pas corrompue",
                    "Erreur Base de Données",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(1);
            }
        }

        private void InitializeAutoBackup()
        {
            try
            {
                _autoBackupService = _serviceProvider?.GetService<AutoBackupService>();
            }
            catch (Exception ex)
            {
                LogError(ex);
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

                var case1 = new Core.Entities.Case
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

                var case2 = new Core.Entities.Case
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