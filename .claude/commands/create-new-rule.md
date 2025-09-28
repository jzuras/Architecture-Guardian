# Create New Rule from Template

CREATE NEW RULE FROM TEMPLATE

You are working with ArchGuard.MCP, a C# architectural validation system that uses a "living template" approach for rule creation.

BEFORE WE BEGIN: I need the following information to create a new rule. Please confirm you have all of these ready:

REQUIRED INPUTS (with examples based on current template rule):

- **Mcp Tool Description**: Description text shown in MCP client tools list
  - Template example: `"Validates that all services referenced in constructors are properly registered in the DI container."`

- **Rule/Check Name**: Base name used for GitHub checks, tool titles, and constant naming (do NOT include "Validate", "Check", or "Rule" in this name)
  - Template example: `"Dependency Registration"`

- **AI Agent Instructions**: Precise prompt text to send to Claude Code for validation
  - Template example: `"Validate Dependency Registration rule: Check that all services referenced in constructors are properly registered in the DI container."`

- **Details Url For Rule/Check**: GitHub check details URL (would be a real website if actually used/needed)
  - Template example: `"https://example.com/details/di-check"`

- **Method Comment**: Optional implementation comment for method body (leave blank for none)
  - Template example: `"Note - this tool is only checking constructors, for simplicity reasons."`
  - Use: Leave empty string `""` if no comment is needed

- **Generation Date**: Date/time string for generation comment (exact string will be used)
  - Template example: `"2024-12-15 10:30 AM"` or `"2024-12-15"` or `"December 15, 2024"`
  - Use: Any format you prefer - this exact string will appear in generated code comments

**NOTE**: The AI Agent Instructions are critical and may require discussion/refinement to get right. They should clearly describe what architectural pattern or rule you want Claude Code to validate.

**AUTOMATIC DERIVATION**: The PascalCase rule name (e.g., `ValidateDependencyRegistration`) will be automatically derived by converting the Rule/Check Name to PascalCase and adding the "Validate" prefix.

**EXAMPLES FOR EACH INPUT** (IMPORTANT: Always show these examples to the user when asking for input):

1. **Mcp Tool Description**: `"Validates that all services referenced in constructors are properly registered in the DI container."`
2. **Rule/Check Name**: `"Dependency Registration"`
3. **AI Agent Instructions**: `"Validate Dependency Registration rule: Check that all services referenced in constructors are properly registered in the DI container."`
4. **Details Url For Rule/Check**: `"https://example.com/details/di-check"`
5. **Method Comment**: `"Note - this tool is only checking constructors, for simplicity reasons."` (or `""` for none)
6. **Generation Date**: `"2024-12-15 10:30 AM"` (or any format you prefer)

**CRITICAL**: When asking the user for input, you MUST show all 6 examples above to ensure clarity.

Do you have all of this information ready? Please respond with:
- YES - I have all the required information and am ready to proceed
- NO - I need to prepare some of this information first

If YES, please provide all six values above and I will proceed with creating the new rule.

---

TASK: Create a new architectural validation rule by copying the template rule structure.

INSTRUCTIONS:
1. EXTRACT TEMPLATE SECTIONS:
   - Find all code between ARCHGUARD_TEMPLATE_*_START and ARCHGUARD_TEMPLATE_*_END markers
   - Copy the complete template code from each section EXACTLY as written
   - **CRITICAL**: Maintain identical control flow structure, variable assignment patterns, if/else logic, and error handling

2. REPLACE TEMPLATE VARIABLES (ONLY THESE SPECIFIC ITEMS):
   - Derive NEW_RULE_NAME_PASCAL from Rule/Check Name (convert to PascalCase + "Validate" prefix)
   - Create new static string constants using the 4 provided input values
   - Replace method names with NEW_RULE_NAME_PASCAL variations
   - Replace ValidationService method calls (e.g., ValidateDependencyRegistrationAsync → ValidateEntityDtoPropertyMappingAsync)
   - Replace constant references (e.g., DependencyRegistrationCheckName → EntityDtoPropertyMappingCheckName)
   - Replace details URL constants (e.g., DependencyRegistrationDetailsUrlForCheck → EntityDtoPropertyMappingDetailsUrlForCheck)
   - Replace method comment: TEMPLATE_METHOD_COMMENT → user-provided method comment (or remove line if empty)

   **DO NOT CHANGE**:
   - Control flow logic (if/else statements, loops, try/catch blocks)
   - Variable assignment patterns (e.g., `var existingId = existingCheckId ?? args.ExistingCheckRunId;`)
   - Error handling structure
   - Parameter handling logic
   - Method signatures (except method name)
   - Comments explaining logic flow

