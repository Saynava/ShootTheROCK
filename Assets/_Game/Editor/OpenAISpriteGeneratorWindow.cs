using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

#pragma warning disable 0649

public class OpenAISpriteGeneratorWindow : EditorWindow
{
    private const string ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string LegacyApiKeyPrefKey = "ShootTheRock.OpenAI.SpriteGenerator.ApiKey";
    private const string OutputFolderPrefKey = "ShootTheRock.OpenAI.SpriteGenerator.OutputFolder";
    private const string ModelPrefKey = "ShootTheRock.OpenAI.SpriteGenerator.Model";
    private const string SizePrefKey = "ShootTheRock.OpenAI.SpriteGenerator.Size";
    private const string QualityPrefKey = "ShootTheRock.OpenAI.SpriteGenerator.Quality";
    private const string StylePrefKey = "ShootTheRock.OpenAI.SpriteGenerator.Style";
    private const string PixelsPerUnitPrefKey = "ShootTheRock.OpenAI.SpriteGenerator.PixelsPerUnit";

    private static readonly string[] Sizes =
    {
        "1024x1024",
        "1536x1024",
        "1024x1536",
        "2048x2048",
        "2048x1152",
        "3840x2160"
    };

    private static readonly string[] Qualities =
    {
        "low",
        "medium",
        "high",
        "auto"
    };

    private string apiKey;
    private string environmentApiKey;
    private string environmentApiKeySource;
    private string model;
    private string outputFolder;
    private string assetName;
    private string assetDescription;
    private string batchInput;
    private string stylePrompt;
    private string selectedSize;
    private string selectedQuality;
    private int pixelsPerUnit;
    private bool savePromptMetadata;
    private bool revealAfterGeneration;
    private bool isGenerating;
    private float progress;
    private string status;
    private string lastSavedAssetPath;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Shoot the ROCK/GPT Sprite Generator")]
    public static void ShowWindow()
    {
        OpenAISpriteGeneratorWindow window = GetWindow<OpenAISpriteGeneratorWindow>("GPT Sprite Generator");
        window.minSize = new Vector2(520f, 620f);
    }

    private void OnEnable()
    {
        EditorPrefs.DeleteKey(LegacyApiKeyPrefKey);
        apiKey = string.Empty;
        environmentApiKey = ResolveEnvironmentApiKey(out environmentApiKeySource);
        model = EditorPrefs.GetString(ModelPrefKey, "gpt-image-2");
        outputFolder = EditorPrefs.GetString(OutputFolderPrefKey, "Assets/_Game/Art/GeneratedSprites");
        selectedSize = EditorPrefs.GetString(SizePrefKey, "1024x1024");
        selectedQuality = EditorPrefs.GetString(QualityPrefKey, "low");
        pixelsPerUnit = EditorPrefs.GetInt(PixelsPerUnitPrefKey, 100);
        stylePrompt = EditorPrefs.GetString(StylePrefKey, DefaultStylePrompt);
        assetName = "rock_enemy";
        assetDescription = "A chunky living rock enemy with glowing cracks, made for a mining arcade action game.";
        batchInput = string.Empty;
        savePromptMetadata = true;
        revealAfterGeneration = true;
        status = string.IsNullOrWhiteSpace(environmentApiKey)
            ? "Ready. Add a session key or set OPENAI_API_KEY."
            : $"Ready. Using OPENAI_API_KEY from {environmentApiKeySource}.";
    }

    private void OnGUI()
    {
        using (new EditorGUI.DisabledScope(isGenerating))
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawApiSection();
            DrawOutputSection();
            DrawPromptSection();
            DrawBatchSection();
            DrawGenerationControls();
            EditorGUILayout.EndScrollView();
        }

