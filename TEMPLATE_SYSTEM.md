# ArchGuard Template System

## Overview

The ArchGuard Template System is a **living template** approach for generating new code functionality by using existing working production code as the template. Unlike traditional code generation tools, there are no separate template files that can drift from the actual implementation - the template IS the working code.

This system is domain-agnostic and could be applied to any scenario where you need to generate multiple similar pieces of functionality that follow the same structural patterns across multiple files.

## Core Concept

The template system works by identifying **what changes** between similar pieces of functionality and **what stays the same**. Template markers are strategically placed around the parts that change, allowing AI agents to understand:

1. **What code sections serve as templates** (unchanging structure)
2. **Where new code should be inserted** (insertion points)
3. **What code was generated from templates** (for updates/regeneration)

## Why This Approach?

### Advantages Over Traditional Methods

**vs. T4 Templates:**
- **Multi-file spanning**: Template markers can span across multiple files in a single generation operation, whereas T4 typically generates single files
- **Living documentation**: The template is always current working code, never out of sync
- **No separate template maintenance**: Template and implementation are the same thing

**vs. Data-Driven Approaches:**
- **Type safety**: Compile-time validation vs. runtime string validation
- **Performance**: Direct method calls vs. switch statement/lookup overhead
- **Debugging clarity**: Specific stack traces vs. generic call sites
- **No central coordination**: Each generated rule is independent vs. maintaining big switch statements

**vs. Manual Copy-Paste:**
- **Deterministic**: Same inputs always produce the same outputs
- **Systematic**: Ensures all necessary locations are updated consistently
- **Traceable**: Generated code is clearly marked and can be regenerated

## Template Markers

The system uses three types of markers to define template boundaries and generation behavior. The markers are prefixed with `ARCHGUARD_` (from "Architecture Guardian", the project name) but the concept is generic and could be adapted to any project naming convention:

### 1. `ARCHGUARD_TEMPLATE_*_START/END`
Marks sections of code that serve as the **template structure**. Everything between these markers represents the pattern that should be followed when generating new functionality.

**Purpose**: Define what the "template" looks like for each code section
**Location**: Around complete functional units (methods, class sections, configuration blocks)

### 2. `ARCHGUARD_GENERATED_RULE_START/END - [RuleName]`
Marks sections of code that were **generated from the template**. These sections can be updated or regenerated while preserving the overall structure.

**Purpose**: Identify generated code for updates and regeneration
**Location**: Around the same structural areas as template markers, but for generated instances

### 3. `ARCHGUARD_INSERTION_POINT_*`
Marks **exact locations** where new generated code should be inserted. These provide deterministic placement for consistent generation results.

**Purpose**: Ensure alphabetical ordering and consistent placement
**Location**: Between existing code sections where new sections should be added

## Template Marker Placement Strategy

Markers are placed around **structural boundaries** where generated code differs from template code:

### Method Definitions
```csharp
// ARCHGUARD_TEMPLATE_VALIDATION_METHOD_START
public static async Task<ValidationResult> ValidateDependencyRegistrationAsync(...)
{
    // Method implementation
}
// ARCHGUARD_TEMPLATE_VALIDATION_METHOD_END
```

### Tool Registrations
```csharp
// ARCHGUARD_TEMPLATE_TOOL_REGISTRATION_START
serverBuilder.WithTool<ValidateDependencyRegistrationAsync>("ValidateDependencyRegistration");
// ARCHGUARD_TEMPLATE_TOOL_REGISTRATION_END
```

### Configuration Constants
```csharp
// ARCHGUARD_TEMPLATE_RULE_CONSTANTS_START
private const string DependencyInjectionPrompt = "...";
private const string DependencyInjectionDescription = "...";
// ARCHGUARD_TEMPLATE_RULE_CONSTANTS_END
```

### Cross-File Coordination
The template system's power comes from coordinating changes across multiple files. In the ArchGuard implementation, this includes:
- **Program.cs**: Tool registration
- **ValidationService.cs**: Method implementation and constants
- **ArchValidationTool.cs**: Tool method definitions
- **Webhook handlers**: Rule execution calls
- **GitHubCheckService.cs**: Check method implementations

