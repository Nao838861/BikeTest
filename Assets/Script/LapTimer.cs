using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System;

/// <summary>
/// ラップタイム計測と表示を行うクラス
/// スタートオブジェクトに接触した時にタイマーを開始し、
/// 30秒以上経過してから再度スタートオブジェクトに接触するとラップタイムを記録する
/// </summary>
public class LapTimer : MonoBehaviour
{
    [Header("タイマー設定")]
    [Tooltip("ラップとして認識する最小時間（秒）")]
    public float MinimumLapTime = 30.0f;
    
    [Tooltip("記録するラップタイムの最大数")]
    public int MaxLapCount = 5;
    
    [Tooltip("スタートオブジェクトのタグ名")]
    public string StartObjectTag = "Player";
    
    [Header("バイク参照設定")]
    [Tooltip("バイクの参照（コース外判定に使用）")]
    public Bike BikeReference;
    
    [Header("データ保存設定")]
    [Tooltip("最速ラップタイムをローカルに保存するかどうか")]
    public bool EnableSaveBestLapTime = true;
    
    [Tooltip("コース名（未設定の場合はシーン名を使用）")]
    public string CourseName = "";
    
    [Tooltip("保存ファイル名（拡張子なし）")]
    public string SaveFileName = "BestLapTimes";
    
    // ラップタイムデータを保存するためのクラス
    [Serializable]
    private class LapTimeData
    {
        public List<CourseRecord> Records = new List<CourseRecord>();
    }
    
    [Serializable]
    private class CourseRecord
    {
        public string CourseName;
        public float BestTime;
    }
    
    [Header("表示設定")]
    [Tooltip("表示位置のX座標オフセット")]
    public float DisplayOffsetX = 20.0f;
    
    [Tooltip("表示位置のY座標オフセット")]
    public float DisplayOffsetY = 20.0f;
    
    [Tooltip("フォントサイズ")]
    public int FontSize = 18;
    
    [Tooltip("現在のラップタイムの色")]
    public Color CurrentLapColor = Color.white;
    
    [Tooltip("最速ラップの色")]
    public Color BestLapColor = Color.green;
    
    // タイマー状態
    private bool isTimerRunning = false;
    private float currentLapTime = 0.0f;
    private float bestLapTime = float.MaxValue;
    
    // ラップタイム履歴
    private List<float> lapTimes = new List<float>();
    
    // コースアウト状態
    private bool isCourseOut = false;
    private bool wasOnCourse = true; // 前回のコース内外状態
    private bool lapInvalidated = false; // 現在のラップが無効化されたかどうか
    
    // 前回のスタートライン通過時間
    private float lastStartLineTime = 0.0f;
    
    // スタート時のタイムスタンプ
    private float lapStartTime = 0.0f;
    
    // ラップカウント
    private int currentLap = 0;
    
    // 初期化
    void Start()
    {
        // タイマー状態をリセット
        ResetTimer();
        
        // 保存された最速ラップタイムを読み込む
        if (EnableSaveBestLapTime)
        {
            LoadBestLapTime();
        }
    }
    // 毎フレーム更新
    void Update()
    {
        // バイク参照が設定されている場合、コース内外状態を確認
        if (BikeReference != null)
        {
            // 前回の状態を保存
            wasOnCourse = !isCourseOut;
            
            // 現在の状態を更新
            isCourseOut = !BikeReference.IsOnCourse;
            
            // コース内→コース外に変化した場合の処理
            if (!wasOnCourse != isCourseOut && isCourseOut)
            {
                Debug.Log("コースアウトを検出しました");
                
                // コースアウト時にラップを無効化
                if (isTimerRunning)
                {
                    lapInvalidated = true;
                    Debug.Log("現在のラップを無効化しました");
                }
            }
        }
        
        // タイマーが動いている場合は経過時間を更新
        // ラップが無効化されていない場合のみ更新
        if (isTimerRunning && !isCourseOut && !lapInvalidated)
        {
            currentLapTime = Time.time - lapStartTime;
        }
    }
    
    /// <summary>
    /// 現在のコース名を取得する
    /// </summary>
    private string GetCurrentCourseName()
    {
        // CourseNameが設定されていればそれを使用、そうでなければシーン名を使用
        return string.IsNullOrEmpty(CourseName) ? SceneManager.GetActiveScene().name : CourseName;
    }
    
    /// <summary>
    /// JSONファイルのパスを取得する
    /// </summary>
    private string GetJsonFilePath()
    {
        // 実行ファイルがあるディレクトリに保存
        return Path.Combine(Application.dataPath, $"{SaveFileName}.json");
    }
    
