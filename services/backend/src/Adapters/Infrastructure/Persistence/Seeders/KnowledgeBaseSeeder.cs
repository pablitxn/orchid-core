using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeders;

public class KnowledgeBaseSeeder(
    ApplicationDbContext context,
    IFileStorageService fileStorage,
    string defaultDocumentsPath = "data/knowledge_base/default_documents")
{
    private async Task SeedForUserAsync(Guid userId)
    {
        // Check if user already has knowledge base files
        var existingFiles = await context.KnowledgeBaseFiles
            .Where(f => f.UserId == userId)
            .AnyAsync();

        if (existingFiles)
            return;

        var defaultFiles = new[]
        {
            new
            {
                FileName = "manifest.txt", // Will be converted to PDF in production
                Title = "System Manifest",
                Description = "Core principles and guidelines for using the knowledge base system",
                Tags = new[] { "system", "guidelines", "manifest", "documentation" },
                MimeType = "application/pdf"
            },
            new
            {
                FileName = "instructions.md",
                Title = "Knowledge Base Instructions",
                Description = "Comprehensive guide on how to use the knowledge base effectively",
                Tags = new[] { "instructions", "guide", "help", "documentation" },
                MimeType = "text/markdown"
            },
            new
            {
                FileName = "system_prompt.md",
                Title = "AI System Prompt",
                Description = "Configuration and behavioral guidelines for the AI assistant",
                Tags = new[] { "ai", "system", "prompt", "configuration" },
                MimeType = "text/markdown"
            },
            new
            {
                FileName = "products.csv",
                Title = "Product Catalog",
                Description = "Sample product catalog with pricing and inventory information",
                Tags = new[] { "products", "catalog", "inventory", "sample-data" },
                MimeType = "text/csv"
            },
            new
            {
                FileName = "trial_balance.csv",
                Title = "Financial Trial Balance",
                Description = "Sample financial trial balance showing accounts and balances",
                Tags = new[] { "finance", "accounting", "trial-balance", "sample-data" },
                MimeType = "text/csv"
            },
            new
            {
                FileName = "budget_2025.csv",
                Title = "Budget Planning 2025",
                Description = "Quarterly budget planning spreadsheet with revenue and expense projections",
                Tags = new[] { "budget", "planning", "finance", "projections", "sample-data" },
                MimeType = "text/csv"
            }
        };

        var timestamp = DateTime.UtcNow;

        foreach (var file in defaultFiles)
        {
            try
            {
                var filePath = Path.Combine(defaultDocumentsPath, file.FileName);

                // For manifest.txt, we would convert to PDF here in production
                // For now, we'll upload it as text and note it should be PDF
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Warning: Default file not found: {filePath}");
                    continue;
                }

                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileSize = fileBytes.Length;

                // Generate unique file name for storage
                var uniqueFileName = $"{userId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                // Upload file to storage
                var fileUrl = await fileStorage.StoreFileAsync(
                    new MemoryStream(fileBytes),
                    uniqueFileName,
                    file.MimeType);

                // Create knowledge base entry
                var kbFile = new KnowledgeBaseFileEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Title = file.Title,
                    Description = file.Description,
                    Tags = file.Tags,
                    MimeType = file.MimeType,
                    FileUrl = fileUrl,
                    FileSize = fileSize,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp
                };

                context.KnowledgeBaseFiles.Add(kbFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding file {file.FileName} for user {userId}: {ex.Message}");
                // Continue with other files
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task SeedAllUsersAsync()
    {
        var users = await context.Users
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var userId in users)
        {
            await SeedForUserAsync(userId);
        }
    }
}