namespace Synentra.BuildingBlocks.Errors;

public readonly record struct ErrorCode(int Value, ErrorCategory Category)
{
    public static string Prefix => "SYN";
    public override string ToString() => $"{Prefix}{Value:D6}";
}