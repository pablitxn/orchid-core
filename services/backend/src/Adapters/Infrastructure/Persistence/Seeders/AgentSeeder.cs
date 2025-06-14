using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeders;

public class AgentSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AgentSeeder> _logger;

    public AgentSeeder(ApplicationDbContext context, ILogger<AgentSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            if (await _context.Agents.AnyAsync())
            {
                _logger.LogInformation("Agents already seeded. Skipping...");
                return;
            }

            _logger.LogInformation("Seeding agents...");

            // Get personality templates
            var customerServiceTemplate = await _context.PersonalityTemplates
                .FirstOrDefaultAsync(pt => pt.Name == "Customer Service");
            var technicalAssistantTemplate = await _context.PersonalityTemplates
                .FirstOrDefaultAsync(pt => pt.Name == "Technical Assistant");
            var salesAgentTemplate = await _context.PersonalityTemplates
                .FirstOrDefaultAsync(pt => pt.Name == "Sales Agent");
            var creativeWriterTemplate = await _context.PersonalityTemplates
                .FirstOrDefaultAsync(pt => pt.Name == "Creative Writer");
            var educationTutorTemplate = await _context.PersonalityTemplates
                .FirstOrDefaultAsync(pt => pt.Name == "Education Tutor");

            // Get plugins
            var calendarPlugin = await _context.Plugins
                .FirstOrDefaultAsync(p => p.Name == "Calendar Sync");
            var crmPlugin = await _context.Plugins
                .FirstOrDefaultAsync(p => p.Name == "CRM Connector");

            var now = DateTime.UtcNow;

            var agents = new List<AgentEntity>
            {
                new AgentEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Sophia - Customer Support",
                    Description = "Friendly and helpful customer support agent ready to assist with any questions or concerns.",
                    AvatarUrl = "/avatars/sophia.png",
                    PersonalityTemplateId = customerServiceTemplate?.Id,
                    Personality = "I'm Sophia, your dedicated customer support assistant. I'm here to help you with any questions, concerns, or issues you might have. I approach every interaction with patience, empathy, and a genuine desire to resolve your needs effectively.",
                    Language = "en",
                    PluginIds = calendarPlugin != null ? new[] { calendarPlugin.Id } : Array.Empty<Guid>(),
                    IsPublic = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AgentEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Max - Tech Expert",
                    Description = "Technical specialist with expertise in troubleshooting and explaining complex tech concepts simply.",
                    AvatarUrl = "/avatars/max.png",
                    PersonalityTemplateId = technicalAssistantTemplate?.Id,
                    Personality = "Hi! I'm Max, your technical expert. I specialize in breaking down complex technical concepts into easy-to-understand explanations. Whether you need help troubleshooting, understanding new technology, or implementing solutions, I'm here to guide you through it step by step.",
                    Language = "en",
                    PluginIds = Array.Empty<Guid>(),
                    IsPublic = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AgentEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Emma - Sales Consultant",
                    Description = "Professional sales consultant focused on understanding your needs and finding the perfect solution.",
                    AvatarUrl = "/avatars/emma.png",
                    PersonalityTemplateId = salesAgentTemplate?.Id,
                    Personality = "Hello! I'm Emma, your sales consultant. I believe in consultative selling - understanding your unique needs before suggesting solutions. I'm here to help you find the perfect product or service that truly adds value to your business or personal goals.",
                    Language = "en",
                    PluginIds = crmPlugin != null ? new[] { crmPlugin.Id } : Array.Empty<Guid>(),
                    IsPublic = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AgentEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Luna - Creative Assistant",
                    Description = "Creative writing specialist helping with content creation, storytelling, and creative projects.",
                    AvatarUrl = "/avatars/luna.png",
                    PersonalityTemplateId = creativeWriterTemplate?.Id,
                    Personality = "Greetings! I'm Luna, your creative muse and writing companion. I thrive on helping you bring your creative visions to life, whether it's crafting compelling stories, developing engaging content, or brainstorming innovative ideas. Let's create something amazing together!",
                    Language = "en",
                    PluginIds = Array.Empty<Guid>(),
                    IsPublic = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AgentEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Professor Alex",
                    Description = "Educational tutor specializing in personalized learning and academic support across various subjects.",
                    AvatarUrl = "/avatars/alex.png",
                    PersonalityTemplateId = educationTutorTemplate?.Id,
                    Personality = "Welcome! I'm Professor Alex, your personal education tutor. I'm passionate about making learning accessible and enjoyable. Whether you need help understanding complex concepts, preparing for exams, or developing study strategies, I'll tailor my approach to your learning style.",
                    Language = "en",
                    PluginIds = Array.Empty<Guid>(),
                    IsPublic = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new AgentEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Nova - Data Analyst",
                    Description = "Data analysis expert helping you understand and visualize complex data patterns and insights.",
                    AvatarUrl = "/avatars/nova.png",
                    Personality = "Hello! I'm Nova, your data analysis companion. I excel at transforming raw data into meaningful insights. Whether you need help with data visualization, statistical analysis, or understanding trends, I'll help you make data-driven decisions with confidence.",
                    Language = "en",
                    PluginIds = Array.Empty<Guid>(),
                    IsPublic = true,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            _context.Agents.AddRange(agents);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully seeded {agents.Count} agents");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding agents");
            throw;
        }
    }
}