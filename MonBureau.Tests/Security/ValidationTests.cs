using System;
using System.IO;
using FluentAssertions;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Core.Validators;
using MonBureau.Infrastructure.Security;
using MonBureau.Infrastructure.Services;
using Xunit;

namespace MonBureau.Tests.Security
{
    /// <summary>
    /// Security and validation tests
    /// Critical for production deployment
    /// </summary>
    public class ValidationTests
    {
        [Fact]
        public void ValidateClient_ValidData_ReturnsValid()
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "Ahmed",
                LastName = "Benali",
                ContactEmail = "ahmed@example.com",
                ContactPhone = "0555123456"
            };

            // ACT
            var result = EntityValidator.ValidateClient(client);

            // ASSERT
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ValidateClient_EmptyFirstName_ReturnsError()
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "",
                LastName = "Benali"
            };

            // ACT
            var result = EntityValidator.ValidateClient(client);

            // ASSERT
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainKey(nameof(Client.FirstName));
        }

        [Fact]
        public void ValidateClient_InvalidEmail_ReturnsError()
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "Ahmed",
                LastName = "Benali",
                ContactEmail = "not-an-email"
            };

            // ACT
            var result = EntityValidator.ValidateClient(client);

            // ASSERT
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainKey(nameof(Client.ContactEmail));
        }

        [Theory]
        [InlineData("0555123456")] // Valid mobile
        [InlineData("0655123456")] // Valid mobile
        [InlineData("0755123456")] // Valid mobile
        [InlineData("+213555123456")] // Valid international
        public void ValidateClient_ValidPhoneFormats_ReturnsValid(string phone)
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "Test",
                LastName = "User",
                ContactPhone = phone
            };

            // ACT
            var result = EntityValidator.ValidateClient(client);

            // ASSERT
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("123")] // Too short
        [InlineData("0123456789")] // Wrong prefix
        [InlineData("abcdefghij")] // Not numeric
        public void ValidateClient_InvalidPhoneFormats_ReturnsError(string phone)
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "Test",
                LastName = "User",
                ContactPhone = phone
            };

            // ACT
            var result = EntityValidator.ValidateClient(client);

            // ASSERT
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainKey(nameof(Client.ContactPhone));
        }

        [Fact]
        public void ValidateCase_ValidData_ReturnsValid()
        {
            // ARRANGE
            var caseEntity = new Case
            {
                Number = "DOSS-2025-0001",
                Title = "Test Case",
                ClientId = 1,
                OpeningDate = DateTime.Today
            };

            // ACT
            var result = EntityValidator.ValidateCase(caseEntity);

            // ASSERT
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("DOSS-2025-0001")] // Valid
        [InlineData("DOSS-2024-9999")] // Valid
        public void ValidateCase_ValidCaseNumber_ReturnsValid(string number)
        {
            // ARRANGE
            var caseEntity = new Case
            {
                Number = number,
                Title = "Test",
                ClientId = 1,
                OpeningDate = DateTime.Today
            };

            // ACT
            var result = EntityValidator.ValidateCase(caseEntity);

            // ASSERT
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("INVALID")] // Wrong format
        [InlineData("DOSS-20250001")] // Missing dash
        [InlineData("CASE-2025-0001")] // Wrong prefix
        public void ValidateCase_InvalidCaseNumber_ReturnsError(string number)
        {
            // ARRANGE
            var caseEntity = new Case
            {
                Number = number,
                Title = "Test",
                ClientId = 1,
                OpeningDate = DateTime.Today
            };

            // ACT
            var result = EntityValidator.ValidateCase(caseEntity);

            // ASSERT
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainKey(nameof(Case.Number));
        }

        [Fact]
        public void ValidateCase_FutureOpeningDate_ReturnsError()
        {
            // ARRANGE
            var caseEntity = new Case
            {
                Number = "DOSS-2025-0001",
                Title = "Test",
                ClientId = 1,
                OpeningDate = DateTime.Today.AddDays(10)
            };

            // ACT
            var result = EntityValidator.ValidateCase(caseEntity);

            // ASSERT
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainKey(nameof(Case.OpeningDate));
        }

        [Fact]
        public void ValidateCaseItem_Expense_RequiresAmount()
        {
            // ARRANGE
            var item = new CaseItem
            {
                CaseId = 1,
                Type = ItemType.Expense,
                Name = "Test Expense",
                Date = DateTime.Today
                // Missing Amount
            };

            // ACT
            var result = EntityValidator.ValidateCaseItem(item);

            // ASSERT
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainKey(nameof(CaseItem.Amount));
        }

        [Fact]
        public void ValidateCaseItem_Document_DoesNotRequireAmount()
        {
            // ARRANGE
            var item = new CaseItem
            {
                CaseId = 1,
                Type = ItemType.Document,
                Name = "Test Document",
                Date = DateTime.Today
                // No Amount - should be valid
            };

            // ACT
            var result = EntityValidator.ValidateCaseItem(item);

            // ASSERT
            result.IsValid.Should().BeTrue();
        }
    }

    public class SecurityTests
    {
        [Fact]
        public void DpapiService_EncryptDecrypt_RoundTrip()
        {
            // ARRANGE
            var service = new DpapiService();
            var plainText = "SensitiveData123!@#";

            // ACT
            var encrypted = service.Encrypt(plainText);
            var decrypted = service.Decrypt(encrypted);

            // ASSERT
            encrypted.Should().NotBe(plainText);
            decrypted.Should().Be(plainText);
        }

        [Fact]
        public void DpapiService_EncryptEmpty_ReturnsEmpty()
        {
            // ARRANGE
            var service = new DpapiService();

            // ACT
            var encrypted = service.Encrypt("");

            // ASSERT
            encrypted.Should().BeEmpty();
        }

        [Fact]
        public void DeviceIdentifier_GenerateId_ReturnsConsistentValue()
        {
            // ARRANGE
            var identifier = new DeviceIdentifier();

            // ACT
            var id1 = identifier.GenerateDeviceId();
            var id2 = identifier.GenerateDeviceId();

            // ASSERT
            id1.Should().NotBeNullOrEmpty();
            id1.Should().Be(id2); // Should be consistent
            id1.Length.Should().Be(64); // SHA256 hex
        }

        [Fact]
        public void SecureLicenseStorage_SaveAndLoad_Success()
        {
            // ARRANGE
            var storage = new SecureLicenseStorage();
            var licenseKey = "MB-2025-TEST1";
            var deviceId = "test-device-123";

            try
            {
                // ACT
                var saved = storage.SaveLicense(licenseKey, deviceId);
                var (success, loadedKey, loadedDevice, _) = storage.LoadLicense();

                // ASSERT
                saved.Should().BeTrue();
                success.Should().BeTrue();
                loadedKey.Should().Be(licenseKey);
                loadedDevice.Should().Be(deviceId);
            }
            finally
            {
                // Cleanup
                storage.DeleteLicense();
            }
        }

        [Fact]
        public void SecureLicenseStorage_Tamper_Detected()
        {
            // ARRANGE
            var storage = new SecureLicenseStorage();
            var licenseKey = "MB-2025-TEST2";
            var deviceId = "test-device-456";

            try
            {
                storage.SaveLicense(licenseKey, deviceId);

                // ACT: Tamper with the file
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var licenseFile = Path.Combine(appData, "MonBureau", "license.dat");

                if (File.Exists(licenseFile))
                {
                    var bytes = File.ReadAllBytes(licenseFile);
                    bytes[bytes.Length / 2] ^= 0xFF; // Flip some bits
                    File.WriteAllBytes(licenseFile, bytes);
                }

                var (success, _, _, _) = storage.LoadLicense();

                // ASSERT: Should detect tampering
                success.Should().BeFalse();
            }
            finally
            {
                storage.DeleteLicense();
            }
        }

        [Fact]
        public void EncryptedBackup_ValidatePassword_RejectsWeak()
        {
            // ACT & ASSERT
            EncryptedBackupService.ValidatePassword("123")
                .isValid.Should().BeFalse();

            EncryptedBackupService.ValidatePassword("password")
                .isValid.Should().BeFalse();

            EncryptedBackupService.ValidatePassword("Pass123!")
                .isValid.Should().BeTrue();
        }

        [Fact]
        public void EncryptedBackup_CreateAndRestore_Success()
        {
            // ARRANGE
            var password = "SecureP@ss123";
            var testFile = Path.GetTempFileName();
            File.WriteAllText(testFile, "Test content");

            var backupPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.bak");
            var restorePath = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid()}");

            try
            {
                // ACT: Create encrypted backup
                var created = EncryptedBackupService.CreateBackup(
                    backupPath,
                    password,
                    new[] { testFile }
                );

                created.Should().BeTrue();
                File.Exists(backupPath).Should().BeTrue();

                // Verify it's encrypted
                EncryptedBackupService.IsEncryptedBackup(backupPath)
                    .Should().BeTrue();

                // Restore
                Directory.CreateDirectory(restorePath);
                var restored = EncryptedBackupService.RestoreBackup(
                    backupPath,
                    password,
                    restorePath
                );

                // ASSERT
                restored.Should().BeTrue();
                var restoredFile = Path.Combine(restorePath, Path.GetFileName(testFile));
                File.Exists(restoredFile).Should().BeTrue();
                File.ReadAllText(restoredFile).Should().Be("Test content");
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile)) File.Delete(testFile);
                if (File.Exists(backupPath)) File.Delete(backupPath);
                if (Directory.Exists(restorePath)) Directory.Delete(restorePath, true);
            }
        }

        [Fact]
        public void EncryptedBackup_WrongPassword_Fails()
        {
            // ARRANGE
            var password = "Correct123!";
            var wrongPassword = "Wrong123!";
            var testFile = Path.GetTempFileName();
            File.WriteAllText(testFile, "Secret data");

            var backupPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.bak");
            var restorePath = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid()}");

            try
            {
                // Create backup with correct password
                EncryptedBackupService.CreateBackup(
                    backupPath,
                    password,
                    new[] { testFile }
                );

                // ACT: Try to restore with wrong password
                Directory.CreateDirectory(restorePath);
                var restored = EncryptedBackupService.RestoreBackup(
                    backupPath,
                    wrongPassword,
                    restorePath
                );

                // ASSERT
                restored.Should().BeFalse();
            }
            finally
            {
                if (File.Exists(testFile)) File.Delete(testFile);
                if (File.Exists(backupPath)) File.Delete(backupPath);
                if (Directory.Exists(restorePath)) Directory.Delete(restorePath, true);
            }
        }
    }
}