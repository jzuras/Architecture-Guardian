# Living Template Approach for Rule Duplication

## Background and Motivation

### The Problem with Traditional Templates (T4, etc.)

Traditional code generation approaches like T4 templates suffer from a fundamental flaw: **template drift**. Here's how this manifests:

1. **Initial State**: Template matches working code perfectly
2. **Over Time**: Working code evolves (bug fixes, framework updates, optimizations)
3. **Template Neglect**: Template remains unchanged because it's separate from production code
4. **Divergence**: Template becomes stale, incorrect, or incompatible
5. **Major Update Event** (e.g., .NET v10): Developer must manually figure out what changed in working code and update template to match

### Real-World Example: .NET Version Upgrade

When .NET v10 is released:
- **Traditional Approach**: Developer analyzes differences between old working code and new v10 requirements, then manually updates template
- **Risk**: Template may not capture all nuances or may introduce bugs
- **Maintenance Burden**: Dual maintenance of both working code and template

### The Discussion Context

This approach emerged from a discussion about whether AI agents can create code deterministically. The counterpart uses T4 templates that regenerate daily (possibly overkill) but represents a valid concern about keeping generated code current. However, frequent regeneration doesn't solve the core problem of template drift.

**Note**: For a detailed technical comparison between T4 templates and our living template approach, including why T4 cannot practically achieve the same benefits, see [T4_VS_LIVING_TEMPLATE.md](./T4_VS_LIVING_TEMPLATE.md).

**Design Decision**: For analysis of why we chose generated specific methods over a data-driven approach with conditional logic, see [GENERATED_VS_DATA_DRIVEN.md](./GENERATED_VS_DATA_DRIVEN.md).

### Build Process Considerations

**Important**: This living template approach is **NOT intended to be part of the build process**. Code generation should be handled externally and infrequently for these reasons:

- **Rare Usage**: New rules are added occasionally, not on every build
- **Framework Upgrades**: Major updates (like .NET versions) happen infrequently 
- **Intentional Process**: Code generation should be a deliberate, reviewed action
- **Build Stability**: Builds should not fail due to code generation issues
- **Source Control**: Generated code should be committed and reviewed like any other code changes

This contrasts with T4 templates that often regenerate on every build, creating potential build instability and unnecessary overhead.

## Our Solution: "Living Template" Approach

### Core Concept

**The actual working production code IS the template.** There is no separate template file that can drift or become stale.

### Key Benefits

1. **No Template Drift**: Template is always current because it's the production code
2. **Single Source of Truth**: One canonical implementation that works and serves as template
3. **Easier Maintenance**: Update one working rule, regenerate all others
4. **Framework Upgrade Safety**: When .NET v10 arrives, update the template rule to work with v10, then regenerate others
5. **Always Buildable**: Template code always compiles because it's the working code

## Implementation Design

### 1. Mark Template Sections in Working Code

Add special comment markers to the existing `ValidateDependencyRegistration` rule to identify it as the canonical template:

#### ArchValidationTool.cs
```csharp
// ARCHGUARD_TEMPLATE_RULE_START
// TEMPLATE_RULE_NAME_PASCAL: ValidateDependencyRegistration  
// TEMPLATE_RULE_NAME_DISPLAY: Validate Dependency Registration
// TEMPLATE_RULE_DESCRIPTION: Validates that all services referenced in constructors are properly registered in the DI container
// TEMPLATE_CHECK_NAME: Validate Dependency Registration Check
// TEMPLATE_CLAUDE_PROMPT: Validate dependency registration rule: Check that all services referenced in constructors are properly registered in the DI container.

/// <summary>
/// Validates that all services referenced in constructors are properly registered in the DI container
/// </summary>
/// <param name="contextFiles">Array of file contents with their paths for analysis</param>
/// <param name="diffs">Optional array of git diffs showing recent changes</param>
/// <returns>Validation result indicating pass/fail with detailed explanation</returns>
[McpServerTool(UseStructuredContent = true, Title = "Validate Dependency Registration", Name = "ValidateDependencyRegistration"),
    Description("Validates that all services referenced in constructors are properly registered in the DI container.")]
public static async Task<string> ValidateDependencyRegistrationAsync(
    IMcpServer server,
    ContextFile[] contextFiles,
    string[]? diffs = null,
    string rootFromWebhook = "")
{
    // Note - this tool is only checking constructors, for simplicity reasons.

    try
    {
        var root = rootFromWebhook;
        if (string.IsNullOrEmpty(root))
        {
            root = await ArchValidationTool.GetRootOrThrowExceptionAsync(server);
        }
 
        return await ValidationService.ValidateDependencyRegistrationAsync(root, contextFiles, diffs);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
        throw new McpException($"Error in ValidateDependencyRegistrationAsync method: {ex.Message}");
    }
}
// ARCHGUARD_TEMPLATE_RULE_END
```

