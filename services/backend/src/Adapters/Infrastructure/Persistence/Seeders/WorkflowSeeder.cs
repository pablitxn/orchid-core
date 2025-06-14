using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeders;

public class WorkflowSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<WorkflowSeeder> _logger;

    public WorkflowSeeder(ApplicationDbContext db, ILogger<WorkflowSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _db.Workflows.AnyAsync())
        {
            _logger.LogInformation("Workflows already seeded");
            return;
        }

        var workflows = new[]
        {
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Data Analysis Pipeline",
                Description = "Automated data cleaning, transformation, and visualization workflow",
                DetailedDescription = @"This comprehensive workflow automates the entire data analysis pipeline:
                    1. **Data Import**: Supports CSV, Excel, JSON, and SQL databases
                    2. **Data Cleaning**: Removes duplicates, handles missing values, standardizes formats
                    3. **Data Transformation**: Apply custom transformations, aggregations, and calculations
                    4. **Statistical Analysis**: Generate descriptive statistics, correlations, and trends
                    5. **Visualization**: Create interactive charts, graphs, and dashboards
                    6. **Report Generation**: Export results as PDF, Excel, or PowerBI reports",
                PriceCredits = 50,
                Author = "DataScience Team",
                Category = "Analytics",
                Steps = 6,
                EstimatedTime = "10-15 minutes",
                Rating = 4.8,
                Runs = 15420,
                Icon = "chart-line",
                Tags = "data-analysis,visualization,reporting,statistics,etl",
                Prerequisites = "Basic understanding of data analysis concepts",
                InputRequirements = "CSV, Excel, or JSON file (max 100MB)",
                OutputFormat = "PDF report with charts, Excel workbook with processed data"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Document Intelligence Suite",
                Description = "Extract insights from documents using AI-powered analysis",
                DetailedDescription = @"Leverage advanced AI to process and understand your documents:
                    1. **OCR Processing**: Extract text from scanned documents and images
                    2. **Entity Recognition**: Identify people, places, organizations, and key concepts
                    3. **Sentiment Analysis**: Determine emotional tone and opinions
                    4. **Summary Generation**: Create concise summaries of long documents
                    5. **Key Points Extraction**: Highlight the most important information
                    6. **Translation**: Translate documents into 50+ languages
                    7. **Classification**: Automatically categorize documents by type and content",
                PriceCredits = 75,
                Author = "AI Research Lab",
                Category = "AI & Machine Learning",
                Steps = 7,
                EstimatedTime = "5-8 minutes",
                Rating = 4.9,
                Runs = 8932,
                Icon = "brain",
                Tags = "ai,nlp,document-processing,ocr,translation,summary",
                Prerequisites = "None - works with any document type",
                InputRequirements = "PDF, Word, TXT, or image files (max 50MB)",
                OutputFormat = "JSON with extracted entities, summary text, translated documents"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Sales CRM Integration",
                Description = "Sync and enrich your CRM data across multiple platforms",
                DetailedDescription = @"Seamlessly integrate your sales data across platforms:
                    1. **Multi-CRM Support**: Connect Salesforce, HubSpot, Pipedrive, and more
                    2. **Data Synchronization**: Real-time bi-directional sync
                    3. **Lead Enrichment**: Add social media profiles, company data, and contact info
                    4. **Duplicate Detection**: Identify and merge duplicate records
                    5. **Activity Tracking**: Consolidate emails, calls, and meetings
                    6. **Pipeline Analytics**: Track deal progress and forecast revenue
                    7. **Custom Field Mapping**: Map fields between different CRM systems",
                PriceCredits = 100,
                Author = "SalesOps Pro",
                Category = "Sales & Marketing",
                Steps = 7,
                EstimatedTime = "20-30 minutes",
                Rating = 4.7,
                Runs = 6521,
                Icon = "users",
                Tags = "crm,sales,integration,salesforce,hubspot,automation",
                Prerequisites = "API credentials for your CRM systems",
                InputRequirements = "CRM API keys, field mapping configuration",
                OutputFormat = "Sync report, enriched contact database, activity logs"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Financial Report Generator",
                Description = "Generate comprehensive financial reports from raw accounting data",
                DetailedDescription = @"Transform your financial data into professional reports:
                    1. **Data Import**: Connect to QuickBooks, Xero, or upload Excel files
                    2. **Account Reconciliation**: Automatically match and reconcile transactions
                    3. **Financial Statements**: Generate P&L, Balance Sheet, Cash Flow
                    4. **Ratio Analysis**: Calculate key financial ratios and KPIs
                    5. **Variance Analysis**: Compare actuals vs budget/forecast
                    6. **Visualization**: Create charts and graphs for key metrics
                    7. **Compliance Check**: Ensure reports meet regulatory standards
                    8. **Export Options**: Generate PDF, Excel, or XBRL formats",
                PriceCredits = 80,
                Author = "FinTech Solutions",
                Category = "Finance",
                Steps = 8,
                EstimatedTime = "15-20 minutes",
                Rating = 4.6,
                Runs = 12843,
                Icon = "dollar-sign",
                Tags = "finance,accounting,reporting,compliance,analysis",
                Prerequisites = "Basic accounting knowledge helpful",
                InputRequirements = "Trial balance, GL data, or accounting software connection",
                OutputFormat = "PDF financial reports, Excel workbooks, XBRL files"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Social Media Analytics",
                Description = "Track and analyze your social media performance across platforms",
                DetailedDescription = @"Comprehensive social media analytics in one place:
                    1. **Multi-Platform Support**: Facebook, Instagram, Twitter, LinkedIn, TikTok
                    2. **Engagement Metrics**: Likes, shares, comments, reach, impressions
                    3. **Audience Demographics**: Age, location, interests, behavior
                    4. **Content Performance**: Identify top-performing posts and optimal timing
                    5. **Competitor Analysis**: Track competitor metrics and strategies
                    6. **Hashtag Analytics**: Discover trending and effective hashtags
                    7. **ROI Calculation**: Measure social media marketing effectiveness
                    8. **Custom Reports**: Create branded reports for clients or stakeholders",
                PriceCredits = 60,
                Author = "Social Analytics Co",
                Category = "Marketing",
                Steps = 8,
                EstimatedTime = "10-15 minutes",
                Rating = 4.5,
                Runs = 9876,
                Icon = "share-2",
                Tags = "social-media,analytics,marketing,engagement,reporting",
                Prerequisites = "Social media account access tokens",
                InputRequirements = "Social media API credentials or CSV exports",
                OutputFormat = "Interactive dashboard, PDF reports, CSV data exports"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "HR Onboarding Automation",
                Description = "Streamline employee onboarding with automated workflows",
                DetailedDescription = @"Complete employee onboarding workflow automation:
                    1. **Document Generation**: Create offer letters, contracts, and NDAs
                    2. **E-Signature Collection**: Send documents for digital signatures
                    3. **Background Checks**: Initiate and track verification processes
                    4. **IT Provisioning**: Create accounts and assign equipment
                    5. **Training Assignment**: Enroll in required courses and track progress
                    6. **Benefits Enrollment**: Guide through benefits selection
                    7. **Buddy Assignment**: Match with mentors and schedule meetings
                    8. **Progress Tracking**: Monitor onboarding completion status",
                PriceCredits = 90,
                Author = "HR Tech Solutions",
                Category = "Human Resources",
                Steps = 8,
                EstimatedTime = "30-45 minutes",
                Rating = 4.8,
                Runs = 5432,
                Icon = "user-plus",
                Tags = "hr,onboarding,automation,employee,training",
                Prerequisites = "HR system access, document templates",
                InputRequirements = "Employee data, role requirements, company policies",
                OutputFormat = "Onboarding checklist, signed documents, provisioning reports"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "E-commerce Inventory Sync",
                Description = "Synchronize inventory across multiple sales channels",
                DetailedDescription = @"Keep inventory perfectly synchronized everywhere:
                    1. **Multi-Channel Support**: Amazon, eBay, Shopify, WooCommerce, Etsy
                    2. **Real-Time Updates**: Instant inventory level synchronization
                    3. **Low Stock Alerts**: Automated notifications for reorder points
                    4. **Oversell Prevention**: Prevent selling out-of-stock items
                    5. **Bulk Updates**: Update prices and quantities across all channels
                    6. **SKU Mapping**: Handle different SKUs across platforms
                    7. **Order Consolidation**: Combine orders from all channels
                    8. **Analytics Dashboard**: Track sales velocity and turnover",
                PriceCredits = 70,
                Author = "E-commerce Pro",
                Category = "E-commerce",
                Steps = 8,
                EstimatedTime = "5-10 minutes",
                Rating = 4.7,
                Runs = 11234,
                Icon = "shopping-cart",
                Tags = "ecommerce,inventory,sync,multichannel,automation",
                Prerequisites = "Store API credentials",
                InputRequirements = "Product catalog, API keys for each platform",
                OutputFormat = "Sync reports, inventory levels, order consolidation file"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Customer Feedback Analysis",
                Description = "Analyze customer feedback and extract actionable insights",
                DetailedDescription = @"Transform customer feedback into business intelligence:
                    1. **Data Collection**: Import reviews, surveys, support tickets
                    2. **Sentiment Analysis**: Determine positive, negative, neutral sentiments
                    3. **Topic Modeling**: Identify common themes and issues
                    4. **Trend Detection**: Spot emerging patterns over time
                    5. **Priority Scoring**: Rank issues by impact and frequency
                    6. **Response Generation**: Create personalized response templates
                    7. **Competitor Comparison**: Analyze competitor reviews
                    8. **Action Items**: Generate prioritized improvement recommendations",
                PriceCredits = 55,
                Author = "Customer Success Team",
                Category = "Customer Service",
                Steps = 8,
                EstimatedTime = "15-20 minutes",
                Rating = 4.6,
                Runs = 7654,
                Icon = "message-circle",
                Tags = "feedback,analysis,sentiment,customer-service,insights",
                Prerequisites = "None",
                InputRequirements = "Customer reviews, survey responses, or support tickets",
                OutputFormat = "Sentiment report, topic analysis, action item list"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "SEO Content Optimizer",
                Description = "Optimize your content for search engines automatically",
                DetailedDescription = @"Boost your search rankings with AI-powered optimization:
                    1. **Content Analysis**: Evaluate current SEO performance
                    2. **Keyword Research**: Find high-value, low-competition keywords
                    3. **Content Suggestions**: Get AI-powered improvement recommendations
                    4. **Meta Tag Generation**: Create optimized titles and descriptions
                    5. **Schema Markup**: Add structured data for rich snippets
                    6. **Internal Linking**: Suggest relevant internal link opportunities
                    7. **Readability Check**: Ensure content is user-friendly
                    8. **Competitor Analysis**: Compare against top-ranking pages
                    9. **Performance Tracking**: Monitor ranking improvements",
                PriceCredits = 45,
                Author = "SEO Masters",
                Category = "Marketing",
                Steps = 9,
                EstimatedTime = "10-15 minutes",
                Rating = 4.7,
                Runs = 13567,
                Icon = "search",
                Tags = "seo,content,optimization,keywords,marketing",
                Prerequisites = "Basic SEO knowledge helpful",
                InputRequirements = "Website URL or content files",
                OutputFormat = "SEO report, optimized content, keyword recommendations"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Email Campaign Automation",
                Description = "Design, send, and track email marketing campaigns",
                DetailedDescription = @"Complete email marketing automation solution:
                    1. **Template Design**: Choose from professional templates or create custom
                    2. **List Segmentation**: Target specific audience segments
                    3. **Personalization**: Dynamic content based on subscriber data
                    4. **A/B Testing**: Test subject lines, content, and send times
                    5. **Send Optimization**: AI-powered best time to send
                    6. **Tracking & Analytics**: Open rates, clicks, conversions
                    7. **Automation Triggers**: Set up drip campaigns and workflows
                    8. **Compliance**: GDPR and CAN-SPAM compliance checks",
                PriceCredits = 40,
                Author = "Email Marketing Pro",
                Category = "Marketing",
                Steps = 8,
                EstimatedTime = "20-30 minutes",
                Rating = 4.5,
                Runs = 18765,
                Icon = "mail",
                Tags = "email,marketing,automation,campaigns,analytics",
                Prerequisites = "Email list, sender verification",
                InputRequirements = "Email list CSV, campaign content",
                OutputFormat = "Campaign reports, engagement analytics, conversion data"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Contract Review Assistant",
                Description = "AI-powered contract analysis and risk assessment",
                DetailedDescription = @"Intelligent contract review and analysis:
                    1. **Text Extraction**: Extract text from PDF and Word contracts
                    2. **Clause Identification**: Identify key contractual clauses
                    3. **Risk Assessment**: Flag potential risks and unfavorable terms
                    4. **Compliance Check**: Verify regulatory compliance
                    5. **Missing Clauses**: Identify important missing provisions
                    6. **Comparison**: Compare against standard templates
                    7. **Summary Generation**: Create executive summaries
                    8. **Negotiation Points**: Suggest areas for negotiation",
                PriceCredits = 85,
                Author = "Legal Tech Inc",
                Category = "Legal",
                Steps = 8,
                EstimatedTime = "15-25 minutes",
                Rating = 4.8,
                Runs = 4321,
                Icon = "file-text",
                Tags = "legal,contracts,review,ai,risk-assessment",
                Prerequisites = "Basic contract knowledge helpful",
                InputRequirements = "Contract documents (PDF or Word)",
                OutputFormat = "Risk assessment report, clause analysis, executive summary"
            },
            new WorkflowEntity
            {
                Id = Guid.NewGuid(),
                Name = "Project Timeline Generator",
                Description = "Create detailed project timelines and Gantt charts automatically",
                DetailedDescription = @"Professional project planning made easy:
                    1. **Task Import**: Import tasks from Excel, Trello, or Asana
                    2. **Dependency Mapping**: Automatically identify task dependencies
                    3. **Resource Allocation**: Assign team members and track capacity
                    4. **Critical Path**: Calculate and highlight critical path
                    5. **Milestone Tracking**: Set and monitor key milestones
                    6. **Risk Analysis**: Identify potential delays and bottlenecks
                    7. **Gantt Chart**: Generate interactive Gantt charts
                    8. **Export Options**: Export to MS Project, PDF, or online tools",
                PriceCredits = 35,
                Author = "PM Tools Co",
                Category = "Project Management",
                Steps = 8,
                EstimatedTime = "10-15 minutes",
                Rating = 4.6,
                Runs = 9876,
                Icon = "calendar",
                Tags = "project-management,timeline,gantt,planning,scheduling",
                Prerequisites = "Project task list",
                InputRequirements = "Task list with durations and dependencies",
                OutputFormat = "Interactive Gantt chart, project timeline, critical path analysis"
            }
        };

        _db.Workflows.AddRange(workflows);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation($"Seeded {workflows.Length} workflows");
    }
}