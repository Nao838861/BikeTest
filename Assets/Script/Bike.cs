using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自転車の制御を行うクラス
/// キーボード入力を受け取り、タイヤに駆動力を伝える
/// </summary>
public class Bike : MonoBehaviour
{
    [Header("タイヤ設定")]
    [Tooltip("後輪のTyreSpringコンポーネント")]
    public TyreSpring RearWheel;
    [Tooltip("前輪のTyreSpringコンポーネント")]
    public TyreSpring FrontWheel;
    
    [Header("ハンドル設定")]
    [Tooltip("ハンドルのGameObject")]
    public Transform HandleTransform;
    [Tooltip("ハンドルの最大回転角度")]
    public float MaxHandleAngle = 30.0f;
    [Tooltip("低速時のハンドルトルク倍率（速度に応じてトルクが変化）")]
    public float LowSpeedHandleTorqueMultiplier = 1.5f;
    [Tooltip("低速域の上限速度（m/s）、これ以上で通常のトルクになる")]
    public float LowSpeedThreshold = 5.5f;  // 約時速20km
    [Tooltip("ハンドルの慣性モーメント（値が大きいほど動きに抜かりがある）")]
    public float HandleInertiaMoment = 0.5f;
    [Tooltip("ハンドルの減衰係数（値が大きいほど速く止まる）")]
    public float HandleDamping = 5.0f;
    [Tooltip("ハンドルの自動中心化トルク")]
    public float HandleCenteringTorque = 2.0f;
    [Tooltip("ハンドル入力中の中心化トルクの倍率（0で完全に無効、1で通常通り）")]
    public float SteeringCenteringMultiplier = 0.5f;
    [Tooltip("セルフステアの強さ（傾きによるハンドル回転の強さ）")]
    public float SelfSteerStrength = 5.0f;
    [Tooltip("セルフステアが有効になる最低速度（m/s）")]
    public float SelfSteerMinSpeed = 2.0f;
    [Tooltip("キャスター角（度）")]
    public float CasterAngle = 25.0f;
    [Tooltip("キャスター効果の強さ")]
    public float CasterEffectStrength = 2.0f;
    [Tooltip("キャスター効果が最大になる速度（m/s）")]
    public float CasterMaxEffectSpeed = 10.0f;
    
    [Header("入力設定")]
    [Tooltip("加速の感度")]
    public float AccelerationSensitivity = 1.0f;
    [Tooltip("ブレーキの感度")]
    public float BrakeSensitivity = 1.0f;
    [Tooltip("入力の滑らかさ（値が大きいほど滑らか）")]
    public float InputSmoothness = 5.0f;
    [Tooltip("最大速度制限（m/s）、これ以上でアクセルの効果が順次減少")]
    public float MaxSpeedThreshold = 16.7f;  // 約60km/h
    
    [Header("安定化設定")]
    [Tooltip("安定化機能を有効にするかどうか")]
    public bool EnableStabilization = true;
    [Tooltip("安定化の比例項ゲイン")]
    public float StabilizationP = 0.5f;
    [Tooltip("安定化の積分項ゲイン")]
    public float StabilizationI = 0.05f;
    [Tooltip("安定化の微分項ゲイン")]
    public float StabilizationD = 0.2f;
    [Tooltip("積分項の最大値（ワインドアップ防止用）")]
    public float IntegralMax = 5.0f;
    [Tooltip("目標の上向きベクトル（通常はVector3.up）")]
    public Vector3 TargetUpDirection = Vector3.up;
    
    [Header("傾き設定")]
    [Tooltip("ハンドル操作による傾きの強さ（値が大きいほど強く傾く）")]
    public float LeanStrength = 0.5f;
    [Tooltip("最大傾き角度（度）")]
    public float MaxLeanAngle = 30.0f;
    [Tooltip("傾きの滑らかさ（値が大きいほど滑らか）")]
    public float LeanSmoothness = 2.0f;
    
