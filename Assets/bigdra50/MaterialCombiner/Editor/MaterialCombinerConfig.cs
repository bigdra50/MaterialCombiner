#if UNITY_EDITOR
namespace MaterialCombiner.Editor
{
    public record MaterialCombinerConfig(
        string OutputBasePath = "Assets/Combined",
        int AtlasSize = 2048,
        int Padding = 2,
        bool UseTimestampFolder = true,
        bool KeepOriginalShader = true,
        string DefaultShaderName = "",
        bool UseMainTextureByDefault = true,
        string CustomTexturePropertyName = "_MainTex",
        bool PreventOverwrite = true,
        bool ProcessChildrenRecursively = false
    );
}
#endif
