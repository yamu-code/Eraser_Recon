
using UnityEngine;


[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public enum MoveMode { Idle, Sneak, Walk, Sprint, Exhausted }   //MoveMode：今の移動方法の種類(状態)の一覧
    public enum SprintStyle {  Hold, Toggle }                      //SprintStyle：ダッシュの入力方式(ホールド/トグル)の一覧

    [Header("移動速度 (m/秒)")]
    [SerializeField] private float sneakSpeed = 1.2f;   //忍び足
    [SerializeField] private float walkSpeed = 2.4f;    //通常歩行
    [SerializeField] private float sprintSpeed = 4.0f;  //ダッシュ
    [SerializeField] private float backwardSpeedMultiplier = 0.7f;  //真後ろに進むときの速さの倍率

    [Header("ダッシュ")]
    [SerializeField] private SprintStyle sprintStyle = SprintStyle.Hold;    //ホールド = 押して

    //テスト中にInspectorで今の状態を見られるようにSerializeFieldにしておく
    [SerializeField] private MoveMode currentMode;
    public MoveMode CurrentMode => currentMode; //他のスクリプトから読むだけ

    [SerializeField] private bool isCrouching;
    public bool IsCrouching => isCrouching;

    [Header("コントローラー")]
    [SerializeField] private float stickSneakEnter = 0.45f; //倒し具合がこれ未満になったら忍び足に入る
    [SerializeField] private float stickSneakExit = 0.55f;  //忍び足中は、これ以上倒すまで忍び足のまま

    [Header("方向切り替えのIdleチラつき防止")]
    [SerializeField] private float directionHoldBuffer = 0.15f;     //入力が0になっても、この秒数だけ直前の方向アニメに送る

    [Header("アニメーションの本来の速さ(m/秒)")]
    [SerializeField] private float walkClipSpeeds = 1.4631f;   //walkアニメーションの本来の速さ
    [SerializeField] private float runClipSpeeds = 4.4841f;  //Runアニメーションの本来の速さ

    [Header("スタミナ")]
    [SerializeField] private float maxStamina = 100f;                       //最大値
    [SerializeField] private float stamina = 100f;                          //今のスタミナ
    [SerializeField] private float sprintStaminaCost = 20f;                 //ダッシュ中に1秒で減る量
    [SerializeField] private float idleRecoveryRate = 15f;                  //止まっているときに1秒で回復する量
    [SerializeField] private float sneakRecoveryRate = 10f;                 //忍び足
    [SerializeField] private float walkRecoveryRate = 5f;                   //歩き
    [SerializeField] private float exhaustedRecoveryRate = 3f;              //疲労中
    [SerializeField] private float exhaustedRecoveryThreshold = 30f;        //疲労が治るスタミナ
    [SerializeField] private bool isExhausted;                              //疲労状態か

    public float Stamina => stamina;
    public bool IsExhausted => isExhausted;

    [Header("しゃがみ")]
    [SerializeField] private float standHeight = 1.8f;          //立っているときの体の高さ
    [SerializeField] private float crouchHeight = 1.2f;         //しゃがんでいるときの体の高さ
    [SerializeField] private LayerMask ceilingLayers = ~0;      //天井チェックでぶつかる物のレイヤー(~0 = 全部)

    // 入力から、今どの移動方法か決める
    private MoveMode DecideMoveMode(Vector2 input)
    {
        float amount = input.magnitude;

        //①入力なし → 止まる(トグルのダッシュも解除)
        if (amount < 0.01f)
        {
            sprintToggled = false;
            return MoveMode.Idle;
        }

        bool sprintInput = IsSprintInputActive();

        //②しゃがみ中：ダッシュ入力があって立ち上がれるなら立ってダッシュへ、それ以外は忍び足
        if (isCrouching)
        {
            if(sprintInput && !isExhausted && CanStandUp())
            {
                isCrouching = false;        //立ち上がって、下のダッシュの判定へ進む
            }
            else
            {
                sprintToggled = false;
                return MoveMode.Sneak;
            }
        }

        //疲労中と同じ速さ(トグルのダッシュも解除)
        if(isExhausted)
        {
            sprintToggled = false;
            return MoveMode.Exhausted;
        }

        //③キーボードのCtrl → 忍び足(トグルのダッシュも解除)
        if (inputActions.Player.Sneak.IsPressed())
        {
            sprintToggled = false;
            return MoveMode.Sneak;
        }

        //④ダッシュ入力中 → ダッシュ(スティックの倒し具合は見ない)
        if (sprintInput)
        {
            return MoveMode.Sprint;
        }

        //⑤スティックを少しだけ倒している → 忍び足
        //  今が忍び足なら「抜ける境目」、そうでなければ「入る境目」で判定する
        float threshold = (currentMode == MoveMode.Sneak) ? stickSneakExit : stickSneakEnter;
        if (amount < threshold)
        {
            return MoveMode.Sneak;
        }

        return MoveMode.Walk;
    }

    //ダッシュボタンが「今ダッシュしたい」状態かどうか(ダッシュ方式によって判定が変わる)
    private bool IsSprintInputActive()
    { 
        //ホールド：押している間だけ
        if(sprintStyle == SprintStyle.Hold)
        {
            return inputActions.Player.Sprint.IsPressed();
        }

        //トグル：押した瞬間にオン/オフ切り替える
        if(inputActions.Player.Sprint.WasPressedThisFrame())
        {
            sprintToggled = !sprintToggled;
        }
        return sprintToggled;
    }

    // Cキー(コントローラーはR3)を押した瞬間に、しゃがむ⇔立つを切り替える
    private void ReadCrouchToggle()
    {
        if(inputActions.Player.Crouch.WasPressedThisFrame())
        {
            if(!isCrouching)
            {
                isCrouching = true;         //しゃがむのはいつでもできる
            }
            else if(CanStandUp())
            {
                isCrouching = false;        //立つのは頭の上に空間があるときだけ
            }
        }
    }

    //移動方法ごとの速度を返す
    private float GetSpeed(MoveMode mode)
    {
        switch(mode)
        {
            case MoveMode.Sneak:  return sneakSpeed;
            case MoveMode.Sprint: return sprintSpeed;
            case MoveMode.Walk:   return walkSpeed;
            case MoveMode.Exhausted: return sneakSpeed;
            default:              return 0f;
        }
    }

    //今の移動方法に合わせて、スタミナを減らす/回復する。0で疲労、決まった量まで回復したら治る
    private void UpdateStamina()
    {
        if(currentMode == MoveMode.Sprint)
        {
            stamina -= sprintStaminaCost * Time.deltaTime;
        }
        else
        {
            stamina += GetRecoveryRate() * Time.deltaTime;
        }
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        if(stamina <= 0f)
        {
            isExhausted = true;
        }
        else if(isExhausted && stamina >= exhaustedRecoveryThreshold)
        {
            isExhausted = false;
        }
    }

    //1秒あたりの回復量を返す(疲労中は止まっていても疲労中の回復量)
    private float GetRecoveryRate()
    {
        if (isExhausted) return exhaustedRecoveryRate;

        switch(currentMode)
        {
            case MoveMode.Idle: return idleRecoveryRate;
            case MoveMode.Sneak: return sneakRecoveryRate;
            default: return walkRecoveryRate;
        }
    }

    //頭の上に、立ち上がれるだけの空間があるか(しゃがんだ頭の位置から、立った頭の位置まで弾を飛ばして調べる)
    private bool CanStandUp()
    {
        float radius = controller.radius;
        Vector3 start = transform.position + Vector3.up * (crouchHeight - radius);
        float distance = standHeight - crouchHeight;
        return !Physics.SphereCast(new Ray(start, Vector3.up), radius, distance, ceilingLayers, QueryTriggerInteraction.Ignore);
    }

    //しゃがみ中かどうかで、CharacterControllerの高さと中心を変える(足元の位置は変えない)
    private void ApplyCrouchHeight()
    {
        float height = isCrouching ? crouchHeight : standHeight;
        controller.height = height;
        controller.center = new Vector3(0f, height / 2f, 0f);
    }

    private Vector2 lastAnimInput;          //アニメに送った最後の方向
    private float lastAnimSpeed;            //アニメに送った最後の速さ
    private bool lastAnimSprinting;         //アニメに送った最後のダッシュ中かどうか
    private float holdTimer;                //方向保持バッファの残り時間

    private CharacterController controller;     //controller：実際にキャラを動かすためのCharacterController本体
    private Animator animator;                  //animator：アニメーションを再生するためのAnimator本体
    private InputSystem_Actions inputActions;   //inputActions：入力を読み取るための箱

    private static readonly int SpeedParam = Animator.StringToHash("Speed");               //StringToHash：文字列を数値に変換しておき、毎フレームの文字列比較を避けて高速化する
    private static readonly int MoveXParam = Animator.StringToHash("MoveX");               //MoveXParam：Animatorの"MoveX"パラメータ用のID
    private static readonly int MoveYParam = Animator.StringToHash("MoveY");               //MoveYParam：Animatorの"MoveY"パラメータ用のID
    private static readonly int IsCrouchingParam = Animator.StringToHash("IsCrouching");   //IsCrouchingParam：Animatorの"IsCrouching"パラメータ用のID
    private static readonly int MotionSpeedParam = Animator.StringToHash("MotionSpeed");   //MotionSpeedParam：Animatorの"MotionSpeed"パラメータ用のID
    private static readonly int IsSprintingParam = Animator.StringToHash("IsSprinting");   //IsSprintingParam：Animatorの"IsSprinting"パラメータ用のID

    [SerializeField] private float gravity = -9.81f;   //gravity：1秒あたり落下速度がどれだけ増えるか

    private float verticalVelocity;   //verticalVelocity：今の上下方向の速度(重力で落ちる速さ)
    private bool sprintToggled;       //sprintToggled：トグル方式でダッシュがON状態かどうか

    // 一番最初に1回だけ呼ばれる：必要な部品を取得して変数に入れておく
    private void Awake()
    {
        controller = GetComponent<CharacterController>();   //GetComponent：同じGameObjectにあるCharacterControllerを取得する
        animator = GetComponent<Animator>();                //GetComponent：同じGameObjectにあるAnimatorを取得する
        inputActions = new InputSystem_Actions();           //new：入力を読み取るための箱を作る
    }

    // このスクリプトが有効になったら入力を受け付け始める
    private void OnEnable()
    {
        inputActions.Player.Enable();   //Enable：入力の読み取りを開始する
    }

    // このスクリプトが無効になったら入力を受け付けるのをやめる
    private void OnDisable()
    {
        inputActions.Player.Disable();   //Disable：入力の読み取りを止める
    }

    private void Start()
    {
       //始まった場所の天井が低くて立てないなら、しゃがんだ状態から始める
       if(!CanStandUp())
        {
            isCrouching = true;
        }
       ApplyCrouchHeight();   //始まった時点の高さをCharacterControllerに反映しておく
    }

    // Update is called once per frame
    // 毎フレーム、入力を読んでキャラを動かし、アニメに値を送る
    void Update()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();   //ReadValue：今の移動入力(スティック/WASD)を取り出す
        ReadCrouchToggle();                      //しゃがむ/立つの切り替え入力を処理する
        currentMode = DecideMoveMode(input);     //今の入力から、今の移動方法を決めて覚えておく

        UpdateStamina();        //今の移動方法に応じてスタミナを増減させる
        ApplyCrouchHeight();    //しゃがみ中かどうかをCharacterControllerの高さに反映する

        Vector3 moveDirection = (transform.right * input.x + transform.forward * input.y).normalized;   //moveDirection：入力をキャラの向き基準の実際の移動方向に変換する

        if(controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; //接地時も軽く下向きに押し続けてSkin Widthの隙間を埋める
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;   //空中にいる間は、重力分だけ下向きの速度を増やしていく
        }

        float moveSpeed = GetSpeed(currentMode);    //今の移動速度。アニメにも使うので変数に入れる
        float backward = Mathf.Clamp01(-input.normalized.y);   //backward：真後ろ方向への入力の強さ(0〜1)
        moveSpeed *= Mathf.Lerp(1f, backwardSpeedMultiplier, backward);   //Lerp：後ろ向きの入力が強いほど、速さを倍率分だけ落とす
        Vector3 velocity = moveDirection * moveSpeed;   //velocity：実際にその方向へ移動する速度

        velocity.y = verticalVelocity;   //上下方向(重力)の速度を合成する

        controller.Move(velocity * Time.deltaTime);   //Move：このフレーム分、実際にキャラを移動させる

        //アニメに送る値:キーを持ち替える一瞬の入力ゼロでIdleがチラつかないように、入力がなくなっても少しの間(directionHoldBuffer秒)は直前の値を送り続ける
        if(input.sqrMagnitude > 0.0001f)
        {
            lastAnimInput = input;                                       //今の入力をアニメ用に覚えておく
            lastAnimSpeed = moveSpeed;                                   //今の速さをアニメ用に覚えておく
            lastAnimSprinting = currentMode == MoveMode.Sprint;          //今ダッシュ中かどうかをアニメ用に覚えておく
            holdTimer = directionHoldBuffer;                             //バッファの残り時間をリセットする
        }
        else if(holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;   //入力が無くなってからの経過時間分だけバッファを減らす
        }
        else
        {
            lastAnimSpeed = 0f;          //バッファも切れたので、止まっている速さ(0)にする
            lastAnimSprinting = false;   //ダッシュ中でもなくする
        }

        //方向は長さ1にして送る(倒し具合でブレンドが崩れないように)
        Vector2 animDir = lastAnimInput.normalized;        //animDir：方向だけを取り出した(長さ1の)ベクトル
        animator.SetFloat(MoveXParam, animDir.x);          //SetFloat：Animatorの"MoveX"パラメータに値を送る
        animator.SetFloat(MoveYParam, animDir.y);          //SetFloat：Animatorの"MoveY"パラメータに値を送る

        //速さを送る。止まった/動いたの判定(0.1)に使う
        animator.SetFloat(SpeedParam, lastAnimSpeed);      //SetFloat：Animatorの"Speed"パラメータに値を送る

        //ダッシュ中かどうかを送る。Walk⇔Runの切り替えに使う
        animator.SetBool(IsSprintingParam, lastAnimSprinting);   //SetBool：Animatorの"IsSprinting"パラメータに値を送る

        //足の速さを移動の速さに合わせる倍率 = 移動速度 ÷ 基準の速さ(全方向共通なので、どの向きでも足の回転の速さがそろう)
        float clipSpeed = lastAnimSprinting ? runClipSpeeds : walkClipSpeeds;   //clipSpeed：今の状態(歩き/ダッシュ)に応じた、アニメ本来の速さ
        animator.SetFloat(MotionSpeedParam, lastAnimSpeed > 0f ? lastAnimSpeed / clipSpeed : 1f);   //SetFloat：Animatorの"MotionSpeed"パラメータに倍率を送る

        animator.SetBool(IsCrouchingParam, isCrouching);   //SetBool：Animatorの"IsCrouching"パラメータに値を送る(しゃがみアニメへの切り替えに使う)
    }
}