**Generic Applicability**: The template system can coordinate changes across any files in any project. The specific files mentioned above are just examples from this implementation - the template markers can be placed in any source files where coordinated changes are needed.

## Current Implementation

### Template Rule
**`ValidateDependencyRegistration`** serves as the canonical template. 
It was chosen as the template because it represents the simplest of the originally planned validation rules, 
making it easier to understand the pattern and test the concept.

### Generated Rules
**`ValidateEntityDtoPropertyMapping`** - Generated from the template to demonstrate the system's capability.

### Template vs Generated Code Comparison
The system works by comparing the structure between template code and generated code. When regenerating, it:
- **Preserves the generated rule's specific functionality** (prompts, descriptions, business logic)
- **Updates the structural patterns** to match any template improvements
- **Does NOT remove and recreate** - it updates in place by comparing differences

This means you can update the template (e.g., for framework upgrades) and regenerate all derived rules to follow the new pattern while maintaining their unique behavior.

## Command Integration

The template system integrates with Claude Code through commands stored in the `.claude/commands` directory:

### Create New Rule Command
- **Purpose**: Generate a completely new validation rule from the template
- **Input Required**: Rule name, validation prompt, description, and other customization parameters
- **Process**: Uses template markers to identify where to insert new code across all relevant files
- **Output**: Fully functional new rule with deterministic placement

**Generic Applicability**: This command pattern can be adapted for any domain where you need to generate new functionality from existing patterns. The input parameters would vary based on what differs between instances in your specific use case (e.g., API endpoint names and routes, entity names and properties, service names and interfaces, etc.).

### Regenerate All Rules Command
- **Purpose**: Update all generated rules to match current template structure
- **Input Required**: New generation date (to track when regeneration occurred)
- **Process**: Compares template structure with generated rule structure and updates to match
- **Preservation**: Maintains rule-specific prompts, descriptions, and business logic
- **Use Case**: Template improvements, framework upgrades, structural changes

## Usage Demonstration

### Real-World Command Usage

When you run the `/create-new-rule` command, you'll see output similar to this (note that the exact wording may vary since the command is interpreted by an AI agent, but the required information will be consistent):

```
> /create-new-rule is running…

● I'll help you create a new architectural validation rule using the living template approach. Let me gather the
  required information first.

● To create a new rule from the template, I need all 6 required inputs. Here are the REQUIRED INPUTS with examples
  based on the current template rule:

  Required Information

  EXAMPLES FOR EACH INPUT (use these as reference):

  1. Mcp Tool Description: "Validates that all services referenced in constructors are properly registered in the DI
   container."
  2. Rule/Check Name: "Dependency Registration"
  3. AI Agent Instructions: "Validate Dependency Registration rule: Check that all services referenced in
  constructors are properly registered in the DI container."
  4. Details Url For Rule/Check: "https://example.com/details/di-check"
  5. Method Comment: "Note - this tool is only checking constructors, for simplicity reasons." (or "" for none)
  6. Generation Date: "2024-12-15 10:30 AM" (or any format you prefer)

  NOTE: The PascalCase rule name (e.g., ValidateDependencyRegistration) will be automatically derived by converting
  the Rule/Check Name to PascalCase and adding the "Validate" prefix.

  Do you have all of this information ready? Please respond with:

  - YES - I have all the required information and am ready to proceed
  - NO - I need to prepare some of this information first

  If YES, please provide all six values above and I will proceed with creating the new rule.

  Todos
  ☐ Gather required inputs for new rule creation
```

You then provide your 6 inputs, and the command generates the new rule across all relevant files. Example responses for these prompts are shown in Step 2 below.

### Recreating the Existing Generated Rule

To demonstrate the deterministic nature of the system, you can remove the existing `ValidateEntityDtoPropertyMapping` rule and recreate it using the process shown in the section above.

#### Step 1: Remove the Generated Rule

First, remove the existing generated code.
This can be done manually (do a global search for ARCHGUARD_GENERATED and remove lines between START and END, inclusively),
or by using this Claude Code instruction (note: Gemini CLI was unable to follow these instructions reliably during testing):

