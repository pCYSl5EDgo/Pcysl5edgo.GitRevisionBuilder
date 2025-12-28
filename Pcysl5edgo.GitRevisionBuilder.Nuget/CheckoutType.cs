using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

public readonly struct CheckoutType : IParsable<CheckoutType>, IEquatable<CheckoutType>, ISpanFormattable
{
    private CheckoutType(byte type) => this.type = type;
    private readonly byte type;
    public static readonly CheckoutType Branch = new(0);
    public static readonly CheckoutType Tag = new(1);
    public static readonly CheckoutType Commit = new(2);
    private const string TextBranch = "branch";
    private const string TextTag = "tag";
    private const string TextCommit = "commit";

    public override string ToString() => type switch
    {
        0 => TextBranch,
        1 => TextTag,
        2 => TextCommit,
        _ => throw new InvalidDataException($"type should be in range [0..2], actual: {type}")
    };

    public static CheckoutType Parse(string s, IFormatProvider? provider = default)
    {
        switch (s.Length)
        {
            case 3:
                if (s.Equals(TextTag, StringComparison.OrdinalIgnoreCase))
                {
                    return Tag;
                }
                break;
            case 6:
                if (s.Equals(TextBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return Branch;
                }
                else if (s.Equals(TextCommit, StringComparison.OrdinalIgnoreCase))
                {
                    return Commit;
                }
                break;
        }

        throw new ArgumentException(s, nameof(s));
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out CheckoutType result)
    {
        if (s is not null)
        {
            switch (s.Length)
            {
                case 3:
                    if (s.Equals(TextTag, StringComparison.OrdinalIgnoreCase))
                    {
                        result = Tag;
                        return true;
                    }
                    break;
                case 6:
                    if (s.Equals(TextBranch, StringComparison.OrdinalIgnoreCase))
                    {
                        result = Branch;
                        return true;
                    }
                    else if (s.Equals(TextCommit, StringComparison.OrdinalIgnoreCase))
                    {
                        result = Commit;
                        return true;
                    }
                    break;
            }
        }

        Unsafe.SkipInit(out result);
        return false;
    }

    public readonly bool Equals(CheckoutType other) => type == other.type;
    public static bool operator ==(CheckoutType left, CheckoutType right) => left.type == right.type;
    public static bool operator !=(CheckoutType left, CheckoutType right) => left.type != right.type;

    public override readonly bool Equals(object? obj) => obj is CheckoutType other && Equals(other);

    public override readonly int GetHashCode() => type;

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        switch (type)
        {
            case 0:
                charsWritten = 6;
                TextBranch.CopyTo(destination);
                break;
            case 1:
                charsWritten = 3;
                TextTag.CopyTo(destination);
                break;
            case 2:
                charsWritten = 6;
                TextCommit.CopyTo(destination);
                break;
        }

        throw new InvalidDataException(nameof(type));
    }

    public readonly string ToString(string? format, IFormatProvider? formatProvider) => ToString();
}
