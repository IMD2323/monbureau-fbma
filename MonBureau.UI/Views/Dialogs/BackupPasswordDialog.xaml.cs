using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonBureau.Infrastructure.Security;

namespace YourApp.Dialogs
{
    public partial class BackupPasswordDialog : Window
    {
        public string Password { get; private set; }
        public bool IsRestore { get; set; }

        public BackupPasswordDialog(bool isRestore = false)
        {
            InitializeComponent();
            IsRestore = isRestore;

            if (isRestore)
            {
                Title = "Restore Backup";
                this.Height = 220;
                ConfirmPasswordBox.Visibility = Visibility.Collapsed;
                Grid.SetRow(ConfirmPasswordBox, 4);

                var label = (Label)FindName("Label");
                if (label != null)
                {
                    Grid.SetRow(label, 4);
                    label.Visibility = Visibility.Collapsed;
                }
            }

            PasswordBox.Focus();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
            ValidatePasswords();
        }

        private void UpdatePasswordStrength()
        {
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(password))
            {
                StrengthBar.Value = 0;
                StrengthBar.Foreground = new SolidColorBrush(Colors.Gray);
                StrengthText.Text = "";
                return;
            }

            int strength = 0;
            bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                if (char.IsLower(c)) hasLower = true;
                if (char.IsDigit(c)) hasDigit = true;
                if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            // Length score (max 40 points)
            strength += Math.Min(password.Length * 4, 40);

            // Complexity score (max 60 points)
            if (hasUpper) strength += 15;
            if (hasLower) strength += 15;
            if (hasDigit) strength += 15;
            if (hasSpecial) strength += 15;

            StrengthBar.Value = strength;

            if (strength < 30)
            {
                StrengthBar.Foreground = new SolidColorBrush(Colors.Red);
                StrengthText.Text = "Weak password";
                StrengthText.Foreground = new SolidColorBrush(Colors.Red);
            }
            else if (strength < 60)
            {
                StrengthBar.Foreground = new SolidColorBrush(Colors.Orange);
                StrengthText.Text = "Moderate password";
                StrengthText.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else if (strength < 85)
            {
                StrengthBar.Foreground = new SolidColorBrush(Colors.YellowGreen);
                StrengthText.Text = "Good password";
                StrengthText.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                StrengthBar.Foreground = new SolidColorBrush(Colors.Green);
                StrengthText.Text = "Strong password";
                StrengthText.Foreground = new SolidColorBrush(Colors.Green);
            }
        }

        private void ValidatePasswords()
        {
            if (IsRestore)
            {
                // For restore, just check if password is not empty
                OkButton.IsEnabled = !string.IsNullOrEmpty(PasswordBox.Password);
            }
            else
            {
                // For backup, validate strength and match
                string password = PasswordBox.Password;
                string confirm = ConfirmPasswordBox.Password;

                bool isValid = !string.IsNullOrEmpty(password) &&
                              password.Length >= 8 &&
                              password == confirm;

                OkButton.IsEnabled = isValid;

                if (!string.IsNullOrEmpty(confirm) && password != confirm)
                {
                    StrengthText.Text = "Passwords do not match";
                    StrengthText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Password cannot be empty.", "Invalid Password",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsRestore)
            {
                if (password.Length < 8)
                {
                    MessageBox.Show("Password must be at least 8 characters long.",
                        "Weak Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (password != ConfirmPasswordBox.Password)
                {
                    MessageBox.Show("Passwords do not match.", "Password Mismatch",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var (isValid, message) = EncryptedBackupService.ValidatePassword(password);
                if (!isValid)
                {
                    var result = MessageBox.Show(
                        $"{message}\n\nDo you want to use this password anyway?",
                        "Weak Password",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            Password = password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}