#### ValidationService.cs
```csharp
// ARCHGUARD_TEMPLATE_RULE_START
// TEMPLATE_METHOD_NAME: ValidateDependencyRegistrationAsync
public static async Task<string> ValidateDependencyRegistrationAsync(string root, ContextFile[] contextFiles, string[]? diffs = null)
{
    try
    {
        // Write inputs to file for debugging.
        var debugData = new
        {
            contextFiles = contextFiles.Select(cf => new
            {
                cf.FilePath,
            }).ToArray(),
            diffs = diffs ?? Array.Empty<string>(),
            timestamp = DateTime.Now
        };

        string debugJson = JsonSerializer.Serialize(debugData, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(debugJson);
        
        string claudeResponse;

        #region Call Claude Code to handle validation
        // Create prompt content.
        var promptBuilder = new StringBuilder();
        // TEMPLATE_CLAUDE_PROMPT_LINE: Next line contains the Claude prompt
        promptBuilder.AppendLine("Validate dependency registration rule: Check that all services referenced in constructors are properly registered in the DI container.");
        promptBuilder.AppendLine("Do not use any external tools, but you may read files as needed.");

        if (diffs is not null && diffs.Length > 0)
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Pay special attention to these recent changes:");
            foreach (var diff in diffs)
            {
                promptBuilder.AppendLine($"```diff");
                promptBuilder.AppendLine(diff);
                promptBuilder.AppendLine("```");
            }
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Return JSON format: {\"passed\": bool, \"violations\": [{\"file\": \"path\", \"line\": number, \"message\": \"description\"}], \"explanation\": \"detailed explanation\"}");
        promptBuilder.AppendLine("Your entire response must be valid JSON that can be parsed directly. Do not include any text before or after the JSON.");

        // ... rest of the method remains exactly the same ...
        
        #endregion
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
        throw new InvalidOperationException($"Error in ValidateDependencyRegistrationAsync method: {ex.Message}", ex);
    }
}
// ARCHGUARD_TEMPLATE_RULE_END
```

#### GitHubCheckService.cs
```csharp
// ARCHGUARD_TEMPLATE_CONSTANT_START
// TEMPLATE_CHECK_NAME_CONSTANT: DependencyRegistrationCheckName
public static string DependencyRegistrationCheckName { get; } = "Validate Dependency Registration Check";
// ARCHGUARD_TEMPLATE_CONSTANT_END

