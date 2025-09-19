# Generated Methods vs Data-Driven Approaches

## Overview

This document explores the architectural trade-off between generating specific methods for each rule versus using a single data-driven method with conditional logic. This fundamental design decision affects performance, maintainability, type safety, and debugging across many software systems.

## The Two Approaches

### Approach 1: Generated Specific Methods
Each rule gets its own dedicated method with specific implementations:

```csharp
// ArchGuard approach - many specific methods
public static async Task<string> ValidateDependencyRegistrationAsync(
    IMcpServer server,
    ContextFile[] contextFiles,
    string[]? diffs = null,
    string rootFromWebhook = "")
{
    // Specific implementation for dependency registration validation
}

public static async Task<string> ValidateAsyncNamingAsync(
    IMcpServer server,
    ContextFile[] contextFiles,
    string[]? diffs = null,
    string rootFromWebhook = "")
{
    // Specific implementation for async naming validation
}

public static async Task<string> ValidateSecurityPatternsAsync(
    IMcpServer server,
    ContextFile[] contextFiles,
    string[]? diffs = null,
    string rootFromWebhook = "")
{
    // Specific implementation for security pattern validation
}
```

### Approach 2: Data-Driven with Conditional Logic
One generic method that dispatches based on rule name:

```csharp
// Data-driven approach - one generic method
public static async Task<string> ExecuteRuleAsync(
    string ruleName,
    IMcpServer server,
    ContextFile[] contextFiles,
    string[]? diffs = null,
    string rootFromWebhook = "")
{
    return ruleName switch
    {
        "ValidateDependencyRegistration" => await ExecuteDependencyRule(server, contextFiles, diffs, rootFromWebhook),
        "ValidateAsyncNaming" => await ExecuteAsyncRule(server, contextFiles, diffs, rootFromWebhook),
        "ValidateSecurityPatterns" => await ExecuteSecurityRule(server, contextFiles, diffs, rootFromWebhook),
        _ => throw new ArgumentException($"Unknown rule: {ruleName}")
    };
}

// Or with a lookup table:
private static readonly Dictionary<string, Func<IMcpServer, ContextFile[], string[]?, string, Task<string>>> RuleMap = new()
{
    ["ValidateDependencyRegistration"] = ExecuteDependencyRule,
    ["ValidateAsyncNaming"] = ExecuteAsyncRule,
    ["ValidateSecurityPatterns"] = ExecuteSecurityRule
};

public static async Task<string> ExecuteRuleAsync(string ruleName, ...)
{
    if (!RuleMap.TryGetValue(ruleName, out var ruleFunc))
        throw new ArgumentException($"Unknown rule: {ruleName}");
    
    return await ruleFunc(server, contextFiles, diffs, rootFromWebhook);
}
```

## Comprehensive Trade-off Analysis

| Aspect | Generated Methods | Data-Driven Switch |
|--------|------------------|-------------------|
| **Type Safety** | ✅ Compile-time validation | ❌ Runtime string validation |
| **Performance** | ✅ Direct method calls (fastest) | ⚠️ String comparison/lookup overhead |
| **Debugging** | ✅ Clear, specific stack traces | ❌ Generic call sites in stack traces |
| **Code Duplication** | ❌ High structural duplication | ✅ Single implementation path |
| **Adding New Rules** | ❌ Code generation/modification needed | ✅ Just add to switch/map |
| **IntelliSense Support** | ✅ Full autocomplete and navigation | ❌ Magic strings, no autocomplete |
| **Refactoring Safety** | ✅ Find/replace, rename refactoring works | ❌ String references easily missed |
| **Memory Usage** | ⚠️ More methods in memory | ✅ Single method + lookup structure |
| **Build-time Validation** | ✅ Missing implementations = compile error | ❌ Missing rules = runtime error |
| **Code Readability** | ✅ Self-documenting method names | ⚠️ Need to look at switch/map |
| **Unit Testing** | ✅ Test each method independently | ⚠️ Test through generic entry point |
| **API Documentation** | ✅ Each method gets own documentation | ❌ Generic method with rule parameter |

## Real-World Framework Examples

### How API Routing Actually Works

Despite using string-based route definitions, modern frameworks compile them to efficient structures:

#### ASP.NET Core Routing
```csharp
// What developers write:
[Route("api/[controller]/[action]")]
public class UsersController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult GetUser(int id) { /* implementation */ }
    
    [HttpPost]
    public IActionResult CreateUser([FromBody] User user) { /* implementation */ }
}

// What the framework compiles to (simplified):
internal static class CompiledRoutes
{
    public static readonly RouteEntry[] Routes = {
        new RouteEntry("GET", "api/users/getuser/{id}", typeof(UsersController), nameof(UsersController.GetUser)),
        new RouteEntry("POST", "api/users/createuser", typeof(UsersController), nameof(UsersController.CreateUser)),
        // ... more routes
    };
}

// Runtime matching (highly optimized):
public IActionResult MatchRoute(string path, string method)
{
    // Uses radix trees, compiled regex, and other optimizations
    foreach (var route in CompiledRoutes.Routes)
    {
        if (route.FastMatch(path, method)) // Optimized matching
            return route.InvokeAction(); // Direct method invocation
    }
    return NotFound();
}
```

