using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// バイクを追従するカメラコンポーネント
/// </summary>
public class BikeCamera : MonoBehaviour
{
    [Header("追従設定")]
    [Tooltip("追従するバイクのTransform")]
    public Transform targetBike;
    
    [Tooltip("バイクからのカメラの相対位置")]
    public Vector3 offset = new Vector3(0, 2.0f, -5.0f);
    
    [Tooltip("カメラの追従速度（位置）")]
    public float positionSmoothSpeed = 5.0f;
    
    [Tooltip("カメラの追従速度（回転）")]
    public float rotationSmoothSpeed = 3.0f;
    
    [Tooltip("バイクの速度ベクトルに基づいて回転するかどうか")]
    public bool rotateWithVelocity = true;
    
    [Tooltip("速度が遅い場合はバイクの向きに合わせる速度の閾値")]
    public float minVelocityForRotation = 2.0f;
    
    [Header("高度な設定")]
    [Tooltip("カメラの上下オフセット調整（地形に応じて）")]
    public float heightOffset = 1.0f;
    
    [Tooltip("カメラの視点の高さ調整")]
    public float lookAtHeightOffset = 1.0f;
    
    [Tooltip("カメラの視野角")]
    public float fieldOfView = 60.0f;
    
    [Header("距離設定")]
    [Tooltip("バイクとカメラの最低距離")]
    public float minDistance = 3.0f;
    
    [Tooltip("バイクとカメラの最大距離")]
    public float maxDistance = 10.0f;
    
    // プライベート変数
    private Vector3 currentVelocity;
    private Rigidbody targetRigidbody;
    private Camera cam;
    private Vector3 smoothDampVelocity;
    private Quaternion targetRotation;
    
    void Start()
    {
        // カメラコンポーネントの取得
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = fieldOfView;
        }
        
        // ターゲットのRigidbodyを取得
        if (targetBike != null)
        {
            targetRigidbody = targetBike.GetComponent<Rigidbody>();
        }
        else
        {
            Debug.LogError("BikeCamera: ターゲットのバイクが設定されていません。");
        }
    }
    
    void LateUpdate()
    {
        if (targetBike == null) return;
        
        // バイクの位置を取得
        Vector3 targetPosition = targetBike.position;
        
        // カメラの目標位置を計算
        Vector3 desiredPosition;
        
        if (rotateWithVelocity && targetRigidbody != null)
        {
            // バイクの速度ベクトルを取得
            Vector3 velocity = targetRigidbody.velocity;
            
            // 速度が十分にある場合は速度ベクトルの方向を使用
            if (velocity.magnitude > minVelocityForRotation)
            {
                // 速度ベクトルの方向を正規化
                Vector3 velocityDirection = velocity.normalized;
                velocityDirection.y *= 0.4f;
                
                // 速度ベクトルの方向に基づいてカメラの位置を計算
                // 速度の逆方向にオフセットを適用
                float distance = Mathf.Abs(offset.z);
                
                // 距離を最低距離と最大距離の間に制限
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
                
                desiredPosition = targetPosition - velocityDirection * distance;
                
                // 高さは固定
                desiredPosition.y = targetPosition.y + offset.y;
                
                // 速度ベクトルの方向を向くようにカメラの回転を計算
                targetRotation = Quaternion.LookRotation(velocityDirection, Vector3.up);
            }
            else
            {
                // 速度が遅い場合はバイクの向きに基づいて位置を計算
                // オフセットの距離を制限する
                Vector3 clampedOffset = offset;
                float distance = Mathf.Abs(clampedOffset.z);
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
                clampedOffset.z = -distance; // zは負の値なのでマイナスを付ける
                
                desiredPosition = targetPosition + targetBike.TransformDirection(clampedOffset);
                targetRotation = Quaternion.LookRotation(targetBike.forward, Vector3.up);
            }
        }
        else
        {
            // 速度ベクトルを使用しない場合はバイクのローカル座標系でのオフセットを適用
            // オフセットの距離を制限する
            Vector3 clampedOffset = offset;
            float distance = Mathf.Abs(clampedOffset.z);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            clampedOffset.z = -distance; // zは負の値なのでマイナスを付ける
            
            desiredPosition = targetPosition + targetBike.TransformDirection(clampedOffset);
            targetRotation = Quaternion.LookRotation(targetBike.forward, Vector3.up);
        }
        
        // 地形に応じた高さ調整（必要に応じてRaycastを使用）
        desiredPosition.y += heightOffset;
        
        // 位置の滑らかな移動
        Vector3 newPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothDampVelocity, 1.0f / positionSmoothSpeed);
        
        // 移動後のバイクからの距離を計算
        Vector3 directionToCamera = newPosition - targetPosition;
        float currentDistance = directionToCamera.magnitude;
        
        // 距離が最小距離より小さい場合は調整
        if (currentDistance < minDistance)
        {
            directionToCamera = directionToCamera.normalized * minDistance;
            newPosition = targetPosition + directionToCamera;
        }
        // 距離が最大距離より大きい場合は調整
        else if (currentDistance > maxDistance)
        {
            directionToCamera = directionToCamera.normalized * maxDistance;
            newPosition = targetPosition + directionToCamera;
        }
        
        // 新しい位置を設定
        transform.position = newPosition;
        
        // 回転の滑らかな変更
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
        
        // カメラの視点をバイクに向ける（オプション）
        // Vector3 lookAtPosition = targetPosition + Vector3.up * lookAtHeightOffset;
        // transform.LookAt(lookAtPosition);
    }
    
    // デバッグ用のギズモ表示
    void OnDrawGizmosSelected()
    {
        if (targetBike == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetBike.position + Vector3.up * lookAtHeightOffset);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(targetBike.position + Vector3.up * lookAtHeightOffset, 0.2f);
    }
}
