using UnityEngine;

/// <summary>
/// 摩擦円モデルの実装（Car.csの実装に完全に合わせる）
/// </summary>
public class FrictionCircleModel : MonoBehaviour, ITyreFrictionModel
{
    // 摩擦円の基本パラメータ（Car.csと同じ値を使用）
    
    // 摩擦係数の設定（Car.csと同じ値を使用）
    public float KineticFrictionCoefficient { get; set; } = 0.8f;  // 動摩擦係数
    public float MinimumFrictionCoefficient { get; set; } = 0.16f;  // 最小摩擦係数
    
    // デバッグ表示用のアクセサーメソッド
    public float GetSlipAngle() { return slipAngle; }
    public float GetFrictionCoefficient() { return frictionCoefficient; }
    public float GetFrictionCircleRadius() { return frictionCircleRadius; }
    
    // コーナリングフォースの設定はCar.csの実装に合わせてハードコード
    
    // 内部状態変数
    private float frictionCircleRadius;  // 摩擦円の半径
    private Vector3 appliedLateralForce;  // 適用された横方向の力
    private float slipAngle;  // スリップアングル
    private float frictionCoefficient;  // 摩擦係数
    
    /// <summary>
    /// 摩擦力を計算する
    /// </summary>
    public Vector3 CalculateFrictionForce(
        float normalForce,
        Vector3 contactPoint,
        Vector3 contactNormal,
        Vector3 longitudinalDir,
        Vector3 lateralDir,
        Vector3 contactVelocity,
        float accelerationInput = 0.0f)
    {
        if (normalForce <= 0) return Vector3.zero;
        
        // 地面の法線ベクトルに対して垂直になるように方向ベクトルを調整
        longitudinalDir = Vector3.ProjectOnPlane(longitudinalDir, contactNormal).normalized;
        lateralDir = Vector3.ProjectOnPlane(lateralDir, contactNormal).normalized;
        
        // 縦方向と横方向の速度成分を計算
        float longitudinalVelocity = Vector3.Dot(contactVelocity, longitudinalDir);
        float lateralVelocity = Vector3.Dot(contactVelocity, lateralDir);
        
        float absoluteVelocity = contactVelocity.magnitude;
        
        // 摩擦円の半径を計算（Car.csの実装を参考）
        // 地面にかかる力に比例した摩擦円の半径
        // Car.csと完全に同じ摩擦円の計算
        frictionCircleRadius = normalForce * 0.9f;
        
        // タイヤ速度から接触面の法線成分を除去
        Vector3 velocityOnPlane = contactVelocity - contactNormal * Vector3.Dot(contactNormal, contactVelocity);
        
        // 前後方向と横方向の速度成分を計算
        float longitudinalVelocityMagnitude = Vector3.Dot(velocityOnPlane, longitudinalDir);
        Vector3 longitudinalVelocityVector = longitudinalDir * longitudinalVelocityMagnitude;
        Vector3 lateralVelocityVector = velocityOnPlane - longitudinalVelocityVector;
        
        // スリップアングルを計算（Car.csと完全に同じ実装）
        float slipAngle = 0;
        
        // Car.csでは速度の閾値チェックは行われていない
        // タイヤの前方向と速度ベクトルの内積からスリップアングルを計算
        float tyreDot = Vector3.Dot(velocityOnPlane.normalized, longitudinalDir);
        tyreDot = Mathf.Clamp(tyreDot, -1.0f, 1.0f); // 範囲を-1から1に制限
        slipAngle = Mathf.Acos(tyreDot);
        
        // スリップアングルの符号を求める（Car.csと完全に同じ方法）
        float sign = 1.0f;
        // Car.csでは上方向を使用しているが、ここでは法線を使用する
        Vector3 crs = Vector3.Cross(velocityOnPlane.normalized, Vector3.up);
        if (Vector3.Dot(crs, longitudinalDir) < 0.0f) sign = -1.0f;
        
        // 符号を適用
        slipAngle *= sign;
        
        // スリップアングルを内部変数に保存
        this.slipAngle = slipAngle;
        
        // コーナリングフォース計算（Car.csと完全に同じ実装）
        float cfCurve = Mathf.Abs(slipAngle) / (10.0f * Mathf.Deg2Rad); // 10度までは線形に力が増える
        cfCurve = Sigmoid(1.0f, cfCurve);
        if (cfCurve > 1.0f) cfCurve = 1.0f;
        float cfMax = cfCurve * 3000.0f; // Car.csと同じ値
        
        // コーナリングフォース計算（Car.csと完全に同じ実装）
        float pow = 60.0f; // Car.csと同じ値
        Vector3 cf = -lateralVelocityVector * pow;
        
        // コーナリングフォースの最大値制限
        if (cf.magnitude > cfMax)
        {
            cf = cf.normalized * cfMax; // Car.csと同じ値
        }
        
        // アクセル力の計算（入力値を使用）
        Vector3 accForce = longitudinalDir * accelerationInput * 400f;
        accForce -= contactNormal * Vector3.Dot(accForce, contactNormal);
        
        // 最終的な摩擦力を計算
        Vector3 frictionForce = cf + accForce;
        
        // 摩擦円を適用（Car.csと完全に同じ実装）
        float mu = 1.0f;
        float ratio = frictionForce.magnitude / frictionCircleRadius;
        frictionCoefficient = mu;
        
        if (ratio > 1.0f)
        {
            // Car.csと同じ摩擦係数計算
            float p0 = 1.02f;
            float p1 = 1.06f;
            float r0 = 1.02f;
            float mu_min = MinimumFrictionCoefficient;
            
            if (ratio > 1.00f && ratio < p0)
            {
                mu = LinerInterpolate(ratio, 1.00f, p0, 1.00f, r0);
            }
            else if (ratio > p0 && ratio < p1)
            {
                mu = LinerInterpolate(ratio, p0, p1, r0, mu_min);
            }
            else
            {
                mu = mu_min;
            }
            
            // 摩擦係数を適用
            frictionForce *= mu;
            frictionCoefficient = mu;
        }
        
        return frictionForce;
    }
    
    // シグモイド関数
    private float Sigmoid(float gain, float x)
    {
        return 1.0f / (1.0f + Mathf.Exp(-gain * x));
    }
    
    // 線形補間（範囲外は補外）
    private float LinerInterpolate(float now, float start, float end, float startVal, float endVal)
    {
        float ratio = (now - start) / (end - start);
        return ratio * (endVal - startVal) + startVal;
    }
    
    // 現在の状態を取得するプロパティ（デバッグ用）
    public float FrictionCircleRadius => frictionCircleRadius;
    public float FrictionCoefficient => frictionCoefficient;
    public float SlipAngle => slipAngle;
}