3. ADD GENERATION MARKERS:
   - **CRITICAL**: EVERY generated code block must be wrapped with complete generation markers including metadata lines:
   ```csharp
   // ARCHGUARD_GENERATED_RULE_START - [NEW_RULE_NAME_PASCAL]
   // Generated from template on: [USER_PROVIDED_GENERATION_DATE]
   // DO NOT EDIT - This code will be regenerated
   [generated method/constant/registration]
   // ARCHGUARD_GENERATED_RULE_END - [NEW_RULE_NAME_PASCAL]
   ```

   **MANDATORY**: The "Generated from template on:" and "DO NOT EDIT" lines MUST be included in EVERY generated code block. Never omit these lines.

4. ADD NEW CODE TO FILES USING DETERMINISTIC INSERTION POINTS:

   **CRITICAL - DETERMINISTIC PLACEMENT ALGORITHM:**
   All new code MUST be inserted at specific insertion points in alphabetical order by rule name to ensure identical output across multiple runs.

   **File-by-File Insertion Algorithm:**

   **ArchValidationTool.cs:**
   - Constants: Find `ARCHGUARD_INSERTION_POINT_CONSTANTS_START` marker
     - Insert new constant in alphabetical order by rule name
   - Methods: Find `ARCHGUARD_INSERTION_POINT_METHODS_START` marker
     - Insert new method in alphabetical order by rule name

   **ValidationService.cs:**
   - Constants: Find `ARCHGUARD_INSERTION_POINT_CONSTANTS_START` marker
     - Insert new constants in alphabetical order by rule name
   - Methods: Find `ARCHGUARD_INSERTION_POINT_METHODS_START` marker
     - Insert new method in alphabetical order by rule name

   **GitHubCheckService.cs:**
   - Constants: Find `ARCHGUARD_INSERTION_POINT_CONSTANTS_START` marker
     - Insert new constant in alphabetical order by rule name
   - Methods: Find `ARCHGUARD_INSERTION_POINT_METHODS_START` marker
     - Insert new check method in alphabetical order by rule name (must accept optional existingCheckId parameter)

   **Program.cs:**
   - Tool Registration: Find `ARCHGUARD_INSERTION_POINT_TOOL_REGISTRATION_START` marker
     - Insert new tool variable registration in alphabetical order by rule name
   - Tool Collection: Find `ARCHGUARD_INSERTION_POINT_TOOL_COLLECTION_START` marker
     - Insert new tool variable reference in alphabetical order by rule name

5. UPDATE WEBHOOK HANDLER BASE CLASS USING DETERMINISTIC INSERTION POINTS:

   **WebhookHandlerBase.cs (IWebhookHandler.cs file - consolidated architecture):**

   All webhook handlers now use a unified `ExecuteAllChecksAsync()` method in the base class. Individual webhook handlers (PushWebhookHandler.cs, CheckSuiteWebhookHandler.cs, etc.) are simple shells that call the base method and do NOT need modification when adding new rules.

   **Phase 1 - Check Creation Section:**
   - Find `ARCHGUARD_INSERTION_POINT_CHECK_CREATION_START` marker in WebhookHandlerBase
   - Insert new check creation block in alphabetical order by rule name:
     ```csharp
     // ARCHGUARD_GENERATED_RULE_START - [NEW_RULE_NAME_PASCAL]
     // Generated from template on: [USER_PROVIDED_GENERATION_DATE]
     // DO NOT EDIT - This code will be regenerated
     CheckExecutionArgs [ruleName]CheckArgs = new();
     long [ruleName]CheckId = 0;
     if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.[RuleName]CheckName, StringComparison.InvariantCultureIgnoreCase))
     {
         [ruleName]CheckArgs = new CheckExecutionArgs
         {
             RepoOwner = repoOwner,
             RepoName = repoName,
             CommitSha = commitSha,
             CheckName = GitHubCheckService.[RuleName]CheckName,
             InstallationId = installationId,
             ExistingCheckRunId = checkRunId,
             InitialTitle = GitHubCheckService.[RuleName]CheckName,
             InitialSummary = $"Starting {GitHubCheckService.[RuleName]CheckName} validation for '{initialSummaryEndText}'."
         };

         [ruleName]CheckId = await CheckService.CreateCheckAsync(GitHubCheckService.[RuleName]CheckName, [ruleName]CheckArgs, GitHubClient, GitHubCheckService.[RuleName]DetailsUrlForCheck);
     }
     // ARCHGUARD_GENERATED_RULE_END - [NEW_RULE_NAME_PASCAL]
     ```

   **Phase 2 - Rule Execution Section:**
   - Find `ARCHGUARD_INSERTION_POINT_RULE_EXECUTION_START` marker in WebhookHandlerBase
   - Insert new rule execution call in alphabetical order by rule name:
     ```csharp
     // ARCHGUARD_GENERATED_RULE_START - [NEW_RULE_NAME_PASCAL]
     // Generated from template on: [USER_PROVIDED_GENERATION_DATE]
     // DO NOT EDIT - This code will be regenerated
     if (checkNameFromCheckRun is null || checkNameFromCheckRun.Equals(GitHubCheckService.[RuleName]CheckName, StringComparison.InvariantCultureIgnoreCase))
     {
         await CheckService.Execute[RuleName]CheckAsync([ruleName]CheckArgs, windowsRoot, wslRoot, installationId, GitHubClient, [ruleName]CheckId, sharedContextFiles, requestBody);
     }
     // ARCHGUARD_GENERATED_RULE_END - [NEW_RULE_NAME_PASCAL]
     ```

   **ALPHABETICAL ORDER REQUIREMENT:**
   - When inserting into insertion points, examine existing generated rules
   - Insert new rule in correct alphabetical position by rule name
   - This ensures deterministic ordering regardless of creation sequence