// ARCHGUARD_TEMPLATE_CHECK_METHOD_START
// TEMPLATE_CHECK_METHOD_NAME: ExecuteDepInjectionCheckAsync
// TEMPLATE_VALIDATION_SERVICE_METHOD: ValidationService.ValidateDependencyRegistrationAsync
// TEMPLATE_CHECK_NAME_REFERENCE: DependencyRegistrationCheckName
public async Task ExecuteDepInjectionCheckAsync(CheckExecutionArgs args, string root, long githubInstallationId, IGitHubClient githubClient)
{
    // ... existing method implementation ...
    
    // 2. Perform AI analysis
    var aiResultJsonString = await ValidationService.ValidateDependencyRegistrationAsync(root, Array.Empty<ContextFile>());
    
    // ... rest of method ...
}
// ARCHGUARD_TEMPLATE_CHECK_METHOD_END
```

#### Program.cs
```csharp
// ARCHGUARD_TEMPLATE_TOOL_REGISTRATION_START
// TEMPLATE_TOOL_VARIABLE: dependencyRegTool
// TEMPLATE_METHOD_REFERENCE: ArchValidationTool.ValidateDependencyRegistrationAsync
var dependencyRegTool = McpServerTool.Create(typeof(ArchValidationTool).GetMethod(nameof(ArchValidationTool.ValidateDependencyRegistrationAsync))!);
dependencyRegTool.ProtocolTool.Name = nameof(ArchValidationTool.ValidateDependencyRegistrationAsync);
dependencyRegTool.ProtocolTool.InputSchema = inputSchema;
// ARCHGUARD_TEMPLATE_TOOL_REGISTRATION_END

// ARCHGUARD_TEMPLATE_TOOL_COLLECTION_START
// Note: AI agent will need to add new tools to this collection
ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
{
    dependencyRegTool
    // New rules will be added here by AI agent
}
// ARCHGUARD_TEMPLATE_TOOL_COLLECTION_END
```

### 2. Template Variable Definitions

The AI agent will look for these template variables in the comments:

- `TEMPLATE_RULE_NAME_PASCAL` - PascalCase rule name (e.g., "ValidateAsyncNaming")
- `TEMPLATE_RULE_NAME_DISPLAY` - Human-readable name (e.g., "Validate Async Naming")  
- `TEMPLATE_RULE_DESCRIPTION` - Description of what the rule validates
- `TEMPLATE_CHECK_NAME` - GitHub check name
- `TEMPLATE_CLAUDE_PROMPT` - The prompt sent to Claude Code
- `TEMPLATE_METHOD_NAME` - Method name in ValidationService
- `TEMPLATE_CHECK_NAME_CONSTANT` - Constant name for the check
- `TEMPLATE_TOOL_VARIABLE` - Variable name for the MCP tool

### 3. Generated Code Markers

All generated code will be marked with special comments to make regeneration reliable and explicit:

```csharp
// ARCHGUARD_GENERATED_RULE_START - ValidateAsyncNaming
// Generated from template on: 2025-09-11 19:45:12
// DO NOT EDIT - This code will be regenerated
public static async Task<string> ValidateAsyncNamingAsync(...)
{
    // Generated method implementation
}
// ARCHGUARD_GENERATED_RULE_END - ValidateAsyncNaming
```

**Benefits of Generation Markers:**
- **Eliminates brittle pattern matching** - no more guessing which methods are generated
- **Explicit identification** - clear distinction between template, generated, and hand-written code
- **Regeneration safety** - only code between markers gets regenerated
- **Debugging clarity** - developers can easily see what's generated vs. manual code

### 4. AI Agent Process

#### For Creating a New Rule:
1. **Scan for template markers**: Find all `ARCHGUARD_TEMPLATE_*_START/END` sections
2. **Extract template code**: Copy the code between markers
3. **Parse template variables**: Read the template variable values from comments
4. **Get new rule specification**: AI agent receives new rule name, description, prompt, etc.
5. **Replace template variables**: Substitute new values for template variables
6. **Insert new code**: Add new methods/constants/registrations to appropriate files
7. **Update collections**: Add new tool to ToolCollection, new calls to webhook handlers

#### For Updating All Rules (e.g., .NET v10 upgrade):
1. **Human updates template rule**: Modify the `ValidateDependencyRegistration` rule to work with new framework
2. **AI scans for generated rules**: Find all `ARCHGUARD_GENERATED_RULE_START/END` sections
3. **Extract rule metadata**: Get rule name and variables from generation markers
4. **Regenerate each rule**: Use updated template with extracted metadata to recreate the rule
5. **Replace generated sections**: Overwrite only the code between generation markers

### 4. Usage Scenarios

#### Scenario 1: Adding a New Rule
```bash
# AI agent command (conceptual)
create-rule --name "ValidateAsyncNaming" --description "Validates that async methods follow proper naming conventions" --prompt "Check that all async methods end with 'Async' suffix and return Task or Task<T>"
```

AI agent:
1. Finds template sections in all files
2. Copies template code
3. Replaces template variables with new rule values
4. Adds new methods to ArchValidationTool.cs and ValidationService.cs
5. Adds new constant and method to GitHubCheckService.cs
6. Adds new tool registration to Program.cs

#### Scenario 2: Framework Upgrade (.NET v10)
```bash
# Human manually updates ValidateDependencyRegistration rule for .NET v10
# Then AI regenerates all other rules

