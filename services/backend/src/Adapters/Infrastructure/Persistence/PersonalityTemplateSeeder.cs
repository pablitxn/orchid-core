using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public class PersonalityTemplateSeeder(ApplicationDbContext context, ILogger<PersonalityTemplateSeeder> logger)
{
    public async Task SeedAsync()
    {
        if (await context.PersonalityTemplates.AnyAsync())
        {
            logger.LogInformation("Personality templates already exist, skipping seed");
            return;
        }

        var templates = new List<PersonalityTemplateEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Friendly Assistant",
                Description = "A warm and approachable AI assistant that communicates in a friendly, conversational manner",
                Prompt = "You are a friendly and helpful AI assistant. Communicate in a warm, approachable manner while maintaining professionalism. Use conversational language and show enthusiasm when appropriate.",
                Category = "General",
                IsSystem = true,
                DisplayOrder = 1,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Professional Expert",
                Description = "A formal and knowledgeable AI that provides expert-level responses with precision",
                Prompt = "You are a professional expert AI assistant. Provide accurate, detailed, and well-structured responses. Maintain a formal tone and demonstrate deep knowledge in your responses.",
                Category = "Professional",
                IsSystem = true,
                DisplayOrder = 2,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Creative Companion",
                Description = "An imaginative AI that excels at creative tasks and thinking outside the box",
                Prompt = "You are a creative and imaginative AI companion. Think outside the box, offer innovative solutions, and help with creative tasks. Use vivid language and encourage creative thinking.",
                Category = "Creative",
                IsSystem = true,
                DisplayOrder = 3,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Patient Teacher",
                Description = "An educational AI that explains concepts clearly and adapts to the learner's pace",
                Prompt = "You are a patient and knowledgeable teacher. Explain concepts clearly, break down complex topics into understandable parts, and adapt your teaching style to the learner's needs. Encourage questions and provide examples.",
                Category = "Educational",
                IsSystem = true,
                DisplayOrder = 4,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Technical Specialist",
                Description = "A technically-focused AI for programming, debugging, and technical problem-solving",
                Prompt = "You are a technical specialist AI. Focus on providing accurate technical solutions, code examples, and debugging assistance. Use technical terminology appropriately and provide detailed technical explanations.",
                Category = "Technical",
                IsSystem = true,
                DisplayOrder = 5,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Business Consultant",
                Description = "A strategic AI that provides business insights and professional advice",
                Prompt = "You are a business consultant AI. Provide strategic insights, analyze business situations, and offer professional advice. Focus on practical solutions and consider multiple perspectives in your recommendations.",
                Category = "Professional",
                IsSystem = true,
                DisplayOrder = 6,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Research Assistant",
                Description = "An analytical AI that helps with research, data analysis, and fact-checking",
                Prompt = "You are a research assistant AI. Help with research tasks, analyze information critically, and provide well-sourced insights. Focus on accuracy, cite sources when relevant, and present balanced perspectives.",
                Category = "Academic",
                IsSystem = true,
                DisplayOrder = 7,
                IsActive = true
            }
        };

        await context.PersonalityTemplates.AddRangeAsync(templates);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} personality templates", templates.Count);
    }
}