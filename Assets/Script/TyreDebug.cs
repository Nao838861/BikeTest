using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// タイヤのデバッグ情報を保持・表示するクラス
/// </summary>
public class TyreDebug : MonoBehaviour
{
    // デバッグ表示用のパラメータ
    public bool ShowDebugInfo = true;
    public float DebugTextScale = 0.01f;
    public Color TextColor = Color.white;
    public bool IsFrontWheel = true; // 前輪か後輪かを識別するフラグ
    
    // デバッグ情報
    [HideInInspector] public float SlipAngle;
    [HideInInspector] public float mSlipAngle; // Car.csで使用されているスリップアングル
    [HideInInspector] public float SlipRatio;
    [HideInInspector] public float FrictionCoefficient;
    [HideInInspector] public float ContactForce;
    [HideInInspector] public Vector3 ContactVelocity;
    [HideInInspector] public Vector3 FrictionForce;
    [HideInInspector] public float mFrictionCirclePow; // Car.csで使用されている摩擦円の大きさ
    [HideInInspector] public Vector3 mCf; // Car.csで使用されているコーナリングフォース
    [HideInInspector] public Vector3 mAcc; // Car.csで使用されている加速度
    [HideInInspector] public float mMu; // Car.csで使用されている摩擦係数
    [HideInInspector] public float Speed; // バイクの速度（m/s）
    
    // 速度計算用のRigidbody参照
    private Rigidbody cachedRigidbody;
    
    private void Start()
    {
        // 初期化時に再帰的に親を追ってRigidbodyを探す
        cachedRigidbody = FindRigidbodyInParents(transform);
        
        if (cachedRigidbody == null)
        {
            Debug.LogWarning("TyreDebug: Rigidbodyが見つかりませんでした。速度表示が正しく動作しません。");
        }
    }
    
    // 再帰的に親を追ってRigidbodyを探す関数
    private Rigidbody FindRigidbodyInParents(Transform currentTransform)
    {
        // 自分自身にRigidbodyがあればそれを返す
        Rigidbody rb = currentTransform.GetComponent<Rigidbody>();
        if (rb != null)
        {
            return rb;
        }
        
        // 親がなければnullを返す
        if (currentTransform.parent == null)
        {
            return null;
        }
        
        // 親を再帰的に探す
        return FindRigidbodyInParents(currentTransform.parent);
    }
    
    private void OnGUI()
    {
        if (!ShowDebugInfo) return;
        
        // 画面上の固定位置に表示するように変更
        Vector3 screenPos;
        
        // 前輪は右上、後輪は右下に固定表示
        if (IsFrontWheel)
        {
            // 前輪の場合は右上
            screenPos = new Vector3(Screen.width - 300, 500, 1);
        }
        else
        {
            // 後輪の場合は右下
            screenPos = new Vector3(Screen.width - 300, 300, 1);
        }
        
        // デバッグ情報を表示（大きくして視認性向上）
        GUIStyle style = new GUIStyle();
        style.normal.textColor = TextColor;
        style.fontSize = (int)(25 * DebugTextScale * 70); // フォントサイズを大きく
        style.fontStyle = FontStyle.Bold; // 太字にする
        style.alignment = TextAnchor.UpperLeft;
        
        // 表示位置を調整
        screenPos.y = Screen.height - screenPos.y;
        
        // 表示位置のオフセット
        float yOffset = 0;
        
        // タイヤの種類を表示
        string wheelType = IsFrontWheel ? "前輪" : "後輪";
        GUI.Label(new Rect(screenPos.x, screenPos.y + yOffset, 300, 30), 
                 $"{wheelType}のデバッグ情報", style);
        yOffset += 40;
        
        // バイクの時速を計算（m/sからkm/hに変換）
        float speedKmh = Speed * 3.6f; // m/s から km/h に変換
        
        // デバッグ情報を表示
        string debugText = string.Format(
            "時速     : {0:F1} km/h\n" +
            "SlipAngle: {1:F1}°\n" +
            "Friction : {2:F2}\n" +
            "Force    : {3:F0}N\n" +
            "FrictionCircle: {4:F0}",
            speedKmh,
            mSlipAngle,
            FrictionCoefficient,
            FrictionForce.magnitude,
            mFrictionCirclePow
        );
        GUI.Label(new Rect(screenPos.x, screenPos.y + yOffset, 300, 100), debugText, style);
        
        // 摩擦円の表示
        DrawFrictionCircle(screenPos);
    }
    