regenerate-all-rules
```

AI agent:
1. Scans for all non-template rule methods
2. For each rule, extracts its current template variable values
3. Regenerates the rule using the updated template code
4. Replaces old implementations with new ones

#### Scenario 3: Bug Fix in Common Logic
```bash
# Human fixes a bug in ValidateDependencyRegistration template
# AI regenerates all other rules to get the fix

regenerate-all-rules
```

### 5. Benefits in Practice

#### No Template Drift
- Template is the working code, so it can never be stale
- When we fix bugs or make improvements, the template automatically gets those fixes
- No risk of template becoming incompatible with current framework

#### Easier Maintenance  
- Fix one rule, regenerate all others
- No need to maintain separate template files
- All rules stay consistent with latest patterns and practices

#### Framework Upgrade Safety
- Update one working rule for new framework version
- Regenerate all others from the proven working implementation
- Reduces risk of incompatibilities or missed changes

#### Build Safety
- Template always compiles because it's production code
- During regeneration, old rules can be temporarily commented out to maintain buildability
- Gradual rollout of changes possible

## Implementation Steps

### Phase 1: Mark Template Rule
1. Add template markers to existing `ValidateDependencyRegistration` rule in all 4 files
2. Add template variable comments
3. Test that existing functionality still works unchanged

### Phase 2: Create AI Agent Tools
1. Build code parser that can find template sections
2. Create template variable extraction logic
3. Build code generation that replaces variables and inserts new code
4. Create validation that ensures generated code compiles

### Phase 3: Test with New Rule
1. Use AI agent to create a second rule (e.g., "ValidateAsyncNaming")
2. Verify both rules work in MCP server and GitHub webhooks
3. Test regeneration of the new rule from template

### Phase 4: Test Framework Upgrade Simulation
1. Simulate a framework change by modifying template rule
2. Use AI agent to regenerate the second rule
3. Verify both rules still work with the "updated framework"

## Success Criteria

- [ ] Template rule continues to work unchanged after adding markers
- [ ] AI agent can create new rules by copying template sections and replacing variables
- [ ] AI agent can regenerate existing rules from updated template
- [ ] Generated code always compiles and passes existing tests
- [ ] Framework upgrade scenario works (update template, regenerate others)
- [ ] Bug fix scenario works (fix template, regenerate others)
- [ ] Multiple rules can coexist and work simultaneously

## Comparison with Traditional Approaches

| Aspect | Traditional T4 Templates | Living Template Approach |
|--------|-------------------------|-------------------------|
| Template Currency | Can become stale | Always current (is working code) |
| Maintenance Burden | Dual (code + template) | Single (just working code) |
| Framework Upgrades | Manual template analysis/update | Update working code, regenerate |
| Bug Fixes | May not propagate to template | Automatic (template gets fixes) |
| Risk of Drift | High | Zero |
| Compile Safety | Template may not compile | Template always compiles |
| Debugging | Generated code may differ from template | Generated code identical to template |

This approach transforms the template from a liability (something that can drift) into an asset (the proven working implementation).

## AI Agent Prompts

### Overview

The living template approach requires precisely worded prompts to ensure AI agents perform code generation consistently and correctly. These prompts act as "macros" for common operations and should be stored at the project level for reuse.

**Storage Options**:
- **In Code Comments**: Add prompts as multi-line comments near template sections (e.g., `/* REGENERATE_ALL_PROMPT: ... */`)
- **Dedicated Prompt Files**: Store in `.prompts/` directory with descriptive names
- **Claude Code Macros**: Use Claude Code's project-level prompt storage feature
- **README/Documentation**: Include in project documentation for reference

### Prompt 1: Regenerate All Rules from Template

```
REGENERATE ALL RULES FROM TEMPLATE

