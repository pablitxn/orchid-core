namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Knowledge base seeder - disabled for cloud deployment (no local filesystem access).
/// Users can upload their own knowledge base files via the API.
/// </summary>
public class KnowledgeBaseSeeder
{
    public Task SeedAllUsersAsync()
    {
        // Knowledge base seeding from local files is disabled for cloud deployment.
        // Users can upload their own files via the /knowledge-base API endpoint.
        return Task.CompletedTask;
    }
}