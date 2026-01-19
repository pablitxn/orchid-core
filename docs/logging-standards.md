# Logging Standards

## Purpose

Use consistent, structured logs across the codebase to make debugging, observability, and auditing reliable.

## 1. Log Levels and When to Use Them

| Level | When to Use | Example |
|-------|-------------|---------|
| `Trace` | Very detailed debugging, never in production | Variable values inside tight loops |
| `Debug` | Useful for development diagnostics | “Entering method X with params Y” |
| `Information` | Significant business events | “User logged in”, “Order created” |
| `Warning` | Unexpected but handled situations | “Retry attempt 2 of 3”, “Cache miss” |
| `Error` | Errors that impact an operation | “Failed to connect to DB”, “API timeout” |
| `Critical` | Requires immediate attention | “Application shutting down”, “Data corruption” |

## 2. Structured Logging Best Practices

```csharp
// ✅ Correct - structured logging
_logger.LogInformation("User {UserId} created order {OrderId} for {Amount}",
    userId, orderId, amount);

// ❌ Incorrect - string interpolation
_logger.LogInformation($"User {userId} created order {orderId} for {amount}");
```

## 3. What to Log and What Not to Log

**Log:**
- Start/end of business operations
- Errors and exceptions with context
- Performance metrics (operation duration)
- Security events (login, logout, access denied)

**Do NOT log:**
- Sensitive data (passwords, tokens, PII)
- Redundant information
- Logs inside high-frequency loops
- Full stack traces in production (except critical incidents)

## 4. Naming Conventions and Event IDs

```csharp
// Use the class name as the category
private readonly ILogger<MyService> _logger;
```

For important logs, use event IDs and keep them centralized in the Application layer (e.g.
`services/backend/src/Core/Application/Logging/EventIds.cs`) so identifiers stay stable.

```csharp
// Example event ID usage
_logger.LogInformation(EventIds.OrderCreated, "Order {OrderId} created", orderId);
```

## 5. Performance Considerations

```csharp
// ✅ Correct - check level before expensive work
if (_logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("Complex data: {Data}", ComputeExpensiveData());
}

// ❌ Incorrect - always computes even when not logged
_logger.LogDebug("Complex data: {Data}", ComputeExpensiveData());
```

## 6. Layer Examples

### Domain Layer

```csharp
public sealed class PricingService
{
    private readonly ILogger<PricingService> _logger;

    public PricingService(ILogger<PricingService> logger) => _logger = logger;

    public Money CalculatePrice(Guid productId, Money basePrice)
    {
        _logger.LogDebug("Calculating price for {ProductId}", productId);
        return basePrice;
    }
}
```

### Application Layer

```csharp
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly ILogger<CreateOrderHandler> _logger;

    public async Task<Guid> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var orderId = await _service.CreateAsync(command, ct);
        _logger.LogInformation(EventIds.OrderCreated, "Order {OrderId} created", orderId);
        return orderId;
    }
}
```

### Adapters Layer (WebApi / Infrastructure)

```csharp
public sealed class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        _logger.LogInformation("Incoming order request {RequestId}", request.RequestId);
        // ...
        return Accepted();
    }
}
```