You are working with ArchGuard.MCP, a C# architectural validation system that uses a "living template" approach for rule duplication.

TASK: Regenerate all non-template rules using the current template rule implementation.

CONTEXT:
- The template rule "ValidateDependencyRegistration" is marked with ARCHGUARD_TEMPLATE_*_START/END comments
- Other rules in the codebase need to be regenerated to match the current template structure
- This is typically done after framework upgrades or major template improvements

INSTRUCTIONS:
1. IDENTIFY TEMPLATE SECTIONS:
   - Find all code between ARCHGUARD_TEMPLATE_*_START and ARCHGUARD_TEMPLATE_*_END markers
   - Extract template variable definitions from comments (e.g., "TEMPLATE_RULE_NAME_PASCAL: ValidateDependencyRegistration")

2. FIND EXISTING NON-TEMPLATE RULES:
   - Search for rule methods that do NOT have template markers
   - Look for patterns: methods ending in "Async" in ArchValidationTool.cs, corresponding methods in ValidationService.cs
   - Identify their current rule names, descriptions, and prompts

3. FOR EACH NON-TEMPLATE RULE:
   a. Extract current rule metadata (name, description, prompt, etc.)
   b. Copy template code sections
   c. Replace template variables with the extracted rule metadata
   d. Replace the old rule implementation with the regenerated code
   e. Ensure all file locations are updated (ArchValidationTool.cs, ValidationService.cs, GitHubCheckService.cs, Program.cs)

4. VERIFY CONSISTENCY:
   - Ensure all rules follow the same code structure as the template
   - Maintain existing rule names and functionality
   - Preserve rule-specific prompts and descriptions
   - Update tool registrations in Program.cs

5. PRESERVE BUILD STABILITY:
   - Ensure all generated code compiles
   - Do not change rule behavior, only structure
   - Maintain existing method signatures and return types

FILES TO MODIFY:
- ArchGuard.MCP/Tools/ArchValidationTool.cs
- ArchGuard.Shared/ValidationService.cs  
- ArchGuard.MCP/Services/GitHubCheckService.cs
- ArchGuard.MCP/Program.cs

EXAMPLE:
If you find a rule "ValidateAsyncNaming", regenerate it by:
- Copying template sections
- Replacing "ValidateDependencyRegistration" with "ValidateAsyncNaming"
- Replacing template descriptions with async naming descriptions
- Keeping the async naming Claude prompt unchanged

DO NOT:
- Modify the template rule itself (ValidateDependencyRegistration)
- Change rule names or core functionality
- Remove existing rules
- Add new rules (this prompt only regenerates existing ones)

Report back with:
- Number of rules regenerated
- Any compilation issues found
- Summary of changes made
```

### Prompt 2: Create New Rule from Template

```
CREATE NEW RULE FROM TEMPLATE

You are working with ArchGuard.MCP, a C# architectural validation system that uses a "living template" approach for rule creation.

BEFORE WE BEGIN: I need the following information to create a new rule. Please confirm you have all of these ready:

