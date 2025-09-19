# T4 Templates vs Living Template Approach

## Overview

This document compares T4 (Text Template Transformation Toolkit) with our "living template" approach for code generation, specifically addressing whether T4 can achieve the same benefits as our living template system.

## T4 Templates - Technical Analysis

### What T4 Is

T4 (Text Template Transformation Toolkit) is Microsoft's code generation framework that uses template files (`.tt` files) with embedded C# code to generate text output, typically source code.

**Basic T4 Structure:**
```xml
<#@ template language="C#" #>
<#@ output extension=".cs" #>
<#@ import namespace="System.IO" #>
<#
    // C# code for logic
    string className = "MyClass";
#>
namespace MyNamespace
{
    public class <#= className #>
    {
        // Generated code here
    }
}
```

### T4 Technical Capabilities

T4 templates **can** read from external sources:
- **Database schemas** (most common use case)
- **XML/JSON configuration files**
- **External text files**
- **Assembly metadata**
- **Theoretically: existing C# source code files**

### Why T4 is Typically Used

1. **Database-First Generation**: Schema → Entity classes
2. **Configuration-Driven**: Settings files → code implementations
3. **Design-Time Models**: UML/DSL → runtime code
4. **API Generation**: OpenAPI specs → client code

## Can T4 Be a "Living Template"?

### Theoretical Possibility

Yes, T4 **could theoretically** read from working C# code files:

```xml
<#@ template language="C#" #>
<#@ import namespace="System.IO" #>
<#@ import namespace="System.Text.RegularExpressions" #>
<#
    // Read the working ValidateDependencyRegistration method
    string sourceCode = File.ReadAllText("ArchValidationTool.cs");
    
    // Parse out method signature, attributes, etc.
    // This gets complex quickly...
    var methodMatch = Regex.Match(sourceCode, @"public static async Task<string> ValidateDependencyRegistrationAsync\((.*?)\)");
    
    // Extract rule name, description, etc.
    // More complex parsing...
#>

// Generate new rule based on extracted patterns
<#= GenerateNewRule("ValidateAsyncNaming", extractedPatterns) #>
```

### Practical Problems with T4 as Living Template

#### 1. Parsing Complexity
T4 would need to parse C# code to extract:
- Method signatures and parameters
- Attribute decorations and their parameters
- XML documentation comments
- Error handling patterns
- Claude prompt text embedded in strings
- Class structures and inheritance

**Reality**: This becomes a complex C# parser implementation within T4, which is error-prone and fragile.

#### 2. Template File Maintenance
Even if T4 reads from working code, you still have a **separate T4 template file** (`.tt`) that:
- Contains the parsing logic
- Defines the generation patterns
- Must be maintained separately from working code
- Can still drift from current framework requirements

#### 3. Framework Compatibility Issues
When .NET v10 changes how attributes work:
- T4 template parsing logic may break
- T4 template output patterns may become invalid
- You still need to update the `.tt` file manually
- **Template drift problem persists**

#### 4. Industry Practice Mismatch
T4 is designed for **data-to-code** transformation, not **code-to-code** transformation:
- **Good fit**: Database schema → Entity classes
- **Poor fit**: Working C# code → Similar C# code patterns

#### 5. Debugging and Maintenance Burden
- T4 templates are harder to debug than regular C#
- Complex parsing logic in T4 is difficult to maintain
- Changes require understanding both C# and T4 syntax
- Generated code debugging traces back to T4, not source

### Example: Why T4 Gets Complicated

Let's say you want T4 to extract this method signature:

```csharp
[McpServerTool(UseStructuredContent = true, Title = "Validate Dependency Registration", Name = "ValidateDependencyRegistration"),
    Description("Validates that all services referenced in constructors are properly registered in the DI container.")]
public static async Task<string> ValidateDependencyRegistrationAsync(
    IMcpServer server,
    ContextFile[] contextFiles,
    string[]? diffs = null,
    string rootFromWebhook = "")
```

The T4 template would need to:
1. Parse multi-line attributes with complex parameters
2. Extract string literals from attribute parameters
3. Handle C# syntax variations (spacing, line breaks, etc.)
4. Understand generic types and nullable references
5. Extract method parameters with defaults

**This becomes a mini C# compiler within T4.**

## Our Living Template Approach Advantages

### No Parsing Required
AI agents understand C# syntax naturally - no custom parsing logic needed.

### No Separate Template File
The working code IS the template - nothing else to maintain.

### Framework Evolution Friendly
When .NET v10 changes attributes:
1. Update the working rule manually (test it works)
2. AI regenerates other rules from the proven working code
3. No template file to update separately

### Simpler Mental Model
- **T4 Approach**: Working Code → T4 Parser → T4 Template → Generated Code
- **Living Template**: Working Code → AI Agent → Generated Code

### Better Error Handling
- Generated code is identical to proven working code
- No parsing errors or template compilation issues
- Easier debugging (generated code looks exactly like template)

## Real-World T4 Use Cases (Where T4 Excels)

### Entity Framework Code-First
```xml
<# foreach(var table in database.Tables) { #>
public class <#= table.Name #>
{
<# foreach(var column in table.Columns) { #>
    public <#= column.DataType #> <#= column.Name #> { get; set; }
<# } #>
}
<# } #>
```

### API Client Generation
```xml
<# foreach(var endpoint in openApiSpec.Endpoints) { #>
public async Task<#= endpoint.ResponseType #> <#= endpoint.Name #>Async(<#= endpoint.Parameters #>)
{
    return await httpClient.GetAsync("<#= endpoint.Url #>");
}
<# } #>
```

**These work well because:**
- Input is structured data (schema, spec)
- Output is predictable patterns
- No complex C# parsing required

## Conclusion

### T4 Cannot Practically Be a Living Template Because:

1. **Architectural Mismatch**: T4 is designed for data-to-code, not code-to-code transformation
2. **Parsing Complexity**: Reading C# code in T4 requires building a complex parser
3. **Template Drift Persists**: The `.tt` file itself still needs framework updates
4. **Maintenance Burden**: Complex T4 templates are harder to maintain than simple AI prompts
5. **Industry Practice**: T4 isn't typically used this way for good reasons

### Our Living Template Approach Is Superior For This Use Case Because:

1. **No Separate Template**: Working code IS the template
2. **AI-Friendly**: Natural language processing handles C# syntax
3. **Framework Adaptive**: Update working code, regenerate others
4. **Simpler**: Direct code-to-code transformation
5. **Zero Template Drift**: Template cannot become stale

### When T4 Is Still Better:

- Database schema → Entity generation
- Configuration files → Code generation  
- Structured data → Code patterns
- Design-time models → Runtime code

**Bottom Line**: T4 could theoretically read from working code, but it would be architecturally awkward, complex to maintain, and wouldn't solve the fundamental template drift problem that our living template approach eliminates.