```
remove the generated code by finding ARCHGUARD_GENERATED_RULE_START and
ARCHGUARD_GENERATED_RULE_END comments in all cs files, deleting those lines
and the lines between the start and end lines.
```

#### Step 2: Recreate Using Original Parameters

Then run `/create-new-rule` and provide these original parameters:

1. **MCP Tool Description**: "Validates Entity-to-DTO property mappings for completeness and accuracy"
2. **Rule/Check Name**: "Entity Dto Property Mapping"
3. **AI Agent Instructions**: "Analyze the provided C# code files to validate that Entity-to-DTO property mappings are complete and accurate. Check for: 1) Missing property mappings, 2) Type mismatches between Entity and DTO properties, 3) Incomplete AutoMapper configurations, 4) Manual mapping code that doesn't handle all properties."
4. **Details URL For Rule/Check**: "https://example.com/details/entity-dto-mapping-check"
5. **Method Comment**: "// Method comment goes here"
6. **Generation Date**: "[original (or new, if you prefer) generation date]"

Running the create new rule command with these exact parameters should produce virtually identical code to the current implementation (save for occasional blank line differences,
and the generation date if changed).

## Test Fixture Integration

The template system works in conjunction with a companion repository called **RulesDemo** that serves as a test fixture for validation rules.

**RulesDemo** is a C# console application that demonstrates 5 common architectural validation rules with both correct implementations and commented-out violations. Currently, 2 of these 5 rule violations are handled by the implemented validation rules in ArchGuard. It provides:

- **Structured test scenarios**: Each rule has correct code and commented violations that can be uncommented for testing
- **Real-world code patterns**: Domain entities, services, repositories, DTOs, and factories in a typical layered architecture
- **GitHub integration**: When ArchGuard is installed as a GitHub App, it automatically runs validation checks on pushes and PRs
- **Controlled testing environment**: Specific lines can be uncommented to trigger individual rule violations

This allows developers to test generated validation rules against known good and bad code patterns, ensuring the template system produces rules that correctly identify architectural violations in real codebases.

## Extensibility

### Beyond Validation Rules
While currently used for validation rule generation, the template system is **domain-agnostic**. The same marker-based approach could be applied to generate:
- **API controllers** with consistent patterns
- **Service implementations** following standard interfaces
- **Database repositories** with common CRUD operations
- **Test classes** with standard test patterns

### Requirements for Extension
To extend the template system to other domains:
1. **Identify the pattern**: What changes between instances vs. what stays the same
2. **Place markers strategically**: Around the variable parts of the pattern
3. **Define insertion points**: For deterministic ordering of generated items
4. **Create generation commands**: This is critical - the commands encode the logic for understanding marker semantics and coordinating multi-file changes. **Use the ArchGuard commands in `.claude/commands` as examples** - they demonstrate how to prompt AI agents to find template markers, extract patterns, apply transformations, and maintain consistency across files. The commands are domain-specific but the patterns are reusable.
5. **Adapt marker naming**: Replace `ARCHGUARD_` with your own project prefix (e.g., `MYPROJECT_TEMPLATE_*_START/END`) to maintain clear marker identification while avoiding confusion with the original Architecture Guardian implementation

### Multi-File Generation Power
The template system's strength lies in its ability to coordinate changes across multiple files in a single generation operation. This makes it particularly suitable for scenarios where a single conceptual change (like adding a new validation rule) requires updates in multiple locations throughout the codebase.

**Command Design is Key**: The generation commands are what make multi-file coordination possible. They contain the intelligence for finding related code sections across files, understanding the relationships between template and generated code, and maintaining consistency. Study the ArchGuard command implementations as they represent proven patterns for complex code generation scenarios.

## Documentation References

For detailed implementation guidance and decision frameworks:
- **Template System vs. T4 Templates**: See `Docs/T4_VS_LIVING_TEMPLATE.md`
- **Template System vs. Data-Driven Approaches**: See `Docs/GENERATED_VS_DATA_DRIVEN.md`
- **Complete Living Template Approach**: See `Docs/LIVING_TEMPLATE_APPROACH.md`

---

*This template system represents a novel approach to code generation that maintains the benefits of traditional templating while avoiding the common pitfalls of template drift and maintenance overhead.*