    private void OnDrawGizmos()
    {
        if (!ShowDebugInfo) return;
        
        // 接触点に力の方向を表示
        if (FrictionForce.magnitude > 0.01f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, FrictionForce.normalized * 0.5f);
        }
    }
    
    private void Update()
    {
        // キャッシュしたRigidbodyから速度を計算
        if (cachedRigidbody != null)
        {
            Speed = cachedRigidbody.velocity.magnitude;
        }
        else
        {
            // Rigidbodyが見つからなかった場合は再探索を試みる
            cachedRigidbody = FindRigidbodyInParents(transform);
            
            if (cachedRigidbody != null)
            {
                Speed = cachedRigidbody.velocity.magnitude;
            }
            else
            {
                Speed = 0f;
            }
        }
    }
    
    // 摩擦円を描画する関数
    private void DrawFrictionCircle(Vector3 screenPos)
    {
        if (mFrictionCirclePow <= 0) return;
        
        // 摩擦円の半径を計算（画面サイズに合わせて調整）
        // 半径を3倍に拡大して視認性を向上
        float circleRadius = Mathf.Sqrt(mFrictionCirclePow) * 6.0f;
        
        // 摩擦円の中心位置（スクリーン座標を使用）
        Vector2 circleCenter = new Vector2(screenPos.x + 150, screenPos.y + 200);
        
#if UNITY_EDITOR
        // 摩擦力の大きさを摩擦円の半径に対する比率で計算
        float forceRatio = 0f;
        if (mFrictionCirclePow > 0.01f && FrictionForce.magnitude > 0.01f)
        {
            forceRatio = FrictionForce.magnitude / mFrictionCirclePow;
        }
        
        // 摩擦限界を超えたかどうかに基づいて色を変更
        Color circleColor;
        if (forceRatio >= 1.0f)
        {
            // 摩擦限界を超えた場合は赤よりのオレンジ
            circleColor = new Color(1.0f, 0.5f, 0.0f, 0.5f); // オレンジ
        }
        else
        {
            // 通常は緑
            circleColor = new Color(0.2f, 0.8f, 0.2f, 0.5f); // 緑
        }
        
        // 摩擦円の描画
        Handles.color = circleColor;
        
        // 太いラインで円を描画するために複数の円を描画
        for (int i = 0; i < 3; i++)
        {
            Handles.DrawWireDisc(circleCenter, Vector3.forward, circleRadius - i * 0.5f);
        }
        
        // 現在の摩擦力を円内に描画
        if (FrictionForce.magnitude > 0.01f)
        {
            // 摩擦力をバイクのローカル座標系に変換
            Vector3 localForce = transform.InverseTransformDirection(FrictionForce);
            
            // 摩擦力の大きさを摩擦円の半径に対する比率で計算
            //float forceRatio = localForce.magnitude / mFrictionCirclePow;
            Vector2 forceVector = new Vector2(
                localForce.x / mFrictionCirclePow * circleRadius, // X軸が前後方向
                localForce.z / mFrictionCirclePow * circleRadius  // Z軸が左右方向
            );
            
            // 力のベクトルを描画（太い線で視認性向上）
            Handles.color = new Color(1.0f, 0.2f, 0.2f, 0.8f);
            
            // 太いラインで描画するために複数の線を描画
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = new Vector2(i * 0.5f - 1.0f, i * 0.5f - 1.0f);
                Handles.DrawLine(circleCenter + offset, circleCenter + forceVector + offset);
            }
            
            // 力の終点に大きめの円を描画
            Handles.DrawSolidDisc(circleCenter + forceVector, Vector3.forward, 3);
        }
        
        // スリップアングルの表示
        if (Mathf.Abs(SlipAngle) > 0.001f)
        {
            // スリップアングルの方向を計算（バイクのローカル座標系で）
            float slipAngleDeg = mSlipAngle; // 度数法でのスリップアングル
            
            // バイクのローカル座標系でのスリップアングルの方向を計算
            float slipRad = SlipAngle; // ラジアン単位のスリップアングル
            float slipX = Mathf.Cos(slipRad) * circleRadius * 0.8f; // X軸方向（前後）
            float slipY = Mathf.Sin(slipRad) * circleRadius * 0.8f; // Z軸方向（左右）
            
            // スリップアングルの矢印を描画（太い線で視認性向上）
            Handles.color = new Color(0.2f, 0.2f, 1.0f, 0.8f);
            
            // 太いラインで描画するために複数の線を描画
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = new Vector2(i * 0.5f - 1.0f, i * 0.5f - 1.0f);
                Handles.DrawLine(circleCenter + offset, circleCenter + new Vector2(slipX, slipY) + offset);
            }
            
            // スリップアングルの終点に大きめの円を描画
            // Handles.DrawSolidDisc(circleCenter + new Vector2(slipX, slipY), Vector3.forward, 6);
            
            // スリップアングルの値を表示（フォントサイズを大きく）
            GUIStyle slipStyle = new GUIStyle();
            slipStyle.normal.textColor = new Color(0.2f, 0.2f, 1.0f);
            slipStyle.fontSize = 24; // フォントサイズを大きく
            GUI.Label(new Rect(circleCenter.x + slipX - 20, circleCenter.y + slipY - 10, 80, 40), 
                     $"{slipAngleDeg:F1}°", slipStyle);
        }
