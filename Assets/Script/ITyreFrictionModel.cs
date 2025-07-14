using UnityEngine;

/// <summary>
/// タイヤの摩擦モデルのインターフェース
/// </summary>
public interface ITyreFrictionModel
{
    /// <summary>
    /// 摩擦力を計算する
    /// </summary>
    /// <param name="normalForce">垂直抗力（地面への圧力）</param>
    /// <param name="contactPoint">接触点</param>
    /// <param name="contactNormal">接触面の法線</param>
    /// <param name="longitudinalDir">タイヤの前後方向</param>
    /// <param name="lateralDir">タイヤの左右方向</param>
    /// <param name="contactVelocity">接触点での速度</param>
    /// <returns>計算された摩擦力</returns>
    Vector3 CalculateFrictionForce(
        float normalForce,
        Vector3 contactPoint,
        Vector3 contactNormal,
        Vector3 longitudinalDir,
        Vector3 lateralDir,
        Vector3 contactVelocity,
        float accelerationInput = 0.0f
    );
    

}
