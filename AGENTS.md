# Agent Coding Guidelines

## Build/Lint/Test Commands
- Build: Use Unity Editor to build project
- Lint: No specific linter configured
- Tests: Use Unity Test Framework
  - Run single test: Use Unity Test Runner window
  - Run all tests: `Unity.exe -runTests -projectPath . -testResults results.xml`

## Code Style Guidelines
- Use 4 spaces for indentation (no tabs)
- Curly braces on same line for control structures
- Private fields prefixed with underscore (_fieldName)
- Use PascalCase for public members, camelCase for private members
- Prefer ScriptableObjects for data containers
- Use Unity's new Input System for input handling
- Follow established patterns in Core/ and Game/ directories

## Naming Conventions
- Classes: PascalCase (e.g., PlayerController)
- Methods: PascalCase (e.g., HandleInput)
- Variables: camelCase (e.g., playerSpeed)
- Constants: UPPER_SNAKE_CASE (e.g., MAX_PLAYERS)

## Error Handling
- Use try-catch blocks for operations that might fail
- Log errors with context using UnityEngine.Debug.Log
- Handle null references before accessing objects

## Architecture Patterns
- Use Service Locator pattern for dependencies (Core/Systems/ServiceLocator/)
- Implement state machines with Scriptable States
- Use TaskRunner for asynchronous operations
- Follow established folder structure (Core, Game, MultiPlayer, Plugins)

## Multiplayer Considerations
- Use PurrNet for networking functionality
- Implement networked components with NetworkBehaviour
- Synchronize state changes across networked clients

## Implementation Checklist for PurrNet Adapter
- [ ] Implement proper player list in PurrNetRunner.Players property
- [ ] Connect to player join/leave events in PurrNetRunner constructor
- [ ] Implement RPC call to server in PurrNetRpcBus.Server method
- [ ] Implement RPC call to all clients in PurrNetRpcBus.Clients method
- [ ] Implement RPC call to specific client in PurrNetRpcBus.Client method
- [ ] Implement RPC request/response pattern in PurrNetRpcBus.ServerRequest method