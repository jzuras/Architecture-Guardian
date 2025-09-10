# ArchGuard (Architecture Guardian) MCP Server

A .NET 9 application that provides C# validation checks
through both AI agent tools (MCP) and automated GitHub webhook checks. 
The current implementation ensures only that all constructor dependencies 
are properly registered in dependency injection containers. The key is
that it can be easily extended to handle any validation needed, to make
sure your Architecure guidelines are properly followed.

Please see my LinkedIn posts for more info:
https://www.linkedin.com/feed/update/urn:li:activity:7370622737263894528/
https://www.linkedin.com/feed/update/urn:li:activity:7371608592623312896/

## Overview

ArchGuard operates in two modes:
1. **MCP Server Mode**: AI agents (GitHub CoPiliot in VS and VS Code) can call validation tools directly
2. **GitHub Webhook Mode**: Automated validation triggered by GitHub events (push, pull requests, check runs)

Both modes use the same core validation logic that spawns Claude Code to analyze C# projects for 
dependency injection issues or any other rules defined.

## Key Features

- **Dual operation modes** - MCP tools + GitHub webhook automation
- **Dynamic repository cloning** - Analyzes any GitHub repository on-demand
- **Cross-platform compatibility** - Windows development with WSL Claude Code integration
- **Background cleanup** - Automatic removal of temporary cloned repositories
- **Private repository support** - GitHub App authentication for private repos

## Architecture

This project extends an OAuth-protected MCP server architecture with GitHub integration capabilities.

### Based On an Earlier Project

**For OAuth 2.0 server, MCP infrastructure, ngrok setup, and basic configuration details, see:**  
[Original Enphase MCP Server Project README](https://github.com/jzuras/OAuth-Protected-MCP-Server/blob/main/README.md)

The OAuth/MCP infrastructure (endpoints, JWT tokens, client registration, ngrok configuration) remains unchanged from the original implementation.

### What's Different in ArchGuard

**New GitHub Integration:**
- Dynamic repository cloning system
- GitHub App authentication for private repositories
- Webhook handlers for push, pull requests, and check runs
- Background repository cleanup service

## Quick Start

### 1. GitHub App Configuration

Create and Install a GitHub App with these permissions:
- **Repository permissions:**
  - Contents: Read
  - Metadata: Read  
  - Pull requests: Read
  - Checks: Write

Configure webhook events:
- Push
- Pull requests  
- Check runs
- Check suites

### 2. Application Configuration

Update `appsettings.json`:
```json
{
  "GitHub": {
    "AppId": "your-github-app-id",
    "PrivateKeyFilePath": "path/to/private-key.pem"
  },
  "RepositoryCloning": {
    "CodingAgentType": "ClaudeCode",
    "CleanupIntervalMinutes": 60,
    "MaxRetentionHours": 2,
    "CleanupAfterValidation": true
  }
}
```

### 3. OAuth/MCP Setup

Follow the original project's setup for:
- ngrok URL configuration
- OAuth client registration
- MCP endpoint protection

### 4. Run the Application

```bash
# Start ngrok tunnel (see original project for details)
ngrok http 7071 --domain=your-static-domain.ngrok-free.app

# Run the application
dotnet run
```

## ArchGuard Validation Tool

### What It Validates

- **Constructor dependencies** - Ensures all ctor injected services are registered
- **Easily Extended**

### How It Works

1. **Repository Access**: Clones GitHub repository to temporary directory
3. **Analysis**: Spawns Claude Code process to analyze the project
4. **Results**: Returns JSON with validation results, violations, and explanations
5. **Cleanup**: Removes temporary repository (immediate or background)

### Tool Input Schema

```json
{
  "contextFiles": [
    { "filePath": "src/Services/SomeService.cs" }
  ],
  "diffs": ["git diff output lines..."]
}
```

## Operation Modes

### MCP Mode (AI Agents)

AI agents call the `ValidateDependencyRegistrationAsync` tool:
- Tool clones repository using MCP server's root access
- Returns structured validation results

### GitHub Webhook Mode

Automated validation triggered by GitHub events:

**Push Events**: Validate changes in pushed commits
**Pull Requests**: Validate PR changes before merge  
**Check Runs**: Re-run validation on demand
**Check Suites**: Comprehensive validation suite

**Workflow:**
1. GitHub sends webhook → ArchGuard receives event
2. Repository cloned to temporary directory
3. Validation runs against specific commit/branch
4. Results posted back to GitHub as check runs
5. Repository cleaned up (immediate or background)

## GitHub Integration Details

### Dynamic Repository Cloning

- **Any repository**: Not limited to specific projects
- **Private repositories**: Uses GitHub App authentication
- **Specific commits**: Clones exact commit from webhook
- **Temporary storage**: Uses system temp directory with cleanup
- **Cross-platform paths**: Windows storage → WSL paths for Claude Code

### Webhook Security

- **Signature verification**: Validates GitHub webhook signatures
- **Installation ID**: Extracts from webhook payload for authentication
- **Event filtering**: Processes only relevant GitHub events

### Background Services

- **Repository cleanup**: Automatically removes old temporary repositories
- **Configurable retention**: Set maximum age for temporary repositories  
- **Disk space monitoring**: Emergency cleanup when space is low
- **Immediate cleanup**: Option to clean up right after validation

## Configuration Reference

### Repository Cloning Settings

```json
{
  "RepositoryCloning": {
    "CodingAgentType": "ClaudeCode",           // Path conversion target
    "CleanupIntervalMinutes": 60,             // How often to check for cleanup
    "MaxRetentionHours": 2,                   // How long to keep repositories  
    "CleanupAfterValidation": true,           // Clean immediately after validation
    "MaxConcurrentClones": 3,                 // Limit concurrent operations
    "RetryAttempts": 3,                       // Clone retry attempts
    "RetryDelaySeconds": 5,                   // Delay between retries
    "TimeoutMinutes": 10                      // Clone operation timeout
  }
}
```

### GitHub App Settings

```json
{
  "GitHub": {
    "AppId": "123456",                        // GitHub App ID
    "PrivateKeyFilePath": "private-key.pem"   // Path to GitHub App private key
    // Note: InstallationId comes from webhooks, not config
  }
}
```

## Troubleshooting

### GitHub Webhook Issues

- **403 Forbidden**: Check GitHub App permissions and installation
- **Empty JSON responses**: Usually indicates repository cloning failed
- **Permission denied errors**: Git pack files may be read-only (handled automatically)

### Repository Cloning Issues

- **Authentication failures**: Verify GitHub App private key and App ID
- **Path conversion errors**: Check WSL installation and path formats
- **Cleanup failures**: May need manual cleanup of temp directories

### MCP/OAuth Issues

**OAuth Parameter Compatibility:**
- **Gemini CLI**: Requires `audience` parameter support (sends both `audience` and `resource`)
- **Other clients**: May use `resource` parameter (RFC 8707 Resource Indicators)  
- **Server support**: ArchGuard supports both parameters with OAuth spec-compliant priority (`audience` > `resource`)

**Authentication errors with "invalid_target":**
- Verify client is sending correct MCP endpoint URL in `audience` or `resource` parameter
- Expected URL format: `https://your-ngrok-domain/mcp/` (with trailing slash)

See Original Enphase MCP Server Project README for additional OAuth server and MCP troubleshooting.

## Copyright and License

### Code

Copyright (©) 2025 Jzuras

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

## Trademarks

All trademarks are the property of their respective owners.
Any trademarks used in this project are used in a purely descriptive manner and to state compatibility.