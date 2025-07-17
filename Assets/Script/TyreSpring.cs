using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[RequireComponent(typeof(Rigidbody))]
public class TyreSpring : MonoBehaviour
{
    [Header("ばね設定")]
    [Tooltip("ばね定数")]
    public float SpringConstant = 10.0f;
    [Tooltip("ダンパー定数")]
    public float DamperConstant = 1.0f;
    [Tooltip("タイヤの静止時の長さ")]
    public float RestLength = 0.5f;
    [Tooltip("摩擦円の倍率（1.0が通常、小さいほど滑りやすい）")]
    public float FrictionMultiplier = 1.0f;
    [Tooltip("最大伸縮距離")]
    public float MaxLength = 0.7f;
    
    [Header("摩擦設定")]
    [Tooltip("摩擦モデルの選択")]
    public FrictionModelType FrictionType = FrictionModelType.FrictionCircle;
    [Tooltip("動摩擦係数")]
    public float KineticFrictionCoefficient = 0.8f;
    
    [Header("駆動設定")]
    [Tooltip("駆動輪かどうか（駆動力を発生させるか）")]
    public bool IsDriveWheel = false;
    [Tooltip("最大駆動力")]
    public float MaxDriveForce = 10.0f;
    [Tooltip("駆動力の適用率（0～1）")]
    [Range(0, 1)]
    public float DriveInput = 0.0f;
    [Tooltip("駆動力の方向ベクトル（タイヤのローカル座標系での方向）")]
    public Vector3 DriveDirection = Vector3.right;
    
    // 摩擦モデルの種類
    public enum FrictionModelType
    {
        None,
        FrictionCircle
    }
    
    [Header("レイキャスト設定")]
    [Tooltip("地面検出用のレイヤーマスク")]
    public LayerMask GroundLayer;
    [Tooltip("タイヤの半径")]
    public float TyreRadius = 0.4f;
    
    [Header("視覚化設定")]
    [Tooltip("タイヤのモデルオブジェクト")]
    public GameObject TyreModel;
    [Tooltip("視覚化の有効化")]
    public bool EnableVisualUpdate = true;
    [Tooltip("視覚化の滑らかさ（値が大きいほど滑らか）")]
    public float VisualSmoothness = 1f;
    
    [Header("デバッグ")]
    [Tooltip("デバッグ表示の有効化")]
    public bool ShowDebug = true;
    [Tooltip("デバッグレイの色")]
    public Color DebugRayColor = Color.red;

    public Rigidbody ParentRigidbody;
   
    // 内部変数
    private float LastLength;
    private float CurrentLength;
    private Vector3 SpringForce;
    private Vector3 DamperForce;
    private Vector3 TotalForce;
    // タイヤが地面に接地しているかどうかを示すプロパティ（publicに変更）
    public bool IsGrounded { get; private set; }
    private float IsGroundedTime; // 着地してからの経過時間
    private Vector3 InitialLocalPosition; // 初期ローカル位置
    private Vector3 groundNormal = Vector3.up; // 地面の法線ベクトル
    
    // 摩擦関連の変数
    private float NormalForce;           // 垂直抗力（地面への圧力）
    private ITyreFrictionModel frictionModel; // 摩擦モデル
    private Vector3 frictionForce;       // 計算された摩擦力
    private Vector3 currentDriveForce;   // 現在の駆動力（デバッグ表示用）
    private TyreDebug tyreDebug;         // タイヤデバッグ表示用
    
    // キャッシュ用変数
    private RaycastHit HitInfo;
    private Vector3 HitPoint;
    private Vector3 SpringDirection;
    
    // Start is called before the first frame update
    void Start()
    {
        // 親のRigidbodyコンポーネントを取得
        ParentRigidbody = GetComponentInParent<Rigidbody>();
        if (ParentRigidbody == null)
        {
            Debug.LogError("TyreSpring: 親オブジェクトにRigidbodyが見つかりません");
            enabled = false;
            return;
        }
        
        // 初期値設定
        LastLength = RestLength;
        CurrentLength = RestLength;
        
        // デバッグ表示用のTyreDebugコンポーネントを取得または作成
        tyreDebug = GetComponent<TyreDebug>();
        if (tyreDebug == null && ShowDebug)
        {
            tyreDebug = gameObject.AddComponent<TyreDebug>();
            tyreDebug.ShowDebugInfo = ShowDebug;
            
            // 前輪か後輪かを判定（名前に「front」が含まれていれば前輪、それ以外は後輪）
            tyreDebug.IsFrontWheel = gameObject.name.ToLower().Contains("front");
        }
        
        // 初期ローカル位置を保存
        InitialLocalPosition = transform.localPosition;
        
        // タイヤモデルが指定されていない場合は警告を表示
        if (TyreModel == null)
        {
            Debug.LogWarning("TyreSpring: タイヤモデルが指定されていません");
        }
        
        // 摩擦モデルを初期化
        InitializeFrictionModel();
    }
    