**Key Insight**: Even "data-driven" frameworks ultimately call specific methods. The routing is data-driven, but execution is method-specific.

#### Express.js (Node.js)
```javascript
// What developers write:
app.get('/api/users/:id', getUserHandler);
app.post('/api/users', createUserHandler);

// What happens internally:
const routes = [
    { method: 'GET', pattern: /^\/api\/users\/(\d+)$/, handler: getUserHandler },
    { method: 'POST', pattern: /^\/api\/users$/, handler: createUserHandler }
];

// Runtime matching:
function matchRoute(method, path) {
    for (const route of routes) {
        if (route.method === method && route.pattern.test(path)) {
            return route.handler; // Still calls specific function
        }
    }
    return notFoundHandler;
}
```

## When Each Approach Wins

### Generated Methods Excel When:

#### Performance is Critical
- **Direct method calls** - no runtime lookup overhead
- **JIT optimization** - each method can be optimized independently
- **Hotpath efficiency** - critical for systems processing many requests

#### Type Safety Matters
- **Compile-time validation** - catch errors before deployment
- **Refactoring safety** - IDE can track all references
- **API contracts** - clear method signatures document expectations

#### Complex Domain Logic
- **Rule-specific parameters** - each rule might need different inputs
- **Specialized error handling** - each rule might fail differently
- **Domain-specific optimizations** - each rule can be optimized for its use case

#### Debugging is Important
- **Clear stack traces** - immediately see which specific rule failed
- **Targeted breakpoints** - set breakpoints on specific rules
- **Profiling clarity** - performance tools show specific method bottlenecks

#### Rules Change Infrequently
- **Architecture rules** - typically stable, added occasionally
- **Business logic** - core business rules don't change often
- **API endpoints** - REST APIs tend to be stable once defined

### Data-Driven Excels When:

#### High Rule Volatility
- **Frequent additions** - new rules added daily/weekly
- **User-configurable rules** - end users can define custom rules
- **A/B testing scenarios** - rules enabled/disabled dynamically

#### Simple, Uniform Logic
- **Validation rules** - similar structure across all rules
- **CRUD operations** - generic create/read/update/delete patterns
- **Configuration processing** - applying settings uniformly

#### Runtime Configuration Needed
- **Feature flags** - enable/disable rules without deployment
- **Multi-tenant systems** - different rules per tenant
- **Environment-specific rules** - different rules per dev/staging/prod

#### Code Size Constraints
- **Embedded systems** - memory limitations favor smaller codebases
- **Mobile applications** - app size affects download/installation
- **Microservices** - when service size matters for deployment

## Real-World Examples by Category

### Generated Approach Success Stories

#### gRPC Service Methods
```csharp
// Each RPC gets its own strongly-typed method
public class UserService : UserServiceBase
{
    public override async Task<GetUserResponse> GetUser(GetUserRequest request, ServerCallContext context) { }
    public override async Task<CreateUserResponse> CreateUser(CreateUserRequest request, ServerCallContext context) { }
    public override async Task<UpdateUserResponse> UpdateUser(UpdateUserRequest request, ServerCallContext context) { }
}
```
**Why this works**: Type safety, performance, clear contracts, infrequent changes.

#### Entity Framework Entity Classes
```csharp
// Each table gets its own class (often generated)
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
}
```
**Why this works**: Type safety, IntelliSense, compile-time validation, database schemas are stable.

#### API Client Libraries
```csharp
// Each API endpoint gets its own method
public class GitHubApiClient
{
    public async Task<User> GetUserAsync(string username) { }
    public async Task<Repository[]> GetUserRepositoriesAsync(string username) { }
    public async Task<Issue[]> GetRepositoryIssuesAsync(string owner, string repo) { }
}
```
**Why this works**: Clear documentation, type safety, stable API contracts.

### Data-Driven Success Stories

#### Validation Frameworks
```csharp
// FluentValidation - rules defined declaratively
public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(1, 100);
        RuleFor(x => x.Email).EmailAddress();
        RuleFor(x => x.Age).GreaterThan(0).LessThan(150);
    }
}
```
**Why this works**: Many similar rules, frequent changes, domain-specific language.

#### Business Rule Engines
```csharp
// Rules defined in configuration/database
var rules = new[]
{
    new Rule { Condition = "customer.Age >= 18", Action = "ApproveCredit" },
    new Rule { Condition = "order.Amount > 1000", Action = "RequireApproval" },
    new Rule { Condition = "user.LoginAttempts > 3", Action = "LockAccount" }
};
```
**Why this works**: Business users can modify rules, frequent changes, runtime configuration.

