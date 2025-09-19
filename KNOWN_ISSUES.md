# Known Issues and Development Notes

This document outlines current limitations, development observations, and areas for future improvement in ArchGuard.

## Active Development Issues

### Debug Output
**Issue**: Extensive debug console output throughout the application
- **Status**: By design during active development phase
- **Impact**: Verbose logging in console during operation
- **Future**: Should be cleaned up for production release

## AI Agent Reliability Issues

### Gemini CLI Output Instability
**Issue**: Gemini CLI sometimes fails with output disappearing during validation
- **Root Cause**: Similar issue occurred with Claude Code, resolved by writing output to files
- **Limitation**: Gemini CLI lacks file output capability in non-interactive mode
- **Workaround Attempted**: `__AGENT_COMPLETE__` markers in commands (unsuccessful so far)
- **Impact**: Unreliable validation results when using Gemini CLI (validation will fail, enabling a re-run)

### Gemini CLI Command Following
**Issue**: Gemini CLI failed to reliably follow simple code removal instructions
- **Example**: Unable to properly remove generated code between template markers
- **Impact**: Gemini CLI will likely fail when running `/create-new-rule` command or template operations
- **Recommendation**: Use Claude Code for all template system operations

## Code Quality Observations

### AI-Generated Code Patterns
**Observation**: Codebase exhibits typical AI-created "overengineering"
- **Positive Impact**: Larger surface area helped during Deterministic Code Generation testing phase
- **Status**: Acceptable for current development phase
- **Future**: May refactor for production simplification

## Feature Implementation Gaps

### Context Files and Diffs Usage
**Current State**: Context files and diffs are captured but not actively processed
- **GitHub Copilot**: Sometimes provides diffs and determines context-relevant files
- **Push Handler**: Logs changed files in commits but doesn't process them further
- **Payload Analysis**: Haven't investigated if GitHub payloads contain actual diff content
- **AI Agent Validation**: Works without this information for currently implemented rules

**Retention Reasons**:
1. **Input Schema Demonstration**: Shows how to define structured tool inputs
2. **AI Agent Compliance Testing**: Validates how well agents follow input schemas
3. **Future Compatibility**: May be useful for AI agents without full codebase access

### Dependency Injection Rule Scope
**Current Limitation**: DI validation only checks constructor dependencies
- **Not Covered**: Property injection, method injection, other DI patterns
- **Extension Approach**: Recommend discussing with Claude Code to plan expansion strategy
- **Implementation**: Use planning discussion output as new prompt for AI Agent

## Future Improvements

### Error Handling
- Improve error messages for failed validations
- Better handling of AI agent timeouts
- More graceful degradation when agents fail

### Performance
- Reduce debug output overhead
- Optimize repository cloning for large repositories
- Consider caching for repeated validations

### Extensibility
- Full context/diff processing implementation
- Expansion of DI rule beyond constructors
- Additional architectural validation rules

---

*This document reflects the current development state and will be updated as issues are resolved and new ones discovered.*