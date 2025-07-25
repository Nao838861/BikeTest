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
    public float LowSpeedHandleTorqueMultiplier = 30.0f;
    [Tooltip("低速域の上限速度（m/s）、これ以上で通常のトルクになる")]
    public float LowSpeedThreshold = 5.5f;  // 約時速20km
    [Tooltip("ハンドルの慣性モーメント（値が大きいほど動きに抜かりがある）")]
    public float HandleInertiaMoment = 0.5f;
    [Tooltip("ハンドルの減衰係数（値が大きいほど速く止まる）")]
    public float HandleDamping = 20.0f;
    [Tooltip("ハンドルの自動中心化トルク")]
    public float HandleCenteringTorque = 200.0f;
    [Tooltip("ハンドル入力中の中心化トルクの倍率（0で完全に無効、1で通常通り）")]
    public float SteeringCenteringMultiplier = 0.5f;
    [Tooltip("セルフステアの強さ（傾きによるハンドル回転の強さ）")]
    public float SelfSteerStrength = 5.0f;
    [Tooltip("セルフステアが有効になる最低速度（m/s）")]
    public float SelfSteerMinSpeed = 0.0f;
    [Tooltip("キャスター角（度）")]
    public float CasterAngle = 25.0f;
    [Tooltip("キャスター効果の強さ")]
    public float CasterEffectStrength = 20.0f;
    [Tooltip("キャスター効果が最大になる速度（m/s）")]
    public float CasterMaxEffectSpeed = 10.0f;
    
    [Header("入力設定")]
    [Tooltip("加速の感度")]
    public float AccelerationSensitivity = 0.2f;
    [Tooltip("ターボ加速の感度")]
    public float TurboAccelerationSensitivity = 0.5f;
    [Tooltip("ブレーキの感度")]
    public float BrakeSensitivity = 1.0f;
    [Tooltip("入力の滑らかさ（値が大きいほど滑らか）")]
    public float InputSmoothness = 5.0f;
    [Tooltip("前後傾きトルクの強さ")]
    public float PitchTorqueStrength = 10.0f;
    [Tooltip("空中での左右回転トルクの強さ")]
    public float AirRollTorqueStrength = 5.0f;
    [Tooltip("空中での回転減衰係数（値が大きいほど速く減衰）")]
    public float AirRotationDamping = 0.5f;
    [Tooltip("最大速度制限（m/s）、これ以上でアクセルの効果が順次減少")]
    public float MaxSpeedThreshold = 30.0f;  // 約60km/h
    
    [Header("安定化設定")]
    [Tooltip("安定化機能を有効にするかどうか")]
    public bool EnableStabilization = true;
    [Tooltip("遠心力安定化機能を有効にするかどうか")]
    public bool EnableCentrifugalStabilization = true;
    [Tooltip("安定化の比例項ゲイン")]
    public float StabilizationP = 20.0f;
    [Tooltip("安定化の積分項ゲイン")]
    public float StabilizationI = 20.0f;
    [Tooltip("安定化の微分項ゲイン")]
    public float StabilizationD = 3.0f;
    [Tooltip("積分項の最大値（ワインドアップ防止用）")]
    public float IntegralMax = 10.0f;
    [Tooltip("目標の上向きベクトル（通常はVector3.up）")]
    public Vector3 TargetUpDirection = Vector3.up;
    [Tooltip("遠心力の影響を受け始める最低速度（m/s）")]
    public float MinCentrifugalSpeed = 2.0f;
    [Tooltip("遠心力の影響が最大になる速度（m/s）")]
    public float MaxCentrifugalSpeed = 15.0f;
    [Tooltip("遠心力の最大影響度（0-1）")]
    public float MaxCentrifugalInfluence = 0.8f;
    [Tooltip("安定化方向の平滑化係数（値が大きいほど滑らか）")]
    public float StabilizationSmoothness = 5.0f;
    [Tooltip("デバッグ表示を有効にするかどうか")]
    public bool EnableDebugDisplay = false;
    
    [Header("傾き設定")]
    [Tooltip("ハンドル操作による傾きの強さ（値が大きいほど強く傾く）")]
    public float LeanStrength = 160.0f;
    [Tooltip("最大傾き角度（度）")]
    public float MaxLeanAngle = 40.0f;
    [Tooltip("傾きの滑らかさ（値が大きいほど滑らか）")]
    public float LeanSmoothness = 2.0f;
    
    [Header("デバッグ")]
    [Tooltip("現在の入力値")]
    [ReadOnly]
    public float CurrentDriveInput = 0.0f;
    [Tooltip("現在の傾き角度（全体）")]
    // デバッグ情報表示用の変数
    private float CurrentRollAngle = 0.0f;
    private float CurrentHandleAngle = 0.0f;
    private float CurrentTiltAngle = 0.0f;
    private float CurrentYawAngle = 0.0f;
    private float CurrentSpeed = 0.0f;
    private float CurrentSteerAngle = 0.0f;
    private float CurrentWheelBase = 0.0f;
    private float DebugDriftStrength = 0.0f;
    private float DebugLeanFactor = 0.0f;
    private float DebugDirectionMultiplier = 1.0f;
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
    
    [Header("地面情報")]
    [Tooltip("地面の法線ベクトル（デバッグ表示用）")]
    [ReadOnly]
    public Vector3 GroundNormal = Vector3.up;
    
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

    public Vector3 getFrontVector()
    {
        Vector3 ret = new Vector3(0,0,0);
        // 両方のタイヤが浮いているか確認
        bool isAirborne = !FrontWheel.IsGrounded && !RearWheel.IsGrounded;
        if (!isAirborne)
        {
            ret = transform.forward*-10.0f;
        }

        return ret;

    }

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
        // エンジン音を更新
        UpdateEngineSound();
    }
    
    /// <summary>
    /// エンジン音を更新する
    /// 速度と加速入力に基づいてピッチとボリュームを調整する
    /// </summary>
    private void UpdateEngineSound()
    {
        // EngineSoundが設定されているか確認
        if (EngineSound == null) return;
        
        // 現在の速度を取得
        float speed = rb.velocity.magnitude;
        
        // 加速入力の強さを取得
        float accelerationInput = Mathf.Abs(targetDriveInput);
        
        // アクセルを押していない場合はアイドリングピッチを使用
        float basePitch;
        if (accelerationInput < 0.01f)
        {
            // アクセルなしの場合はアイドリングピッチ
            basePitch = IdleEnginePitch;
        }
        else
        {
            // 速度とアクセル入力に基づいてピッチを計算
            float normalizedSpeed = Mathf.Clamp01(speed / 30.0f); // 30m/sを最大速度として正規化
            basePitch = Mathf.Lerp(MinEnginePitch, MaxEnginePitch, normalizedSpeed);
        }
        
        // ターボ使用時はピッチを上げる、滑らかに補間する
        float targetMultiplier = (isTurboActive && !isOverheated) ? TurboEnginePitchMultiplier : 1.0f;
        
        // 現在の乗数を目標値に向けて滑らかに補間
        currentEnginePitchMultiplier = Mathf.Lerp(
            currentEnginePitchMultiplier, 
            targetMultiplier, 
            Time.deltaTime * EnginePitchSmoothness
        );
        
        // 最終的なピッチを計算
        float finalPitch = basePitch * currentEnginePitchMultiplier;
        
        // 加速入力に基づいてボリュームを計算
        float volume = Mathf.Lerp(MinEngineVolume, MaxEngineVolume, accelerationInput);
        
        // ピッチとボリュームを適用
        EngineSound.pitch = finalPitch;
        EngineSound.volume = volume;
        
        // エンジン音が再生されていない場合は再生開始
        if (!EngineSound.isPlaying)
        {
            EngineSound.Play();
        }
    }
    
    void FixedUpdate()
    {
        // 地面の法線を更新
        UpdateGroundNormal();
        
        // キーボード入力の処理
        ProcessInput();

        // 駆動力の適用
        ApplyDriveForce();
        
        // 両方のタイヤが浮いているか確認
        bool isAirborne = !FrontWheel.IsGrounded && !RearWheel.IsGrounded;
        
        // 地面を走っている時のみ抗力を適用
        if (!isAirborne)
        {
            ApplyGroundResistance();
        }
        
        // 安定化力の適用
        // 両方のタイヤが空中に浮いているときは安定化力を適用しない
        if (EnableStabilization && EnablePIDControl && (FrontWheel.IsGrounded || RearWheel.IsGrounded))
        {
            ApplyStabilizationForce();
        }
        
        // 空中での回転減衰を適用
        if (isAirborne)
        {
            ApplyAirRotationDamping();
        }
        
        // ハンドルの回転を適用
        ApplyHandleRotation();
        
        // デバッグ情報の更新
        UpdateDebugInfo();
    }
    
    // ハンドル入力中かどうかを追跡する変数
    private bool isSteeringInput = false;
    
    // ドリフトモードのフラグ
    private bool isDriftMode = false;
    
    // ドリフトエフェクトの強さ（0～1）
    private float driftEffectStrength = 0f;
    
    // 現在のターボゲージ量
    private float currentTurboGauge = 100f;
    
    // オーバーヒート状態かどうか
    private bool isOverheated = false;
    
    // ターボ入力状態を保持する変数
    private bool isTurboActive = false;
    
    // エンジン音のピッチ補間用の変数
    private float currentEnginePitchMultiplier = 1.0f;
    
    // オーバーヒート回復倍率
    public float overheatRecoveryMultiplier = 2.0f;
    
    [Header("地面抗力設定")]
    [Tooltip("地面を走っている時の速度抗力係数")]
    public float GroundResistanceCoefficient = 0.05f;
    
    [Tooltip("地面を走っている時の速度二乗に比例する抗力係数")]
    public float GroundDragCoefficient = 0.01f;
    
    [Header("サウンド設定")]
    [Tooltip("エンジン音のAudioSource")]
    public AudioSource EngineSound;
    
    [Tooltip("エンジン音の最小ピッチ")]
    public float MinEnginePitch = 0.5f;
    
    [Tooltip("エンジン音の最大ピッチ")]
    public float MaxEnginePitch = 2.0f;
    
    [Tooltip("アクセルを押していない時のアイドリングピッチ")]
    public float IdleEnginePitch = 0.6f;
    
    [Tooltip("エンジン音の最小ボリューム")]
    public float MinEngineVolume = 0.2f;
    
    [Tooltip("エンジン音の最大ボリューム")]
    public float MaxEngineVolume = 1.0f;
    
    [Tooltip("ターボ使用時のエンジンピッチ倍率")]
    public float TurboEnginePitchMultiplier = 1.3f;
    
    [Tooltip("エンジン音のピッチ変化の滑らかさ（値が小さいほど滑らか）")]
    public float EnginePitchSmoothness = 5.0f;
    
    // ドリフト中の摩擦係数倍率
    [Header("ドリフト設定")]
    [Tooltip("ドリフト中の摩擦円倍率")]
    public float DriftFrictionMultiplier = 0.1f;
    
    [Tooltip("ドリフト中のY軸回転力")]
    public float DriftYawTorqueStrength = 2.0f;
    
    [Tooltip("ドリフトエフェクトの強まる速さ（大きいほど速く強まる）")]
    public float DriftEffectRiseSpeed = 2.0f;
    
    [Tooltip("ドリフトエフェクトの弱まる速さ（大きいほど速く弱まる）")]
    public float DriftEffectFallSpeed = 1.0f;
    
    [Tooltip("ドリフト中の傾きによる回転力の倍率")]
    public float DriftLeanYawMultiplier = 1.5f;
    
    [Tooltip("ドリフト中の傾き速度の倍率")]
    public float DriftLeanSpeedMultiplier = 2.0f;
    
    // ドリフト中のターボ使用時の回転速度倍率
    public float TurboDriftRotationMultiplier = 1.8f;
    
    // ドリフト中のターボ使用時の速度低下倍率（1.0未満の値で速度が低下）
    public float TurboDriftSpeedMultiplier = 0.7f;
    
    [Header("リセット設定")]
    [Tooltip("リセット時の高さオフセット（メートル）")]
    public float ResetHeightOffset = 1.5f;
    [Tooltip("リセット時の速度リセットの強さ")]
    public float ResetVelocityDamping = 0.9f;
    
    // リセットボタンの連打防止用フラグ
    private bool isResetButtonPressed = false;
    
    [Header("ターボゲージ設定")]
    [Tooltip("ターボゲージの最大値")]
    public float MaxTurboGauge = 100f;
    [Tooltip("ターボ使用時のゲージ消費速度")]
    public float TurboConsumptionRate = 30f;
    [Tooltip("ターボゲージの回復速度")]
    public float TurboRecoveryRate = 10f;
    [Tooltip("ターボ使用可能な最小ゲージ量")]
    public float MinTurboThreshold = 10f;
    
    // キーボード入力の処理
    void ProcessInput()
    {
        // Fire1、Fire2、Fire3、Fire4の入力を取得
        bool turboInput = Input.GetButton("Fire1");    // ターボ加速
        isTurboActive = turboInput; // クラス変数に入力状態を保存
        bool accelerateInput = Input.GetButton("Fire3"); // 通常加速
        bool brakeInput = Input.GetButton("Fire2");     // 後退
        bool driftInput = Input.GetButton("Fire4");     // ドリフト
        
        // Resetボタンの入力を取得
        bool resetInput = Input.GetButton("Reset");  // Resetボタン（デフォルトではSpaceキー）
        
        // Resetボタンの処理
        // ボタンが押されているかどうかを確認
        if (resetInput && !isResetButtonPressed)
        {
            // ボタンが押されておらず、新たに押された場合のみリセットを実行
            ResetBike();
            // フラグをセットして連打を防止
            isResetButtonPressed = true;
        }
        else if (!resetInput)
        {
            // ボタンが離されたらフラグをリセット
            isResetButtonPressed = false;
        }
        
        // ドリフトモードの切り替え
        // Fire4ボタンが押されているとドリフトモードを有効にする
        isDriftMode = driftInput;
        
        // ドリフトエフェクトの強さを徐々に変化させる
        if (isDriftMode)
        {
            // ドリフトボタンが押されている場合、徐々に強くする
            driftEffectStrength = Mathf.Min(1.0f, driftEffectStrength + DriftEffectRiseSpeed * Time.deltaTime);
        }
        else
        {
            // ドリフトボタンが離された場合、徐々に弱くする
            driftEffectStrength = Mathf.Max(0.0f, driftEffectStrength - DriftEffectFallSpeed * Time.deltaTime);
        }
        
        // ドリフトエフェクトの強さに基づいて後輪の摩擦円倍率を設定
        if (RearWheel != null)
        {
            // 通常の摩擦円からドリフト摩擦円までの間を補間
            float frictionMultiplier = Mathf.Lerp(1.0f, DriftFrictionMultiplier, driftEffectStrength);
            RearWheel.FrictionMultiplier = frictionMultiplier;
        }
        
        // 左右キーの入力を取得（傾き制御用）
        float horizontalInput = Input.GetAxis("Horizontal");
        
        // 上下キーの入力を取得（前後傾き用）
        float verticalInput = Input.GetAxis("Vertical");
        
        // 両方のタイヤが浮いているか確認
        bool isAirborne = !FrontWheel.IsGrounded && !RearWheel.IsGrounded;
        
        // ドリフト中のY軸回転制御
        if (driftEffectStrength > 0.01f && RearWheel.IsGrounded && Mathf.Abs(horizontalInput) > 0.01f)
        {
            // バイクの傾き角度を取得
            float rollAngleDegrees = CurrentRollAngle;
            
            // 傾き角度の絶対値を正規化して倍率として使用（0～1の範囲に収める）
            float leanFactor = Mathf.Clamp01(Mathf.Abs(rollAngleDegrees) / 45.0f);
            
            // 傾きの方向と入力の方向が一致している場合に回転力を増大
            float directionMultiplier = 1.0f;
            if ((rollAngleDegrees > 0 && horizontalInput > 0) || (rollAngleDegrees < 0 && horizontalInput < 0))
            {
                // 傾きと同じ方向に操作すると回転力が増大
                directionMultiplier = 1.0f + leanFactor * DriftLeanYawMultiplier;
            }
            
            // ターボ使用中かつオーバーヒートしていない場合は回転力を増加
            float turboRotationMultiplier = 1.0f;
            if (turboInput && !isOverheated)
            {
                turboRotationMultiplier = TurboDriftRotationMultiplier;
            }
            
            // ドリフトエフェクトの強さ、傾き角度、入力の強さに基づいてY軸回転力を計算
            Vector3 yawTorque = transform.up * horizontalInput * DriftYawTorqueStrength * driftEffectStrength * directionMultiplier * turboRotationMultiplier;
            rb.AddTorque(yawTorque);
            
            // デバッグ情報の更新
            if (ShowDebugInfo)
            {
                DebugDriftStrength = driftEffectStrength;
                DebugLeanFactor = leanFactor;
                DebugDirectionMultiplier = directionMultiplier;
            }
        }
        
        // 前後傾きのトルクを適用
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            // 上キーで前に倒れる、下キーでウイリー方向に回転
            Vector3 rightAxis = transform.right;
            float torqueMultiplier = 1.0f;
            
            // 両方のタイヤが地面についている場合はトルクを増大
            if (FrontWheel.IsGrounded && RearWheel.IsGrounded)
            {
                torqueMultiplier = 1.0f;
            }
            
            Vector3 pitchTorque = rightAxis * -verticalInput * PitchTorqueStrength * torqueMultiplier;
            rb.AddTorque(pitchTorque);
        }
        
        // 空中での左右回転トルクを適用
        if (isAirborne && Mathf.Abs(horizontalInput) > 0.01f)
        {
            // バイクのup方向ベクトルを中心に回転させる
            Vector3 upAxis = transform.up;
            Vector3 rollTorque = upAxis * horizontalInput * AirRollTorqueStrength;
            rb.AddTorque(rollTorque);
        }
        
        // ハンドル入力中かどうかを更新
        isSteeringInput = Mathf.Abs(horizontalInput) > 0.01f;

        // 入力に基づいて目標駆動力を設定
        if (brakeInput)
        {
            // 後退（バック）
            targetDriveInput = -BrakeSensitivity;
        }
        else if (turboInput && !isOverheated)
        {
            // ターボ加速（Fire1）
            targetDriveInput = TurboAccelerationSensitivity;
            
            // ターボゲージを消費
            currentTurboGauge -= TurboConsumptionRate * Time.deltaTime;
            
            // ゲージが完全に消費されたらオーバーヒート状態に
            if (currentTurboGauge <= 0f)
            {
                currentTurboGauge = 0f;
                isOverheated = true;
            }
        }
        else if (turboInput && isOverheated)
        {
            // ターボ使用不可またはオーバーヒートの場合は通常加速
            targetDriveInput = AccelerationSensitivity;
        }
        else if (accelerateInput)
        {
            // 通常加速（Fire2）
            targetDriveInput = AccelerationSensitivity;
        }
        else
        {
            // 入力がない場合は減速
            targetDriveInput = 0.0f;
        }
        
        // ターボ使用していない、またはオーバーヒートの場合はゲージを回復
        if (!turboInput || isOverheated)
        {
            // 回復速度の計算（オーバーヒート時は速度が倍になる）
            float recoveryRate = TurboRecoveryRate;
            if (isOverheated)
            {
                recoveryRate *= overheatRecoveryMultiplier;
            }
            
            // ターボゲージを回復
            currentTurboGauge += recoveryRate * Time.deltaTime;
            
            // 最大値を超えないように制限
            if (currentTurboGauge >= MaxTurboGauge)
            {
                currentTurboGauge = MaxTurboGauge;
                
                // オーバーヒート状態が解除される
                isOverheated = false;
            }
        }
        
        // 左右の入力を傾き制御に使用
        // プレイヤーは傾きのみを制御し、ハンドルは自動的に回転する
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            // プレイヤーの入力に基づいて目標傾き角度を徐々に蓄積
            // 入力方向に応じて蓄積速度を調整
            float targetMaxAngle = horizontalInput * MaxLeanAngle;
            
            // ドリフト中は傾きの蓄積速度を増加
            float leanSpeedMultiplier = 1.0f;
            if (driftEffectStrength > 0.01f)
            {
                // ドリフト強度に応じて傾きの速さを増加
                leanSpeedMultiplier = 1.0f + driftEffectStrength * DriftLeanSpeedMultiplier;
            }
            
            float accumulationSpeed = LeanAccumulationRate * leanSpeedMultiplier * Time.deltaTime;
            
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
    
    [Header("低速ブースト設定")]
    public float LowSpeedBoostThreshold = 8.33f; // 時速30km（約8.33 m/s）
    public float LowSpeedBoostMultiplier = 1.5f; // 低速時のブースト倍率
    
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
        if (rb != null)
        {
            float speed = rb.velocity.magnitude;
            
            // 低速時（時速30km以下）はアクセルの力を強くする
            // ただし、ターボ使用時は低速ブーストを適用しない
            if (speed < LowSpeedBoostThreshold && CurrentDriveInput > 0 && !isTurboActive)
            {
                // 速度が遅いほどブースト効果が大きくなる
                float boostRatio = 1.0f - (speed / LowSpeedBoostThreshold);
                float boostMultiplier = Mathf.Lerp(1.0f, LowSpeedBoostMultiplier, boostRatio);
                
                // アクセル入力を増強
                CurrentDriveInput *= boostMultiplier;
            }
            
            // ドリフト中のターボ使用時は速度を低下させる
            if (driftEffectStrength > 0.01f && isTurboActive && !isOverheated)
            {
                // 傾き角度を取得
                float rollAngleDegrees = Mathf.Abs(CurrentRollAngle);
                
                // 傾きが大きいほど速度低下が大きくなる（最大40度で最大効果）
                float leanFactor = Mathf.Clamp01(rollAngleDegrees / 40.0f);
                
                // ドリフト強度と傾き角度に応じた速度低下倍率を計算
                float driftTurboSpeedMultiplier = Mathf.Lerp(1.0f, TurboDriftSpeedMultiplier, leanFactor * driftEffectStrength);
                
                // 速度を低下させる力を適用
                Vector3 slowDownForce = -rb.velocity.normalized * speed * (1.0f - driftTurboSpeedMultiplier) * 2.0f;
                rb.AddForce(slowDownForce, ForceMode.Acceleration);
            }
            
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
        
        // 前輪と後輪に駆動力を設定
        if (RearWheel != null)
        {
            RearWheel.DriveInput = CurrentDriveInput;
        }
        
        // ブレーキの場合は前輪にも適用
        if (FrontWheel != null && CurrentDriveInput < 0)
        {
            // 前輪にもブレーキ力を適用
            FrontWheel.DriveInput = CurrentDriveInput;
        }
        else if (FrontWheel != null)
        {
            // ブレーキ以外の場合は前輪には駆動力を適用しない
            FrontWheel.DriveInput = 0;
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
            
            // バイクの前後方向を実際の地面に投影
            Vector3 forwardOnGround = Vector3.ProjectOnPlane(currentForward, GroundNormal).normalized;
            
            // 現在の左右の傾き角度を計算
            Vector3 sideProjection = Vector3.ProjectOnPlane(currentUp, forwardOnGround).normalized;
            float currentRollAngle = Vector3.SignedAngle(GroundNormal, sideProjection, forwardOnGround);
            
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
                float torqueMultiplier =    (LowSpeedHandleTorqueMultiplier) * speedFactor;
                
                // 現在のハンドル角度に基づいてトルクを追加
                float factor = -Input.GetAxis("Horizontal");
                float lowSpeedExtraTorque = factor * torqueMultiplier;
                totalTorque += lowSpeedExtraTorque;
                //Debug.Log("lowSpeedExtraTorque: " + lowSpeedExtraTorque);
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
        

    
    [Header("空中制御設定")]
    [Tooltip("空中で前傾時の前方加速力の強さ")]
    public float AirForwardThrustStrength = 10.0f;
    [Tooltip("空中で前傾加速が有効になる最小角度（度）")]
    public float AirForwardThrustMinAngle = 10.0f;
    [Tooltip("空中で前傾加速が最大になる角度（度）")]
    public float AirForwardThrustMaxAngle = 45.0f;
    [Tooltip("前傾加速が有効になる左右の傾きの最大角度（度）")]
    public float MaxRollAngleForForwardThrust = 15.0f;
    
    /// <summary>
    /// 空中での回転減衰と制御を適用する
    /// </summary>
    private void ApplyAirRotationDamping()
    {
        if (rb == null) return;
        
        // 現在の角速度を取得
        Vector3 angularVelocity = rb.angularVelocity;
        
        // 角速度に減衰を適用
        angularVelocity *= (1.0f - AirRotationDamping * Time.fixedDeltaTime);
        
        // 新しい角速度を設定
        rb.angularVelocity = angularVelocity;
        
        // 前後の傾き（ピッチ角）と左右の傾き（ロール角）を確認
        // 前方に傾いており、かつ左右の傾きが少ない場合のみ加速力を適用
        if (CurrentPitchAngle < -AirForwardThrustMinAngle && // マイナスの値は前方に傾いていることを示す
            Mathf.Abs(CurrentRollAngle) < MaxRollAngleForForwardThrust) // 左右の傾きが指定範囲内
        {
            // 傾き角度に応じた加速力の強さを計算（角度が大きいほど強い加速力）
            float thrustFactor = Mathf.Clamp01(Mathf.Abs(CurrentPitchAngle) - AirForwardThrustMinAngle) / 
                                (AirForwardThrustMaxAngle - AirForwardThrustMinAngle);
            
            // ロール角度に基づく加速力の減衰係数を計算
            // ロール角度が0度に近いほど加速力が強くなる
            float rollFactor = 1.0f - (Mathf.Abs(CurrentRollAngle) / MaxRollAngleForForwardThrust);
            
            // バイクの前方方向を取得
            Vector3 forwardDirection = -transform.forward;
            
            // 前方への加速力を適用（ピッチ角度とロール角度の両方を考慮）
            Vector3 forwardThrust = forwardDirection * AirForwardThrustStrength * thrustFactor * rollFactor;
            rb.AddForce(forwardThrust, ForceMode.Acceleration);
            
            // デバッグ情報があれば表示
            if (ShowDebugInfo)
            {
                Debug.DrawRay(transform.position, forwardThrust, Color.blue, 0.1f);
            }
        }
    }
    
    /// <summary>
    /// デバッグ情報を更新する
    /// </summary>
    private void UpdateDebugInfo()
    {
        if (rb == null) return;
        
        // 現在の入力値を更新
        CurrentDriveInput = RearWheel.DriveInput;
        
        // 現在の傾き角度を計算
        Vector3 currentUp = transform.up;
        Vector3 worldUp = GroundNormal;
        
        // 全体の傾き角度を計算
        CurrentTiltAngle = Vector3.Angle(currentUp, worldUp);
        
        // 左右方向の傾き角度（ロール）を計算
        Vector3 rightVector = transform.right;
        Vector3 projectedRight = Vector3.ProjectOnPlane(rightVector, GroundNormal).normalized;
        float rollSign = Vector3.Dot(transform.up, Vector3.Cross(projectedRight, rightVector)) < 0 ? -1 : 1;
        CurrentRollAngle = Vector3.Angle(rightVector, projectedRight) * rollSign;
        
        // 前後方向の傾き角度（ピッチ）を計算
        // ワールド座標系での傾きを計算するため、Vector3.up（ワールドの上方向）を使用
        Vector3 forwardVector = transform.forward;
        Vector3 horizontalForward = new Vector3(forwardVector.x, 0, forwardVector.z).normalized;
        
        // バイクの前方ベクトルと水平面上の前方ベクトルの角度を計算
        float pitchAngle = Vector3.Angle(forwardVector, horizontalForward);
        
        // 前傾きか後傾きかの符号を決定（前傾きはプラス、後傾きはマイナス）
        float pitchSign = forwardVector.y < 0 ? 1 : -1;
        
        // 最終的なピッチ角度を設定
        CurrentPitchAngle = pitchAngle * pitchSign;
        
        // デバッグ表示用に実際の前方ベクトルを可視化
        if (ShowDebugInfo)
        {
            Debug.DrawRay(transform.position, forwardVector * 3, Color.red, 0.1f);
            Debug.DrawRay(transform.position, horizontalForward * 3, Color.green, 0.1f);
        }
        
    }

    /// <summary>
    /// デバッグ情報を画面に表示する
    /// </summary>
    void OnGUI()
    {
        if (!ShowDebugInfo) return;
        
        // 通常のデバッグ情報を右上に表示
        DisplayDebugInfo();
        
        // ターボゲージを画面中央下に表示
        DisplayTurboGauge();
    }
    
    /// <summary>
    /// 通常のデバッグ情報を右上に表示する
    /// </summary>
    private void DisplayDebugInfo()
    {
        // デバッグ情報の表示位置とサイズを設定
        int width = 300;
        int height = 270;
        int padding = 10;
        int lineHeight = 20;
        
        // 背景を表示
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
        backgroundTexture.Apply();
        
        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = backgroundTexture;
        
        GUI.Box(new Rect(Screen.width - width - padding, padding, width, height), "", backgroundStyle);
        
        // テキストスタイルの設定
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
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"方向（Yaw）: {CurrentYawAngle:F1}度", textStyle);
        y += lineHeight;
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"速度: {CurrentSpeed:F1} m/s", textStyle);
        y += lineHeight;
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"ハンドル角度: {CurrentSteerAngle:F1}度", textStyle);
        y += lineHeight;
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"PID出力: {pidOutputValue:F3}", textStyle);
        y += lineHeight;
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"地面法線: {GroundNormal.ToString("F2")}", textStyle);
        y += lineHeight;
        
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
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"最大速度: {MaxSpeedThreshold * 3.6f:F1} km/h", textStyle);
        y += lineHeight;
        GUI.Label(new Rect(Screen.width - width - padding + 10, y, width - 20, lineHeight), $"ハンドル入力中の中心化倍率: {SteeringCenteringMultiplier:F2}", textStyle);
        y += lineHeight;
        
        // 使用したリソースの解放
        Object.Destroy(backgroundTexture);
    }
    
    /// <summary>
    /// ターボゲージを画面中央下に表示する
    /// </summary>
    private void DisplayTurboGauge()
    {
        // ターボゲージの表示位置とサイズを設定
        int gaugeWidth = 300;
        int gaugeHeight = 80;
        int padding = 20;
        int lineHeight = 20;
        
        // 画面中央下に配置
        int posX = (Screen.width - gaugeWidth) / 2;
        int posY = Screen.height - gaugeHeight - padding;
        
        // 背景を表示
        Texture2D backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
        backgroundTexture.Apply();
        
        GUIStyle backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = backgroundTexture;
        
        //GUI.Box(new Rect(posX, posY, gaugeWidth, gaugeHeight), "", backgroundStyle);
        
        // テキストスタイルの設定
        GUIStyle textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 16;
        textStyle.alignment = TextAnchor.MiddleCenter;
        
        // ターボゲージのタイトルと状態を表示
        int y = posY + padding;
        /*
        string turboStatus = isOverheated ? "OVERHEAT" : "";
        GUI.Label(new Rect(posX, y, gaugeWidth, lineHeight), $"TEMP {currentTurboGauge:F1} / {MaxTurboGauge:F0} ({turboStatus})", textStyle);
        y += lineHeight + 5;
        */
        // ターボゲージのバーを表示
        float barWidth = gaugeWidth - padding * 2;
        float barHeight = 15;
        float barX = posX + padding;
        float barY = y;
        
        // 背景バー
        GUI.Box(new Rect(barX, barY, barWidth, barHeight), "", new GUIStyle(GUI.skin.box));
        
        // ゲージの割合を計算
        float gaugeFillWidth = (currentTurboGauge / MaxTurboGauge) * barWidth;
        
        // ゲージの色を設定
        Color gaugeColor;
        if (isOverheated)
        {
            // オーバーヒート時は赤
            gaugeColor = Color.red;
        }
        else if (currentTurboGauge < MaxTurboGauge * 0.20f)
        {
            // 残量20%以下は黄色
            gaugeColor = Color.yellow;
        }
        else
        {
            // 通常時は緑
            gaugeColor = Color.green;
        }
        
        // ゲージの塗りつぶし
        Texture2D gaugeTexture = new Texture2D(1, 1);
        gaugeTexture.SetPixel(0, 0, gaugeColor);
        gaugeTexture.Apply();
        
        // ゲージを描画
        GUI.DrawTexture(new Rect(barX, barY, gaugeFillWidth, barHeight), gaugeTexture);
        
        // テクスチャを破棄
        Object.Destroy(backgroundTexture);
        Object.Destroy(gaugeTexture);
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
    /// 地面を走っている時のみ受ける抗力を適用する
    /// 速度に比例する抗力と速度の二乗に比例する抗力を適用する
    /// </summary>
    private void ApplyGroundResistance()
    {
        // 現在の速度を取得
        Vector3 velocity = rb.velocity;
        
        // 地面に沿った速度成分を計算
        Vector3 groundVelocity = Vector3.ProjectOnPlane(velocity, GroundNormal);
        float speed = groundVelocity.magnitude;
        
        if (speed > 0.1f)
        {
            // 速度に比例する抗力
            Vector3 linearResistance = -groundVelocity.normalized * speed * GroundResistanceCoefficient;
            
            // 速度の二乗に比例する抗力（空気抗力など）
            Vector3 quadraticResistance = -groundVelocity.normalized * speed * speed * GroundDragCoefficient;
            
            // 合計抗力を適用
            rb.AddForce(linearResistance + quadraticResistance, ForceMode.Force);
        }
    }
    
    /// <summary>
    /// バイクをリセットする
    /// 現在位置から上に移動し、傾きと速度をリセットする
    /// </summary>
    void ResetBike()
    {
        if (rb != null)
        {
            // 現在の位置を取得
            Vector3 currentPosition = transform.position;
            
            // 地面の法線方向に高さオフセットを加える
            Vector3 resetPosition = currentPosition + GroundNormal * ResetHeightOffset;
            
            // 位置をリセット
            transform.position = resetPosition;
            
            // 回転をリセット（地面の法線に基づいてバイクを立てる）
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, GroundNormal) * transform.rotation;
            transform.rotation = targetRotation;
            
            // 速度と角速度をリセット
            rb.velocity *= (1.0f - ResetVelocityDamping);
            rb.angularVelocity *= (1.0f - ResetVelocityDamping);
            
            // 目標傾き角度をリセット
            targetLeanAngle = 0.0f;
            
            // ドリフトエフェクトをリセット
            driftEffectStrength = 0.0f;
            isDriftMode = false;
            
            // タイヤの摩擦円倍率をリセット
            if (RearWheel != null)
            {
                RearWheel.FrictionMultiplier = 1.0f;
            }
        }
    }
    
    /// <summary>
    /// 地面の法線ベクトルを更新する
    /// タイヤが地面に接触している場合は、その法線を使用する
    /// 両方のタイヤが浮いている場合は、Vector3.upを使用する
    /// </summary>
    void UpdateGroundNormal()
    {
        // デフォルトは上向き（水平面）
        Vector3 normal = Vector3.up;
        
        // 前輪と後輪の接地状態を確認
        bool frontGrounded = FrontWheel != null && FrontWheel.IsGrounded;
        bool rearGrounded = RearWheel != null && RearWheel.IsGrounded;
        
        if (frontGrounded && rearGrounded)
        {
            // 両方のタイヤが接地している場合は、両方の法線の平均を使用
            normal = (FrontWheel.GetGroundNormal() + RearWheel.GetGroundNormal()).normalized;
        }
        else if (frontGrounded)
        {
            // 前輪のみ接地している場合
            normal = FrontWheel.GetGroundNormal();
        }
        else if (rearGrounded)
        {
            // 後輪のみ接地している場合
            normal = RearWheel.GetGroundNormal();
        }
        
        // 法線ベクトルを更新
        GroundNormal = normal;
    }
    
    /// <summary>
    /// バイクの安定化力をPID制御で適用する
    /// プレイヤーの入力（targetLeanAngle）を目標角度として使用
    /// ロール（左右の傾き）のみを補正し、ピッチ（前後の傾き）は補正しない
    /// 遠心力と地形法線を考慮した安定化を行う
    /// </summary>
    void ApplyStabilizationForce()
    {
        if (rb == null || !EnableStabilization) return;
        
        // 現在のバイクの向きを取得
        Vector3 currentUp = transform.up;
        Vector3 currentForward = transform.forward;
        
        // ワールドY軸を基準に計算するため、Vector3.upを使用
        Vector3 worldUp = Vector3.up;
        
        // バイクの前後方向をワールド水平面に投影
        Vector3 forwardOnGround = Vector3.ProjectOnPlane(currentForward, worldUp).normalized;
        Vector3 bikeRight = Vector3.Cross(forwardOnGround, worldUp).normalized;
        
        // 現在の左右の傾き角度を計算（ワールドY軸基準）
        Vector3 sideProjection = Vector3.ProjectOnPlane(currentUp, forwardOnGround).normalized;
        float currentRollAngle = Vector3.SignedAngle(worldUp, sideProjection, forwardOnGround);
        
        // プレイヤーの入力を目標角度として使用
        float targetRollAngle = targetLeanAngle;
        
        // 速度と方向を取得
        Vector3 velocity = rb.velocity;
        float speed = velocity.magnitude;
        
        // 回転を抑制する最大速度
        float maxAngleSpeed = 2.0f;
        if (speed < maxAngleSpeed)
        {
            // 低速時は傾きを抑制
            float angleFactor = speed / maxAngleSpeed;
            targetRollAngle *= angleFactor;
        }
        
        // 目標上向きベクトルを初期化（デフォルトはワールドY軸）
        Vector3 targetUpVector = worldUp;
        
        // 遠心力安定化が有効で、速度が最低閾値を超えている場合
        if (EnableCentrifugalStabilization && speed > MinCentrifugalSpeed)
        {
            // 速度ベクトルを水平面に投影して方向を取得
            Vector3 velocityOnGround = Vector3.ProjectOnPlane(velocity, worldUp).normalized;
            
            // 曲率（角速度）を計算 - バイクの回転速度から前方向の成分を抽出
            float angularSpeed = Vector3.Dot(rb.angularVelocity, transform.up);
            
            // 遠心力方向を計算（速度方向に垂直で水平な方向）
            Vector3 centrifugalDir = Vector3.Cross(worldUp, velocityOnGround).normalized;
            
            // 角速度の符号に基づいて遠心力の方向を調整
            if (angularSpeed < 0)
            {
                centrifugalDir = -centrifugalDir;
            }
            
            // 遠心力の強さを計算（速度の二乗に比例、角速度の大きさに比例）
            float centrifugalStrength = speed * Mathf.Abs(angularSpeed) * 0.1f;
            
            // 速度に基づく遠心力の影響度を計算（MinCentrifugalSpeedからMaxCentrifugalSpeedまでの範囲で線形補間）
            float speedFactor = Mathf.Clamp01((speed - MinCentrifugalSpeed) / (MaxCentrifugalSpeed - MinCentrifugalSpeed));
            float centrifugalInfluence = speedFactor * MaxCentrifugalInfluence;
            
            // 地形の法線を考慮した上向きベクトルを計算
            Vector3 terrainUpVector = worldUp;// GroundNormal;
            
            // 遠心力方向と地形法線を考慮した目標上向きベクトルを計算
            Vector3 centrifugalUpVector = Vector3.Lerp(terrainUpVector, centrifugalDir, centrifugalInfluence).normalized;
            
            // 最終的な目標上向きベクトルを設定
            targetUpVector = centrifugalUpVector;
            
            // デバッグ表示用に遠心力ベクトルを保存
            if (EnableDebugDisplay)
            {
                Debug.DrawRay(transform.position, centrifugalDir * 2.0f, Color.red);
                Debug.DrawRay(transform.position, targetUpVector * 2.0f, Color.green);
                Debug.DrawRay(transform.position, terrainUpVector * 2.0f, Color.blue);
            }
        }
        
        // 目標上向きベクトルに基づいて目標ロール角度を調整
        if (EnableCentrifugalStabilization && speed > MinCentrifugalSpeed)
        {
            // 目標上向きベクトルを前方向に垂直な平面に投影
            Vector3 targetSideProjection = Vector3.ProjectOnPlane(targetUpVector, forwardOnGround).normalized;
            
            // 目標ロール角度を計算（ワールドY軸と目標上向きベクトルの角度）
            float targetUpRollAngle = Vector3.SignedAngle(worldUp, targetSideProjection, forwardOnGround);
            
            // プレイヤー入力による目標角度と遠心力による目標角度を合成
            targetRollAngle = targetRollAngle + targetUpRollAngle;
        }
        
        // PID制御の計算
        float rollError = targetRollAngle - currentRollAngle;
        
        // 積分項の計算（積分ワインドアップ防止）
        rollErrorIntegral += rollError * Time.fixedDeltaTime;
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