        DrawStatusSection();
    }

    private void DrawApiSection()
    {
        EditorGUILayout.LabelField("OpenAI", EditorStyles.boldLabel);
        model = EditorGUILayout.TextField("Model", model);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Environment Key", string.IsNullOrWhiteSpace(environmentApiKey) ? "Not found" : $"OPENAI_API_KEY found ({environmentApiKeySource})");
        }

        apiKey = EditorGUILayout.PasswordField("Session API Key", apiKey);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload Environment Key"))
            {
                environmentApiKey = ResolveEnvironmentApiKey(out environmentApiKeySource);
                status = string.IsNullOrWhiteSpace(environmentApiKey)
                    ? "OPENAI_API_KEY was not found in this Unity process."
                    : $"OPENAI_API_KEY loaded from {environmentApiKeySource}.";
            }

            if (GUILayout.Button("Clear Session Key"))
            {
                apiKey = string.Empty;
                status = "Session key cleared.";
            }
        }

        EditorGUILayout.HelpBox(
            "Prefer OPENAI_API_KEY for local editor use. Session API Key is only kept in memory until Unity closes, and is not saved to this project. GPT Image 2 does not currently support transparent background output, so this tool asks for a flat chroma-key background.",
            MessageType.Info);
    }

    private void DrawOutputSection()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
            if (GUILayout.Button("Pick", GUILayout.Width(58f)))
                PickOutputFolder();
        }

        pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", Mathf.Max(1, pixelsPerUnit));
        selectedSize = DrawPopup("Size", selectedSize, Sizes);
        selectedQuality = DrawPopup("Quality", selectedQuality, Qualities);
        savePromptMetadata = EditorGUILayout.Toggle("Save Prompt Metadata", savePromptMetadata);
        revealAfterGeneration = EditorGUILayout.Toggle("Reveal After Generate", revealAfterGeneration);
    }

    private void DrawPromptSection()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Single Sprite", EditorStyles.boldLabel);
        assetName = EditorGUILayout.TextField("File Name", assetName);

        EditorGUILayout.LabelField("Description");
        assetDescription = EditorGUILayout.TextArea(assetDescription, GUILayout.MinHeight(54f));

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Shared Style");
        stylePrompt = EditorGUILayout.TextArea(stylePrompt, GUILayout.MinHeight(116f));

        if (GUILayout.Button("Reset Style Prompt"))
            stylePrompt = DefaultStylePrompt;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Prompt Preview", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(BuildPrompt(assetDescription), MessageType.None);
    }

    private void DrawBatchSection()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Batch", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Optional. One sprite per line: file_name | description. If this is empty, the single sprite fields above are used.", MessageType.None);
        batchInput = EditorGUILayout.TextArea(batchInput, GUILayout.MinHeight(88f));
    }

    private void DrawGenerationControls()
    {
        EditorGUILayout.Space(10f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Sprite", GUILayout.Height(34f)))
                _ = GenerateFromUiAsync();

            if (GUILayout.Button("Reveal Output", GUILayout.Height(34f), GUILayout.Width(120f)))
                RevealOutputFolder();
        }
    }

    private void DrawStatusSection()
    {
        EditorGUILayout.Space(4f);
        Rect rect = EditorGUILayout.GetControlRect(false, 22f);
        EditorGUI.ProgressBar(rect, isGenerating ? progress : 0f, status);
    }

    private async Task GenerateFromUiAsync()
    {
        if (isGenerating)
            return;

        string resolvedApiKey = ResolveApiKey();
        model = model.Trim();
        outputFolder = NormalizeAssetFolder(outputFolder);

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            status = "Set OPENAI_API_KEY or add a session API key first.";
            Repaint();
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            status = "Model is required.";
            Repaint();
            return;
        }

        if (!outputFolder.StartsWith("Assets/", StringComparison.Ordinal) && outputFolder != "Assets")
        {
            status = "Output folder must be inside Assets.";
            Repaint();
            return;
        }

        List<SpriteJob> jobs = ParseJobs();
        if (jobs.Count == 0)
        {
            status = "Add a sprite description first.";
            Repaint();
            return;
        }

        EditorPrefs.SetString(ModelPrefKey, model);
        EditorPrefs.SetString(OutputFolderPrefKey, outputFolder);
        EditorPrefs.SetString(SizePrefKey, selectedSize);
        EditorPrefs.SetString(QualityPrefKey, selectedQuality);
        EditorPrefs.SetString(StylePrefKey, stylePrompt);
        EditorPrefs.SetInt(PixelsPerUnitPrefKey, pixelsPerUnit);

        isGenerating = true;
        progress = 0f;

        try
        {
            EnsureAssetFolder(outputFolder);

            for (int i = 0; i < jobs.Count; i++)
            {
                SpriteJob job = jobs[i];
                progress = (float)i / jobs.Count;
                status = $"Generating {job.FileName} ({i + 1}/{jobs.Count})...";
                Repaint();

                string prompt = BuildPrompt(job.Description);
                string assetPath = await GenerateSpriteAsync(job.FileName, prompt, resolvedApiKey);
                ConfigureTextureAsSprite(assetPath);
                lastSavedAssetPath = assetPath;

                if (savePromptMetadata)
                    SavePromptMetadata(assetPath, prompt);
            }

            progress = 1f;
            status = jobs.Count == 1 ? $"Saved {lastSavedAssetPath}" : $"Saved {jobs.Count} sprites.";
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (revealAfterGeneration)
                RevealGeneratedAsset();
        }
        catch (Exception exception)
        {
            status = $"Generation failed: {exception.Message}";
            Debug.LogException(exception);
        }
        finally
        {
            isGenerating = false;
            Repaint();
        }
    }

    private async Task<string> GenerateSpriteAsync(string fileName, string prompt, string resolvedApiKey)
    {
        ImageGenerationRequest payload = new ImageGenerationRequest
        {
            model = model,
            prompt = prompt,
            size = selectedSize,
            quality = selectedQuality,
            output_format = "png",
            n = 1
        };

        string json = JsonUtility.ToJson(payload);
        using UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {resolvedApiKey}");

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            progress = Mathf.Clamp01(progress + 0.0025f);
            Repaint();
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
            throw new InvalidOperationException(ExtractErrorMessage(request.downloadHandler.text, request.error));

        ImageGenerationResponse response = JsonUtility.FromJson<ImageGenerationResponse>(request.downloadHandler.text);
        if (response == null || response.data == null || response.data.Length == 0 || string.IsNullOrWhiteSpace(response.data[0].b64_json))
            throw new InvalidOperationException("OpenAI response did not include image data.");

        byte[] imageBytes = Convert.FromBase64String(response.data[0].b64_json);
        string safeName = MakeSafeFileName(fileName);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}.png");
        string fullPath = Path.GetFullPath(assetPath);
        File.WriteAllBytes(fullPath, imageBytes);
        AssetDatabase.ImportAsset(assetPath);
        return assetPath;
    }

    private void ConfigureTextureAsSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Mathf.Max(1, pixelsPerUnit);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private void SavePromptMetadata(string assetPath, string prompt)
    {
        string metadataPath = Path.ChangeExtension(assetPath, ".prompt.txt");
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Model: {model}");
        builder.AppendLine($"Size: {selectedSize}");
        builder.AppendLine($"Quality: {selectedQuality}");
        builder.AppendLine();
        builder.AppendLine(prompt);
        File.WriteAllText(Path.GetFullPath(metadataPath), builder.ToString());
        AssetDatabase.ImportAsset(metadataPath);
    }

    private List<SpriteJob> ParseJobs()
    {
        List<SpriteJob> jobs = new List<SpriteJob>();

        if (string.IsNullOrWhiteSpace(batchInput))
        {
            if (!string.IsNullOrWhiteSpace(assetDescription))
                jobs.Add(new SpriteJob(assetName, assetDescription));
            return jobs;
        }

        string[] lines = batchInput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split(new[] { '|' }, 2);
            if (parts.Length == 1)
            {
                string fallbackName = $"sprite_{jobs.Count + 1:00}";
                jobs.Add(new SpriteJob(fallbackName, parts[0].Trim()));
                continue;
            }

            jobs.Add(new SpriteJob(parts[0].Trim(), parts[1].Trim()));
        }

        return jobs;
    }

    private string ResolveApiKey()
    {
        string sessionKey = apiKey.Trim();
        if (!string.IsNullOrWhiteSpace(sessionKey))
            return sessionKey;

        environmentApiKey = ResolveEnvironmentApiKey(out environmentApiKeySource);
        return environmentApiKey.Trim();
    }

    private static string ResolveEnvironmentApiKey(out string source)
    {
        string processKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, EnvironmentVariableTarget.Process);
        if (!string.IsNullOrWhiteSpace(processKey))
        {
            source = "Process";
            return processKey;
        }

        string userKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(userKey))
        {
            source = "User";
            return userKey;
        }

        string machineKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, EnvironmentVariableTarget.Machine);
        if (!string.IsNullOrWhiteSpace(machineKey))
        {
            source = "Machine";
            return machineKey;
        }

        source = "none";
        return string.Empty;
    }

    private string BuildPrompt(string description)
    {
        return $"{description.Trim()}\n\n{stylePrompt.Trim()}";
    }

    private void PickOutputFolder()
    {
        string absoluteFolder = EditorUtility.OpenFolderPanel("Sprite output folder", Application.dataPath, string.Empty);
        if (string.IsNullOrWhiteSpace(absoluteFolder))
            return;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string normalizedAbsolute = Path.GetFullPath(absoluteFolder).Replace('\\', '/');
        string normalizedRoot = projectRoot.Replace('\\', '/');

        if (!normalizedAbsolute.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            status = "Pick a folder inside this Unity project.";
            return;
        }

        outputFolder = normalizedAbsolute.Substring(normalizedRoot.Length).TrimStart('/');
        outputFolder = NormalizeAssetFolder(outputFolder);
    }

    private void RevealOutputFolder()
    {
        EnsureAssetFolder(outputFolder);
        EditorUtility.RevealInFinder(Path.GetFullPath(outputFolder));
    }

    private void RevealGeneratedAsset()
    {
        if (string.IsNullOrWhiteSpace(lastSavedAssetPath))
        {
            RevealOutputFolder();
            return;
        }

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(lastSavedAssetPath);
        if (asset == null)
        {
            RevealOutputFolder();
            return;
        }

        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

    private static string NormalizeAssetFolder(string folder)
    {
        return string.IsNullOrWhiteSpace(folder)
            ? "Assets/_Game/Art/GeneratedSprites"
            : folder.Trim().Replace('\\', '/').TrimEnd('/');
    }

    private static void EnsureAssetFolder(string folder)
    {
        string normalizedFolder = NormalizeAssetFolder(folder);
        if (AssetDatabase.IsValidFolder(normalizedFolder))
            return;

        string[] parts = normalizedFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static string DrawPopup(string label, string value, string[] values)
    {
        int index = Array.IndexOf(values, value);
        if (index < 0)
            index = 0;

        int selectedIndex = EditorGUILayout.Popup(label, index, values);
        return values[Mathf.Clamp(selectedIndex, 0, values.Length - 1)];
    }

    private static string ExtractErrorMessage(string responseText, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return fallback;

        OpenAIErrorResponse response = JsonUtility.FromJson<OpenAIErrorResponse>(responseText);
        if (response != null && response.error != null && !string.IsNullOrWhiteSpace(response.error.message))
            return response.error.message;

        return responseText;
    }

    private static string MakeSafeFileName(string value)
    {
        string trimmed = string.IsNullOrWhiteSpace(value) ? "sprite" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(invalid, '_');

        trimmed = trimmed.Replace(' ', '_').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(trimmed) ? "sprite" : trimmed;
    }

    private const string DefaultStylePrompt =
        "Create a single 2D game sprite for Shoot the ROCK. Stylized arcade mining action look, clear readable silhouette, centered object, orthographic three-quarter view, consistent top-left lighting, crisp edges, no text, no UI, no watermark. Put the object on a perfectly flat solid #00FF00 chroma-key background for easy cleanup after generation.";

    private readonly struct SpriteJob
    {
        public readonly string FileName;
        public readonly string Description;

        public SpriteJob(string fileName, string description)
        {
            FileName = string.IsNullOrWhiteSpace(fileName) ? "sprite" : fileName;
            Description = description ?? string.Empty;
        }
    }

    [Serializable]
    private class ImageGenerationRequest
    {
        public string model;
        public string prompt;
        public string size;
        public string quality;
        public string output_format;
        public int n;
    }

    [Serializable]
    private class ImageGenerationResponse
    {
        public ImageData[] data;
    }

    [Serializable]
    private class ImageData
    {
        public string b64_json;
        public string revised_prompt;
    }

    [Serializable]
    private class OpenAIErrorResponse
    {
        public OpenAIError error;
    }

    [Serializable]
    private class OpenAIError
    {
        public string message;
    }
}
