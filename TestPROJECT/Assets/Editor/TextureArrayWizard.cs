using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TextureArrayWizard : EditorWindow
{
    private List<Texture2D> _textureList = new List<Texture2D> { null, null, null };
    private Vector2 _scrollPos;

    [MenuItem("Tools/TextureArrayWizard")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureArrayWizard>("纹理数组合成");
        window.minSize = new Vector2(400, 500);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        GUILayout.Label("草类纹理数组合成助手", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("提示：所有贴图必须具有相同的尺寸(如1024x1024)和格式。", MessageType.Info);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        
        // 动态列表绘制
        for (int i = 0; i < _textureList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _textureList[i] = (Texture2D)EditorGUILayout.ObjectField($"物种 ID [{i}]", _textureList[i], typeof(Texture2D), false);
            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                _textureList.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("添加新贴图槽位"))
        {
            _textureList.Add(null);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(20);

        // --- 合成按钮部分 ---
        GUI.backgroundColor = Color.green; // 按钮变绿，醒目
        if (GUILayout.Button("一键生成纹理数组 (TextureArray)", GUILayout.Height(60))) 
        {
            ExecuteCombine();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);
    }

    private void ExecuteCombine()
    {
        // 1. 判空检查
        List<Texture2D> validTextures = new List<Texture2D>();
        foreach (var t in _textureList)
        {
            if (t != null) validTextures.Add(t);
        }

        if (validTextures.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "你还没拖入任何贴图呢！", "我知道了");
            return;
        }

        // 2. 规格检查（以第一张贴图为基准）
        int w = validTextures[0].width;
        int h = validTextures[0].height;
        TextureFormat fmt = validTextures[0].format;

        foreach (var t in validTextures)
        {
            if (t.width != w || t.height != h)
            {
                EditorUtility.DisplayDialog("规格错误", $"贴图 [{t.name}] 尺寸为 {t.width}x{t.height}，不符合基准尺寸 {w}x{h}！", "去修改");
                return;
            }
            if (t.format != fmt)
            {
                EditorUtility.DisplayDialog("格式错误", $"贴图 [{t.name}] 格式为 {t.format}，与基准格式 {fmt} 不一致！\n请在导入设置中统一压缩格式。", "去修改");
                return;
            }
        }

        // 3. 开始合成
        string path = EditorUtility.SaveFilePanelInProject("保存纹理数组", "NewTextureArray", "asset", "选择保存位置");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            // 创建数组对象
            Texture2DArray texArray = new Texture2DArray(w, h, validTextures.Count, fmt, true);
            texArray.filterMode = validTextures[0].filterMode;
            texArray.wrapMode = validTextures[0].wrapMode;

            for (int i = 0; i < validTextures.Count; i++)
            {
                for (int m = 0; m < validTextures[i].mipmapCount; m++)
                {
                    Graphics.CopyTexture(validTextures[i], 0, m, texArray, i, m);
                }
            }

            AssetDatabase.CreateAsset(texArray, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 选中生成的资源
            Selection.activeObject = texArray;
            EditorUtility.DisplayDialog("成功", $"美哉！{validTextures.Count}张贴图已成功合成为纹理数组。\n位置：{path}", "太棒了");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("意外错误", e.Message, "好吧");
        }
    }
}