#else
        // ビルド版ではよりシンプルな表示
        // 摩擦力の大きさを摩擦円の半径に対する比率で計算
        float forceRatio = 0f;
        if (mFrictionCirclePow > 0.01f && FrictionForce.magnitude > 0.01f)
        {
            forceRatio = FrictionForce.magnitude / mFrictionCirclePow;
        }
        
        // 摩擦限界を超えたかどうかに基づいて色を変更
        if (forceRatio >= 1.0f)
        {
            // 摩擦限界を超えた場合は赤よりのオレンジ
            GUI.color = new Color(1.0f, 0.5f, 0.0f, 0.5f); // オレンジ
        }
        else
        {
            // 通常は緑
            GUI.color = new Color(0.2f, 0.8f, 0.2f, 0.5f); // 緑
        }
        
        // 摩擦円を表す円
        GUI.DrawTexture(new Rect(circleCenter.x - circleRadius, circleCenter.y - circleRadius, 
                               circleRadius * 2, circleRadius * 2), Texture2D.whiteTexture);
        
        // 摩擦力を表示
        if (FrictionForce.magnitude > 0.01f)
        {
            GUI.color = new Color(1.0f, 0.2f, 0.2f, 0.8f);
            float forceRatio = FrictionForce.magnitude / mFrictionCirclePow;
            Vector2 forceVector = new Vector2(
                FrictionForce.x / mFrictionCirclePow * circleRadius, // X軸が前後方向
                FrictionForce.z / mFrictionCirclePow * circleRadius  // Z軸が左右方向
            );
            
            // 力の終点に小さな円を描画
            GUI.DrawTexture(new Rect(circleCenter.x + forceVector.x - 3, circleCenter.y + forceVector.y - 3, 6, 6), 
                           Texture2D.whiteTexture);
        }
#endif
    }
}
