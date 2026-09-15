using TMPro;
using UnityEngine;

/// <summary>Shared art references for the garden interface. Created by GardenDesignSetup.</summary>
[CreateAssetMenu(menuName = "Soulmates/Garden Theme")]
public sealed class GardenTheme : ScriptableObject
{
    public Sprite illustration;
    public Sprite logo,
        panel,
        button,
        keycap,
        ground,
        heart;
    public Sprite helpIcon,
        pauseIcon;
    public Sprite arrow;
    public Sprite sparkle;
    public Sprite retryLogo;
    public Sprite softCard;
    public Sprite tutorial;
    public Sprite rounded;
    public Sprite star;
    public Sprite boy;
    public Sprite girl;
    public Material headingMaterial;
    public Material bodyMaterial;
    public TMP_FontAsset heading;
    public TMP_FontAsset body;
}
