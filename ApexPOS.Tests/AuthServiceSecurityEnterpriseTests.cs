using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;
using FluentAssertions;
using Xunit;

namespace ApexPOS.Tests;

/// <summary>
/// Comprehensive enterprise unit and integration tests for AuthService.
/// Covers Authentication, Role Management, PIN access, Duplicate Prevention,
/// Input Validation Guard Clauses, and Audit Logging.
/// </summary>
public class AuthServiceSecurityEnterpriseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;
    private readonly AuthService _authService;

    private const int AdminUserId = 1;
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "admin123";

    public AuthServiceSecurityEnterpriseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();
        DbSeeder.Seed(_db);

        _authService = new AuthService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    #region Authentication Tests

    [Fact]
    public void Login_ValidCredentials_ReturnsAuthenticatedUser()
    {
        // Arrange & Act
        var user = _authService.Login(DefaultAdminUsername, DefaultAdminPassword);

        // Assert
        user.Should().NotBeNull();
        user!.Username.Should().Be(DefaultAdminUsername);
        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Login_IncorrectPassword_ReturnsNull()
    {
        // Arrange
        const string wrongPassword = "wrong_password_123";

        // Act
        var user = _authService.Login(DefaultAdminUsername, wrongPassword);

        // Assert
        user.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "admin")]
    [InlineData("admin", null)]
    [InlineData("", "admin")]
    [InlineData("admin", "")]
    [InlineData("   ", "   ")]
    public void Login_NullOrEmptyOrWhitespaceCredentials_ReturnsNullGracefully(string? username, string? password)
    {
        // Arrange & Act
        var user = _authService.Login(username!, password!);

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public void Login_InactiveUserAccount_ReturnsNull()
    {
        // Arrange
        var user = _db.Users.First(u => u.Username != DefaultAdminUsername);
        user.IsActive = false;
        _db.SaveChanges();

        // Act
        var authenticated = _authService.Login(user.Username, "cashier");

        // Assert
        authenticated.Should().BeNull("inactive user accounts must be prevented from logging in");
    }

    [Fact]
    public void LoginByPin_ValidPin_ReturnsAuthenticatedUser()
    {
        // Arrange
        var cashier = _db.Users.First(u => u.Role == UserRole.Cashier && u.IsActive);
        string validPin = cashier.PinCode;

        // Act
        var user = _authService.LoginByPin(validPin);

        // Assert
        user.Should().NotBeNull();
        user!.Id.Should().Be(cashier.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9999999")]
    public void LoginByPin_InvalidOrEmptyPin_ReturnsNull(string? invalidPin)
    {
        // Arrange & Act
        var user = _authService.LoginByPin(invalidPin!);

        // Assert
        user.Should().BeNull();
    }

    #endregion

    #region User Management & Validation Tests

    [Fact]
    public void CreateUser_ValidParameters_CreatesUserWithHashedPasswordAndAuditLog()
    {
        // Arrange
        const string newUsername = "stock_mgr_01";
        const string fullName = "Arun Kumar";
        const string rawPassword = "SecurePassword@123";
        const string pinCode = "5678";
        const UserRole role = UserRole.StockMaintainer;

        // Act
        var created = _authService.CreateUser(newUsername, fullName, role, rawPassword, pinCode, AdminUserId);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().BeGreaterThan(0);
        created.Username.Should().Be(newUsername);
        created.FullName.Should().Be(fullName);
        created.Role.Should().Be(role);
        created.IsActive.Should().BeTrue();

        // Verify password is hashed, not stored plain text
        created.PasswordHash.Should().NotBe(rawPassword);
        DbSeeder.VerifyPassword(rawPassword, created.PasswordHash).Should().BeTrue();

        // Verify audit log
        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "CREATE_USER" && a.Details.Contains(newUsername));
        audit.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateUser_NullOrEmptyUsername_ThrowsArgumentNullException(string? invalidUsername)
    {
        // Arrange & Act
        Action act = () => _authService.CreateUser(invalidUsername!, "Name", UserRole.Cashier, "1234", "1234", AdminUserId);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("username");
    }

    [Fact]
    public void CreateUser_DuplicateUsername_ThrowsInvalidOperationException()
    {
        // Arrange - "admin" already exists
        const string existingUsername = "ADMIN"; // Case-insensitive test

        // Act
        Action act = () => _authService.CreateUser(existingUsername, "Admin Copy", UserRole.Admin, "pass", "1111", AdminUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{existingUsername.ToLower()}*already exists*");
    }

    [Fact]
    public void UpdateUser_ModifiesRoleAndActiveStatusWithAuditLog()
    {
        // Arrange
        var user = _db.Users.First(u => u.Role == UserRole.Cashier);
        int userId = user.Id;
        const string updatedName = "Updated Cashier Name";
        const UserRole updatedRole = UserRole.StockMaintainer;

        // Act
        _authService.UpdateUser(userId, updatedName, updatedRole, newPassword: null, newPin: "9988", isActive: true, AdminUserId);

        // Assert
        var updated = _db.Users.AsNoTracking().First(u => u.Id == userId);
        updated.FullName.Should().Be(updatedName);
        updated.Role.Should().Be(updatedRole);
        updated.PinCode.Should().Be("9988");

        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "UPDATE_USER" && a.Details.Contains(user.Username));
        audit.Should().NotBeNull();
    }

    #endregion
}
