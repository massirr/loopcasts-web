using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace LoopCastsWeb.Services;

public static class Input
{
    /// [EmailAddress] only wants one @ that isn't first or last, so "B@ddsfsd" passes. A real address has a dot in the domain.
    public static bool IsEmail([NotNullWhen(true)] string? s) =>
        s is { Length: <= 254 } && Regex.IsMatch(s.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s.]{2,}$");
}
