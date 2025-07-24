using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移を管理するクラス
/// F1キーでSampleScene、F2キーでTerrainSceneに遷移する
/// </summary>
public class SceneChanger : MonoBehaviour
{
    // シーン名を定数として定義
    private const string SAMPLE_SCENE = "SampleScene";
    private const string TERRAIN_SCENE = "TerrainScene";
    
    // 現在のシーン名を表示するためのUI用変数
    [SerializeField]
    private bool showSceneNameOnGUI = true;
    
    // Update is called once per frame
    void Update()
    {
        // F1キーが押されたらSampleSceneに遷移
        if (Input.GetKeyDown(KeyCode.F1))
        {
            LoadScene(SAMPLE_SCENE);
        }
        
        // F2キーが押されたらTerrainSceneに遷移
        if (Input.GetKeyDown(KeyCode.F2))
        {
            LoadScene(TERRAIN_SCENE);
        }
    }
    
    /// <summary>
    /// 指定されたシーンを読み込む
    /// </summary>
    /// <param name="sceneName">読み込むシーン名</param>
    private void LoadScene(string sceneName)
    {
        Debug.Log($"シーン遷移: {sceneName}に移動します");
        
        try
        {
            // シーンを読み込む
            SceneManager.LoadScene(sceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"シーン遷移エラー: {sceneName}の読み込みに失敗しました。\n{e.Message}");
        }
    }
    
    // シーン名をGUIに表示
    private void OnGUI()
    {
        if (showSceneNameOnGUI)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            GUI.Label(new Rect(10, 10, 300, 20), $"現在のシーン: {currentScene}");
            GUI.Label(new Rect(10, 30, 300, 20), "F1: SampleScene / F2: TerrainScene");
        }
    }
}
