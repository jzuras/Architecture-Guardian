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


### LocalFoundry Hardware Requirements
**Issue**: LocalFoundry may fail with hardware resource limitations with certain models
- **Symptoms**:
  - `Status: 500 (Internal Server Error)` from LocalFoundry API
  - Loud fan noise indicating high CPU/GPU usage
  - Validation timeouts or system unresponsiveness
- **Root Cause**: phi-4-mini model requires significant computational resources
- **Hardware Impact**: May exceed capabilities of standard development machines
- **Workarounds**:
  1. **Test Model Compatibility**: Use LocalFoundry command-line chatbot to test hardware capability
  2. **Prompt File Testing**: Use validation prompt files (provided in repository) to test locally before relying on automated validation
  3. **Alternative Models**: Investigate lighter models available in LocalFoundry catalog
  4. **Fallback Strategy**: Configure system to use ClaudeCode or GeminiCLI when LocalFoundry fails
- **Testing Approach**: Repository will include sample prompt files for manual LocalFoundry testing
**Bottom Line**: Not Recommended!
- **Reason**:
  - The models that are small enough to run on local hardware (such as qwen2.5-0.5b) are not capable, at this time, of consistently correct analyis or following simple instructions.

### LocalFoundry with MCP Server / Tools
**Issue**: The MCP Tools' inputs are file names, not file contents, which is insufficient for analyis.
- **Not Fixed**: The inputs were not corrected because Local Foundry is not suitable for this use case at this time.

### GitHub Models Integration
**Status**: Recently implemented, testing in progress
- **Implementation**: Successfully integrated with OpenAI-compatible endpoint
- **Configuration**: Supports PAT from appsettings.json or environment variable
- **Known Limitations**:
  - Network dependency (requires internet connection)
  - Rate limits on free tier
  - Latency from network round-trips
- **Advantages**:
  - Better instruction following expected (GPT-4 vs local models)
  - No local hardware requirements
  - Likely cleaner JSON output than smaller models
- **Testing Status**: Build successful, awaiting live API validation testing
- **Documentation**: See README.md and GITHUB_MODELS_IMPLEMENTATION_GUIDE.md

### LocalFoundry Validation Accuracy
**Issue**: Extensive testing revealed significant validation accuracy limitations
- **Symptoms**:
  - **Validation accuracy issues**: qwen2.5-0.5b model shows inconsistent results
  - **False positives and missed violations**: Model easily confused by complex scenarios
  - **Context sensitivity issues**: Complex C# code scenarios confuse the model
- **Performance Impact**: 77-82 seconds per validation, sometimes even 7 minutes or more
- **Technical Status**:
  - ✅ Race condition handling for concurrent webhook processing
  - ✅ Robust JSON parsing pipeline for malformed AI responses
  - ✅ Fresh chat client per validation (no context contamination)
  - ✅ Complete integration with existing validation workflow
  - ⚠️ Model accuracy insufficient for reliable architectural validation


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