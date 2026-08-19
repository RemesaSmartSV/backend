using System.Security.Claims;

namespace RemesaSmartSV.Services;

public static class ClaimsPrincipalExtensions
{
    public static int GetIdUsuario(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue("idUsuario"), out var id) ? id : 0;

    public static int GetIdHogar(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue("idHogar"), out var id) ? id : 0;
}