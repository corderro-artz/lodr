namespace Lodr.Models;

// X = inches from trailer front, Y = inches from left wall; pallets are floor-level (no Z)
public record PalletPosition(float X, float Y, bool Rotated, int Index);