    // 摩擦モデルの初期化
    private void InitializeFrictionModel()
    {
        switch (FrictionType)
        {
            case FrictionModelType.FrictionCircle:
                frictionModel = new FrictionCircleModel
                {
                    KineticFrictionCoefficient = KineticFrictionCoefficient
                };
                break;
                
            case FrictionModelType.None:
            default:
                frictionModel = null;
                break;
        }
    }
    
    // Update is called once per frame
    void FixedUpdate()
    {
        CalculateSpringForce();
        if (IsGrounded && frictionModel != null)
        {
            CalculateFrictionForce();
        }
        UpdateTyreVisual();
    }
    
    // タイヤの視覚化を更新
    void UpdateTyreVisual()
    {
        if (!EnableVisualUpdate || TyreModel == null) return;
        
        // 現在のばねの長さに基づいてタイヤモデルの位置を計算
        Vector3 targetPosition = Vector3.zero;
        
        // 地面に接触している場合は、ばねの長さに応じて位置を調整
        if (IsGrounded)
        {
            // ばねが下向きに伸びているので、CurrentLengthが大きいほどローカルY座標はマイナスになる
            targetPosition.y = -CurrentLength;
        }
        else
        {
            // 地面に接触していない場合は、最大長に合わせてタイヤモデルを調整
            CurrentLength = MaxLength;
            targetPosition.y = -MaxLength;
            
            // 地面に接触していない場合は、力をゼロに設定
            SpringForce = Vector3.zero;
            DamperForce = Vector3.zero;
            TotalForce = Vector3.zero;
            NormalForce = 0f;
            
            // 着地時間をリセット
            IsGroundedTime = 0f;
        }
        
        // 滑らかな移動を実現
        TyreModel.transform.localPosition = Vector3.Lerp(
            TyreModel.transform.localPosition,
            targetPosition,
            Time.fixedDeltaTime * VisualSmoothness
        );
    }
    
    // ばね力の計算とRigidbodyへの適用
    void CalculateSpringForce()
    {
        // 基準位置を計算（InitialLocalPositionをワールド座標に変換）
        Vector3 basePosition = transform.TransformPoint(InitialLocalPosition);
        
        // レイキャストで地面との距離を検出（InitialLocalPosition基準）
        IsGrounded = Physics.Raycast(
            basePosition,
            -transform.up,
            out HitInfo,
            MaxLength + TyreRadius,
            GroundLayer
        );
        
        if (IsGrounded)
        {
            // 地面との接触点を計算
            HitPoint = HitInfo.point;
            
            // 地面の法線を保存
            groundNormal = HitInfo.normal;
            
            // ばねの現在の長さを計算（タイヤの半径を考慮）
            CurrentLength = Vector3.Distance(basePosition, HitPoint) - TyreRadius;
            CurrentLength = Mathf.Clamp(CurrentLength, 0, MaxLength);
            
            // ばねの方向ベクトル（上向き）
            SpringDirection = transform.up;
            
            // 着地してからの時間を更新
            IsGroundedTime += Time.fixedDeltaTime;
            
            // フックの法則に基づくばね力の計算: F = -k * x
            // xは自然長からの変位
            float displacement = RestLength - CurrentLength;
            SpringForce = SpringDirection * SpringConstant * displacement;
            
            // ダンパー力の計算: F = -c * v
            // vは速度（長さの変化率）
            float velocity = (LastLength - CurrentLength) / Time.fixedDeltaTime;
            DamperForce = SpringDirection * DamperConstant * velocity;
            
            // 合計力の計算
            TotalForce = SpringForce + DamperForce;
            
            // 垂直抗力（地面への圧力）を計算 - これが摩擦力の基準になる
            NormalForce = Vector3.Dot(TotalForce, SpringDirection);
            if (NormalForce < 0) NormalForce = 0; // 負の値にならないように
            
            // 親Rigidbodyに力を適用（基準位置に力を加える）
            ParentRigidbody.AddForceAtPosition(TotalForce, basePosition);
            
            // 現在の長さを記録
            LastLength = CurrentLength;
        }
        else
        {
            // 地面に接触していない場合は自然長に設定
            CurrentLength = MaxLength;
            LastLength = MaxLength;
            NormalForce = 0;
        }
    }
    
