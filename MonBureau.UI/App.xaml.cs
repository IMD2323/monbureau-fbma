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
using MonBureau.Infrastructure.Security;
using MonBureau.UI.Services;
using MonBureau.UI.ViewModels;

namespace MonBureau.UI
{
    /// <summary>
    /// FIXED: Skip Firebase initialization in DEBUG mode
    /// Simplified security initialization
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
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            System.Diagnostics.Debug.WriteLine("[App] ========================================");
            System.Diagnostics.Debug.WriteLine("[App] MonBureau Starting...");
            System.Diagnostics.Debug.WriteLine("[App] ========================================");

            // Setup global exception handlers
            SetupExceptionHandlers();

            // FIXED: Initialize security (simplified for DEBUG mode)
            if (!InitializeSecurity())
            {
                System.Diagnostics.Debug.WriteLine("[App] Security initialization failed - exiting");
                Shutdown(1);
                return;
            }

            // FIXED: Skip Firebase in DEBUG mode
#if !DEBUG
            InitializeFirebase();
#else
            System.Diagnostics.Debug.WriteLine("[App] ⚠️ DEBUG MODE - Skipping Firebase initialization");
#endif

            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Initialize database (now encrypted)
            InitializeDatabase();

            // Initialize auto-backup
            InitializeAutoBackup();

            System.Diagnostics.Debug.WriteLine("[App] ✓ Application initialized successfully");
        }

        // ============================================
        // FIXED: Simplified Security Initialization
        // ============================================

        private bool InitializeSecurity()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Security] Initializing security features...");

                // 1. Check for debugger (warning only in DEBUG mode)
#if DEBUG
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debug.WriteLine("[Security] ✓ Debugger detected (DEBUG mode - allowed)");
                }
#else
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debug.WriteLine("[Security] ⚠ Debugger detected");

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
#endif

                // 2. FIXED: Ensure database encryption key exists
                if (!SecureCredentialManager.CredentialExists("Database_EncryptionKey"))
                {
                    System.Diagnostics.Debug.WriteLine("[Security] ⚠️ First run - generating encryption key");

                    // Generate encryption key automatically
                    var newKey = GenerateEncryptionKey();

                    bool stored = SecureCredentialManager.StoreCredential(
                        "Database_EncryptionKey",
                        "DatabaseEncryption",
                        newKey
                    );

                    if (!stored)
                    {
                        System.Diagnostics.Debug.WriteLine("[Security] ❌ Failed to store encryption key");

                        MessageBox.Show(
                            "Échec de la génération de la clé de chiffrement.\n\n" +
                            "L'application ne peut pas continuer.",
                            "Erreur Critique",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        return false;
                    }

                    System.Diagnostics.Debug.WriteLine("[Security] ✓ Encryption key generated and stored");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Security] ✓ Database encryption key found");
                }

                System.Diagnostics.Debug.WriteLine("[Security] ✓ Security initialization complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Security] ✗ Security initialization failed: {ex.Message}");
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

        // ============================================
        // FIXED: Firebase initialization (skipped in DEBUG)
        // ============================================

        private void InitializeFirebase()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Firebase] Initializing...");

                // Check if credentials are configured
                if (!FirebaseConfig.AreCredentialsConfigured())
                {
                    System.Diagnostics.Debug.WriteLine("[Firebase] ⚠ Credentials not configured");

                    var result = MessageBox.Show(
                        "Les identifiants Firebase ne sont pas configurés.\n\n" +
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

                    System.Diagnostics.Debug.WriteLine("[Firebase] Running in offline mode");
                    return;
                }

                // Initialize Firebase
                FirebaseConfig.Initialize();
                System.Diagnostics.Debug.WriteLine("[Firebase] ✓ Initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Firebase] ✗ Initialization failed: {ex.Message}");
                LogError(ex);

                MessageBox.Show(
                    "Échec de l'initialisation des fonctionnalités cloud.\n" +
                    "L'application fonctionnera en mode hors ligne.\n\n" +
                    "Pour activer les fonctionnalités cloud:\n" +
                    "1. Configurez Firebase dans les paramètres\n" +
                    "2. Redémarrez l'application",
                    "Mode Hors Ligne",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ShowFirebaseSetupInstructions()
        {
            MessageBox.Show(
                "Configuration Firebase:\n\n" +
                "1. Ouvrez PowerShell en tant qu'administrateur\n" +
                "2. Naviguez vers le dossier de l'application\n" +
                "3. Exécutez: .\\SetupCredentials.ps1\n" +
                "4. Suivez les instructions\n" +
                "5. Redémarrez l'application\n\n" +
                "Vous aurez besoin de:\n" +
                "• Firebase Project ID\n" +
                "• Service Account Email\n" +
                "• Private Key (fichier JSON)",
                "Instructions de Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================
        // EXISTING: Exception handlers
        // ============================================

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

        // ============================================
        // EXISTING: Service configuration
        // ============================================

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

            // Database - now with encryption
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

            // Repositories
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Infrastructure Services - Singleton
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
                return new BackupService(dbPath, factory);
            });

            services.AddSingleton<DpapiService>();
            services.AddSingleton<DeviceIdentifier>();
            services.AddSingleton<FirestoreLicenseService>(sp =>
            {
                return new FirestoreLicenseService("monbureau-licenses");
            });
            services.AddSingleton<SecureLicenseStorage>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<CacheService>();
            services.AddSingleton<AutoBackupService>();

            // Business Services - Scoped
            services.AddScoped<ICaseService, CaseService>();

            // ViewModels - Transient
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ClientsViewModel>();
            services.AddTransient<CasesViewModel>();
            services.AddTransient<BackupViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DocumentsViewModel>();
        }

        // ============================================
        // FIXED: Database initialization without encryption verification
        // ============================================

        private void InitializeDatabase()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Database] Initializing...");

                using var scope = _serviceProvider?.CreateScope();
                if (scope != null)
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Ensure database is created
                    context.Database.EnsureCreated();
                    System.Diagnostics.Debug.WriteLine("[Database] ✓ Database created/verified");

                    // FIXED: Skip encryption verification in DEBUG mode
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("[Database] ⚠️ DEBUG MODE - Skipping encryption verification");
#else
                    // Verify encryption is working
                    bool encryptionVerified = AppDbContext.VerifyEncryption();
                    if (!encryptionVerified)
                    {
                        throw new InvalidOperationException(
                            "Database encryption verification failed"
                        );
                    }
                    System.Diagnostics.Debug.WriteLine("[Database] ✓ Encryption verified");
