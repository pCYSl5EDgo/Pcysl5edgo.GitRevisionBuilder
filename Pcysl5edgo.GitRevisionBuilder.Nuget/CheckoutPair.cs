namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

internal readonly struct CheckoutPair(CheckoutType type, string name, string? additionalOption)
{
    public readonly CheckoutType Type = type;
    public readonly string Name = name;
    public readonly string? AdditionalOption = additionalOption;
}