    /// <summary>
    /// 最速ラップタイムをJSONファイルに保存する
    /// </summary>
    private void SaveBestLapTimeToFile()
    {
        if (!EnableSaveBestLapTime || bestLapTime == float.MaxValue) return;
        
        string courseName = GetCurrentCourseName();
        string filePath = GetJsonFilePath();
        
        // 既存のデータを読み込むか、新規作成
        LapTimeData lapTimeData = new LapTimeData();
        
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                lapTimeData = JsonUtility.FromJson<LapTimeData>(json) ?? new LapTimeData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ファイルの読み込みエラー: {ex.Message}");
                lapTimeData = new LapTimeData();
            }
        }
        
        // 既存の記録を探す
        CourseRecord existingRecord = null;
        foreach (var record in lapTimeData.Records)
        {
            if (record.CourseName == courseName)
            {
                existingRecord = record;
                break;
            }
        }
        
        // 記録がないか、より速いタイムの場合は更新
        if (existingRecord == null)
        {
            // 新しいコースの記録を追加
            CourseRecord newRecord = new CourseRecord
            {
                CourseName = courseName,
                BestTime = bestLapTime
            };
            lapTimeData.Records.Add(newRecord);
            
            try
            {
                // JSONに変換して保存
                string json = JsonUtility.ToJson(lapTimeData, true); // trueで整形されたJSONを出力
                File.WriteAllText(filePath, json);
                Debug.Log($"新しい最速ラップタイムを保存しました: {courseName} - {FormatTime(bestLapTime)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ファイルの保存エラー: {ex.Message}");
            }
        }
        else if (bestLapTime < existingRecord.BestTime)
        {
            // 既存の記録を更新
            existingRecord.BestTime = bestLapTime;
            
            try
            {
                // JSONに変換して保存
                string json = JsonUtility.ToJson(lapTimeData, true);
                File.WriteAllText(filePath, json);
                Debug.Log($"新しい最速ラップタイムを保存しました: {courseName} - {FormatTime(bestLapTime)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ファイルの保存エラー: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 保存された最速ラップタイムをJSONファイルから読み込む
    /// </summary>
    private void LoadBestLapTime()
    {
        string courseName = GetCurrentCourseName();
        string filePath = GetJsonFilePath();
        
        // ファイルが存在すれば読み込む
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                LapTimeData lapTimeData = JsonUtility.FromJson<LapTimeData>(json);
                
                if (lapTimeData != null)
                {
                    // 対象コースの記録を探す
                    foreach (var record in lapTimeData.Records)
                    {
                        if (record.CourseName == courseName)
                        {
                            bestLapTime = record.BestTime;
                            Debug.Log($"保存された最速ラップタイムを読み込みました: {courseName} - {FormatTime(bestLapTime)}");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ファイルの読み込みエラー: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// タイマーをリセットする
    /// </summary>
    public void ResetTimer()
    {
        isTimerRunning = false;
        currentLapTime = 0.0f;
        lapStartTime = 0.0f;
        lastStartLineTime = 0.0f;
        currentLap = 0;
        lapTimes.Clear();
        bestLapTime = float.MaxValue;
    }
    
    /// <summary>
    /// タイマーを開始する
    /// </summary>
    public void StartTimer()
    {
        lapStartTime = Time.time;
        lastStartLineTime = Time.time;
        isTimerRunning = true;
        currentLapTime = 0.0f;
        lapInvalidated = false; // ラップ無効化状態をリセット
    }
    
    /// <summary>
    /// ラップを完了し、記録を更新する
    /// </summary>
    private void CompleteLap()
    {
        // ラップタイムが最小ラップタイムより大きい場合のみ記録
        if (currentLapTime >= MinimumLapTime && !lapInvalidated)
        {
            // ラップタイムを記録
            lapTimes.Add(currentLapTime);
            
            // 最速ラップタイムを更新
            if (currentLapTime < bestLapTime)
            {
                bestLapTime = currentLapTime;
                Debug.Log($"新しい最速ラップタイム: {FormatTime(bestLapTime)}");
                
                // 最速ラップタイムをローカルに保存
                if (EnableSaveBestLapTime)
                {
                    SaveBestLapTimeToFile();
                }
            }
            
            Debug.Log($"ラップ完了: {FormatTime(currentLapTime)}");
        }
        else if (lapInvalidated)
        {
            Debug.Log("無効化されたラップのため、記録されませんでした");
            lapInvalidated = false; // 次のラップのためにリセット
        }
        else
        {
            Debug.Log($"ラップタイムが短すぎるため記録されませんでした: {FormatTime(currentLapTime)}");
        }
        
        // 新しいラップを開始
        StartTimer();
    }
    
    /// <summary>
    /// 衝突判定（トリガーによる）
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("衝突検出:" + other.tag);
        // スタートオブジェクトとの衝突を検出
        if (other.CompareTag(StartObjectTag))
        {
            // 現在の時間を取得
            float currentTime = Time.time;
            
            // タイマーが動いていない場合は開始
            if (!isTimerRunning)
            {
                StartTimer();
                Debug.Log("タイマー開始");
            }
            // タイマーが動いていて、最小ラップタイム以上経過している場合はラップ完了
            else if (currentTime - lastStartLineTime >= MinimumLapTime && !lapInvalidated)
            {
                CompleteLap();
                Debug.Log($"ラップ完了: {FormatTime(currentLapTime)}");
            }
            // ラップが無効化されている場合は新しいラップを開始
            else if (lapInvalidated)
            {
                Debug.Log("無効化されたラップをリセットし、新しいラップを開始します");
                StartTimer(); // タイマーをリセットして新しいラップを開始
            }
            // 最小ラップタイム未満の場合は無視（誤検出防止）
            else
            {
                Debug.Log($"最小ラップタイム未満のため無視: {currentTime - lastStartLineTime}秒");
            }
        }
    }
    
    /// <summary>
    /// 時間を「分:秒.ミリ秒」形式にフォーマットする
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        int minutes = (int)(timeInSeconds / 60);
        int seconds = (int)(timeInSeconds % 60);
        int milliseconds = (int)((timeInSeconds * 1000) % 1000);
        
        return string.Format("{0:00}:{1:00}.{2:000}", minutes, seconds, milliseconds);
    }
    
    /// <summary>
    /// GUI表示
    /// </summary>
    void OnGUI()
    {
        // フォントスタイルを設定
        GUIStyle style = new GUIStyle();
        style.fontSize = FontSize;
        style.normal.textColor = CurrentLapColor;
        
        // 最速ラップ用のスタイル
        GUIStyle bestLapStyle = new GUIStyle();
        bestLapStyle.fontSize = FontSize;
        bestLapStyle.normal.textColor = BestLapColor;
        
        // コースアウト警告用のスタイル
        GUIStyle warningStyle = new GUIStyle();
        warningStyle.fontSize = FontSize + 4;
        warningStyle.fontStyle = FontStyle.Bold;
        warningStyle.normal.textColor = Color.red;
        
        // コースアウト時は警告表示
        float x = DisplayOffsetX;
        float y = DisplayOffsetY;
        
        // コースアウトか、またはラップが無効化されている場合に警告表示
        if (isTimerRunning && (isCourseOut || lapInvalidated))
        {
            GUI.Label(new Rect(x, y, 300, 30), "コースアウト!", warningStyle);
            y += 30;
            
            GUI.Label(new Rect(x, y, 300, 20), "ラップタイム計測停止", 
                new GUIStyle() { fontSize = FontSize, normal = { textColor = Color.yellow } });
            y += 30;
        }
        
        // 現在のラップタイムを表示
        GUI.Label(new Rect(x, y, 300, 20), 
            $"現在ラップ: {FormatTime(currentLapTime)}", 
            style);
        y += 25;
        
        // 最速ラップタイムを表示（記録がある場合のみ）
        if (bestLapTime < float.MaxValue)
        {
            GUI.Label(new Rect(x, y, 300, 20), 
                $"最速ラップ: {FormatTime(bestLapTime)}", 
                bestLapStyle);
            y += 25;
        }
        
        // ラップ履歴を表示
        GUI.Label(new Rect(x, y, 300, 30), "ラップ履歴:", style);
        y += 25;
        
        // 各ラップタイムを表示
        for (int i = lapTimes.Count - 1; i >= 0; i--)
        {
            float lapTime = lapTimes[i];
            string lapText = $"Lap {currentLap - (lapTimes.Count - 1 - i)}: {FormatTime(lapTime)}";
            
            // 最速ラップは強調表示
            if (Mathf.Approximately(lapTime, bestLapTime))
            {
                GUI.Label(new Rect(x, y, 300, 25), lapText, bestLapStyle);
            }
            else
            {
                GUI.Label(new Rect(x, y, 300, 25), lapText, style);
            }
            
            y += 25;
        }
    }
}
