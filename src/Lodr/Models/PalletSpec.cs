namespace Lodr.Models;

public record PalletSpec(
    float Length,
    float Width,
    float Height,
    bool CanRotate,
    int? Quantity = null
)
{
    public static readonly PalletSpec GmaStandard = new(48f, 40f, 48f, CanRotate: true);
    public bool IsValid => Length > 0 && Width > 0 && Height > 0;
}