REQUIRED INPUTS:
- NEW_RULE_NAME_PASCAL: PascalCase rule name (e.g., "ValidateAsyncNaming")
- NEW_RULE_NAME_DISPLAY: Human-readable rule name (e.g., "Validate Async Naming")  
- NEW_RULE_DESCRIPTION: Detailed description of what the rule validates (e.g., "Validates that async methods follow proper naming conventions")
- NEW_CHECK_NAME: GitHub check name (e.g., "Validate Async Naming Check")
- NEW_CLAUDE_PROMPT: The precise prompt text to send to Claude Code for validation (e.g., "Check that all async methods end with 'Async' suffix and return Task or Task<T>")

NOTE: The Claude prompt is critical and may require discussion/refinement to get right. It should clearly describe what architectural pattern or rule you want Claude Code to validate.

Do you have all of this information ready? Please respond with:
- YES - I have all the required information and am ready to proceed
- NO - I need to prepare some of this information first

If YES, please provide all five values above and I will proceed with creating the new rule.

---

TASK: Create a new architectural validation rule by copying the template rule structure.

INSTRUCTIONS:
1. EXTRACT TEMPLATE SECTIONS:
   - Find all code between ARCHGUARD_TEMPLATE_*_START and ARCHGUARD_TEMPLATE_*_END markers
   - Copy the complete template code from each section

2. REPLACE TEMPLATE VARIABLES:
   - Replace "ValidateDependencyRegistration" with NEW_RULE_NAME_PASCAL
   - Replace "Validate Dependency Registration" with NEW_RULE_NAME_DISPLAY
   - Replace template description with NEW_RULE_DESCRIPTION
   - Replace template check name with NEW_CHECK_NAME
   - Replace template Claude prompt with NEW_CLAUDE_PROMPT

3. ADD NEW CODE TO FILES:
   - ArchValidationTool.cs: Add new method after existing rules
   - ValidationService.cs: Add new method after existing rules
   - GitHubCheckService.cs: Add new constant and check method
   - Program.cs: Add new tool registration and update ToolCollection

4. UPDATE COLLECTIONS:
   - Add new tool variable to ToolCollection in Program.cs
   - Add new rule calls to webhook handlers if needed

5. MAINTAIN PATTERNS:
   - Follow exact same structure as template
   - Use same error handling patterns
   - Maintain same async patterns and signatures
   - Keep same JSON response format

EXAMPLE USAGE:
NEW_RULE_NAME_PASCAL: ValidateAsyncNaming
NEW_RULE_NAME_DISPLAY: Validate Async Naming
NEW_RULE_DESCRIPTION: Validates that async methods follow proper naming conventions (end with 'Async')
NEW_CHECK_NAME: Validate Async Naming Check
NEW_CLAUDE_PROMPT: Validate async naming convention rule: Check that all async methods end with 'Async' suffix and return Task or Task<T>.

DO NOT:
- Modify existing rules or template sections
- Change the shared inputSchema in Program.cs
- Alter the template markers or comments

Report back with:
- Files modified
- New methods/constants added
- Any compilation issues
- Summary of new rule created
```

### Prompt Storage in Code

Consider adding prompt references directly in the code near template sections:

```csharp
// ARCHGUARD_TEMPLATE_RULE_START
// REGENERATION_PROMPT: See LIVING_TEMPLATE_APPROACH.md "Regenerate All Rules from Template"
// CREATION_PROMPT: See LIVING_TEMPLATE_APPROACH.md "Create New Rule from Template"
// TEMPLATE_RULE_NAME_PASCAL: ValidateDependencyRegistration
// ... rest of template markers
```

Or create a dedicated prompts file:
```
.prompts/
├── regenerate-all-rules.md
├── create-new-rule.md
└── update-template.md
```

### Usage Notes

- **Test First**: Always test prompts on a copy/branch before applying to main code
- **Review Generated Code**: AI-generated code should be reviewed for correctness
- **Incremental Application**: For multiple rules, consider regenerating one at a time
- **Build Verification**: Ensure project builds successfully after generation
- **Version Control**: Commit generated changes as atomic commits with clear messages