using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public class LocalizationAutoTranslator : EditorWindow
{
    private string sourceLocaleCode = "zh-Hans";
    private float translationDelay = 0.3f;
    private bool overwriteExisting = true;

    private bool isTranslating = false;
    private string currentStatus = "就绪";
    private float progress = 0f;
    private int totalStrings = 0;
    private int completedStrings = 0;

    private Vector2 scrollPosition;
    private List<string> availableLocaleCodes = new List<string>();

    [MenuItem("Tools/本地化自动翻译工具")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationAutoTranslator>("自动翻译工具");
    }

    private void OnEnable()
    {
        RefreshAvailableLocales();
    }

    private void RefreshAvailableLocales()
    {
        availableLocaleCodes.Clear();

        if (LocalizationSettings.Instance == null || LocalizationSettings.AvailableLocales == null)
            return;

        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
           
            if (locale != null && !string.IsNullOrEmpty(locale.Identifier.Code))
            {
                availableLocaleCodes.Add(locale.Identifier.Code);
            }
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("本地化自动翻译工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (isTranslating)
        {
            DrawProgressUI();
        }
        else
        {
            DrawConfigUI();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawConfigUI()
    {
        // 源语言配置
        EditorGUILayout.LabelField("源语言配置", EditorStyles.boldLabel);

        if (availableLocaleCodes.Count > 0)
        {
            int currentSourceIndex = availableLocaleCodes.IndexOf(sourceLocaleCode);
            if (currentSourceIndex == -1) currentSourceIndex = 0;

            int newSourceIndex = EditorGUILayout.Popup("源语言", currentSourceIndex, availableLocaleCodes.ToArray());
            if (newSourceIndex >= 0 && newSourceIndex < availableLocaleCodes.Count)
            {
                sourceLocaleCode = availableLocaleCodes[newSourceIndex];
            }
        }
        else
        {
            EditorGUILayout.HelpBox("未找到可用的语言环境，请检查Localization Settings配置", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // 目标语言显示
        EditorGUILayout.LabelField("目标语言", EditorStyles.boldLabel);

        var targetLocales = GetTargetLocales();
        if (targetLocales.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            foreach (var localeCode in targetLocales)
            {
                EditorGUILayout.LabelField($"• {GetLocaleDisplayName(localeCode)}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.HelpBox($"将自动翻译到 {targetLocales.Count} 种语言", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("没有找到其他目标语言", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 翻译设置
        EditorGUILayout.LabelField("翻译设置", EditorStyles.boldLabel);
        translationDelay = EditorGUILayout.Slider("翻译间隔(秒)", translationDelay, 0.1f, 2f);
        overwriteExisting = EditorGUILayout.Toggle("覆盖已存在的翻译", overwriteExisting);

        EditorGUILayout.Space();

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新语言列表", GUILayout.Height(30)))
        {
            RefreshAvailableLocales();
        }

        if (GUILayout.Button("扫描字符串", GUILayout.Height(30)))
        {
            ScanAllStrings();
        }

        GUI.enabled = !isTranslating && targetLocales.Count > 0;
        if (GUILayout.Button("开始翻译", GUILayout.Height(30)))
        {
            StartTranslation();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // 状态显示
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(currentStatus, MessageType.Info);
    }

    private List<string> GetTargetLocales()
    {
        var targetLocales = new List<string>();

        if (availableLocaleCodes.Count == 0)
            return targetLocales;

        foreach (var localeCode in availableLocaleCodes)
        {
            if (localeCode != sourceLocaleCode)
            {
                targetLocales.Add(localeCode);
            }
        }

        return targetLocales;
    }

    private string GetLocaleDisplayName(string localeCode)
    {
        var locale = LocalizationSettings.AvailableLocales.Locales
            .FirstOrDefault(l => l.Identifier.Code == localeCode);

        if (locale != null)
        {
            return $"{localeCode} ({locale.ToString()})";
        }

        return localeCode;
    }

    private void DrawProgressUI()
    {
        EditorGUILayout.LabelField("翻译进度", EditorStyles.boldLabel);

        // 进度条
        Rect rect = GUILayoutUtility.GetRect(200, 20);
        EditorGUI.ProgressBar(rect, progress, $"{progress:P2}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"状态: {currentStatus}");
        EditorGUILayout.LabelField($"进度: {completedStrings} / {totalStrings}");

        if (GUILayout.Button("取消翻译"))
        {
            StopTranslation();
        }
    }

    private void ScanAllStrings()
    {
        var collections = Resources.FindObjectsOfTypeAll<StringTableCollection>();
        int stringCount = 0;

        foreach (var collection in collections)
        {
            if (collection == null) continue;

            var sourceTable = collection.StringTables.FirstOrDefault(table =>
                table?.LocaleIdentifier.Code == sourceLocaleCode);

            if (sourceTable != null)
            {
                stringCount += sourceTable.Count;
            }
        }

        var targetCount = GetTargetLocales().Count;
        currentStatus = $"找到 {collections.Length} 个表格集合，{stringCount} 个字符串，将翻译到 {targetCount} 种语言";
        Repaint();
    }

    private async void StartTranslation()
    {
        if (isTranslating) return;

        isTranslating = true;
        currentStatus = "初始化中...";
        progress = 0f;
        completedStrings = 0;
        totalStrings = 0;

        Repaint();

        try
        {
            // 收集所有字符串
            var allEntries = CollectAllSourceStrings();
            var targetLocales = GetTargetLocales();
            totalStrings = allEntries.Count * targetLocales.Count;

            if (totalStrings == 0)
            {
                currentStatus = "未找到可翻译的字符串";
                isTranslating = false;
                return;
            }

            currentStatus = $"开始翻译 {allEntries.Count} 个字符串到 {targetLocales.Count} 种语言";
            Repaint();

            // 翻译每种语言
            foreach (string targetLocaleCode in targetLocales)
            {
                if (!isTranslating) break;
                await TranslateForLocale(allEntries, targetLocaleCode);
            }

            if (isTranslating)
            {
                // 保存资源
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                currentStatus = "翻译完成！";
                progress = 1f;

                EditorUtility.DisplayDialog("完成", $"所有翻译已完成并保存到 {targetLocales.Count} 种语言", "确定");
            }
        }
        finally
        {
            isTranslating = false;
            Repaint();
        }
    }

    private List<TranslationEntry> CollectAllSourceStrings()
    {
        var entries = new List<TranslationEntry>();
        var collections = Resources.FindObjectsOfTypeAll<StringTableCollection>();

        foreach (var collection in collections)
        {
            if (collection == null) continue;

            var sourceTable = collection.StringTables.FirstOrDefault(table =>
                table?.LocaleIdentifier.Code == sourceLocaleCode);

            if (sourceTable == null) continue;

            foreach (var entry in sourceTable.Values)
            {
                if (!string.IsNullOrEmpty(entry.Value))
                {
                    entries.Add(new TranslationEntry
                    {
                        collection = collection,
                        key = entry.Key,
                        value = entry.Value,
                        tableName = collection.name
                    });
                }
            }
        }

        return entries;
    }

/*    private async Task TranslateForLocale(List<TranslationEntry> entries, string targetLocaleCode)
    {
        if (!isTranslating) return;

        currentStatus = $"翻译到 {targetLocaleCode}...";
        Repaint();

        var targetLocale = LocalizationSettings.AvailableLocales.Locales
            .FirstOrDefault(locale => locale.Identifier.Code == targetLocaleCode);

        if (targetLocale == null)
        {
            Debug.LogWarning($"未找到目标语言环境: {targetLocaleCode}");
            return;
        }

        string targetLanguage = GetLanguageName(targetLocaleCode);
        Debug.Log("翻译为" + targetLanguage);
        foreach (var entry in entries)
        {
            if (!isTranslating) break;
            await TranslateSingleEntry(entry, targetLocale, targetLanguage);
            await Task.Delay((int)(translationDelay * 1000));
        }

        if (isTranslating)
        {
            Debug.Log($"完成 {targetLocaleCode} 的翻译");
        }
    }*/

    private async Task TranslateSingleEntry(TranslationEntry entry, Locale targetLocale, string targetLanguage)
    {
        if (!isTranslating) return;

        // 获取目标表格
        var targetTable = entry.collection.StringTables.FirstOrDefault(table =>
            table?.LocaleIdentifier == targetLocale.Identifier);

        if (targetTable == null)
        {
            completedStrings++;
            UpdateProgress();
            return;
        }

        // 检查是否已存在
        var existingEntry = targetTable.GetEntry(entry.key);
        if (existingEntry != null && !overwriteExisting && !string.IsNullOrEmpty(existingEntry.Value))
        {
            completedStrings++;
            UpdateProgress();
            return;
        }

        // 调用Qwen翻译
        string translatedText = await TranslateWithQwen(entry.value, targetLanguage);

        if (!string.IsNullOrEmpty(translatedText) && isTranslating)
        {
            if (existingEntry == null)
            {
                targetTable.AddEntry(entry.key, translatedText);
            }
            else
            {
                existingEntry.Value = translatedText;
            }

            // 标记表格为脏
            EditorUtility.SetDirty(targetTable);
        }

        completedStrings++;
        UpdateProgress();
    }
    //腾讯云机器翻译
    private async Task TranslateForLocale(List<TranslationEntry> entries, string targetLocaleCode)
    {
        if (!isTranslating) return;

        currentStatus = $"翻译到 {targetLocaleCode}...";
        Repaint();

        var targetLocale = LocalizationSettings.AvailableLocales.Locales
            .FirstOrDefault(locale => locale.Identifier.Code == targetLocaleCode);

        if (targetLocale == null)
        {
            Debug.LogWarning($"未找到目标语言环境: {targetLocaleCode}");
            return;
        }

         string targetLanguage = GetLanguageName(targetLocale.LocaleName);
        // Debug.Log("翻译为" + targetLocaleCode);
        Debug.Log($"locale的输出值1{targetLocale.ToString()}---{targetLocale.Identifier}---{targetLocale.LocaleName}----{targetLocale.name}---{targetLanguage}");
        // 创建腾讯云翻译器实例
        var translator = new TencentCloudTranslator();

        foreach (var entry in entries)
        {
            if (!isTranslating) break;
            await TranslateSingleEntry(entry, targetLocale, targetLanguage, translator);
            await Task.Delay((int)(translationDelay * 1000));
        }

        if (isTranslating)
        {
            Debug.Log($"完成 {targetLocaleCode} 的翻译");
        }
    }

    private async Task TranslateSingleEntry(TranslationEntry entry, Locale targetLocale, string targetLanguage, TencentCloudTranslator translator)
    {
        if (!isTranslating) return;

        // 获取目标表格
        var targetTable = entry.collection.StringTables.FirstOrDefault(table =>
            table?.LocaleIdentifier == targetLocale.Identifier);

        if (targetTable == null)
        {
            completedStrings++;
            UpdateProgress();
            return;
        }

        // 检查是否已存在
        var existingEntry = targetTable.GetEntry(entry.key);
        if (existingEntry != null && !overwriteExisting && !string.IsNullOrEmpty(existingEntry.Value))
        {
            completedStrings++;
            UpdateProgress();
            return;
        }

        // 调用腾讯云翻译
        string translatedText = await translator.TranslateWithTencent(entry.value, targetLanguage);

        if (!string.IsNullOrEmpty(translatedText) && isTranslating)
        {
            if (existingEntry == null)
            {
                targetTable.AddEntry(entry.key, translatedText);
            }
            else
            {
                existingEntry.Value = translatedText;
            }

            // 标记表格为脏
            EditorUtility.SetDirty(targetTable);
        }

        completedStrings++;
        UpdateProgress();
    }
    private async Task<string> TranslateWithQwen(string text, string targetLanguage)
    {
        try
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = System.TimeSpan.FromSeconds(30);
                string prompt = GetTranslationPrompt(text,targetLanguage);
                var requestData = new OllamaRequest
                {
                    model = "qwen2.5:7b",
                    prompt = prompt,
                    stream = false
                };
                Debug.Log(prompt);
                string jsonData = JsonUtility.ToJson(requestData);
                var content = new System.Net.Http.StringContent(jsonData, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://localhost:11434/api/generate", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var result = JsonUtility.FromJson<OllamaResponse>(responseString);
                return CleanTranslationResult(result?.response ?? "");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"翻译失败: {e.Message}");
            return text;
        }
    }
    private string GetTranslationPrompt(string text, string targetLanguage)
    {
        return targetLanguage.ToLower() switch
        {
            "korean" or "ko"or "Korean" =>
           $"你将以下中文文本翻译成韩语。请确保只输出纯韩文文本，不要混合其他语言。\n\n" +
           $"原文：{text}\n\n" +
           $"翻译要求：\n" +
           $"- 使用自然的韩语口语\n" +
           $"- 只输出韩文，不要任何其他语言\n" +
           $"- 不要添加解释或备注\n" +
           $"\n",

            "japanese" or "ja" or "Japanese" =>
      $"执行以下步骤：\n" +
      $"步骤1：准确理解中文句子的含义\n" +
      $"步骤2：转换成自然的日语口语表达\n" +
      $"步骤3：确保只使用日语字符（平假名、片假名、汉字）\n" +
      $"步骤4：输出最终翻译结果，不要任何额外文本\n" +
      $"特别注意：\n" +
      $"- 疑问句要准确表达疑问\n" +
      $"- 不要使用「あなた」除非必要\n" +
      $"- 保持对话的自然流畅\n" +
      $"原文：{text}\n" ,
       

            "english" or "en" or "English" =>
                $"将以下中文游戏对话翻译成自然的英语：\n" +
                $"要求：\n" +
                $"1. 使用自然的英语口语表达\n" +
                $"2. 保持对话的流畅性和亲切感\n" +
                $"3. 适合游戏角色对话使用\n" +
                $"4. 只返回翻译结果\n" +
                $"文本：{text}",

            _ =>
                $"将以下游戏对话文本翻译成{targetLanguage}：\n" +
                $"要求：\n" +
                $"1. 翻译结果自然流畅，适合游戏对话使用\n" +
                $"2. 保持口语化的表达方式\n" +
                $"3. 只返回翻译结果，不要任何解释\n" +
                $"文本：{text}"
        };
    }
    private string CleanTranslationResult(string rawResult)
    {
        if (string.IsNullOrEmpty(rawResult)) return "";

        string cleaned = rawResult
            .Replace("翻译结果：", "")
            .Replace("译文：", "")
            .Replace("Translation:", "")
            .Replace("以下是翻译结果：", "")
            .Trim()
            .Trim('"', '\'', '“', '”', '【', '】');

        return string.IsNullOrEmpty(cleaned) ? "" : cleaned;
    }

    private void UpdateProgress()
    {
        progress = totalStrings > 0 ? (float)completedStrings / totalStrings : 0f;
        currentStatus = $"翻译中... {completedStrings}/{totalStrings}";
        Repaint();
    }

    private void StopTranslation()
    {
        isTranslating = false;
        currentStatus = "已取消";
        Repaint();
    }

    /*        private string GetLanguageName(string localeCode)
            {
                return localeCode.ToLower() switch
                {
                    "en" or "en-us" => "english",
                    "zh" or "zh-cn" or "zh-tw" => "chinese",
                    "ja" or "ja-jp" => "japanese",
                    "ko" or "ko-kr" => "korean",
                    "fr" or "fr-fr" => "french",
                    "es" or "es-es" => "spanish",
                    "de" or "de-de" => "german",
                    "ru" or "ru-ru" => "russian",
                    "pt" or "pt-br" => "portuguese",
                    "it" or "it-it" => "italian",
                    _ => "english"
                };
            }*/
    private string GetLanguageName(string localeCode)
    {
    
       
        var match = System.Text.RegularExpressions.Regex.Match(localeCode, @"\(([^)]+)\)");
      
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        else
        {
            Debug.LogError("locale转换失败");
            return null;
        }
    
}

    // 移除括号内容的辅助方法
    private string RemoveParenthesesContent(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // 使用正则表达式去掉所有括号及其内容
        return System.Text.RegularExpressions.Regex.Replace(input, @"\s*\([^)]*\)", "").Trim();
    }

  
[System.Serializable]
private class TranslationEntry
{
    public StringTableCollection collection;
    public string key;
    public string value;
    public string tableName;
}

[System.Serializable]
private class OllamaRequest
{
    public string model;
    public string prompt;
    public bool stream;
}

[System.Serializable]
private class OllamaResponse
{
    public string model;
    public string response;
}
}
