using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Infrastructure.Data;
using MonBureau.Infrastructure.Repositories;
using Xunit;

namespace MonBureau.Tests.Repositories;

public class RepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Repository<Client> _repository;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new Repository<Client>(_context);
    }

    [Fact]
    public async Task AddAsync_AddsEntitySuccessfully()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Test",
            LastName = "Client",
            ContactEmail = "test@example.com"
        };

        // Act
        await _repository.AddAsync(client);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.Clients.FindAsync(client.Id);
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectEntity()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Ahmed",
            LastName = "Benali"
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(client.Id);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Ahmed");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        // Arrange
        _context.Clients.AddRange(
            new Client { FirstName = "Client1", LastName = "Test1" },
            new Client { FirstName = "Client2", LastName = "Test2" },
            new Client { FirstName = "Client3", LastName = "Test3" }
        );
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task FindAsync_FiltersCorrectly()
    {
        // Arrange
        _context.Clients.AddRange(
            new Client { FirstName = "Ahmed", LastName = "Benali" },
            new Client { FirstName = "Fatima", LastName = "Mansour" },
            new Client { FirstName = "Ahmed", LastName = "Khelifi" }
        );
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.FindAsync(c => c.FirstName == "Ahmed");

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(c => c.FirstName.Should().Be("Ahmed"));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntitySuccessfully()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Original",
            LastName = "Name"
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        client.FirstName = "Updated";
        await _repository.UpdateAsync(client);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Clients.FindAsync(client.Id);
        updated!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntitySuccessfully()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "ToDelete",
            LastName = "Client"
        };
        _context.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(client);
        await _context.SaveChangesAsync();

        // Assert
        var deleted = await _context.Clients.FindAsync(client.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        _context.Clients.AddRange(
            new Client { FirstName = "C1", LastName = "Test" },
            new Client { FirstName = "C2", LastName = "Test" }
        );
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.CountAsync();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 1; i <= 20; i++)
        {
            _context.Clients.Add(new Client
            {
                FirstName = $"Client{i}",
                LastName = "Test"
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var page1 = await _repository.GetPagedAsync(0, 10);
        var page2 = await _repository.GetPagedAsync(10, 10);

        // Assert
        page1.Should().HaveCount(10);
        page2.Should().HaveCount(10);
        page1.Should().NotIntersectWith(page2);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public class UnitOfWorkTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        // Arrange
        var client = new Client
        {
            FirstName = "Test",
            LastName = "Client"
        };

        // Act
        await _unitOfWork.Clients.AddAsync(client);
        var result = await _unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().BeGreaterThan(0);
        var saved = await _context.Clients.FindAsync(client.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_CommitsSuccessfully()
    {
        // Arrange
        var client = new Client { FirstName = "Trans", LastName = "Test" };

        // Act
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.Clients.AddAsync(client);
        await _unitOfWork.CommitTransactionAsync();

        // Assert
        var saved = await _context.Clients.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_RollsBackOnError()
    {
        // Arrange
        var client = new Client { FirstName = "Rollback", LastName = "Test" };

        // Act
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.Clients.AddAsync(client);
        await _unitOfWork.RollbackTransactionAsync();

        // Assert
        var saved = await _context.Clients.FirstOrDefaultAsync();
        saved.Should().BeNull();
    }

    [Fact]
    public void Repositories_AreNotNull()
    {
        // Assert
        _unitOfWork.Clients.Should().NotBeNull();
        _unitOfWork.Cases.Should().NotBeNull();
        _unitOfWork.CaseItems.Should().NotBeNull();
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        _context.Dispose();
    }
}