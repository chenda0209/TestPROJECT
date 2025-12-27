using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TextureArrayWizard : EditorWindow
{
    private List<Texture2D> _textureList = new List<Texture2D> { null, null, null };
    private Vector2 _scrollPos;

    // 合成选项
    private bool _generateMipmaps = true;
    private int _anisoLevel = 1;
    private FilterMode _filterMode = FilterMode.Bilinear;
    private TextureFormat _targetFormat = TextureFormat.DXT5; // 默认给个通用的

    [MenuItem("Tools/高级纹理数组合成")]
    public static void ShowWindow() => GetWindow<TextureArrayWizard>("高级纹理合成");

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("1. 贴图预览 (自动跳过空白)", EditorStyles.boldLabel);
        
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(350));
        for (int i = 0; i < _textureList.Count; i++)
        {
            DrawTextureRow(i);
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("添加新槽位 +", GUILayout.Height(30))) _textureList.Add(null);

        EditorGUILayout.Space(10);
        DrawOptions();

        EditorGUILayout.Space(10);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("开始硬件级合成", GUILayout.Height(60))) ExecuteCombine();
        GUI.backgroundColor = Color.white;
    }

    private void DrawTextureRow(int i)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        
        // 贴图框靠左
        _textureList[i] = (Texture2D)EditorGUILayout.ObjectField(GUIContent.none, _textureList[i], typeof(Texture2D), false, GUILayout.Width(80), GUILayout.Height(80));

        GUILayout.Space(10);
        if (_textureList[i] != null)
        {
            string info = $"贴图 ID: {i}\n尺寸: {_textureList[i].width}x{_textureList[i].height}\n格式: {_textureList[i].format}";
            
            // 只要格式不匹配选定的合成格式，就标红预警
            if (_textureList[i].format != _targetFormat) GUI.color = Color.red;
            
            EditorGUILayout.LabelField(info, GUILayout.ExpandWidth(true), GUILayout.Height(80));
            GUI.color = Color.white;
        }
        else
        {
            EditorGUILayout.LabelField("< 空白槽位 >", EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true), GUILayout.Height(80));
        }

        if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(80))) _textureList.RemoveAt(i);
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawOptions()
    {
        EditorGUILayout.LabelField("2. 合成参数 (必须与贴图格式一致才能硬件直传)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        _generateMipmaps = EditorGUILayout.Toggle("生成 Mipmaps", _generateMipmaps);
        _anisoLevel = EditorGUILayout.IntSlider("各向异性等级", _anisoLevel, 0, 16);
        _filterMode = (FilterMode)EditorGUILayout.EnumPopup("过滤模式", _filterMode);
        
        // 把格式选项还给你！
        _targetFormat = (TextureFormat)EditorGUILayout.EnumPopup("目标合成格式", _targetFormat);
        
        EditorGUILayout.HelpBox("提示：如果点击合成报错，请检查贴图格式是否与上方选择的格式一致。", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void ExecuteCombine()
    {
        List<Texture2D> valids = _textureList.FindAll(t => t != null);
        if (valids.Count == 0) return;

        int w = valids[0].width;
        int h = valids[0].height;

        // 硬件拷贝前最后的尊严：格式校验
        foreach (var t in valids)
        {
            if (t.format != _targetFormat)
            {
                EditorUtility.DisplayDialog("格式冲突", 
                    $"贴图 [{t.name}] 的格式是 {t.format}，\n但你选择的合成格式是 {_targetFormat}。\n\n硬件直传要求格式必须严格一致，请修改其中之一。", "知道了");
                return;
            }
        }

        string path = EditorUtility.SaveFilePanelInProject("保存数组", "FinalGrassArray", "asset", "确定");
        if (string.IsNullOrEmpty(path)) return;

        // 走硬件 Copy 路径，不需要 Read/Write 权限
        Texture2DArray array = new Texture2DArray(w, h, valids.Count, _targetFormat, _generateMipmaps, false);
        array.anisoLevel = _anisoLevel;
        array.filterMode = _filterMode;
        array.wrapMode = TextureWrapMode.Repeat;

        for (int i = 0; i < valids.Count; i++)
        {
            int mips = _generateMipmaps ? valids[i].mipmapCount : 1;
            for (int m = 0; m < mips; m++)
            {
                // GPU 内部直传
                Graphics.CopyTexture(valids[i], 0, m, array, i, m);
            }
        }

        AssetDatabase.CreateAsset(array, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = array;
        EditorUtility.DisplayDialog("完成", "硬件直传成功！", "");
    }
}