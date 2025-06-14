using System.ComponentModel;
using System.Data;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Infrastructure.Ai.SemanticKernel.Plugins;

/// <summary>
///     MathEnginePlugin – a comprehensive native-code plugin that exposes
///     both elementary and aggregate mathematical operations required for
///     typical tax-analysis scenarios.
/// </summary>
public sealed class MathEnginePlugin(
    ILogger<MathEnginePlugin> logger,
    IActivityPublisher activityPublisher,
    IChatCompletionService chatCompletion)
{
    private readonly IActivityPublisher _activityPublisher =
        activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));

    private readonly IChatCompletionService _chatCompletion =
        chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));

    private readonly ILogger<MathEnginePlugin> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private async Task PublishToolAsync(string tool, object parameters, object result)
    {
        await _activityPublisher.PublishAsync("tool_invocation", new { tool, parameters, result });
    }

    private void PublishTool(string tool, object parameters, object result)
    {
        try
        {
            _activityPublisher.PublishAsync("tool_invocation", new { tool, parameters, result }).GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish tool invocation for {Tool}", tool);
            // Swallow exception to prevent telemetry issues from breaking core functionality
        }
    }

    /* ----------------------------------------------------------------
     *  Elementary arithmetic
     * -------------------------------------------------------------- */

    [KernelFunction("add")]
    [Description("Adds two numbers and returns the sum.")]
    public double Add(
        [Description("First addend.")] double a,
        [Description("Second addend.")] double b)
    {
        var result = a + b;
        PublishTool("add", new { a, b }, result);
        return result;
    }

    [KernelFunction("subtract")]
    [Description("Subtracts the second number from the first.")]
    public double Subtract(
        [Description("Minuend.")] double a,
        [Description("Subtrahend.")] double b)
    {
        var result = a - b;
        PublishTool("subtract", new { a, b }, result);
        return result;
    }

    [KernelFunction("multiply")]
    [Description("Multiplies two numbers and returns the product.")]
    public double Multiply(
        [Description("First factor.")] double a,
        [Description("Second factor.")] double b)
    {
        var result = a * b;
        PublishTool("multiply", new { a, b }, result);
        return result;
    }

    [KernelFunction("divide")]
    [Description("Divides the dividend by the divisor. Throws on zero divisor.")]
    public double Divide(
        [Description("Dividend.")] double a,
        [Description("Divisor (non-zero).")] double b)
    {
        var result = b == 0
            ? throw new DivideByZeroException("Cannot divide by zero.")
            : a / b;
        PublishTool("divide", new { a, b }, result);
        return result;
    }

    [KernelFunction("percentage_of")]
    [Description("Returns {value} × {percent} / 100.")]
    public double PercentageOf(
        [Description("Base value.")] double value,
        [Description("Percent (e.g. 20 for 20 %).")]
        double percent)
    {
        var result = value * percent / 100.0;
        PublishTool("percentage_of", new { value, percent }, result);
        return result;
    }

    [KernelFunction("increase_by_percent")]
    [Description("Applies growth: value × (1 + percent/100).")]
    public double IncreaseByPercent(
        [Description("Original value.")] double value,
        [Description("Percent growth (e.g. 5 for 5 %).")]
        double percent)
    {
        var result = value * (1 + percent / 100.0);
        PublishTool("increase_by_percent", new { value, percent }, result);
        return result;
    }

    /* ----------------------------------------------------------------
     *  Percentage helpers
     * -------------------------------------------------------------- */

    [KernelFunction("percent_change")]
    [Description("Returns (new – old) ÷ old × 100.")]
    public double PercentChange(
        [Description("Original value.")] double oldValue,
        [Description("New value.")] double newValue)
    {
        var result = oldValue == 0 ? 0 : (newValue - oldValue) * 100.0 / oldValue;
        PublishTool("percent_change", new { oldValue, newValue }, result);
        return result;
    }

    /* ----------------------------------------------------------------
     *  Aggregate helpers – accept a JSON or CSV list of numbers
     * -------------------------------------------------------------- */

    [KernelFunction("sum")]
    [Description("Returns the sum of all numbers in the provided list.")]
    public double Sum(
        [Description("Numbers as CSV or JSON array, e.g. \"1,2,3\" or \"[1,2,3]\".")]
        string numbers)
    {
        var result = Parse(numbers).Sum();
        PublishTool("sum", new { numbers }, result);
        return result;
    }

    [KernelFunction("average")]
    [Description("Returns the arithmetic mean of the numbers in the list.")]
    public double Average(
        [Description("Numbers as CSV or JSON array.")]
        string numbers)
    {
        var vals = Parse(numbers);
        var result = !vals.Any() ? 0 : vals.Average();
        PublishTool("average", new { numbers }, result);
        return result;
    }

    [KernelFunction("max")]
    [Description("Returns the maximum value in the list.")]
    public double Max(
        [Description("Numbers as CSV or JSON array.")]
        string numbers)
    {
        var result = Parse(numbers).DefaultIfEmpty().Max();
        PublishTool("max", new { numbers }, result);
        return result;
    }

    [KernelFunction("count_greater_than")]
    [Description("Counts how many numbers exceed the specified threshold.")]
    public int CountGreaterThan(
        [Description("Numbers as CSV or JSON array.")]
        string numbers,
        [Description("Threshold.")] double threshold)
    {
        var result = Parse(numbers).Count(n => n > threshold);
        PublishTool("count_greater_than", new { numbers, threshold }, result);
        return result;
    }

    /* ----------------------------------------------------------------
     *  Tax-specific helpers
     * -------------------------------------------------------------- */

    [KernelFunction("net_tax")]
    [Description("Computes Net Tax = (income − deductions) × rate %.")]
    public double NetTax(
        [Description("Gross income.")] double income,
        [Description("Total deductions.")] double deductions,
        [Description("Tax rate in percent.")] double ratePercent)
    {
        var result = (income - deductions) * ratePercent / 100.0;
        PublishTool("net_tax", new { income, deductions, ratePercent }, result);
        return result;
    }

    [KernelFunction("deduction_ratio")]
    [Description("Returns deductions ÷ income as a decimal ratio rounded to 4 decimals.")]
    public double DeductionRatio(
        [Description("Gross income.")] double income,
        [Description("Total deductions.")] double deductions)
    {
        double result;
        if (income == 0) result = 0;
        else result = Math.Round(deductions / income, 4);
        PublishTool("deduction_ratio", new { income, deductions }, result);
        return result;
    }

    [KernelFunction("effective_tax_rate")]
    [Description("Returns Net Tax ÷ Income as a decimal ratio rounded to 4 decimals.")]
    public double EffectiveTaxRate(
        [Description("Net Tax paid.")] double netTax,
        [Description("Gross income.")] double income)
    {
        double result;
        if (income == 0) result = 0;
        else result = Math.Round(netTax / income, 4);
        PublishTool("effective_tax_rate", new { netTax, income }, result);
        return result;
    }

    /* ----------------------------------------------------------------
     *  Internal helpers
     * -------------------------------------------------------------- */

    private static double[] Parse(string raw)
    {
        // Accept JSON array or plain CSV
        raw = raw.Trim();
        if (raw.StartsWith('[') && raw.EndsWith(']'))
            raw = raw[1..^1];

        return raw.Split(new[] { ',', ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(double.Parse)
            .ToArray();
    }

    /* ----------------------------------------------------------------
     *  Extended statistical and expression evaluation helpers
     * -------------------------------------------------------------- */
    [KernelFunction("median")]
    [Description("Returns the median value of the provided list of numbers.")]
    public double Median(
        [Description("Numbers as CSV or JSON array.")]
        string numbers)
    {
        var vals = Parse(numbers).OrderBy(n => n).ToArray();
        var result = default(double);
        var count = vals.Length;
        if (count == 0) result = 0;
        else if (count % 2 == 1) result = vals[count / 2];
        else result = (vals[count / 2 - 1] + vals[count / 2]) / 2.0;
        PublishTool("median", new { numbers }, result);
        return result;
    }

    [KernelFunction("stddev")]
    [Description("Returns the population standard deviation of the provided list of numbers.")]
    public double StdDev(
        [Description("Numbers as CSV or JSON array.")]
        string numbers)
    {
        var vals = Parse(numbers);
        double result;
        if (!vals.Any())
        {
            result = 0;
        }
        else
        {
            var mean = vals.Average();
            var sumSq = vals.Select(v => (v - mean) * (v - mean)).Sum();
            result = Math.Sqrt(sumSq / vals.Length);
        }

        PublishTool("stddev", new { numbers }, result);
        return result;
    }

    [KernelFunction("percentile")]
    [Description("Returns the specified percentile (0-100) of the provided list of numbers.")]
    public double Percentile(
        [Description("Numbers as CSV or JSON array.")]
        string numbers,
        [Description("Percentile to compute (0-100).")]
        double percentile)
    {
        var vals = Parse(numbers).OrderBy(n => n).ToArray();
        double result;
        var count = vals.Length;
        if (count == 0)
        {
            result = 0;
        }
        else if (percentile <= 0)
        {
            result = vals.First();
        }
        else if (percentile >= 100)
        {
            result = vals.Last();
        }
        else
        {
            var rank = percentile / 100.0 * (count - 1);
            var lower = (int)Math.Floor(rank);
            var upper = (int)Math.Ceiling(rank);
            if (lower == upper)
            {
                result = vals[lower];
            }
            else
            {
                var fraction = rank - lower;
                result = vals[lower] + (vals[upper] - vals[lower]) * fraction;
            }
        }

        PublishTool("percentile", new { numbers, percentile }, result);
        return result;
    }

    [KernelFunction("eval")]
    [Description(
        "Evaluates a simple arithmetic expression and returns the result. Supports +, -, *, /, and parentheses.")]
    public double Eval(
        [Description("Arithmetic expression to evaluate.")]
        string expression)
    {
        try
        {
            var table = new DataTable();
            table.Columns.Add("expression", typeof(double), expression);
            var row = table.NewRow();
            table.Rows.Add(row);
            var result = (double)row["expression"];
            PublishTool("eval", new { expression }, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating expression: {Expression}", expression);
            throw;
        }
    }
}