    [Header("デバッグ")]
    [Tooltip("現在の入力値")]
    [ReadOnly]
    public float CurrentDriveInput = 0.0f;
    [Tooltip("現在の傾き角度（全体）")]
    [ReadOnly]
    public float CurrentTiltAngle = 0.0f;
    [Tooltip("現在のハンドル角度")]
    [ReadOnly]
    public float CurrentHandleAngle = 0.0f;
    [Tooltip("左右方向の傾き角度（ロール）")]
    [ReadOnly]
    public float CurrentRollAngle = 0.0f;
    [Tooltip("前後方向の傾き角度（ピッチ）")]
    [ReadOnly]
    public float CurrentPitchAngle = 0.0f;
    [Tooltip("PID制御の出力値")]
    [ReadOnly]
    public float pidOutputValue = 0.0f;
    
    // PID制御用の変数
    [Header("デバッグ設定")]
    public bool ShowDebugInfo = true;
    public bool ShowForceVectors = true;
    public float ForceVectorScale = 0.1f;
    
    [Header("機能のON/OFF切り替え")]
    [Tooltip("セルフステア機能の有効/無効")]
    public bool EnableSelfSteer = true;
    [Tooltip("キャスター効果の有効/無効")]
    public bool EnableCasterEffect = true;
    [Tooltip("PID制御の有効/無効（安定化力）")]
    public bool EnablePIDControl = true;
    
    // 内部変数
    private float targetDriveInput = 0.0f;  // 目標駆動入力
    private float targetLeanAngle = 0.0f;  // プレイヤーが指定する目標傾き角度
    private Rigidbody rb;
    private Transform handleTransform;
    
    [Header("傾き入力設定")]
    [Tooltip("傾きの蓄積速度（値が大きいほど早く最大傾きに達する）")]
    public float LeanAccumulationRate = 30.0f;
    [Tooltip("傾きの減衰速度（値が大きいほど早く傾きが戻る）")]
    public float LeanDecayRate = 15.0f;
    
    // ハンドルの物理パラメータ
    private float HandleAngularVelocity = 0f;   // ハンドルの角速度
    
    // デバッグ用ベクトル表示用変数
    private Vector3 leanTorqueVector = Vector3.zero;
    private Vector3 stabilizationTorqueVector = Vector3.zero;
    private Vector3 leanTorquePosition;
    private Vector3 stabilizationTorquePosition;
    private float selfSteerTorqueValue = 0f; // セルフステアトルクの値
    private float casterEffectValue = 0f;   // キャスター効果の値
    
    // PID制御用変数
    private float rollErrorPrev = 0f;      // 前回のエラー値
    private float rollErrorIntegral = 0f;   // エラーの積分値
    private float currentRollAngleValue = 0f; // 現在のロール角度（デバッグ用）
    private float targetRollAngleValue = 0f;  // 目標ロール角度（デバッグ用）
    private float rollErrorValue = 0f;        // ロール角度のエラー（差分）（デバッグ用）
    private float rollErrorDerivativeValue = 0f; // エラーの微分値（デバッグ用）
    private float rollErrorIntegralValue = 0f;   // エラーの積分値（デバッグ用）

    // Start is called before the first frame update
    void Start()
    {
        // Rigidbodyコンポーネントを取得
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Bike: Rigidbodyコンポーネントが見つかりません");
            enabled = false;
            return;
        }
        
        // タイヤが設定されているか確認
        if (RearWheel == null)
        {
            Debug.LogError("Bike: 後輪のTyreSpringが設定されていません");
            enabled = false;
            return;
        }
        
        // ハンドルが設定されているか確認
        if (HandleTransform == null)
        {
            Debug.LogError("Bike: ハンドルのTransformが設定されていません");
            enabled = false;
            return;
        }
        
