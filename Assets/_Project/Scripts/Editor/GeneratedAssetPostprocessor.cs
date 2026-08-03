using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public sealed class GeneratedAssetPostprocessor : AssetPostprocessor
    {
        private const string ModelRoot = "Assets/_Project/Art/Models/";
        private const string TextureRoot = "Assets/_Project/Art/Textures/";
        private const string CharacterTextures = TextureRoot + "Characters/";
        private const string FabricTextures = TextureRoot + "Fabric/";

        private const int CharacterTextureSize = 1024;
        private const int EnvironmentTextureSize = 1024;

        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            if (!importer.assetPath.StartsWith(ModelRoot) || !importer.importSettingsMissing)
            {
                return;
            }

            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.weldVertices = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;

            if (IsCharacter(importer.assetPath))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                return;
            }

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.generateSecondaryUV = true;
            importer.isReadable = false;
        }

        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (!importer.assetPath.StartsWith(TextureRoot) || !importer.importSettingsMissing)
            {
                return;
            }

            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;

            if (importer.assetPath.StartsWith(FabricTextures))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 256;
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = importer.assetPath.StartsWith(CharacterTextures)
                ? CharacterTextureSize
                : EnvironmentTextureSize;
        }

        private static bool IsCharacter(string assetPath)
        {
            return assetPath.Contains("MC_ArchiveOfficer") || assetPath.Contains("B_Colleague");
        }
    }
}
