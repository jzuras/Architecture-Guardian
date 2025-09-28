# Regenerate All Rules from Template

REGENERATE ALL RULES FROM TEMPLATE

You are working with ArchGuard.MCP, a C# architectural validation system that uses a "living template" approach for rule duplication.

BEFORE WE BEGIN: I need the generation date/time to use in the regenerated code markers. Please confirm you have this ready:

- **Generation Date**: Date/time string for generation comment (exact string will be used)
  - Examples: `"2024-12-15 10:30 AM"` or `"2024-12-15"` or `"December 15, 2024"`
  - Use: Any format you prefer - this exact string will appear in generated code comments

**CRITICAL**: DO NOT assume today's date or make up any date. DO NOT proceed with regeneration until the user explicitly provides the generation date they want to use.

**EXAMPLE**: ValidateEntityDtoPropertyMapping will be regenerated with the user-provided generation date while preserving all existing constant VALUES and method comments.

Do you have the generation date ready? Please respond with:
- YES - I have the generation date and am ready to proceed
- NO - I need to prepare the generation date first

If YES, please provide the generation date you want to use and I will proceed with regenerating all rules.

**MANDATORY**: You MUST wait for the user to provide the generation date. Do not guess, assume, or use system date.

---

TASK: Regenerate all non-template rules using the current template rule implementation to match the latest template structure exactly.

CONTEXT:
- The template rule "ValidateDependencyRegistration" is marked with ARCHGUARD_TEMPLATE_*_START/END comments
- Generated rules are marked with ARCHGUARD_GENERATED_RULE_START/END - [RuleName] comments
- Rules use deterministic insertion points (ARCHGUARD_INSERTION_POINT_*_START/END) in alphabetical order
- Each rule is built around 4 static string constants that define behavior
- All generated code must maintain identical control flow to template (same if/else, try/catch, variable patterns)

INSTRUCTIONS:

1. IDENTIFY TEMPLATE SECTIONS:
   - Find all code between ARCHGUARD_TEMPLATE_*_START and ARCHGUARD_TEMPLATE_*_END markers in all files
   - Extract complete template code EXACTLY as written (preserve all control flow, debugging statements, logging)
   - Note the template's 4 static string constants pattern:
     * DependencyRegistrationMcpToolDescription
     * DependencyRegistrationCheckName
     * DependencyRegistrationAiAgentInstructions
     * DependencyRegistrationDetailsUrlForCheck

2. FIND EXISTING GENERATED RULES:
   - Search for all ARCHGUARD_GENERATED_RULE_START/END - [RuleName] markers across all files
   - Extract rule names from markers (e.g., "ValidateEntityDtoPropertyMapping")
   - For each rule, extract existing constants and their VALUES (must preserve unchanged):
     * [RuleName]McpToolDescription value
     * [RuleName]CheckName value
     * [RuleName]AiAgentInstructions value
     * [RuleName]DetailsUrlForCheck value
   - Extract existing method comments from generated rule methods (preserve exactly as written)

3. FOR EACH GENERATED RULE - REGENERATE WITH EXACT TEMPLATE STRUCTURE:

   **Core Replacement Strategy:**
   a. Copy template sections EXACTLY (identical control flow, variable patterns, error handling)
   b. Replace ONLY these specific items:
      - Method names: ValidateDependencyRegistration → [RuleName] (e.g., ValidateEntityDtoPropertyMapping)
      - Constant names: DependencyRegistration → [RuleName] (e.g., EntityDtoPropertyMapping)
      - ValidationService calls: ValidateDependencyRegistrationAsync → [RuleName]Async
      - Details URL references: DependencyRegistrationDetailsUrlForCheck → [RuleName]DetailsUrlForCheck
      - Method comment: TEMPLATE_METHOD_COMMENT → extracted existing method comment
   c. PRESERVE UNCHANGED:
      - All constant VALUES (string content)
      - Control flow structure (if/else, loops, try/catch)
      - Variable assignment patterns
      - All debugging statements and logging calls
      - Method signatures (except method names)
      - Error handling logic

   **Specific File Updates:**

   **ArchValidationTool.cs:**
   - Constants: Replace constant NAME only, preserve VALUE
   - Methods: Copy template method exactly, change only method name and constant references

   **ValidationService.cs:**
   - Constants: Replace constant NAMES only, preserve VALUES
   - Methods: Copy template method with identical structure, change only method name and constant references

   **GitHubCheckService.cs:**
   - Constants: Replace constant NAME only, preserve VALUE
   - Methods: Copy template method EXACTLY (same control flow, error handling), change only method name and constant references

   **Program.cs:**
   - Tool Registration: Copy template registration pattern, change only variable names and constant references
   - Tool Collection: Update collection reference in alphabetical order

4. UPDATE WEBHOOK HANDLER BASE CLASS:
   Find and regenerate all webhook sections in the consolidated base class:

   **WebhookHandlerBase.cs (IWebhookHandler.cs file - consolidated architecture):**
   - Check Creation: Copy template pattern in ARCHGUARD_INSERTION_POINT_CHECK_CREATION_START section
   - Rule Execution: Copy template pattern in ARCHGUARD_INSERTION_POINT_RULE_EXECUTION_START section

   **IMPORTANT - NO INDIVIDUAL WEBHOOK HANDLER MODIFICATION:**
   Individual webhook handlers (PushWebhookHandler.cs, CheckSuiteWebhookHandler.cs, PullRequestWebhookHandler.cs, CheckRunWebhookHandler.cs) are simple shells that call ExecuteAllChecksAsync() and do NOT need modification during rule regeneration.