6. UPDATE STRATEGY PATTERN FILES USING DETERMINISTIC INSERTION POINTS:

   **Strategy Pattern Files (ARCHGUARD_INSERTION_POINT_METHODS_START markers):**
   - **IValidationStrategy.cs**: Insert new method signature in alphabetical order by rule name
   - **FileSystemValidationStrategy.cs**: Insert new method implementation in alphabetical order by rule name
   - **ApiValidationStrategy.cs**: Insert new method implementation in alphabetical order by rule name

7. UPDATE COLLECTIONS:
   - Add new tool variable to ToolCollection in Program.cs
   - Ensure proper naming conventions and async patterns

8. MAINTAIN PATTERNS:
   - Follow exact same structure as template
   - Use same error handling patterns
   - Maintain same async patterns and signatures
   - Keep same JSON response format

8. **VALIDATION STEP - MANDATORY FOR DETERMINISTIC OUTPUT**:
   After generating all code, perform this validation to ensure byte-for-byte identical output across multiple runs:

   **Generation Marker Verification**:
   - VERIFY that EVERY generated code block includes ALL three required lines:
     1. `// ARCHGUARD_GENERATED_RULE_START - [RuleName]`
     2. `// Generated from template on: [USER_PROVIDED_GENERATION_DATE]`
     3. `// DO NOT EDIT - This code will be regenerated`
   - NO generated code block should be missing the metadata lines
   - If any block is missing these lines, regenerate that specific block with complete markers

   **Deterministic Placement Verification**:
   - Verify all new code is placed at correct insertion points (ARCHGUARD_INSERTION_POINT_*_START markers)
   - Confirm alphabetical ordering by rule name within each insertion point
   - Check that no code was inserted outside of insertion points
   - Validate that existing generated rules remain in alphabetical order

   **Control Flow Verification**:
   - Compare the generated GitHubCheckService method against the template method
   - Verify IDENTICAL control flow structure:
     - Same number of if/else branches
     - Same variable assignment patterns (e.g., `var existingId = existingCheckId ?? args.ExistingCheckRunId;`)
     - Same try/catch block structure
     - Same parameter handling logic
   - If ANY structural differences exist, regenerate the method using exact template structure

   **Line-by-Line Verification**:
   - Compare generated ValidationService method against template method
   - Verify all Console.WriteLine statements are present (including debugging output)
   - Check that no individual statements are omitted within code blocks
   - Confirm all logging calls, error handling statements, and debug output are copied
   - Generated method should have similar line count to template (accounting only for constant/method name changes)

   **Template Consistency Check**:
   - Generated methods should have identical code flow to template methods
   - Only differences should be: method names, constant references, ValidationService calls
   - Everything else must be structurally identical
   - All debugging statements, console output, and logging must be preserved

   **Deterministic Output Test**:
   - After completion, the same inputs should produce identical files if run again
   - Generated rule markers should indicate the rule name consistently
   - All insertion points should maintain alphabetical order

EXAMPLE USAGE:
Mcp Tool Description: "Validates that async methods follow proper naming conventions (end with 'Async')"
Rule/Check Name: "Async Naming"
AI Agent Instructions: "Validate async naming convention rule: Check that all async methods end with 'Async' suffix and return Task or Task<T>."
Details Url For Rule/Check: "https://example.com/details/async-naming"
Method Comment: ""
Generation Date: "2024-12-15 2:45 PM"

(This would automatically derive NEW_RULE_NAME_PASCAL: ValidateAsyncNaming)

DO NOT:
- Modify existing rules or template sections
- Change the shared inputSchema in Program.cs
- Alter the template markers or comments
- Change control flow logic, variable patterns, or structural elements

Report back with:
- Files modified
- New methods/constants added
- Validation step results (control flow comparison AND deterministic placement verification)
- Confirmation that all code was inserted at correct insertion points in alphabetical order
- Any compilation issues
- Summary of new rule created
- **Deterministic Output Confirmation**: Multiple runs with identical inputs should produce byte-for-byte identical files