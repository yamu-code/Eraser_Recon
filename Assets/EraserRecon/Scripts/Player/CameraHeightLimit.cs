using UnityEngine;
using Unity.Cinemachine;

public class CameraHeightLimit : CinemachineExtension
{
    [Header("参照(頭のてっぺんの高さに置いた空のオブジェクト)")]
    [SerializeField] private Transform headTop;   //headTop：カメラの上限として使う位置を覚えておく箱

    // PostPipelineStageCallback：Cinemachineが各段階(Body/Aim/Noise/Finalize)の処理を終えるたびに呼ばれる関数
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,   //vcam：今処理されているCinemachineカメラ本体
        CinemachineCore.Stage stage,         //stage：今どの段階の処理が終わったところかを表す
        ref CameraState state,               //state：カメラの位置・向きなどの計算結果(refなので直接書き換えられる)
        float deltaTime)                     //deltaTime：前回のフレームからの経過時間
    {
        // Bodyステージ(位置を決める段階)が終わった直後以外は何もしない
        if(stage != CinemachineCore.Stage.Body) return;   //return：ここで関数を抜けて、以降の処理をしない

        float maxY = headTop.position.y;   //maxY：上限の高さ(頭のてっぺんのワールドY座標)
        float currentY = state.RawPosition.y + state.PositionCorrection.y;   //currentY：今Cinemachineが計算したカメラの高さ

        // 今の高さが上限を超えていたら、超えた分だけ下に押し戻す
        if(currentY > maxY)
        {
            state.PositionCorrection.y += maxY - currentY;   //PositionCorrection：Cinemachineの計算結果に足す補正値
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