5. UPDATE STRATEGY PATTERN FILES:
   Find and regenerate all strategy pattern implementations:

   **Strategy Pattern Files (ARCHGUARD_INSERTION_POINT_METHODS_START sections):**
   - **IValidationStrategy.cs**: Regenerate method signatures using template pattern
   - **FileSystemValidationStrategy.cs**: Regenerate method implementations using template pattern
   - **ApiValidationStrategy.cs**: Regenerate method implementations using template pattern

6. CRITICAL - DETERMINISTIC PLACEMENT VERIFICATION:
   - Replace ONLY code between ARCHGUARD_GENERATED_RULE_START/END markers (preserve markers)
   - **MANDATORY**: Ensure ALL generated code blocks include complete generation markers with metadata lines:
     ```csharp
     // ARCHGUARD_GENERATED_RULE_START - [RuleName]
     // Generated from template on: [USER_PROVIDED_GENERATION_DATE]
     // DO NOT EDIT - This code will be regenerated
     [regenerated code content]
     // ARCHGUARD_GENERATED_RULE_END - [RuleName]
     ```
   - Maintain alphabetical order within ALL insertion points
   - Update generation timestamp in markers to user-provided generation date
   - Verify no code placed outside insertion points

6. VALIDATION REQUIREMENTS:

   **Control Flow Identity Check:**
   - Generated methods must have IDENTICAL structure to template methods
   - Same number of if/else branches, same variable assignment patterns
   - Same try/catch block structure, same parameter handling logic
   - All Console.WriteLine, Logger calls, and debugging statements preserved

   **Constant Preservation Check:**
   - All existing constant VALUES remain exactly the same
   - Only constant NAMES change to match rule naming pattern
   - No modification of string content within constants

   **Alphabetical Order Check:**
   - All generated rules maintain alphabetical order within insertion points
   - ValidateEntityDtoPropertyMapping comes after ValidateDependencyRegistration

FILES TO MODIFY:
- ArchGuard.MCP/Tools/ArchValidationTool.cs (constants + methods)
- ArchGuard.Shared/ValidationService.cs (constants + methods)
- ArchGuard.MCP/Services/GitHubCheckService.cs (constants + methods)
- ArchGuard.MCP/Program.cs (tool registration + collection)
- ArchGuard.MCP/Services/WebhookHandlers/IWebhookHandler.cs (WebhookHandlerBase consolidation - check creation + execution)
- ArchGuard.Shared/IValidationStrategy.cs (method signatures)
- ArchGuard.Shared/FileSystemValidationStrategy.cs (method implementations)
- ArchGuard.Shared/ApiValidationStrategy.cs (method implementations)

EXAMPLE - ValidateEntityDtoPropertyMapping Regeneration:
- Extract existing constants: EntityDtoPropertyMappingMcpToolDescription value, etc.
- Extract existing method comment: `"Method comment goes here"` (or whatever currently exists)
- Copy template ValidationService method EXACTLY
- Replace only: ValidateDependencyRegistrationAsync → ValidateEntityDtoPropertyMappingAsync
- Replace only: DependencyRegistrationAiAgentInstructions → EntityDtoPropertyMappingAiAgentInstructions
- Replace only: TEMPLATE_METHOD_COMMENT → extracted existing method comment
- Update generation marker: `// Generated from template on: [USER_PROVIDED_GENERATION_DATE]`
- Preserve: All control flow, debugging statements, error handling, constant VALUES
- Place in alphabetical order after ValidateDependencyRegistration in insertion points

DO NOT:
- Modify the template rule itself (ValidateDependencyRegistration)
- Change rule names or core functionality
- Remove existing rules
- Add new rules (this prompt only regenerates existing ones)
- Change any static string constant VALUES
- Modify control flow logic, variable patterns, or structural elements
- Place code outside of insertion points

MANDATORY FINAL VALIDATION:

**Generation Marker Verification (CRITICAL)**:
- VERIFY that EVERY regenerated code block includes ALL three required lines:
  1. `// ARCHGUARD_GENERATED_RULE_START - [RuleName]`
  2. `// Generated from template on: [USER_PROVIDED_GENERATION_DATE]`
  3. `// DO NOT EDIT - This code will be regenerated`
- NO regenerated code block should be missing the metadata lines
- If any block is missing these lines, regenerate that specific block with complete markers

**Code Quality Verification**:
- All generated code compiles without errors
- Generated methods have identical control flow to template methods
- All constant values preserved unchanged
- All generated code placed in correct insertion points in alphabetical order
- All debugging statements and logging preserved in generated methods

Report back with:
- Number of rules regenerated (should be 1: ValidateEntityDtoPropertyMapping)
- Files modified and sections updated
- Validation confirmation: control flow identity + constant preservation + alphabetical order
- Any compilation issues found
- Summary of changes made