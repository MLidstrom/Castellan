# Contributing to Castellan

Thank you for your interest in contributing to Castellan! We welcome contributions from the community to help improve this AI-powered Windows security monitoring platform.

## 🤝 How to Contribute

### Reporting Issues
- Check if the issue already exists in [GitHub Issues](https://github.com/MLidstrom/Castellan/issues)
- Provide a clear description of the problem
- Include steps to reproduce the issue
- Share relevant logs and system information

### Suggesting Features
- Open a discussion in [GitHub Discussions](https://github.com/MLidstrom/Castellan/discussions)
- Explain the use case and benefits
- Consider how it fits with existing features

### Submitting Pull Requests
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. **Add comprehensive tests** - All new functionality must include unit tests
5. **Ensure all tests pass** - Run `dotnet test src\Castellan.Tests\` (currently 393 tests, 100% success rate)
6. **Follow testing standards** - Use XUnit, FluentAssertions, and Moq for consistency
7. Commit with clear messages (`git commit -m 'Add amazing feature'`)
8. Push to your branch (`git push origin feature/amazing-feature`)
9. Open a Pull Request

## 🛠️ Development Setup

### Prerequisites
- .NET 8.0 SDK or later
- Docker Desktop (for Qdrant)
- Node.js 16+ (for React admin)
- Ollama (optional, for local AI)

### Getting Started
```powershell
# Clone the repository
git clone https://github.com/MLidstrom/Castellan.git
cd Castellan

# Start Qdrant (use Docker)
docker run -p 6333:6333 qdrant/qdrant

# Configure authentication (required)
$env:AUTHENTICATION__JWT__SECRETKEY = "your-development-jwt-secret-key-minimum-64-characters"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-dev-password"

# Build the solution
dotnet build Castellan.sln

# Run tests
dotnet test src\Castellan.Tests\Castellan.Tests.csproj

# Start the worker service
cd src\Castellan.Worker
dotnet run
```

**Important:** See [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md) for detailed authentication configuration.

### React Admin Development
```powershell
cd castellan-admin
npm install
npm start  # Runs on http://localhost:8080
```

### Troubleshooting

If `npm start` fails when running as a background job, you can use `Start-Process` to run the server in the background:

```powershell
Start-Process -FilePath "npm.cmd" -ArgumentList "start" -WindowStyle Hidden
```

## 📝 Coding Standards

### C# Guidelines
- Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and small
- Write unit tests for new functionality

### TypeScript/React Guidelines
- Follow [React Best Practices](https://react.dev/learn/thinking-in-react)
- Use functional components with hooks
- Implement proper error boundaries
- Add TypeScript types for all props
- Keep components focused and reusable

### Commit Messages
- Use clear, descriptive commit messages
- Start with a verb (Add, Fix, Update, Remove)
- Reference issue numbers when applicable
- Keep the first line under 50 characters

## 🧪 Testing

### Current Test Status
- **393 total tests** with **100% success rate** (all passing)
- Comprehensive coverage for authentication, security events, notifications, and file operations
- Recent additions include critical tests for connection pooling, vector operations, and pipeline processing

### Running Tests
```powershell
# All tests (recommended)
dotnet test src\Castellan.Tests\

# With verbose output
dotnet test src\Castellan.Tests\ --verbosity normal

# Specific test class
dotnet test src\Castellan.Tests\ --filter "FullyQualifiedName~AuthControllerTests"

# With coverage
dotnet test src\Castellan.Tests\ --collect:"XPlat Code Coverage"
```

### Writing Tests - Required Standards
- **Framework**: Use XUnit with FluentAssertions (not MSTest)
- **Mocking**: Use Moq for dependency injection and external services
- **Pattern**: Follow AAA pattern (Arrange, Act, Assert)
- **Naming**: Use `Method_Scenario_ExpectedBehavior` format
- **Data**: Use `TestDataFactory` for consistent test data creation

### Testing Requirements for New Code
- **Controllers**: Test all endpoints, validation, error handling, return types
- **Services**: Test public methods, configuration validation, exception scenarios
- **Configuration**: Mock `IConfiguration` using `SetupGet(c => c[key])` pattern
- **Constructor Validation**: Test actual behavior (ArgumentNullException vs NullReferenceException)
- **File Operations**: Implement proper cleanup and isolation

### Example Test Structure
```csharp
[Fact]
public void Method_Scenario_ExpectedBehavior()
{
    // Arrange
    var mockService = new Mock<IService>();
    var controller = new Controller(mockService.Object);
    
    // Act
    var result = controller.Method();
    
    // Assert
    result.Should().NotBeNull();
    result.Should().BeOfType<OkResult>();
}
```

## 📋 Pull Request Process

1. **Update Documentation** - Update README.md and other docs as needed
2. **Add Tests** - Ensure new code is covered by tests
3. **Pass CI Checks** - All automated checks must pass
4. **Code Review** - Address reviewer feedback
5. **Squash Commits** - Keep history clean when merging

## 🔒 Security

### Reporting Security Issues
- **DO NOT** open public issues for security vulnerabilities
- Use GitHub Security Advisories or contact maintainers directly
- Include detailed information about the vulnerability
- Allow time for patches before public disclosure

### Security Best Practices
- Never commit secrets or API keys
- Validate all user inputs
- Use parameterized queries
- Follow principle of least privilege
- Keep dependencies updated

## 📚 Resources

- [Project Documentation](docs/)
- [Architecture Overview](README.md#architecture)
- [API Documentation](docs/API.md)
- [Build Guide](docs/BUILD_GUIDE.md)

## 💬 Communication

- **GitHub Issues** - Bug reports and feature requests
- **GitHub Discussions** - General discussions and questions
- **Discord** - Real-time chat (coming soon)

## 📄 License

By contributing to Castellan, you agree that your contributions will be licensed under the GNU Affero General Public License v3.0 (AGPL-3.0).

The AGPL-3.0 license is a strong copyleft license that requires any modified versions of the software, including those used to provide network services, to be released under the same license terms. This ensures the community benefits from all improvements.

## 🙏 Recognition

Contributors will be recognized in:
- The project README
- Release notes
- Annual contributor reports

Thank you for helping make Castellan better! 🏰🛡️