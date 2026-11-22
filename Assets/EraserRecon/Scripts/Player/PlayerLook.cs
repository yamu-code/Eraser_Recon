using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerLook : MonoBehaviour
{
    [Header("感度")]
    [SerializeField] private float mouseSensitivity = 1f;    //マウスの感度
    [SerializeField] private float stickSensitivity = 1f;    //スティックの感度

    [Header("上下の角度制限(度数)")]
    [SerializeField] private float topClamp = 70f;     //見上げる上限
    [SerializeField] private float bottomClamp = -30f; //見下げる下限

    [Header("参照(Playerの子、目線の高さにあるCameraTarget)")]
    [SerializeField] private Transform cameraTarget;

    private float pitch;                        //今の上下角度を覚えておく
    private bool isCurrentDeviceMouse = true;   //今使っているのがマウスかどうか(最後に入力があった方を記録)
    private InputSystem_Actions inputActions;   //入力を読み取るための箱

    // Input Actionsの実体を作る
    private void Awake()
    {
        inputActions = new InputSystem_Actions();                                 //new：入力を読み取るための箱を作る
    }

    // このスクリプトが有効になったら入力を受け付け始める
    private void OnEnable()
    {
        inputActions.Player.Enable();        //Enable：入力の読み取りを開始する
        inputActions.Player.Look.performed += OnLookPerformed;   //+=：入力が来るたびにOnLookPerformedを呼ぶよう登録する
    }

    // このスクリプトが無効になったら入力を受け付けるのをやめる
    private void OnDisable()
    {
        inputActions.Player.Look.performed -= OnLookPerformed;   //-=：登録したイベントを解除する(消し忘れ防止)
        inputActions.Player.Disable();                          //Disable：入力の読み取りを止める
    }

    // Look入力が来るたびに、それがマウスからのものかを記録するだけの関数
    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        // 入力デバイスがマウスかどうかを判定する
        isCurrentDeviceMouse = context.control.device is Mouse;   //device is Mouse：入力元の機器がマウスかどうか判定する
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // 毎フレーム、視点の入力を読んでキャラとカメラの向きに反映する
    void Update()
    {
        // 視点入力(マウス/スティック)を読み取る
        Vector2 look = inputActions.Player.Look.ReadValue<Vector2>();            //ReadValue：今の入力の値を取り出す

        // マウスかスティックかで、倍率と感度を切り替える
        float deltaTimeMultiplier = isCurrentDeviceMouse ? 1f : Time.deltaTime;         //?:（三項演算子）：条件によってどちらかの値を選ぶ
        float sensitivity = isCurrentDeviceMouse ? mouseSensitivity : stickSensitivity; //?:（三項演算子）：ここも同じ仕組み

        // 左右(Yaw)：キャラ本体を直接回す(CameraTargetはPlayerの子なので一緒についてくる)
        transform.Rotate(Vector3.up, look.x * sensitivity * deltaTimeMultiplier);  //Rotate：指定した軸を中心に回転させる

        // 上下(Pitch)：角度を制限してから、CameraTarget自身のローカル回転に反映する
        pitch -= look.y * sensitivity * deltaTimeMultiplier;                       //-=：これまでの角度に今回分を足し引きする
        pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);                       //Mathf.Clamp：値を上限/下限の範囲に収める
        cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);   //localRotation：親(Player)のYawに、ここでPitchを上乗せする
    }
}
