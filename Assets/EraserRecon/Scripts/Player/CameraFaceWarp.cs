using UnityEngine;
using Unity.Cinemachine;

public class CameraFaceWarp : CinemachineExtension
{
    [Header("参照(体の当たり判定。重なったかの判定に使う)")]
    [SerializeField] private CharacterController playerCollider;   //playerCollider：Playerの体の範囲を知るためのCharacterController

    [Header("参照(目線の位置。向きの基準)")]
    [SerializeField] private Transform cameraTarget;   //cameraTarget：ワープ先を計算する基準(目の位置)

    [Header("ワープ時、顔からどれだけ前に置くか")]
    [SerializeField] private float faceOffsetDistance = 0.3f;   //faceOffsetDistance：顔の前にワープしたときの距離

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,   //vcam：今処理されているCinemachineカメラ本体
        CinemachineCore.Stage stage,         //stage：今どの段階の処理が終わったところかを表す
        ref CameraState state,               //state：カメラの位置・向きなどの計算結果(refなので直接書き換えられる)
        float deltaTime)                     //deltaTime：前回のフレームからの経過時間
    {
        // Bodyステージ(位置を決める段階)が終わった直後以外は何もしない
        if(stage != CinemachineCore.Stage.Body) return;   //return：ここで関数を抜けて、以降の処理をしない

        Vector3 candidatePosition = state.RawPosition + state.PositionCorrection;   //candidatePosition：今までの計算で決まったカメラの位置

        // カメラの位置が体の当たり判定の中に入ってしまっているか判定する
        if(playerCollider.bounds.Contains(candidatePosition))   //Contains：指定した点が、この範囲(Bounds)の中にあるか調べる
        {
            Vector3 warpPosition = cameraTarget.position + cameraTarget.forward * faceOffsetDistance;   //warpPosition：目の位置から、見ている方向に少し進んだ場所

            state.RawPosition = warpPosition;          //RawPosition：カメラの位置を直接ワープ先に書き換える
            state.PositionCorrection = Vector3.zero;   //PositionCorrection：それまでの補正をリセットする(ワープ後に二重に補正しないため)
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
