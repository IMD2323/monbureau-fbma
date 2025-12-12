using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MonBureau.Infrastructure.Services;

namespace MonBureau.UI.ViewModels
{
    /// <summary>
    /// FIXED: Proper BackupInfoViewModel structure to prevent StackOverflowException
    /// </summary>
    public partial class BackupViewModel : ObservableObject, IDisposable
    {
        private readonly BackupService _backupService;
        private readonly AutoBackupService? _autoBackupService;
        private bool _disposed;

        [ObservableProperty]
        private ObservableCollection<BackupInfoViewModel> _backupHistory = new();

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _autoBackupEnabled;

        [ObservableProperty]
        private int _selectedInterval;

        [ObservableProperty]
        private int _maxBackupCount = 30;

        [ObservableProperty]
        private DateTime? _lastBackupDate;

        public BackupViewModel(BackupService backupService, AutoBackupService autoBackupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _autoBackupService = autoBackupService;

            System.Diagnostics.Debug.WriteLine("[BackupViewModel] Created");
        }

        public async Task InitializeAsync()
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[BackupViewModel] Already disposed, skipping initialization");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[BackupViewModel] Initializing...");

                await LoadAutoBackupSettingsAsync();
                await RefreshHistoryAsync();

                System.Diagnostics.Debug.WriteLine("[BackupViewModel] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] ❌ Initialization error: {ex.Message}");
                StatusMessage = $"Erreur d'initialisation: {ex.Message}";
            }
        }

        private async Task LoadAutoBackupSettingsAsync()
        {
            try
            {
                if (_autoBackupService == null)
                {
                    System.Diagnostics.Debug.WriteLine("[BackupViewModel] AutoBackupService not available");
                    return;
                }

                var settings = _autoBackupService.GetSettings();
                AutoBackupEnabled = settings.Enabled;
                SelectedInterval = (int)settings.IntervalType;
                MaxBackupCount = settings.MaxBackupCount;
                LastBackupDate = settings.LastBackupDate;

                var lastBackupInfo = await _autoBackupService.GetLastBackupInfoAsync();
                if (lastBackupInfo != null)
                {
                    LastBackupDate = lastBackupInfo.CreatedAt;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] Error loading settings: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CreateBackup()
        {
            if (_disposed) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Création de la sauvegarde...";

                var (success, message, filePath) = await _backupService.CreateBackupAsync();

                if (success)
                {
                    StatusMessage = message;
                    await RefreshHistoryAsync();

                    MessageBox.Show(
                        $"Sauvegarde créée avec succès!\n\n{message}",
                        "Succès",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Erreur lors de la sauvegarde:\n\n{message}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] CreateBackup error: {ex.Message}");
                MessageBox.Show(
                    $"Erreur: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
            }
        }

        [RelayCommand]
        private async Task RestoreBackup(string backupPath)
        {
            if (_disposed || string.IsNullOrEmpty(backupPath)) return;

            var result = MessageBox.Show(
                "⚠️ ATTENTION ⚠️\n\n" +
                "La restauration remplacera TOUTES les données actuelles.\n" +
                "Une sauvegarde pré-restauration sera créée automatiquement.\n\n" +
                "Êtes-vous sûr de vouloir continuer?",
                "Confirmer la Restauration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Restauration en cours...";

                var (success, message) = await _backupService.RestoreBackupAsync(backupPath);

                if (success)
                {
                    MessageBox.Show(
                        "Restauration réussie!\n\n" +
                        "L'application va redémarrer pour appliquer les changements.",
                        "Succès",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Restart application
                    System.Diagnostics.Process.Start(
                        Environment.ProcessPath ?? Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show(
                        $"Erreur lors de la restauration:\n\n{message}",
                        "Erreur",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] RestoreBackup error: {ex.Message}");
                MessageBox.Show(
                    $"Erreur: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
            }
        }

        [RelayCommand]
        private async Task SelectRestoreFile()
        {
            if (_disposed) return;

            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Sélectionner une sauvegarde",
                    Filter = "Fichiers de sauvegarde (*.zip)|*.zip|Tous les fichiers (*.*)|*.*",
                    InitialDirectory = _backupService.DefaultBackupPath
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    await RestoreBackup(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] SelectRestoreFile error: {ex.Message}");
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteBackup(string backupPath)
        {
            if (_disposed || string.IsNullOrEmpty(backupPath)) return;

            var result = MessageBox.Show(
                $"Supprimer cette sauvegarde?\n\n{Path.GetFileName(backupPath)}\n\n" +
                "Cette action est irréversible.",
                "Confirmer la Suppression",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                File.Delete(backupPath);
                await RefreshHistoryAsync();

                MessageBox.Show(
                    "Sauvegarde supprimée avec succès",
                    "Succès",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] DeleteBackup error: {ex.Message}");
                MessageBox.Show(
                    $"Erreur lors de la suppression: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RefreshHistory()
        {
            await RefreshHistoryAsync();
        }

        private async Task RefreshHistoryAsync()
        {
            if (_disposed) return;

            try
            {
                var backupFiles = await _backupService.GetBackupHistoryAsync();

                // Use Dispatcher for thread safety
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    BackupHistory.Clear();

                    foreach (var file in backupFiles.OrderByDescending(f => File.GetCreationTime(f)))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);

                            // Create backup info
                            var backupInfo = new BackupInfoViewModel
                            {
                                FilePath = file,
                                FileName = fileInfo.Name,
                                CreatedAt = fileInfo.CreationTime,
                                Size = fileInfo.Length,
                                ClientCount = 0,
                                CaseCount = 0,
                                ItemCount = 0
                            };

                            BackupHistory.Add(backupInfo);

                            // Load metadata asynchronously
                            _ = Task.Run(async () =>
                            {
                                var metadata = await _backupService.GetBackupMetadataAsync(file);

                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (metadata != null)
                                    {
                                        backupInfo.ClientCount = metadata.ClientCount;
                                        backupInfo.CaseCount = metadata.CaseCount;
                                        backupInfo.ItemCount = metadata.ItemCount;
                                    }
                                });
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[BackupViewModel] Error processing file {file}: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] RefreshHistory error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenBackupFolder()
        {
            if (_disposed) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _backupService.DefaultBackupPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] OpenBackupFolder error: {ex.Message}");
                MessageBox.Show(
                    $"Impossible d'ouvrir le dossier: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void SaveAutoBackupSettings()
        {
            if (_disposed || _autoBackupService == null) return;

            try
            {
                var settings = new BackupSettings
                {
                    Enabled = AutoBackupEnabled,
                    IntervalType = (BackupInterval)SelectedInterval,
                    MaxBackupCount = MaxBackupCount,
                    LastBackupDate = LastBackupDate ?? DateTime.UtcNow,
                    BackupPath = _backupService.DefaultBackupPath
                };

                _autoBackupService.UpdateSettings(settings);

                MessageBox.Show(
                    "Configuration enregistrée avec succès!",
                    "Succès",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupViewModel] SaveSettings error: {ex.Message}");
                MessageBox.Show(
                    $"Erreur: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            System.Diagnostics.Debug.WriteLine("[BackupViewModel] Disposing...");

            BackupHistory.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);

            System.Diagnostics.Debug.WriteLine("[BackupViewModel] ✅ Disposal complete");
        }
    }

    /// <summary>
    /// FIXED: Proper observable properties to prevent StackOverflowException
    /// </summary>
    public partial class BackupInfoViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private DateTime _createdAt;

        [ObservableProperty]
        private long _size;

        partial void OnSizeChanged(long value)
        {
            OnPropertyChanged(nameof(FormattedSize));
        }

        [ObservableProperty]
        private int _clientCount;

        [ObservableProperty]
        private int _caseCount;

        [ObservableProperty]
        private int _itemCount;

        /// <summary>
        /// Computed property - automatically updates when Size changes
        /// </summary>
        

        public string FormattedSize =>
            Size < 1024 ? $"{Size} B" :
            Size < 1024 * 1024 ? $"{Size / 1024.0:F2} KB" :
            $"{Size / (1024.0 * 1024.0):F2} MB";

    }
}