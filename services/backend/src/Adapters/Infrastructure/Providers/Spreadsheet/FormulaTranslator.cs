using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Translates natural language queries to Excel formulas using LLM.
/// </summary>
public sealed class FormulaTranslator(
    IChatCompletionPort chatCompletion,
    ILogger<FormulaTranslator> logger) : IFormulaTranslator
{
    private readonly IChatCompletionPort _chatCompletion = chatCompletion;
    private readonly ILogger<FormulaTranslator> _logger = logger;

    public async Task<FormulaTranslation> TranslateAsync(
        string query,
        WorkbookSummary summary,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Translating query: {Query}", query);
        
        var prompt = BuildPrompt(query, summary);
        var functionSchema = GetFunctionSchema();
        
        try
        {
            var messages = new[]
            {
                new ChatMessage("system", "You are an expert Excel formula translator. Generate precise Excel formulas based on user queries and workbook metadata."),
                new ChatMessage("user", prompt)
            };
            
            var response = await _chatCompletion.CompleteAsync(messages, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new InvalidOperationException("Empty response from LLM");
            }
            
            var translation = ParseResponse(response);
            
            _logger.LogInformation("Generated formula: {Formula}", translation.Formula);
            
            return translation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating query to formula");
            return new FormulaTranslation
            {
                Formula = string.Empty,
                Explanation = "Error generating formula",
                NeedsClarification = true,
                ClarificationPrompt = "Could you please rephrase your question?"
            };
        }
    }

    private static string BuildPrompt(string query, WorkbookSummary summary)
    {
        return $"""
            User Query: {query}
            
            Workbook Context:
            Sheet: {summary.SheetName}
            Total Rows: {summary.TotalRows}
            
            Available Columns (use these exact aliases in formulas):
            {string.Join("\n", summary.Columns.Select(c => 
                $"- {c.Alias} (type: {c.DataType}, original: {c.Original})"))}
            
            Column Statistics:
            {JsonSerializer.Serialize(summary.Columns.Select(c => new
            {
                column = c.Alias,
                type = c.DataType,
                min = c.Min,
                max = c.Max,
                mean = c.Mean,
                uniqueCount = c.UniqueCount,
                topValues = c.TopValues.Take(3)
            }), new JsonSerializerOptions { WriteIndented = true })}
            
            Instructions:
            1. Generate an Excel formula to answer the user's query
            2. Use column aliases from the list above (e.g., Date, Customer, Total)
            3. Reference the main table range as needed
            4. If the query is ambiguous, set needsClarification to true
            5. Provide a clear explanation of what the formula does
            
            Return JSON only:
                "formula": "=YOUR_FORMULA_HERE",
                "explanation": "Brief explanation of what the formula does",
                "needsClarification": false,
                "clarificationPrompt": null
            """;
    }

    private static string GetFunctionSchema()
    {
        return """
            {
                "name": "generate_formula",
                "description": "Generate an Excel formula from natural language",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "formula": {
                            "type": "string",
                            "description": "The Excel formula in A1 notation"
                        },
                        "explanation": {
                            "type": "string",
                            "description": "Explanation of what the formula does"
                        },
                        "needsClarification": {
                            "type": "boolean",
                            "description": "Whether the query needs clarification"
                        },
                        "clarificationPrompt": {
                            "type": "string",
                            "description": "Question to ask user for clarification"
                        }
                    },
                    "required": ["formula", "explanation", "needsClarification"]
                }
            }
            """;
    }

    private static FormulaTranslation ParseResponse(string response)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                return new FormulaTranslation
                {
                    Formula = root.GetProperty("formula").GetString() ?? string.Empty,
                    Explanation = root.GetProperty("explanation").GetString() ?? string.Empty,
                    NeedsClarification = root.TryGetProperty("needsClarification", out var needsClarif) && needsClarif.GetBoolean(),
                    ClarificationPrompt = root.TryGetProperty("clarificationPrompt", out var clarif) ? clarif.GetString() : null
                };
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - we'll return a default response
        }
        
        // Fallback parsing
        return new FormulaTranslation
        {
            Formula = ExtractFormula(response),
            Explanation = "Formula generated from query",
            NeedsClarification = false
        };
    }

    private static string ExtractFormula(string response)
    {
        // Simple heuristic to extract formula
        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("="))
            {
                return trimmed;
            }
        }
        
        return "=ERROR(\"Could not generate formula\")";
    }
}