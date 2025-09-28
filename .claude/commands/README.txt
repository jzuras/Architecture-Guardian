# ArchGuard Living Template Prompts

This directory contains slash command prompts for the ArchGuard living template system.

## Available Prompts

### `/regenerate-all-rules`
**File**: `regenerate-all-rules.md`  
**Purpose**: Regenerate all existing non-template rules from the current template  
**Use case**: After framework upgrades, template improvements, or bug fixes to template rule

### `/create-new-rule`  
**File**: `create-new-rule.md`  
**Purpose**: Create a new architectural validation rule from template  
**Use case**: Adding new validation rules to the system

## How to Use

1. **In Claude Code**: Type `/` followed by the prompt name
2. **Example**: `/create-new-rule` or `/regenerate-all-rules`
3. **Follow prompts**: Provide required information when asked
4. **Review output**: Always review and test generated code

## Template System

The living template system uses the existing `ValidateDependencyRegistration` rule as the canonical template. Template sections are marked with:

- `ARCHGUARD_TEMPLATE_*_START` / `ARCHGUARD_TEMPLATE_*_END` 
- Template variable comments (e.g., `TEMPLATE_RULE_NAME_PASCAL: ValidateDependencyRegistration`)

## Files Modified by Prompts

Both prompts modify these files:
- `ArchGuard.MCP/Tools/ArchValidationTool.cs` (constants + methods)
- `ArchGuard.Shared/ValidationService.cs` (constants + methods)
- `ArchGuard.MCP/Services/GitHubCheckService.cs` (constants + methods)
- `ArchGuard.MCP/Program.cs` (tool registration + collection)
- `ArchGuard.MCP/Services/WebhookHandlers/IWebhookHandler.cs` (WebhookHandlerBase consolidation)
- `ArchGuard.Shared/IValidationStrategy.cs` (method signatures)
- `ArchGuard.Shared/FileSystemValidationStrategy.cs` (method implementations)
- `ArchGuard.Shared/ApiValidationStrategy.cs` (method implementations)

## Architecture Changes (Updated for Consolidated Webhook Handlers)

The system now uses a consolidated WebhookHandlerBase architecture where:
- All webhook logic is centralized in `WebhookHandlerBase.ExecuteAllChecksAsync()`
- Individual webhook handlers (Push, PR, CheckRun, CheckSuite) are simple shells
- Template markers and insertion points are in the base class, not individual handlers
- New rules automatically work across all webhook event types

Always build and test after using these prompts!