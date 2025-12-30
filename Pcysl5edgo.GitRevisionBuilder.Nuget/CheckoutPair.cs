namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

public readonly struct CheckoutPair(CheckoutType type, string name, string? additionalOption)
{
    public readonly CheckoutType Type = type;
    public readonly string Name = name;
    public readonly string? AdditionalOption = additionalOption;
}
