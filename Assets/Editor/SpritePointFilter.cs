using UnityEditor;
using UnityEngine;


/// <summary>
/// Forces nearest-neighbour (Point) filtering on every texture imported under Assets/Sprites, so the
/// pixel art stays crisp when scaled instead of blurring with bilinear filtering. Runs automatically
/// on import: it covers the existing sprites once the folder is reimported, and any new sprite added
/// there from then on. Scoped to Assets/Sprites so third-party art (e.g. asset packs) is left alone.
/// </summary>
public class SpritePointFilter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Sprites/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.filterMode = FilterMode.Point; // nearest-neighbour scaling
    }
}
