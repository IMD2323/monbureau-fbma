using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Infrastructure.Data;
using MonBureau.Infrastructure.Services;
using Xunit;

namespace MonBureau.Tests.Services;

public class CaseServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CaseService _service;

    public CaseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _service = new CaseService(_context);
    }

    [Fact]
    public void CanCloseCase_OpenCase_ReturnsTrue()
    {
        // Arrange
        var caseEntity = new Case
        {
            Status = CaseStatus.Open,
            OpeningDate = DateTime.Today.AddDays(-5)
        };

        // Act
        var result = _service.CanCloseCase(caseEntity);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanCloseCase_ClosedCase_ReturnsFalse()
    {
        // Arrange
        var caseEntity = new Case
        {
            Status = CaseStatus.Closed
        };

        // Act
        var result = _service.CanCloseCase(caseEntity);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanReopenCase_ClosedCase_ReturnsTrue()
    {
        // Arrange
        var caseEntity = new Case
        {
            Status = CaseStatus.Closed
        };

        // Act
        var result = _service.CanReopenCase(caseEntity);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateTotalExpensesAsync_ReturnsCorrectTotal()
    {
        // Arrange
        var caseEntity = new Case
        {
            Number = "DOSS-2025-0001",
            Title = "Test Case",
            ClientId = 1,
            OpeningDate = DateTime.Today
        };
        _context.Cases.Add(caseEntity);
        await _context.SaveChangesAsync();

        _context.CaseItems.AddRange(
            new CaseItem { CaseId = caseEntity.Id, Type = ItemType.Expense, Name = "Exp1", Amount = 100, Date = DateTime.Today },
            new CaseItem { CaseId = caseEntity.Id, Type = ItemType.Expense, Name = "Exp2", Amount = 200, Date = DateTime.Today },
            new CaseItem { CaseId = caseEntity.Id, Type = ItemType.Document, Name = "Doc1", Date = DateTime.Today }
        );
        await _context.SaveChangesAsync();

        // Act
        var total = await _service.CalculateTotalExpensesAsync(caseEntity.Id);

        // Assert
        total.Should().Be(300);
    }

    [Fact]
    public async Task HasPendingItemsAsync_WithFutureTasks_ReturnsTrue()
    {
        // Arrange
        var caseEntity = new Case
        {
            Number = "DOSS-2025-0001",
            Title = "Test",
            ClientId = 1,
            OpeningDate = DateTime.Today
        };
        _context.Cases.Add(caseEntity);
        await _context.SaveChangesAsync();

        _context.CaseItems.Add(new CaseItem
        {
            CaseId = caseEntity.Id,
            Type = ItemType.Task,
            Name = "Future Task",
            Date = DateTime.Today.AddDays(5)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasPendingItemsAsync(caseEntity.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateClosureAsync_ValidCase_ReturnsValid()
    {
        // Arrange
        var caseEntity = new Case
        {
            Number = "DOSS-2025-0001",
            Title = "Test",
            ClientId = 1,
            Status = CaseStatus.InProgress,
            OpeningDate = DateTime.Today.AddDays(-10)
        };
        _context.Cases.Add(caseEntity);
        await _context.SaveChangesAsync();

        // Act
        var (isValid, errors) = await _service.ValidateClosureAsync(caseEntity);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public class BackupServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testBackupPath;
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        var tempPath = Path.GetTempPath();
        _testDbPath = Path.Combine(tempPath, $"test_{Guid.NewGuid()}.db");
        _testBackupPath = Path.Combine(tempPath, "TestBackups");

        // Create empty test database
        File.WriteAllText(_testDbPath, "test data");

        Directory.CreateDirectory(_testBackupPath);
        _service = new BackupService(_testDbPath);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesBackupFile()
    {
        // Act
        var (success, message, filePath) = await _service.CreateBackupAsync(_testBackupPath);

        // Assert
        success.Should().BeTrue();
        filePath.Should().NotBeNullOrEmpty();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task GetBackupHistoryAsync_ReturnsBackups()
    {
        // Arrange
        await _service.CreateBackupAsync();

        // Act
        var backups = await _service.GetBackupHistoryAsync();

        // Assert
        backups.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
                File.Delete(_testDbPath);

            if (Directory.Exists(_testBackupPath))
                Directory.Delete(_testBackupPath, true);
        }
        catch { }
    }
}

public class DeviceIdentifierTests
{
    [Fact]
    public void GenerateDeviceId_ReturnsNonEmptyString()
    {
        // Arrange
        var service = new DeviceIdentifier();

        // Act
        var result = service.GenerateDeviceId();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Length.Should().Be(64); // SHA256 hex string
    }

    [Fact]
    public void GenerateDeviceId_SameMachine_ReturnsSameId()
    {
        // Arrange
        var service = new DeviceIdentifier();

        // Act
        var id1 = service.GenerateDeviceId();
        var id2 = service.GenerateDeviceId();

        // Assert
        id1.Should().Be(id2);
    }
}

public class DpapiServiceTests
{
    [Fact]
    public void Encrypt_Decrypt_RoundTrip_Success()
    {
        // Arrange
        var service = new DpapiService();
        var plainText = "Sensitive Data";

        // Act
        var encrypted = service.Encrypt(plainText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        encrypted.Should().NotBe(plainText);
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var service = new DpapiService();

        // Act
        var result = service.Encrypt("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_InvalidData_ReturnsEmpty()
    {
        // Arrange
        var service = new DpapiService();

        // Act
        var result = service.Decrypt("invalid-data");

        // Assert
        result.Should().BeEmpty();
    }
}

public class ErrorServiceTests
{
    private readonly ErrorService _service;

    public ErrorServiceTests()
    {
        _service = new ErrorService();
    }

    [Fact]
    public void GetUserFriendlyMessage_ArgumentNullException_ReturnsUserFriendly()
    {
        // Arrange
        var exception = new ArgumentNullException("testParam");

        // Act
        var message = _service.GetUserFriendlyMessage(exception);

        // Assert
        message.Should().Contain("Paramètre requis");
        message.Should().Contain("testParam");
    }

    [Fact]
    public void GetUserFriendlyMessage_FileNotFoundException_ReturnsUserFriendly()
    {
        // Arrange
        var exception = new FileNotFoundException();

        // Act
        var message = _service.GetUserFriendlyMessage(exception);

        // Assert
        message.Should().Contain("Fichier introuvable");
    }

    [Fact]
    public void GetErrorDetails_LogsAndReturnsDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var details = _service.GetErrorDetails(exception, "Test Context");

        // Assert
        details.Should().NotBeNull();
        details.UserMessage.Should().NotBeNullOrEmpty();
        details.TechnicalMessage.Should().Be("Test error");
        details.Context.Should().Be("Test Context");
    }

    [Theory]
    [InlineData(typeof(DbUpdateConcurrencyException), true)]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(ArgumentException), false)]
    public void GetErrorDetails_SetsCanRetryCorrectly(Type exceptionType, bool expectedCanRetry)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        // Act
        var details = _service.GetErrorDetails(exception);

        // Assert
        details.CanRetry.Should().Be(expectedCanRetry);
    }
}