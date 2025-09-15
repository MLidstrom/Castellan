using Xunit;
using FluentAssertions;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;

namespace Castellan.Tests.Services;

public class BCryptPasswordHashingServiceTests : IDisposable
{
    private readonly BCryptPasswordHashingService _service;

    public BCryptPasswordHashingServiceTests()
    {
        _service = new BCryptPasswordHashingService();
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Always_CreatesValidService()
    {
        // Arrange & Act & Assert
        _service.Should().NotBeNull();
        _service.Should().BeAssignableTo<IPasswordHashingService>();
    }

    #endregion

    #region HashPassword Tests

    [Fact]
    public void HashPassword_ValidPassword_ReturnsHashedPassword()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hashedPassword = _service.HashPassword(password);

        // Assert
        hashedPassword.Should().NotBeNullOrEmpty();
        hashedPassword.Should().NotBe(password);
        hashedPassword.Should().StartWith("$2");  // BCrypt hash format
        hashedPassword.Length.Should().Be(60);    // Standard BCrypt hash length
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ReturnsDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _service.HashPassword(password);
        var hash2 = _service.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2); // Different salts should produce different hashes
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void HashPassword_InvalidPassword_ThrowsArgumentException(string? password)
    {
        // Arrange, Act & Assert
        var action = () => _service.HashPassword(password!);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Password cannot be null or empty*");
    }

    #endregion

    #region VerifyPassword Tests

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var hashedPassword = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(password, hashedPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456@";
        var hashedPassword = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(wrongPassword, hashedPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "validhash")]
    [InlineData("   ", "validhash")]
    [InlineData(null, "validhash")]
    public void VerifyPassword_NullOrEmptyPassword_ReturnsFalse(string? password, string hashedPassword)
    {
        // Arrange, Act & Assert
        var result = _service.VerifyPassword(password!, hashedPassword);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("password", "")]
    [InlineData("password", "   ")]
    [InlineData("password", null)]
    public void VerifyPassword_NullOrEmptyHash_ReturnsFalse(string password, string? hashedPassword)
    {
        // Arrange, Act & Assert
        var result = _service.VerifyPassword(password, hashedPassword!);
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_InvalidHashFormat_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var invalidHash = "not-a-valid-bcrypt-hash";

        // Act
        var result = _service.VerifyPassword(password, invalidHash);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ValidatePasswordComplexity Tests

    [Theory]
    [InlineData("StrongP@ss47!", true)]
    [InlineData("AnotherGood8@", true)]
    [InlineData("Complex#Pass9", true)]
    public void ValidatePasswordComplexity_ValidPasswords_ReturnsSuccess(string password, bool shouldBeValid)
    {
        // Arrange, Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().Be(shouldBeValid);
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Password is required")]
    [InlineData("   ", "Password is required")]
    [InlineData(null, "Password is required")]
    public void ValidatePasswordComplexity_NullOrEmptyPassword_ReturnsFailure(string? password, string expectedError)
    {
        // Arrange, Act
        var result = _service.ValidatePasswordComplexity(password!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(expectedError);
    }

    [Fact]
    public void ValidatePasswordComplexity_TooShort_ReturnsFailure()
    {
        // Arrange
        var password = "Short1!";

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must be at least 8 characters long");
    }

    [Fact]
    public void ValidatePasswordComplexity_TooLong_ReturnsFailure()
    {
        // Arrange
        var password = new string('a', 129) + "B1!"; // 132 characters

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must be no more than 128 characters long");
    }

    [Fact]
    public void ValidatePasswordComplexity_NoLowercase_ReturnsFailure()
    {
        // Arrange
        var password = "PASSWORD123!";

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one lowercase letter");
    }

    [Fact]
    public void ValidatePasswordComplexity_NoUppercase_ReturnsFailure()
    {
        // Arrange
        var password = "password123!";

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one uppercase letter");
    }

    [Fact]
    public void ValidatePasswordComplexity_NoDigit_ReturnsFailure()
    {
        // Arrange
        var password = "Password!";

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one number");
    }

    [Fact]
    public void ValidatePasswordComplexity_NoSpecialCharacter_ReturnsFailure()
    {
        // Arrange
        var password = "Password123";

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;':\",./<>?~`)");
    }

    [Theory]
    [InlineData("Password123")]
    [InlineData("Admin1234!")]
    [InlineData("Qwerty123!")]
    [InlineData("Test123456!")]
    public void ValidatePasswordComplexity_WeakPatterns_ReturnsFailure(string password)
    {
        // Arrange, Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password contains common weak patterns (e.g., 123, abc, password)");
    }

    [Fact]
    public void ValidatePasswordComplexity_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var password = "weak"; // Too short, no uppercase, no digit, no special char

        // Act
        var result = _service.ValidatePasswordComplexity(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(4);
        result.Errors.Should().Contain("Password must be at least 8 characters long");
        result.Errors.Should().Contain("Password must contain at least one uppercase letter");
        result.Errors.Should().Contain("Password must contain at least one number");
        result.Errors.Should().Contain("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;':\",./<>?~`)");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void EndToEnd_HashAndVerifyWorkflow_WorksCorrectly()
    {
        // Arrange
        var password = "SecureP@ssw0rd!";

        // Act
        var validationResult = _service.ValidatePasswordComplexity(password);
        var hashedPassword = _service.HashPassword(password);
        var verifyResult = _service.VerifyPassword(password, hashedPassword);
        var wrongPasswordResult = _service.VerifyPassword("WrongPassword", hashedPassword);

        // Assert
        validationResult.IsValid.Should().BeTrue();
        hashedPassword.Should().NotBeNullOrEmpty();
        verifyResult.Should().BeTrue();
        wrongPasswordResult.Should().BeFalse();
    }

    #endregion
}
