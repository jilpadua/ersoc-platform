using System.Security.Claims;
using Ersms.Infrastructure;
using Ersms.Infrastructure.Persistence;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Api.Controllers;

public sealed record LoginRequest(string Email, string Password);
public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    Guid OrganizationId,
    Guid? BranchId,
    string TimeZoneId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ErsmsDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ErsmsDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiErrors.Fail(ErrorCodes.Validation, "Email and password are required.");

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || user.Status != "Active")
            return ApiErrors.Fail(ErrorCodes.InvalidCredentials, "Invalid email or password.", StatusCodes.Status401Unauthorized);

        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return ApiErrors.Fail(ErrorCodes.InvalidCredentials, "Invalid email or password.", StatusCodes.Status401Unauthorized);

        var claims = await _db.BuildUserClaimsAsync(user);
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, claims);

        return Ok(await BuildMe(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var id))
            return ApiErrors.Fail(ErrorCodes.Unauthorized, "Not authenticated.", StatusCodes.Status401Unauthorized);

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return ApiErrors.Fail(ErrorCodes.Unauthorized, "Not authenticated.", StatusCodes.Status401Unauthorized);

        return Ok(await BuildMe(user));
    }

    private async Task<MeResponse> BuildMe(ApplicationUser user)
    {
        var claims = await _db.BuildUserClaimsAsync(user);
        var timeZoneId = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == user.OrganizationId)
            .Select(o => o.TimeZoneId)
            .FirstOrDefaultAsync() ?? "Asia/Manila";
        return new MeResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.OrganizationId,
            user.BranchId,
            string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Manila" : timeZoneId,
            claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList(),
            claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList());
    }
}

public static class ApiErrors
{
    public static IActionResult Fail(string code, string message, int status = StatusCodes.Status400BadRequest)
    {
        return new ObjectResult(new { error = new { code, message } }) { StatusCode = status };
    }

    public static IActionResult FromResult(Result result) =>
        Fail(result.ErrorCode!, result.ErrorMessage!, MapStatus(result.ErrorCode));

    public static IActionResult FromResult<T>(Result<T> result) =>
        Fail(result.ErrorCode!, result.ErrorMessage!, MapStatus(result.ErrorCode));

    private static int MapStatus(string? code) => code switch
    {
        ErrorCodes.Unauthorized or ErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
        ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.Conflict or ErrorCodes.InvalidTransition => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
