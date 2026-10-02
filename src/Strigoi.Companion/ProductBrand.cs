namespace Strigoi.Companion;

internal static class ProductBrand
{
#if GAME_FUSION
    internal const string IntegratedName = "Strigoi Assistant Companion";
#else
    internal const string IntegratedName = "Strigoi Companion Assistant";
#endif
}