    // 摩擦力の計算と適用
    void CalculateFrictionForce()
    {
        if (!IsGrounded || NormalForce <= 0 || frictionModel == null) return;
        
        // 摩擦モデルのパラメータを更新（インスペクタでの変更を反映）
        if (frictionModel is FrictionCircleModel circleModel)
        {
            circleModel.KineticFrictionCoefficient = KineticFrictionCoefficient;
            // 摩擦円の倍率を設定（ドリフトモード用）
            circleModel.FrictionCircleMultiplier = FrictionMultiplier;
        }
        
        // 接触点での車体の速度を計算
        Vector3 contactVelocity = ParentRigidbody.GetPointVelocity(HitPoint);
        
        // 摩擦モデルを使用して摩擦力を計算
        // アクセル入力値を渡す
        float accelInput = IsDriveWheel ? DriveInput : 0.0f;
        
        frictionForce = frictionModel.CalculateFrictionForce(
            NormalForce,
            HitPoint,
            HitInfo.normal,
            transform.forward,
            transform.right,
            contactVelocity,
            accelInput
        );
        
        // 摩擦力を適用
        ParentRigidbody.AddForceAtPosition(frictionForce, HitPoint);
        
        // デバッグ情報を更新
        if (tyreDebug != null && frictionModel is FrictionCircleModel circleModel2)
        {
            tyreDebug.FrictionForce = frictionForce;
            tyreDebug.ContactForce = NormalForce;
            
            // Car.csと完全に同じ方法でスリップアングルを設定
            float slipAngleRad = circleModel2.GetSlipAngle();
            tyreDebug.SlipAngle = slipAngleRad;
            tyreDebug.mSlipAngle = Mathf.Abs(slipAngleRad) * 360.0f / (2.0f * Mathf.PI);
            
            // Car.csでは符号を別で扱う
            if (slipAngleRad < 0) tyreDebug.mSlipAngle *= -1.0f;
            
            tyreDebug.FrictionCoefficient = circleModel2.GetFrictionCoefficient();
            tyreDebug.ContactVelocity = contactVelocity;
            tyreDebug.mFrictionCirclePow = circleModel2.GetFrictionCircleRadius();
        }
        
        // デバッグ用に駆動力を記録
        if (IsDriveWheel && Mathf.Abs(DriveInput) > 0.01f)
        {
            // タイヤの前方向に駆動力がかかるように計算
            Vector3 driveDirection = transform.forward;
            
            // 地面の法線ベクトルに対して垂直になるように調整
            driveDirection = Vector3.ProjectOnPlane(driveDirection, HitInfo.normal).normalized;
            
            // デバッグ用に駆動力を計算して保存（実際には適用しない）
            currentDriveForce = driveDirection * DriveInput * MaxDriveForce;
            
            // デバッグ用に方向を表示
            //Debug.Log($"駆動方向: {driveDirection}, 入力: {DriveInput}");
        }
        else
        {
            currentDriveForce = Vector3.zero;
        }
    }
    
    /// <summary>
    /// 地面の法線ベクトルを取得する
    /// </summary>
    /// <returns>地面の法線ベクトル（接地していない場合はVector3.up）</returns>
    public Vector3 GetGroundNormal()
    {
        return IsGrounded ? groundNormal : Vector3.up;
    }
    
    // デバッグ描画
    void OnDrawGizmos()
    {
        if (!ShowDebug) return;
        
        // 基準位置を計算（InitialLocalPositionをワールド座標に変換）
        Vector3 basePosition = transform.TransformPoint(InitialLocalPosition);
        
        // レイキャストの方向ベクトルを保存
        Vector3 rayDirection = -transform.up;
        
        // レイキャストの可視化（InitialLocalPosition基準）
        Gizmos.color = DebugRayColor;
        Gizmos.DrawLine(basePosition, basePosition + rayDirection * (MaxLength + TyreRadius));
        
        // タイヤモデルの位置を計算
        Vector3 tyrePosition = basePosition;
        if (TyreModel != null && Application.isPlaying)
        {
            // タイヤモデルのワールド位置を取得
            tyrePosition = TyreModel.transform.position;
        }
        else if (Application.isPlaying)
        {
            // タイヤモデルがない場合は、現在の長さに基づいて位置を計算
            tyrePosition = basePosition + rayDirection * (IsGrounded ? CurrentLength : MaxLength);
        }
        
        // 地面接触点の可視化
        if (IsGrounded && Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(HitPoint, 0.1f);
            
            // ばね力の可視化（タイヤ位置基準）
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(tyrePosition, SpringForce.normalized * 0.5f);
            
            // 摩擦力の可視化
            if (frictionModel != null && frictionForce.magnitude > 0.01f)
            {
                // 摩擦力を表示
                Gizmos.color = Color.red;
                Gizmos.DrawRay(HitPoint, frictionForce.normalized * 0.5f);
            }
            
            // 駆動力の可視化
            if (currentDriveForce.magnitude > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(HitPoint, currentDriveForce.normalized * 0.5f);
            }
        }
        
        // タイヤの半径の可視化（タイヤ位置基準）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(tyrePosition, TyreRadius);
        
        // 基準位置の可視化
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(basePosition, 0.05f);
    }
}