#### Generic CRUD Controllers
```csharp
// One controller handles all entity types
[Route("api/[controller]")]
public class GenericController<T> : ControllerBase where T : class
{
    [HttpGet]
    public async Task<IActionResult> GetAll() { /* generic implementation */ }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] T entity) { /* generic implementation */ }
}
```
**Why this works**: Uniform behavior, reduces boilerplate, many similar entities.

## The Configuration vs Code Spectrum

This decision sits on a fundamental spectrum in software architecture:

```
Pure Configuration ←→ Hybrid Approaches ←→ Pure Code
        ↑                    ↑                  ↑
   Flexible,          Best of both        Performant,
   Runtime           worlds              Type-safe
```

### Pure Configuration
- **Examples**: JSON rule definitions, database-driven workflows
- **Benefits**: Maximum flexibility, non-developers can modify
- **Costs**: Runtime overhead, harder debugging, no type safety

### Hybrid Approaches
- **Examples**: Code generation from templates, compiled DSLs
- **Benefits**: Flexibility during development, performance at runtime
- **Costs**: Build complexity, additional tooling needed

### Pure Code
- **Examples**: Hand-written methods, compiled implementations
- **Benefits**: Maximum performance, full type safety, best tooling
- **Costs**: Less flexible, requires developer changes

## Recommendation for ArchGuard Rules

**Stick with generated specific methods** because ArchGuard rules have these characteristics:

### Performance Matters
- Validation runs on **every commit/PR** in CI/CD pipelines
- Multiple rules may run simultaneously
- Claude Code execution is already expensive - don't add lookup overhead

### Type Safety is Valuable
- **Compile-time validation** catches integration errors early
- **Refactoring support** helps maintain consistency across rules
- **API contracts** make integration easier for consumers

### Rules are Complex, Not Simple
Each rule has:
- **Different Claude prompts** - not just parameter variations
- **Different validation logic** - architectural patterns vary significantly
- **Different error patterns** - each rule fails in unique ways
- **Different complexity** - some rules are simple, others very complex

### Rules Change Infrequently
- **Architectural rules are stable** - maybe 1-2 new rules per year
- **Framework changes are rare** - .NET major versions every 1-2 years
- **Generation cost is acceptable** - infrequent regeneration is fine

### Debugging is Critical
- When validation fails in CI/CD, developers need **clear error traces**
- **Specific rule names** in stack traces help identify problems quickly
- **Targeted fixes** are easier with method-specific implementations

### The "Duplication" Isn't Real Duplication
The structural similarity between rules is **infrastructure**, not business logic:
- **Claude Code execution pattern** - this is infrastructure, should be consistent
- **Error handling pattern** - this is infrastructure, should be consistent  
- **Rule-specific logic** - prompts, validation criteria, error messages are all different

This is similar to how API controllers have similar structure but different business logic - the structure is framework boilerplate, not duplicated domain logic.

## The Big Switch Statement Problem

Your intuition about the "big switch statement" is exactly right. Data-driven approaches inevitably become:

```csharp
// This grows with every new rule
string result = ruleName switch
{
    "ValidateDependencyRegistration" => await ValidateDependencyReg(...),
    "ValidateAsyncNaming" => await ValidateAsyncNaming(...),
    "ValidateSecurityPatterns" => await ValidateSecurityPatterns(...),
    "ValidateNullableUsage" => await ValidateNullableUsage(...),
    "ValidateExceptionHandling" => await ValidateExceptionHandling(...),
    "ValidateLoggingPatterns" => await ValidateLoggingPatterns(...),
    // ... 20 more rules ...
    _ => throw new ArgumentException($"Unknown rule: {ruleName}")
};
```

**Problems with this approach:**
1. **Maintenance burden** - every new rule requires modifying the central switch
2. **Merge conflicts** - multiple developers adding rules hit the same switch statement
3. **Testing complexity** - need to test the switch logic plus individual rules
4. **Runtime errors** - typos in rule names cause runtime failures
5. **No IntelliSense** - IDE can't help with rule name strings

**Generated methods avoid all these problems** - each rule is independent, no central coordination point needed.

## Conclusion

For ArchGuard's architectural validation rules, **generated specific methods are the superior choice** because:

1. **Performance requirements** - CI/CD validation needs to be fast
2. **Type safety benefits** - compile-time validation prevents integration errors
3. **Debugging needs** - clear stack traces are critical for developer productivity
4. **Rule complexity** - architectural rules are sophisticated, not simple data validation
5. **Change frequency** - rules are added infrequently, making generation cost acceptable
6. **No real duplication** - structural similarity is infrastructure, not business logic

The data-driven approach makes sense for systems with frequent rule changes, simple uniform logic, or runtime configuration needs. But for architectural validation, the benefits of specific methods far outweigh the costs.

This decision aligns with how successful frameworks handle similar problems - they use configuration for flexibility where needed, but ultimately execute through specific, optimized code paths.