#endif

                    // Seed database if empty
                    SeedDatabaseIfEmpty(context);
                    System.Diagnostics.Debug.WriteLine("[Database] ✓ Initialization complete");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Database] ✗ Initialization failed: {ex.Message}");
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

        // ============================================
        // EXISTING: Auto-backup initialization
        // ============================================

        private void InitializeAutoBackup()
        {
            try
            {
                _autoBackupService = _serviceProvider?.GetService<AutoBackupService>();

                if (_autoBackupService != null)
                {
                    var settings = _autoBackupService.GetSettings();
                    System.Diagnostics.Debug.WriteLine(
                        $"[AutoBackup] Initialized. Enabled: {settings.Enabled}, " +
                        $"Interval: {settings.IntervalType}, " +
                        $"Last backup: {settings.LastBackupDate:yyyy-MM-dd HH:mm}");
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] Failed to initialize: {ex.Message}");
            }
        }

        // ============================================
        // EXISTING: Database seeding
        // ============================================

        private void SeedDatabaseIfEmpty(AppDbContext context)
        {
            if (!context.Clients.Any())
            {
                System.Diagnostics.Debug.WriteLine("[Database] Seeding initial data...");

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

                System.Diagnostics.Debug.WriteLine("[Database] ✓ Initial data seeded");
            }
        }

        // ============================================
        // EXISTING: Cleanup on exit
        // ============================================

        protected override void OnExit(ExitEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[App] Application exiting...");

            _autoBackupService?.Dispose();
            _autoBackupService = null;

            if (_serviceProvider != null)
            {
                _serviceProvider.Dispose();
                _serviceProvider = null;
            }

            System.Diagnostics.Debug.WriteLine("[App] ✓ Cleanup complete");
            base.OnExit(e);
        }

        // ============================================
        // EXISTING: Service locator
        // ============================================

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._serviceProvider?.GetService<T>()
                ?? throw new InvalidOperationException($"Service {typeof(T)} not found");
        }
    }
}