using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MonBureau.Core.Entities;
using MonBureau.Core.Enums;
using MonBureau.Infrastructure.Data;
using MonBureau.Infrastructure.Repositories;
using MonBureau.Infrastructure.Services;
using Xunit;

namespace MonBureau.Tests.Integration
{
    /// <summary>
    /// Integration tests for critical workflows
    /// Tests: Database → Repository → Service → UI flow
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly UnitOfWork _unitOfWork;
        private readonly string _testDbPath;

        public IntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            _unitOfWork = new UnitOfWork(_context);
        }

        [Fact]
        public async Task CompleteWorkflow_CreateClientCaseAndDocument_Success()
        {
            // ARRANGE: Create client
            var client = new Client
            {
                FirstName = "Ahmed",
                LastName = "Benali",
                ContactEmail = "ahmed@example.com",
                ContactPhone = "0555123456"
            };

            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            client.Id.Should().BeGreaterThan(0);

            // ACT: Create case for client
            var caseEntity = new Case
            {
                Number = "DOSS-2025-0001",
                Title = "Test Case",
                ClientId = client.Id,
                Status = CaseStatus.Open,
                OpeningDate = DateTime.Today
            };

            await _unitOfWork.Cases.AddAsync(caseEntity);
            await _unitOfWork.SaveChangesAsync();

            caseEntity.Id.Should().BeGreaterThan(0);

            // ACT: Add document to case
            var document = new CaseItem
            {
                CaseId = caseEntity.Id,
                Type = ItemType.Document,
                Name = "Test Document",
                Date = DateTime.Today
            };

            await _unitOfWork.CaseItems.AddAsync(document);
            await _unitOfWork.SaveChangesAsync();

            // ASSERT: Verify complete chain
            var loadedCase = await _context.Cases
                .Include(c => c.Client)
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == caseEntity.Id);

            loadedCase.Should().NotBeNull();
            loadedCase!.Client.Should().NotBeNull();
            loadedCase.Client.FullName.Should().Be("Ahmed Benali");
            loadedCase.Items.Should().HaveCount(1);
            loadedCase.Items.First().Name.Should().Be("Test Document");
        }

        [Fact]
        public async Task CascadeDelete_DeleteClient_RemovesCases()
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "Fatima",
                LastName = "Mansour"
            };

            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            var case1 = new Case
            {
                Number = "DOSS-2025-0001",
                Title = "Case 1",
                ClientId = client.Id,
                OpeningDate = DateTime.Today
            };

            var case2 = new Case
            {
                Number = "DOSS-2025-0002",
                Title = "Case 2",
                ClientId = client.Id,
                OpeningDate = DateTime.Today
            };

            await _unitOfWork.Cases.AddAsync(case1);
            await _unitOfWork.Cases.AddAsync(case2);
            await _unitOfWork.SaveChangesAsync();

            // ACT: Delete client
            await _unitOfWork.Clients.DeleteAsync(client);
            await _unitOfWork.SaveChangesAsync();

            // ASSERT: Cases should be deleted
            var remainingCases = await _context.Cases
                .Where(c => c.ClientId == client.Id)
                .ToListAsync();

            remainingCases.Should().BeEmpty();
        }

        [Fact]
        public async Task Pagination_LoadLargeCaseList_ReturnsCorrectPage()
        {
            // ARRANGE: Create 100 cases
            var client = new Client
            {
                FirstName = "Test",
                LastName = "Client"
            };

            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            for (int i = 1; i <= 100; i++)
            {
                var caseEntity = new Case
                {
                    Number = $"DOSS-2025-{i:D4}",
                    Title = $"Case {i}",
                    ClientId = client.Id,
                    OpeningDate = DateTime.Today.AddDays(-i)
                };

                await _unitOfWork.Cases.AddAsync(caseEntity);
            }

            await _unitOfWork.SaveChangesAsync();

            // ACT: Load page 2 (skip 20, take 20)
            var page2 = await _unitOfWork.Cases.GetPagedAsync(20, 20);

            // ASSERT
            page2.Should().HaveCount(20);
            page2.Should().NotContain(c => c.Number == "DOSS-2025-0001"); // First item
            page2.Should().Contain(c => c.Number == "DOSS-2025-0021"); // 21st item
        }

        [Fact]
        public async Task Transaction_Rollback_RestoresOriginalState()
        {
            // ARRANGE
            var client = new Client
            {
                FirstName = "Original",
                LastName = "Name"
            };

            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            var originalId = client.Id;

            // ACT: Begin transaction and modify
            await _unitOfWork.BeginTransactionAsync();

            client.FirstName = "Modified";
            await _unitOfWork.Clients.UpdateAsync(client);
            await _unitOfWork.SaveChangesAsync();

            // Rollback
            await _unitOfWork.RollbackTransactionAsync();

            // ASSERT: Data should be unchanged
            var reloadedClient = await _context.Clients.FindAsync(originalId);
            reloadedClient!.FirstName.Should().Be("Original");
        }

        [Fact]
        public async Task Search_FilterCasesByClientName_ReturnsMatches()
        {
            // ARRANGE
            var client1 = new Client { FirstName = "Ahmed", LastName = "Benali" };
            var client2 = new Client { FirstName = "Fatima", LastName = "Mansour" };

            await _unitOfWork.Clients.AddAsync(client1);
            await _unitOfWork.Clients.AddAsync(client2);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.Cases.AddAsync(new Case
            {
                Number = "DOSS-2025-0001",
                Title = "Case 1",
                ClientId = client1.Id,
                OpeningDate = DateTime.Today
            });

            await _unitOfWork.Cases.AddAsync(new Case
            {
                Number = "DOSS-2025-0002",
                Title = "Case 2",
                ClientId = client2.Id,
                OpeningDate = DateTime.Today
            });

            await _unitOfWork.SaveChangesAsync();

            // ACT: Search for cases of client "Ahmed"
            var results = await _context.Cases
                .Include(c => c.Client)
                .Where(c => c.Client.FirstName.Contains("Ahmed"))
                .ToListAsync();

            // ASSERT
            results.Should().HaveCount(1);
            results.First().Client.FirstName.Should().Be("Ahmed");
        }

        [Fact]
        public async Task ExpenseCalculation_MultipleExpenses_ReturnsCorrectTotal()
        {
            // ARRANGE
            var client = new Client { FirstName = "Test", LastName = "Client" };
            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            var caseEntity = new Case
            {
                Number = "DOSS-2025-0001",
                Title = "Expense Test",
                ClientId = client.Id,
                OpeningDate = DateTime.Today
            };

            await _unitOfWork.Cases.AddAsync(caseEntity);
            await _unitOfWork.SaveChangesAsync();

            // Add expenses
            await _unitOfWork.CaseItems.AddAsync(new CaseItem
            {
                CaseId = caseEntity.Id,
                Type = ItemType.Expense,
                Name = "Expense 1",
                Amount = 1000m,
                Date = DateTime.Today
            });

            await _unitOfWork.CaseItems.AddAsync(new CaseItem
            {
                CaseId = caseEntity.Id,
                Type = ItemType.Expense,
                Name = "Expense 2",
                Amount = 2500m,
                Date = DateTime.Today
            });

            await _unitOfWork.SaveChangesAsync();

            // ACT
            var caseService = new CaseService(_context);
            var total = await caseService.CalculateTotalExpensesAsync(caseEntity.Id);

            // ASSERT
            total.Should().Be(3500m);
        }

        [Fact]
        public async Task BackupRestore_CreateAndRestore_DataIntact()
        {
            // ARRANGE: Create test data
            var client = new Client
            {
                FirstName = "Backup",
                LastName = "Test"
            };

            await _unitOfWork.Clients.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();

            var backupService = new BackupService(_testDbPath);
            var backupPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid()}.zip");

            try
            {
                // ACT: Create backup
                var (createSuccess, _, _) = await backupService.CreateBackupAsync(backupPath);
                createSuccess.Should().BeTrue();

                // Delete original data
                await _unitOfWork.Clients.DeleteAsync(client);
                await _unitOfWork.SaveChangesAsync();

                var clientsAfterDelete = await _context.Clients.ToListAsync();
                clientsAfterDelete.Should().BeEmpty();

                // Restore backup
                var (restoreSuccess, _) = await backupService.RestoreBackupAsync(backupPath);
                restoreSuccess.Should().BeTrue();

                // ASSERT: Data should be restored
                // Note: Need to recreate context after restore
                var newContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={_testDbPath}")
                    .Options);

                var restoredClients = await newContext.Clients.ToListAsync();
                restoredClients.Should().HaveCount(1);
                restoredClients.First().FirstName.Should().Be("Backup");
            }
            finally
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
        }

        public void Dispose()
        {
            _unitOfWork?.Dispose();
            _context?.Dispose();

            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch { }
            }
        }
    }
}