        // 後輪を駆動輪に設定
        RearWheel.IsDriveWheel = true;
    }
    
    // Update is called once per frame
    void Update()
    {
        // キーボード入力の処理
        ProcessInput();
    }
    
    void FixedUpdate()
    {
        // 駆動力の適用
        ApplyDriveForce();
        
        // 安定化力の適用
        if (EnableStabilization && EnablePIDControl)
        {
            ApplyStabilizationForce();
        }
        
        // ハンドルの回転を適用
        ApplyHandleRotation();
        
        // デバッグ情報の更新
        UpdateDebugInfo();
    }
    
    // ハンドル入力中かどうかを追跡する変数
    private bool isSteeringInput = false;
    
    // キーボード入力の処理
    void ProcessInput()
    {
        // 上下キーの入力を取得（前後移動用）
        float verticalInput = Input.GetAxis("Vertical");
        
        // 左右キーの入力を取得（傾き制御用）
        float horizontalInput = Input.GetAxis("Horizontal");
        
        // ハンドル入力中かどうかを更新
        isSteeringInput = Mathf.Abs(horizontalInput) > 0.01f;
        
        // 入力に基づいて目標駆動力を設定
        if (verticalInput > 0)
        {
            // 前進（アクセル）
            targetDriveInput = verticalInput * AccelerationSensitivity;
        }
        else if (verticalInput < 0)
        {
            // 後退（バック）
            targetDriveInput = verticalInput * BrakeSensitivity;
        }
        else
        {
            // 入力がない場合は微々に減速
            targetDriveInput = 0f;
        }
        
        // 左右の入力を傾き制御に使用
        // プレイヤーは傾きのみを制御し、ハンドルは自動的に回転する
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            // プレイヤーの入力に基づいて目標傾き角度を徐々に蓄積
            // 入力方向に応じて蓄積速度を調整
            float targetMaxAngle = horizontalInput * MaxLeanAngle;
            float accumulationSpeed = LeanAccumulationRate * Time.deltaTime;
            
            // 現在の傾き角度と目標最大角度の符号が同じ場合（同じ方向に傾いている場合）
            if (Mathf.Sign(targetLeanAngle) == Mathf.Sign(targetMaxAngle) || targetLeanAngle == 0)
            {
                // 目標角度に向かって蓄積
                targetLeanAngle = Mathf.MoveTowards(targetLeanAngle, targetMaxAngle, accumulationSpeed);
            }
            else
            {
                // 逆方向の場合は、まず0に戻してから反対方向に傾ける（より速く方向転換）
                float decaySpeed = LeanDecayRate * Time.deltaTime;
                targetLeanAngle = Mathf.MoveTowards(targetLeanAngle, 0, decaySpeed);
                
                // 0になったら新しい方向に傾き始める
                if (Mathf.Approximately(targetLeanAngle, 0))
                {
                    targetLeanAngle = Mathf.MoveTowards(targetLeanAngle, targetMaxAngle, accumulationSpeed);
                }
            }
        }
        else
        {
            // 入力がない場合は目標傾き角度を徐々にゼロに戻す
            float decaySpeed = LeanDecayRate * Time.deltaTime;
            targetLeanAngle = Mathf.MoveTowards(targetLeanAngle, 0, decaySpeed);
        }
    }
    
    // 駆動力の適用
    void ApplyDriveForce()
    {
        // 現在の入力値を目標値に滑らかに近づける
        CurrentDriveInput = Mathf.Lerp(
            CurrentDriveInput,
            targetDriveInput,
            Time.fixedDeltaTime * InputSmoothness
        );
        
        // 速度に応じた駆動力の調整係数を計算
        float speedFactor = 1.0f;
        if (rb != null && rb.velocity.magnitude > 0)
        {
            float speed = rb.velocity.magnitude;
            
            // 最大速度を超えると加速力が減少する
            if (speed > MaxSpeedThreshold)
            {
                // 速度が速いほど加速力が小さくなる
                // 最大速度の1.2倍でほぼゼロになるように設定
                float overSpeedRatio = (speed - MaxSpeedThreshold) / (MaxSpeedThreshold * 0.2f);
                speedFactor = Mathf.Clamp01(1.0f - overSpeedRatio);
                
                // アクセル入力が正の場合のみ加速力を制限
                if (CurrentDriveInput > 0)
                {
                    CurrentDriveInput *= speedFactor;
                }
            }
        }
        
        // 後輪に駆動力を設定
        if (RearWheel != null)
        {
            RearWheel.DriveInput = CurrentDriveInput;
        }
    }
    
    /// <summary>
    /// ハンドルの回転を物理演算で適用する関数
    /// セルフステアとキャスター効果によってハンドルが自動的に回転する
    /// </summary>
    void ApplyHandleRotation()
    {
        if (HandleTransform == null) return;
        
        // ハンドルの物理演算を行う
        // 1. プレイヤーからの入力トルクは0（プレイヤーはハンドルを操作しない）
        float totalTorque = 0f;
        
        // 中心復帰力はキャスター効果の一部として実装するため、ここでは低速時のみの弱い中心復帰力を設定
        // 低速時には重力と慣性モーメントによる弱い中心復帰力が働く
        // ハンドル入力中は中心復帰力を調整する
        float centeringMultiplier = isSteeringInput ? SteeringCenteringMultiplier : 1.0f;
        float lowSpeedCenteringTorque = -CurrentHandleAngle * HandleCenteringTorque * 0.5f * centeringMultiplier;
        totalTorque += lowSpeedCenteringTorque;
        
        // 3. セルフステア機能とキャスター効果の計算
        float selfSteerTorque = 0f;
        float casterTorque = 0f;
        
        // 速度が一定以上ある場合のみ処理を適用
        if (rb != null && rb.velocity.magnitude > SelfSteerMinSpeed)
        {
            // 現在のバイクの向きを取得
            Vector3 currentUp = transform.up;
            Vector3 currentForward = transform.forward;
            
            // バイクの前後方向を地面に投影
            Vector3 forwardOnGround = Vector3.ProjectOnPlane(currentForward, Vector3.up).normalized;
            
            // 現在の左右の傾き角度を計算
            Vector3 sideProjection = Vector3.ProjectOnPlane(currentUp, forwardOnGround).normalized;
            float currentRollAngle = Vector3.SignedAngle(Vector3.up, sideProjection, forwardOnGround);
            
            float speed = rb.velocity.magnitude;
            float speedFactor = 1.0f;//Mathf.Clamp01((speed - SelfSteerMinSpeed) / 5.0f);
            
            // セルフステア機能が有効な場合
            if (EnableSelfSteer)
            {
                // 傾き角度に応じたセルフステアトルクを計算
                // 右に傾いている場合（負の角度）は右にハンドルが切れるようにトルクを加える
                selfSteerTorque = -currentRollAngle * SelfSteerStrength * speedFactor;
                
                // トルクに追加
                totalTorque += selfSteerTorque;
            }
            
            // キャスター効果が有効な場合
            if (EnableCasterEffect)
            {
                // キャスター角効果の計算
                // 速度が速いほどキャスター効果が強くなる
                float casterSpeedFactor = Mathf.Clamp01(speed / CasterMaxEffectSpeed);
                
                // キャスター角による効果を計算
                // キャスター角が大きいほど、傾きに対する自己修正力が強くなる
                float casterEffect = Mathf.Sin(CasterAngle * Mathf.Deg2Rad) * CasterEffectStrength * casterSpeedFactor;
                
                // 1. 傾きに応じたキャスタートルクの計算
                float rollBasedTorque = -currentRollAngle * casterEffect * speedFactor;
                
                // 2. ハンドル角度に応じた中心復帰トルクの計算
                // 速度が速いほど中心復帰力が強くなるトレール効果
                float trailEffect = Mathf.Sin(CasterAngle * Mathf.Deg2Rad) * casterSpeedFactor * 2.0f;
                
                // ハンドル入力中は中心復帰力を調整する
                float casterCenteringMultiplier = isSteeringInput ? SteeringCenteringMultiplier : 1.0f;
                float centeringTorque = -CurrentHandleAngle * trailEffect * casterCenteringMultiplier;

                // 3. ジャイロ効果による安定化トルク
                // 速度が速いほど強くなる
                //float gyroEffect = Mathf.Clamp01(speed / 15.0f) * 1.5f;
                //float gyroTorque = -HandleAngularVelocity * gyroEffect;
                float gyroTorque = 0;

                // 全てのトルクを組み合わせる
                casterTorque = rollBasedTorque + centeringTorque + gyroTorque;
                
                // トルクに追加
                totalTorque += casterTorque;
                
                // デバッグ用にキャスター効果の値を保存
                casterEffectValue = casterEffect;
            }
        }
        
        // 4. 減衰トルクを計算
        float dampingTorque = -HandleAngularVelocity * HandleDamping;
        totalTorque += dampingTorque;
        
        // 5. 角速度を更新
        // 角加速度 = トルク / 慣性モーメント
        float angularAcceleration = totalTorque / HandleInertiaMoment;
        HandleAngularVelocity += angularAcceleration * Time.fixedDeltaTime;
        
        // 6. 角度を更新
        CurrentHandleAngle += HandleAngularVelocity * Time.fixedDeltaTime;
        
        // 7. 速度に応じたトルク調整を適用
        // 低速域ではトルクを増加させ、ハンドルが傾きやすくなるようにする
        if (rb != null)
        {
            float speed = rb.velocity.magnitude;
            if (speed < LowSpeedThreshold)
            {
                // 低速域ではトルクを大きくする
                // 速度が遅いほど大きくなるように計算
                float speedFactor = 1.0f - (speed / LowSpeedThreshold);
                float torqueMultiplier = 1.0f + (LowSpeedHandleTorqueMultiplier - 1.0f) * speedFactor;
                
                // 現在のハンドル角度に基づいてトルクを追加
                float lowSpeedExtraTorque = -CurrentHandleAngle * 0.2f * torqueMultiplier;
                totalTorque += lowSpeedExtraTorque;
                
                // 角速度を更新（トルク調整後）
                float extraAcceleration = lowSpeedExtraTorque / HandleInertiaMoment;
                HandleAngularVelocity += extraAcceleration * Time.fixedDeltaTime;
            }
        }
        
        // ハンドル角度を最大角度に制限
        CurrentHandleAngle = Mathf.Clamp(CurrentHandleAngle, -MaxHandleAngle, MaxHandleAngle);
        
        // 最大角度に達した場合、角速度を減らす
        if ((CurrentHandleAngle == MaxHandleAngle && HandleAngularVelocity > 0) ||
            (CurrentHandleAngle == -MaxHandleAngle && HandleAngularVelocity < 0))
        {
            HandleAngularVelocity = 0f;
        }
        
        // デバッグ用にセルフステアトルクの値を保存
        selfSteerTorqueValue = selfSteerTorque;
        
        // ハンドルの回転を適用（X軸回転）
        HandleTransform.localRotation = Quaternion.Euler(CurrentHandleAngle, 0, 0);
    }
        
        /// <summary>
        /// デバッグ情報を更新する
        /// </summary>
        void UpdateDebugInfo()
        {
            if (rb == null) return;
            
            // 現在のバイクの向きを取得
            Vector3 currentUp = transform.up;
            Vector3 currentForward = transform.forward;
            
            // バイクの前後方向を地面に投影
            Vector3 forwardOnGround = Vector3.ProjectOnPlane(currentForward, Vector3.up).normalized;
            
            // バイクの横方向を計算
            Vector3 bikeRight = Vector3.Cross(forwardOnGround, Vector3.up).normalized;
            
            // 全体の傾き角度を計算
            CurrentTiltAngle = Vector3.Angle(currentUp, Vector3.up);
            
            // 左右方向の傾き角度（ロール）を計算
            Vector3 idealUp = Vector3.up;
            Vector3 debugSideProjection = Vector3.ProjectOnPlane(currentUp, forwardOnGround);
            CurrentRollAngle = Vector3.SignedAngle(idealUp, debugSideProjection, forwardOnGround);
            
            // 前後方向の傾き角度（ピッチ）を計算
            Vector3 frontBackDirection = Vector3.Cross(bikeRight, Vector3.up).normalized;
            Vector3 frontProjection = Vector3.ProjectOnPlane(currentUp, bikeRight);
            CurrentPitchAngle = Vector3.SignedAngle(idealUp, frontProjection, bikeRight);
            
            // トルクベクトルを表示
            DrawForceVectors();
        }
        
        /// <summary>
        /// デバッグ情報を画面に表示する
        /// </summary>
        void OnGUI()
        {
            if (!ShowDebugInfo) return;
            
            // デバッグ情報の表示位置とサイズを設定
            int width = 300;
            int height = 300;
            int padding = 10;
            int lineHeight = 20;
            
            // 画面右上に表示
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 16;
            
            // 背景を半透明にする
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
            texture.Apply();
            style.normal.background = texture;
            
            // デバッグ情報を表示
            GUI.Box(new Rect(Screen.width - width - padding, padding, width, height), "", style);
            
            // デバッグテキストのスタイル
            GUIStyle textStyle = new GUIStyle();
            textStyle.normal.textColor = Color.white;
            textStyle.fontSize = 14;
            textStyle.alignment = TextAnchor.MiddleLeft;
            
            // 各種情報を表示
            int y = padding + 5;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"左右の傾き（Roll）: {CurrentRollAngle:F1}度", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"前後の傾き（Pitch）: {CurrentPitchAngle:F1}度", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"全体の傾き: {CurrentTiltAngle:F1}度", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"ハンドル角度: {CurrentHandleAngle:F1}度", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"アクセル入力: {CurrentDriveInput:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"プレイヤー目標傾き: {targetLeanAngle:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"セルフステアトルク: {selfSteerTorqueValue:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"キャスター効果: {casterEffectValue:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"現在角度: {currentRollAngleValue:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"目標角度: {targetRollAngleValue:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"PID出力: {pidOutputValue:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"P 角度差分: {rollErrorValue:F2} {rollErrorPrev:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"I 積分項: {rollErrorIntegralValue:F2}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"D 微分項: {rollErrorDerivativeValue:F2}", textStyle);
            y += lineHeight;
/*            
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"セルフステア: {(EnableSelfSteer ? "ON" : "OFF")}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"キャスター効果: {(EnableCasterEffect ? "ON" : "OFF")}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"PID制御: {(EnablePIDControl ? "ON" : "OFF")}", textStyle);
            y += lineHeight;
 */           
            // 速度情報の表示
            float currentSpeed = 0f;
            float speedKmh = 0f;
            if (rb != null)
            {
                currentSpeed = rb.velocity.magnitude;
                speedKmh = currentSpeed * 3.6f; // m/sからkm/hに変換
            }
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"速度: {speedKmh:F1} km/h", textStyle);
            y += lineHeight;
/*            
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"最大速度: {MaxSpeedThreshold * 3.6f:F1} km/h", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"ハンドル入力: {(isSteeringInput ? "ON" : "OFF")}", textStyle);
            y += lineHeight;
            GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"ハンドル入力中の中心化倍率: {SteeringCenteringMultiplier:F2}", textStyle);
*/            
        }
        
        /// <summary>
        /// トルクベクトルをゲーム画面に表示する
        /// </summary>
        void DrawForceVectors()
        {
            if (!ShowForceVectors) return;
            
            // ハンドル操作による傾きトルクを赤で表示
            Debug.DrawLine(leanTorquePosition, leanTorquePosition + leanTorqueVector * ForceVectorScale, Color.red, Time.deltaTime);
            
            // 安定化トルクを青で表示
            Debug.DrawLine(stabilizationTorquePosition, stabilizationTorquePosition + stabilizationTorqueVector * ForceVectorScale, Color.blue, Time.deltaTime);
    }
    
    
    
    /// <summary>
    /// バイクの安定化力をPID制御で適用する
    /// プレイヤーの入力（targetLeanAngle）を目標角度として使用
    /// ロール（左右の傾き）のみを補正し、ピッチ（前後の傾き）は補正しない
    /// </summary>
    void ApplyStabilizationForce()
    {
        if (rb == null || !EnableStabilization) return;
        
        // 現在のバイクの向きを取得
        Vector3 currentUp = transform.up;
        Vector3 currentForward = transform.forward;
        
        // バイクの前後方向を地面に投影
        Vector3 forwardOnGround = Vector3.ProjectOnPlane(currentForward, Vector3.up).normalized;
        Vector3 bikeRight = Vector3.Cross(forwardOnGround, Vector3.up).normalized;
        
        // 現在の左右の傾き角度を計算
        Vector3 sideProjection = Vector3.ProjectOnPlane(currentUp, forwardOnGround).normalized;
        float currentRollAngle = Vector3.SignedAngle(Vector3.up, sideProjection, forwardOnGround);
        
        // プレイヤーの入力を目標角度として使用
        float targetRollAngle = targetLeanAngle;
        
        // 速度に応じて目標角度を調整（低速時は傾きを抑制）
        Vector3 velocity = rb.velocity;
        float speed = velocity.magnitude;
        
        if (speed < 2.0f)
        {
            // 低速時は傾きを抑制
            float speedFactor = speed / 2.0f;
            targetRollAngle *= speedFactor;
        }
        
        // PID制御の計算
        float rollError = targetRollAngle - currentRollAngle;
        
        // 積分項の計算（積分ワインドアップ防止）
        rollErrorIntegral += rollError * Time.fixedDeltaTime;
        /*
        // 動的減衰：エラーが小さい時は積分値を減衰
        if (Mathf.Abs(rollError) < 2.0f)
        {
            rollErrorIntegral *= 0.98f; // 毎フレーム2%減衰
        }
        */
        rollErrorIntegral = Mathf.Clamp(rollErrorIntegral, -IntegralMax, IntegralMax);
        
        // 微分項の計算
        float rollErrorDerivative = (rollError - rollErrorPrev) / Time.fixedDeltaTime;
        rollErrorPrev = rollError;
        
        // PID出力の計算
        float pidOutput = (rollError * StabilizationP) + 
                         (rollErrorIntegral * StabilizationI) + 
                         (rollErrorDerivative * StabilizationD);
        
        // デバッグ用にPID出力値を保存
        pidOutputValue = pidOutput;
        
        // トルクベクトルを計算 - 前後方向の軸で回転させることで横方向の傾きを制御
        Vector3 stabilizationTorque = forwardOnGround * pidOutput * 0.5f;
        
        // トルクを適用
        rb.AddTorque(stabilizationTorque, ForceMode.Force);
        
        // デバッグ用に角度値、エラー値、微分値、積分値を保存
        currentRollAngleValue = currentRollAngle;
        targetRollAngleValue = targetRollAngle;
        rollErrorValue = rollError;
        rollErrorDerivativeValue = rollErrorDerivative;
        rollErrorIntegralValue = rollErrorIntegral;
        
        // デバッグ用にトルクベクトルを保存
        stabilizationTorqueVector = stabilizationTorque;
        stabilizationTorquePosition = transform.position + Vector3.up * 0.7f; // バイクの上部に表示
    }
}
