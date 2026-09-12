namespace Memory.Application;

internal static class EnumGuard
{
    public static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Unknown {typeof(TEnum).Name} value.");
        }
    }
}
