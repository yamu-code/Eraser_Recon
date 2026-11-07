# PlayerController.cs 解説

- ファイル：`Assets/EraserRecon/Scripts/Player/PlayerController.cs`
- 役割：プレイヤーの入力（移動・ダッシュ・忍び足・しゃがみ）を読み、「今どの移動方法か（MoveMode）」を決めて、スタミナを増減させ（0で疲労状態）、CharacterControllerで実際に動かす（重力・接地維持も含む）。その結果をAnimator（`PlayerAnimator.controller`）へ送り、見た目のアニメーションと連動させる。
- 依存するもの：
  - `CharacterController`（同じGameObjectに必要。Player：Height 1.8／Radius 0.3／Center (0, 0.9, 0)）
  - `Animator`（同じGameObjectに必要。Controllerは `Assets/EraserRecon/Animations/Player/PlayerAnimator.controller`、**Apply Root Motionはオフ**）
  - Animatorのパラメータ：`Speed`(Float)／`MoveX`(Float)／`MoveY`(Float)／`MotionSpeed`(Float)／`IsSprinting`(Bool)／`IsCrouching`(Bool)。名前は完全一致が必要
  - Animatorのステート：Idle／Walk（Speed倍率＝MotionSpeed）／Run（Speed倍率＝MotionSpeed）
  - Input Actions（`Assets/InputSystem_Actions.inputactions` から自動生成される `InputSystem_Actions` クラス）の `Player` マップ：`Move`(Vector2)／`Sprint`／`Sneak`／`Crouch`
- 最終更新：2026-10-16（スタミナ・疲労状態を追加：セット⑫）／2026-10-15（ダッシュ機能の完成に合わせて全面書き直し。解説を①〜⑥の形式に変更）

### この資料の読み方（各セット共通の①〜⑥）
① 重要な部品（読み方つき）とその意味 → ② その1行の最終的な意味 → ③ まとまり全体での意味 → ④ どこと繋がっているか → ⑤ **どういう仕様・動作にしたいときに使うか** → ⑥ **起きやすいバグと、調整する場所**

---

## コード全文

```csharp
using UnityEngine;


[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public enum MoveMode { Idle, Sneak, Walk, Sprint, Exhausted }
    public enum SprintStyle {  Hold, Toggle }

    [Header("移動速度 (m/秒)")]
    [SerializeField] private float sneakSpeed = 0.8f;   //忍び足
    [SerializeField] private float walkSpeed = 1.3f;    //通常歩行
    [SerializeField] private float sprintSpeed = 3.8f;  //ダッシュ
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

        //②しゃがみ中は必ず忍び足(トグルのダッシュも解除)
        if (isCrouching)
        {
            sprintToggled = false;
            return MoveMode.Sneak;
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
        if (IsSprintInputActive())
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
            isCrouching = !isCrouching;
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

    private Vector2 lastAnimInput;          //アニメに送った最後の方向
    private float lastAnimSpeed;            //アニメに送った最後の速さ
    private bool lastAnimSprinting;         //アニメに送った最後のダッシュ中かどうか
    private float holdTimer;                //方向保持バッファの残り時間

    private CharacterController controller;
    private Animator animator;
    private InputSystem_Actions inputActions;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int MoveXParam = Animator.StringToHash("MoveX");
    private static readonly int MoveYParam = Animator.StringToHash("MoveY");
    private static readonly int IsCrouchingParam = Animator.StringToHash("IsCrouching");
    private static readonly int MotionSpeedParam = Animator.StringToHash("MotionSpeed");
    private static readonly int IsSprintingParam = Animator.StringToHash("IsSprinting");

    [SerializeField] private float gravity = -9.81f;

    private float verticalVelocity;
    private bool sprintToggled;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
        ReadCrouchToggle();
        currentMode = DecideMoveMode(input);

        UpdateStamina();

        Vector3 moveDirection = (transform.right * input.x + transform.forward * input.y).normalized;

        if(controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; //接地時も軽く下向きに押し続けてSkin Widthの隙間を埋める
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        float moveSpeed = GetSpeed(currentMode);    //今の移動速度。アニメにも使うので変数に入れる
        float backward = Mathf.Clamp01(-input.normalized.y);
        moveSpeed *= Mathf.Lerp(1f, backwardSpeedMultiplier, backward);
        Vector3 velocity = moveDirection * moveSpeed;

        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

        //アニメに送る値:キーを持ち替える一瞬の入力ゼロでIdleがチラつかないように、入力がなくなっても少しの間(directionHoldBuffer秒)は直前の値を送り続ける
        if(input.sqrMagnitude > 0.0001f)
        {
            lastAnimInput = input;
            lastAnimSpeed = moveSpeed;
            lastAnimSprinting = currentMode == MoveMode.Sprint;
            holdTimer = directionHoldBuffer;
        }
        else if(holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
        }
        else
        {
            lastAnimSpeed = 0f;
            lastAnimSprinting = false;
        }

        //方向は長さ1にして送る(倒し具合でブレンドが崩れないように)
        Vector2 animDir = lastAnimInput.normalized;
        animator.SetFloat(MoveXParam, animDir.x);
        animator.SetFloat(MoveYParam, animDir.y);

        //速さを送る。止まった/動いたの判定(0.1)に使う
        animator.SetFloat(SpeedParam, lastAnimSpeed);

        //ダッシュ中かどうかを送る。Walk⇔Runの切り替えに使う
        animator.SetBool(IsSprintingParam, lastAnimSprinting);

        //足の速さを移動の速さに合わせる倍率 = 移動速度 ÷ 基準の速さ(全方向共通なので、どの向きでも足の回転の速さがそろう)
        float clipSpeed = lastAnimSprinting ? runClipSpeeds : walkClipSpeeds;
        animator.SetFloat(MotionSpeedParam, lastAnimSpeed > 0f ? lastAnimSpeed / clipSpeed : 1f);

        animator.SetBool(IsCrouchingParam, isCrouching);
    }
}
```

---

## 全体の流れ（1フレームで起きること）

```
Update（毎フレーム）
 ├ ① 入力を読む ……………… Move（スティック／WASD）
 ├ ② しゃがみの切り替え ……… ReadCrouchToggle()
 ├ ③ 移動方法を決める ……… DecideMoveMode() → Idle／Sneak／Walk／Sprint／Exhausted
 ├ ③' スタミナを増減する …… UpdateStamina()（0で疲労、30で回復）
 ├ ④ 動かす ………………………… 方向 × 速さ（GetSpeed＋後ろ倍率）＋重力 → controller.Move()
 ├ ⑤ アニメに送る値を決める … 方向保持バッファ（0.15秒）
 └ ⑥ Animatorへ送る …………… MoveX／MoveY／Speed／IsSprinting／MotionSpeed／IsCrouching
```

**覚え方**：「**読む → 決める → 動かす → 見た目に伝える**」。どのキャラ操作スクリプトもほぼこの順番。バグが出たら、この4段のどこで値がおかしくなっているかを、Inspectorの `Current Mode` やAnimatorウィンドウのパラメータを見て切り分ける。

---

## セット①：スクリプトの土台（部品の保証と「移動方法」の型）

```csharp
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public enum MoveMode { Idle, Sneak, Walk, Sprint }
    public enum SprintStyle {  Hold, Toggle }
```

**① 部品**
- `RequireComponent（リクワイア・コンポーネント）`：「この部品が必ず要る」という印。`typeof（タイプオブ）(X)` で「Xという種類」を指定する。
- `MonoBehaviour（モノビヘイビア）`：GameObjectに付けて動かすスクリプトの元。これを継承するとUnityが `Awake`／`Update` などを自動で呼ぶ。
- `enum（イーナム）`：決まった選択肢に名前を付けた自作の型。
- `MoveMode（ムーブモード）`：「移動方法」という意味を込めた名前。`SprintStyle（スプリント・スタイル）`：「ダッシュのやり方」。

**② 1行の意味**
- `[RequireComponent(typeof(CharacterController))]` は「このスクリプトを付けたら、CharacterControllerが無ければ自動で追加し、外せないようにする」という意味。
- `public enum MoveMode { Idle, Sneak, Walk, Sprint }` は「移動方法は 止まる／忍び足／歩き／ダッシュ の4つしかない、という型を作る」という意味。

**③ まとまりの意味**：このスクリプトが動くのに必要な部品を保証し、「今の移動方法」を4択の型で表せるようにしている。

**④ つながり**：`MoveMode` は `DecideMoveMode()` が返し、`currentMode` に入り、`GetSpeed()` で速度に変わる。`SprintStyle` はInspectorの「Sprint Style」欄と `IsSprintInputActive()` の分岐につながる。

**⑤ どういう仕様のときに使うか**
- `RequireComponent`：「このスクリプトは〇〇が無いと絶対に動かない」とき（移動ならCharacterController、アニメ連動ならAnimator）。
- `enum`：「状態が決まった数だけあり、同時に2つにはならない」とき。例：移動方法、ゾンビの状態（Sleep／Patrol／Alert…）、武器の種類、ダッシュ方式。bool を何個も並べる（isSneak, isWalk, isSprint…）と「両方trueになる」矛盾が起きるが、enumなら必ず1つに決まる。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| `MissingComponentException` が出る | `RequireComponent` を付け忘れた、または付ける前から置いてあったGameObject。一度スクリプトを外して付け直す |
| 状態が2つ同時にオンになる（歩きとダッシュが同時など） | bool をやめて enum にまとめる |
| enum の名前を後から並べ替えたら、Inspectorの選択がずれた | enum は中身が番号で保存される。並べ替えずに末尾へ追加する |

---

## セット②：Inspectorで調整する値

```csharp
[Header("移動速度 (m/秒)")]
[SerializeField] private float sneakSpeed = 0.8f;
[SerializeField] private float walkSpeed = 1.3f;
[SerializeField] private float sprintSpeed = 3.8f;
[SerializeField] private float backwardSpeedMultiplier = 0.7f;

[SerializeField] private MoveMode currentMode;
public MoveMode CurrentMode => currentMode;
```

**① 部品**
- `[SerializeField]（シリアライズ・フィールド）`：`private` でもInspectorに表示して、シーンに保存する印。
- `[Header("...")]（ヘッダー）`：Inspectorに見出しを出す。
- `0.8f` の `f`：小数（float）の印。
- `=>（アロー）`：読むだけの窓口（プロパティ）。
- 変数名：`sneakSpeed（スニーク・スピード）`＝忍び足の速さ、`walkSpeed（ウォーク・スピード）`、`sprintSpeed（スプリント・スピード）`、`backwardSpeedMultiplier（バックワード・スピード・マルチプライヤー）`＝後ろに進むときの掛け算、`currentMode（カレント・モード）`＝今の移動方法、`stickSneakEnter／Exit（スティック・スニーク・エンター／イグジット）`＝忍び足に入る／抜ける倒し具合、`directionHoldBuffer（ディレクション・ホールド・バッファ）`＝方向を持っておく猶予、`walkClipSpeeds／runClipSpeeds（ウォーク／ラン・クリップ・スピーズ）`＝アニメの基準の速さ。

**② 1行の意味**
- `[SerializeField] private float walkSpeed = 1.3f;` は「歩きの速さ 1.3 m/秒を、ほかのスクリプトからは触れないけどInspectorでは調整できる値として持つ」という意味。
- `public MoveMode CurrentMode => currentMode;` は「今の移動方法を、ほかのスクリプトが読めるようにする（書き換えはさせない）」という意味。

**③ まとまりの意味**：ゲームバランスに関わる数値を全部Inspectorに出し、Playしながら調整できるようにしている。仕様書の数値（GameSpec）はここに入る。

**④ つながり**：速度3つは `GetSpeed()` で使われる。`backwardSpeedMultiplier` は `Update` の `Mathf.Lerp` で使われる。`CurrentMode` は将来、足音・ゾンビの感知（`PlayerDetection`）・スタミナが読む予定。

**⑤ どういう仕様のときに使うか**
- `[SerializeField] private`：「数値をテストしながら調整したい、でもほかのスクリプトに勝手に書き換えられたくない」とき。速度・HP・感知範囲・クールダウンなど、ほぼ全部のバランス調整値。
- `public 型 名前 => 変数;`：「ほかのシステムに今の状態を教えたいが、変えさせたくない」とき。例：ゾンビAIがプレイヤーの移動方法を読んで、足音の大きさを決める。
- テスト中だけ `[SerializeField]` を状態（`currentMode`、`isCrouching`）に付けておくと、Play中にInspectorで「今どの状態か」が目で見えてデバッグが楽になる。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| コードの数字（例：1.3f）を変えたのに速さが変わらない | Inspectorに保存された値が優先される。Inspectorで直接入力するか、コンポーネントの **⋮ → Reset** |
| Play中に調整した値が、止めたら元に戻った | Play中の変更は保存されない。決まった値をメモして、止めてから入力し直す |
| ほかのスクリプトから値を変えようとしてエラー | `=>` は読むだけ。変えたいなら変える専用のメソッドを作る |

---

## セット③：部品の取得と入力の起動・停止

```csharp
private void Awake()
{
    controller = GetComponent<CharacterController>();
    animator = GetComponent<Animator>();
    inputActions = new InputSystem_Actions();
}
private void OnEnable()  { inputActions.Player.Enable(); }
private void OnDisable() { inputActions.Player.Disable(); }
```

**① 部品**
- `Awake（アウェイク）`：オブジェクトができた直後に1回だけUnityが呼ぶ。
- `OnEnable（オン・イネーブル）`／`OnDisable（オン・ディセーブル）`：有効になった時／無効になった時にUnityが呼ぶ。
- `GetComponent<X>()（ゲット・コンポーネント）`：同じGameObjectに付いている部品Xを取ってくる。
- `new InputSystem_Actions()（ニュー・インプットシステム・アクションズ）`：入力設定を使う準備をする。
- `.Enable()（イネーブル）`／`.Disable()（ディセーブル）`：入力の受付を開始／停止する。

**② 1行の意味**
- `controller = GetComponent<CharacterController>();` は「自分に付いているCharacterControllerを探して、`controller` という箱に入れておく」という意味。
- `inputActions.Player.Enable();` は「Playerの入力（移動・ダッシュなど）の受付を開始する」という意味。

**③ まとまりの意味**：ゲーム開始時に、使う部品と入力の仕組みを1回だけ用意し、オブジェクトが有効な間だけ入力を受け付ける。

**④ つながり**：`controller` は `Update` の `controller.Move()`／`isGrounded` で、`animator` は `SetFloat`／`SetBool` で使う。`inputActions` は Input Actions ファイル（`InputSystem_Actions.inputactions`）から自動生成されたクラスで、`Player.Move`／`Sprint`／`Sneak`／`Crouch` を読むときに使う。

**⑤ どういう仕様のときに使うか**
- 「宣言 → `Awake` で `GetComponent` → あとは使うだけ」の3点セット：同じオブジェクトの部品を何度も使うとき（移動、アニメ、音、当たり判定など）。`GetComponent` は重いので、`Update` で毎回呼ばない。
- `OnEnable`／`OnDisable` で `Enable`／`Disable`：ポーズ中・会話中・死亡時など、「操作を受け付けない時間」を作りたいとき。プレイヤーを無効にすれば、入力も自動で止まる。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| コードは合っているのに、キーを押しても何も反応しない | `Enable()` を書き忘れている（`new` しただけでは動かない） |
| `NullReferenceException` が `controller` や `animator` の行で出る | `Awake` で `GetComponent` していない、または部品が付いていない |
| Input Actionsに追加したアクションがコードで見つからない（赤線） | Input Actionsウィンドウで **Save Asset** を押していない（`InputSystem_Actions.cs` が作り直されていない） |

---

## セット④：Animatorのパラメータ番号

```csharp
private static readonly int SpeedParam = Animator.StringToHash("Speed");
private static readonly int MoveXParam = Animator.StringToHash("MoveX");
// …MoveY／IsCrouching／MotionSpeed／IsSprinting も同じ
```

**① 部品**
- `Animator.StringToHash("名前")（ストリング・トゥ・ハッシュ）`：パラメータ名（文字）を番号に変換する。
- `static readonly（スタティック・リードオンリー）`：全員で1つだけ共有し、最初に決めたら書き換えない値。
- 変数名の `〜Param（パラム）`：「Animatorのパラメータの番号」という意味を込めている。

**② 1行の意味**：「Animatorの `Speed` というパラメータを番号に変換して、`SpeedParam` として最初に1回だけ用意しておく」という意味。

**③ まとまりの意味**：Animatorとやり取りする6つのパラメータの番号を、1か所にまとめて用意している。

**④ つながり**：Animatorウィンドウの **Parameters タブ**の名前と完全一致している必要がある。`Update` の最後の `animator.SetFloat(SpeedParam, …)` などで使う。

**⑤ どういう仕様のときに使うか**：スクリプトからAnimatorに値を送るときは、ほぼ毎回この形にする（移動、攻撃、被ダメージ、しゃがみ、ゾンビの状態など）。文字で毎回探すより速く、名前の打ち間違いも1か所にまとまる。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| 値を送っているのにアニメが変わらない。コンソールに `Parameter 'xxx' does not exist` | ここの文字列とAnimatorのParametersの名前が違う（大文字・小文字、スペースも区別される） |
| `SetFloat` しているのにアニメが変わらない（エラーなし） | パラメータの種類が違う（Bool のパラメータに `SetFloat` している等）。Parametersタブで型を確認 |

---

## セット⑤：移動方法を決める（`DecideMoveMode`）

```csharp
private MoveMode DecideMoveMode(Vector2 input)
{
    float amount = input.magnitude;
    if (amount < 0.01f)              { sprintToggled = false; return MoveMode.Idle; }   //①
    if (isCrouching)                 { sprintToggled = false; return MoveMode.Sneak; }  //②
    if (inputActions.Player.Sneak.IsPressed()) { sprintToggled = false; return MoveMode.Sneak; } //③
    if (IsSprintInputActive())       { return MoveMode.Sprint; }                        //④
    float threshold = (currentMode == MoveMode.Sneak) ? stickSneakExit : stickSneakEnter;  //⑤
    if (amount < threshold)          { return MoveMode.Sneak; }
    return MoveMode.Walk;
}
```
（実際のコードは見やすく改行してある。ここでは流れが分かるように詰めて書いた）

**① 部品**
- `.magnitude（マグニチュード）`：長さ。スティックなら倒し具合（0〜1）。`amount（アマウント）`＝「どれくらい倒しているか」。
- `return（リターン）`：答えを返して、その関数をそこで終わらせる。
- `条件 ? A : B`（三項演算子／さんこうえんざんし）：条件がtrueならA、falseならB。
- `threshold（スレッショルド）`：境目の値。

**② 1行の意味**
- `if (amount < 0.01f) { … return MoveMode.Idle; }` は「ほとんど倒していないなら、トグルのダッシュも解除して『止まる』に決める」という意味。
- `float threshold = (currentMode == MoveMode.Sneak) ? stickSneakExit : stickSneakEnter;` は「今が忍び足なら境目を 0.55（抜けにくい）、そうでなければ 0.45（入りにくい）にする」という意味。

**③ まとまりの意味**：**上から順に条件を調べて、最初に当てはまったものに決める**（優先順位）。止まる ＞ しゃがみ ＞ Ctrl ＞ ダッシュ ＞ 倒し具合 ＞ 歩き。ダッシュ入力中は倒し具合を見ないので、L3を押し込んだときに倒し具合がゆるんでも忍び足と誤判定されない。

**④ つながり**：`Update` の `currentMode = DecideMoveMode(input);` から呼ばれる。結果の `currentMode` は `GetSpeed()`（速さ）と、アニメに送る `lastAnimSprinting`（Walk⇔Run）につながる。`stickSneakEnter／Exit` はInspectorの「コントローラー」欄。

**⑤ どういう仕様のときに使うか**
- **上から順に `if … return` で決める形**：「いくつかの状態があって、優先順位がある」とき。例：ゾンビAI（死亡 ＞ 怯み ＞ 攻撃 ＞ 追跡 ＞ 徘徊）、武器を使えるか（リロード中 ＞ ダッシュ中 ＞ 構え中）。優先順位を変えたいときは、`if` の順番を入れ替えるだけで済む。
- **入る境目と抜ける境目をずらす（ヒステリシス）**：「境目付近で状態がチカチカ切り替わるのを防ぎたい」とき。例：スティックの倒し具合、スタミナ切れと回復（0で疲労、30で回復）、ゾンビの感知範囲（入る距離と見失う距離をずらす）。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| ダッシュボタンを押しているのに忍び足になる | `if` の順番（ダッシュの判定が忍び足の判定より後ろにある）。この機能で一度対策済み |
| スティックを中間くらいに倒すと、忍び足と歩きがチカチカ切り替わる | `stickSneakEnter`／`stickSneakExit` の差を広げる（例：0.4／0.6） |
| 少し倒しただけなのに歩きになる／深く倒しても忍び足のまま | `stickSneakEnter`／`Exit` の値そのもの（コントローラーごとに倒し具合の感覚が違うので、実機で調整） |
| 止まったのにトグルのダッシュが残っていて、次に動いた瞬間から走り出す | 「止まる」の `if` の中の `sprintToggled = false;` が消えていないか |

---

## セット⑥：ダッシュの入力（ホールド／トグル）

```csharp
private bool IsSprintInputActive()
{
    if(sprintStyle == SprintStyle.Hold)
    {
        return inputActions.Player.Sprint.IsPressed();
    }
    if(inputActions.Player.Sprint.WasPressedThisFrame())
    {
        sprintToggled = !sprintToggled;
    }
    return sprintToggled;
}
```

**① 部品**
- `IsPressed()（イズ・プレスト）`：押している間ずっと true。
- `WasPressedThisFrame()（ワズ・プレスト・ディス・フレーム）`：押した瞬間の1フレームだけ true。
- `!（ノット）`：true と false を反対にする。
- `sprintToggled（スプリント・トグルド）`：「トグルでダッシュがオンになっているか」。

**② 1行の意味**
- `return inputActions.Player.Sprint.IsPressed();` は「ダッシュボタン（Shift・L3・△）を今押しているなら true を返す」という意味。
- `sprintToggled = !sprintToggled;` は「ダッシュのオン／オフを反対にする」という意味。

**③ まとまりの意味**：ダッシュ方式（Inspectorの Sprint Style）によって、「押している間だけ走る」か「1回押すたびに走る／やめるを切り替える」かを決める。

**④ つながり**：ボタンの割り当ては **Input Actions の Sprint**（キーボード：左Shift、コントローラー：L3と△）。方式はInspectorの **Sprint Style**（Hold／Toggle）。結果は `DecideMoveMode()` の④で使われる。

**⑤ どういう仕様のときに使うか**

| したい動作 | 使うもの |
|---|---|
| 押している間だけ続く（ダッシュ、構える、ガード、しゃがみ歩き） | `IsPressed()` |
| 押した瞬間に1回だけ（ジャンプ、リロード、インタラクト、武器切り替え） | `WasPressedThisFrame()` |
| 押すたびにオン／オフ（トグルのダッシュ、しゃがみ、懐中電灯） | `WasPressedThisFrame()` ＋ `x = !x;` |
| 離した瞬間に1回だけ（弓を離して撃つ、溜め攻撃の発動） | `WasReleasedThisFrame()（ワズ・リリースト・ディス・フレーム）` |

覚え方：**「続く動作は IsPressed、1回きりの動作は WasPressedThisFrame」**

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| トグルなのに、1回押しただけでオン／オフがチカチカする | `WasPressedThisFrame()` ではなく `IsPressed()` を使っている |
| ボタンを押しても走らない | Input Actions の Sprint に割り当てがない、Use in control scheme のチェック漏れ、Save Asset の押し忘れ |
| △で走ると、ほかの動作も同時に起きる | 同じボタンが2つのアクションに割り当たっている（今は△が Interact にも入っている。インタラクトを作るときに○へ変える） |

---

## セット⑦：しゃがみの切り替え

```csharp
private void ReadCrouchToggle()
{
    if(inputActions.Player.Crouch.WasPressedThisFrame())
    {
        isCrouching = !isCrouching;
    }
}
```

**① 部品**：`isCrouching（イズ・クラウチング）`＝「しゃがんでいるか」。`WasPressedThisFrame()` と `!` はセット⑥と同じ。

**② 1行の意味**：`isCrouching = !isCrouching;` は「しゃがみ⇔立ちを反対にする」という意味。

**③ まとまりの意味**：C キー（コントローラーは R3）を押した瞬間に、しゃがむ／立つを切り替える（トグルの型）。

**④ つながり**：`isCrouching` は `DecideMoveMode()` の②（しゃがみ中は必ず忍び足）と、Animator の `IsCrouching` パラメータに送られる。ボタンは Input Actions の Crouch。

**⑤ どういう仕様のときに使うか**：「押すたびに状態が切り替わる」とき全般（しゃがみ、ライトのオン／オフ、地図を開く／閉じる、エイムのトグル）。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| しゃがんでも見た目が変わらない | まだしゃがみのアニメ・高さ変更が未実装（次の機能）。Animator の `IsCrouching` を使う遷移を作る必要がある |
| 天井の低い場所で立ち上がれてしまう | 未実装。立ち上がる前に頭上に障害物がないかを調べる処理（天井チェック）が必要 |

---

## セット⑧：移動方法ごとの速さ（`GetSpeed`）

```csharp
private float GetSpeed(MoveMode mode)
{
    switch(mode)
    {
        case MoveMode.Sneak:  return sneakSpeed;
        case MoveMode.Sprint: return sprintSpeed;
        case MoveMode.Walk:   return walkSpeed;
        default:              return 0f;
    }
}
```

**① 部品**
- `switch（スイッチ）`：値によって処理を分ける。`case（ケース）`：「この値のとき」。`default（デフォルト）`：「どれにも当てはまらないとき」。

**② 1行の意味**：`case MoveMode.Sprint: return sprintSpeed;` は「移動方法がダッシュなら、ダッシュの速さ（3.8）を返す」という意味。

**③ まとまりの意味**：移動方法（enum）を、実際の速さ（m/秒）に変換する「対応表」。止まるとき（Idle）は `default` で 0。

**④ つながり**：`Update` の `float moveSpeed = GetSpeed(currentMode);` から呼ばれる。速さの数値はセット②のInspector値。

**⑤ どういう仕様のときに使うか**：「enum の値ごとに、数値や処理を変えたい」とき。例：移動方法 → 速さ・足音の大きさ・スタミナの減り方、ゾンビの状態 → 移動速度、武器の種類 → 攻撃力。`if` を何個も並べるより、対応表として一目で読める。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| 新しい移動方法（例：疲労状態）を追加したら、その状態で動かない | `switch` に `case` を足し忘れて `default`（0）になっている。enum に追加したら `switch` も必ず見直す |

---

## セット⑨：実際に動かす（方向・重力・後ろ向きの倍率・Move）

```csharp
Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
ReadCrouchToggle();
currentMode = DecideMoveMode(input);

Vector3 moveDirection = (transform.right * input.x + transform.forward * input.y).normalized;

if(controller.isGrounded && verticalVelocity < 0) { verticalVelocity = -2f; }
else { verticalVelocity += gravity * Time.deltaTime; }

float moveSpeed = GetSpeed(currentMode);
float backward = Mathf.Clamp01(-input.normalized.y);
moveSpeed *= Mathf.Lerp(1f, backwardSpeedMultiplier, backward);
Vector3 velocity = moveDirection * moveSpeed;
velocity.y = verticalVelocity;
controller.Move(velocity * Time.deltaTime);
```

**① 部品**
- `ReadValue<Vector2>()（リード・バリュー）`：今の入力値を読む。`Vector2（ベクター・ツー）`＝x（左右）と y（前後）の2つの数。
- `transform.right／transform.forward（トランスフォーム・ライト／フォワード）`：キャラの右方向／前方向。
- `.normalized（ノーマライズド）`：向きはそのままで、長さを1にしたもの。
- `controller.isGrounded（イズ・グラウンデッド）`：地面に着いているか。
- `Time.deltaTime（タイム・デルタタイム）`：前のフレームからの秒数。
- `Mathf.Clamp01（マスエフ・クランプ・ゼロワン）`：0〜1の範囲に収める。
- `Mathf.Lerp(a, b, t)（ラープ）`：a と b の間を割合 t で選ぶ。
- `*=`：今の値に掛けて上書き。
- `controller.Move（ムーブ）`：指定した量だけ動かす（壁にはぶつかって止まる）。
- 変数名：`moveDirection（ムーブ・ディレクション）`＝進む向き、`verticalVelocity（バーティカル・ベロシティ）`＝上下の速さ、`backward（バックワード）`＝どれくらい後ろ向きか（0〜1）、`velocity（ベロシティ）`＝最終的な速度。

**② 1行の意味**
- `Vector3 moveDirection = (transform.right * input.x + transform.forward * input.y).normalized;` は「入力を、キャラの向きを基準にした3Dの進む向きに変え、斜めでも速くならないように長さを1にする」という意味。
- `float backward = Mathf.Clamp01(-input.normalized.y);` は「真後ろなら1、真横や前なら0という『後ろ向き度』を作る」という意味（斜め後ろは約0.7）。
- `moveSpeed *= Mathf.Lerp(1f, backwardSpeedMultiplier, backward);` は「後ろ向き度に応じて、速さを 1倍〜0.7倍 にする」という意味（真後ろ＝0.7倍、斜め後ろ＝約0.79倍）。
- `controller.Move(velocity * Time.deltaTime);` は「1秒あたりの速度を、このフレームの分だけに直して動かす」という意味。

**③ まとまりの意味**：入力 → 進む向き → 速さ（移動方法＋後ろ向きの倍率）→ 重力を足して、**横の移動と縦の移動を1回の `Move` でまとめて**動かす。

**④ つながり**：入力は Input Actions の Move。速さは `GetSpeed()`、後ろ向きの倍率はInspectorの `Backward Speed Multiplier`。`moveSpeed` はこのあとアニメに送る `lastAnimSpeed` にも使われる（だから変数に入れてある）。

**⑤ どういう仕様のときに使うか**
- `方向 × 速さ × Time.deltaTime`：**動くもの全部**（プレイヤー、ゾンビ、弾、動く床）。fps が違っても同じ速さで動く。
- `.normalized`：「斜め入力でも速くならないようにしたい」とき。
- `Mathf.Lerp`：「ある値から別の値へ、割合でなめらかに変えたい」とき。例：後ろ向きほど遅くする、近いほど音を大きくする、カメラを少しずつ追いかけさせる。
- 「接地中は弱く下に押す／空中は重力を足し続ける」：CharacterController を使うキャラにはほぼ必ず必要な型（CharacterController は重力を自動でやってくれない）。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| 斜めに進むと速くなる | `.normalized` が抜けている |
| fps によって速さが変わる（重いPCだと遅い） | `Time.deltaTime` を掛け忘れている |
| 地面の上で少し浮く・`isGrounded` が false のまま | 接地中の `verticalVelocity = -2f`（下に押す力）が無い。下の「バグ修正の記録」参照 |
| 将来ジャンプを作ったら飛べない | `&& verticalVelocity < 0` を消していないか（これが無いと上向きの速さが -2 に上書きされる） |
| `Move` を2回呼んだら、接地判定が不安定になった | 横と縦は1つの `velocity` にまとめて、`Move` は1フレーム1回だけ |
| 後ろ向きが遅すぎる／速すぎる | Inspectorの `Backward Speed Multiplier`（0.7＝7割の速さ。0.8 で少し速く） |
| カメラの向きと進む向きがずれる | `transform.right／forward`（キャラの向き基準）を使っている。カメラ基準にしたいなら、カメラの向きを使う形に変える |

---

## セット⑩：アニメに送る値と方向保持バッファ

```csharp
if(input.sqrMagnitude > 0.0001f)
{
    lastAnimInput = input;
    lastAnimSpeed = moveSpeed;
    lastAnimSprinting = currentMode == MoveMode.Sprint;
    holdTimer = directionHoldBuffer;
}
else if(holdTimer > 0f)
{
    holdTimer -= Time.deltaTime;
}
else
{
    lastAnimSpeed = 0f;
    lastAnimSprinting = false;
}
```

**① 部品**
- `.sqrMagnitude（スクエア・マグニチュード）`：長さの2乗。計算が速いので「入力があるか」の判定に使う。
- `-=`：今の値から引いて上書き。
- 変数名：`lastAnimInput（ラスト・アニム・インプット）`＝アニメに送った最後の方向、`lastAnimSpeed（ラスト・アニム・スピード）`、`lastAnimSprinting（ラスト・アニム・スプリンティング）`、`holdTimer（ホールド・タイマー）`＝保持の残り時間。

**② 1行の意味**
- `holdTimer = directionHoldBuffer;` は「入力があるあいだは、保持タイマーを 0.15秒 に満タンにし直す」という意味。
- `holdTimer -= Time.deltaTime;` は「入力が消えたら、タイマーを少しずつ減らす（その間は直前の値を送り続ける）」という意味。

**③ まとまりの意味**：W から S に持ち替えるような**一瞬の入力ゼロ**で、アニメが Idle（棒立ち）を挟まないように、入力が消えても 0.15秒 だけは直前の方向・速さ・ダッシュ中かを Animator に送り続ける。**実際の移動（セット⑨）には本物の入力を使う**ので、操作の反応は遅くならない。

**④ つながり**：ここで決めた `lastAnim〜` の3つが、次のセット⑪で Animator に送られる。猶予の秒数はInspectorの `Direction Hold Buffer`。

**⑤ どういう仕様のときに使うか**：「入力やセンサーの一瞬の途切れで、見た目や状態が切り替わってほしくない」とき。例：方向キーの持ち替え、地面判定が段差で一瞬切れる（コヨーテタイム）、ゾンビが一瞬プレイヤーを見失っても追跡を続ける。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| キーを持ち替えると一瞬棒立ちになる | `Direction Hold Buffer` を少し長く（0.15 → 0.2） |
| 止まったあと、少し遅れて止まるアニメになる | `Direction Hold Buffer` が長すぎる（0.1 くらいまで短く） |

---

## セット⑪：Animatorへ送る（足の速さ合わせを含む）

```csharp
Vector2 animDir = lastAnimInput.normalized;
animator.SetFloat(MoveXParam, animDir.x);
animator.SetFloat(MoveYParam, animDir.y);
animator.SetFloat(SpeedParam, lastAnimSpeed);
animator.SetBool(IsSprintingParam, lastAnimSprinting);

float clipSpeed = lastAnimSprinting ? runClipSpeeds : walkClipSpeeds;
animator.SetFloat(MotionSpeedParam, lastAnimSpeed > 0f ? lastAnimSpeed / clipSpeed : 1f);

animator.SetBool(IsCrouchingParam, isCrouching);
```

**① 部品**
- `animator.SetFloat(番号, 値)（セット・フロート）`：小数のパラメータに値を書き込む。
- `animator.SetBool(番号, 値)（セット・ブール）`：true／false のパラメータに値を書き込む。
- 変数名：`animDir（アニム・ディア）`＝アニメ用の方向、`clipSpeed（クリップ・スピード）`＝今使う基準の速さ。

**② 1行の意味**
- `animator.SetFloat(MoveXParam, animDir.x);` は「左右の向き（-1〜1）を Animator の MoveX に送り、Blend Tree の左右の混ぜ具合を決める」という意味。
- `animator.SetBool(IsSprintingParam, lastAnimSprinting);` は「ダッシュ中かどうかを送り、Walk⇔Run の切り替えに使わせる」という意味。
- `animator.SetFloat(MotionSpeedParam, lastAnimSpeed > 0f ? lastAnimSpeed / clipSpeed : 1f);` は「足の再生倍率を『実際の速さ ÷ アニメの基準の速さ』にする（止まっているときは 1倍）」という意味。

**③ まとまりの意味**：スクリプトで決めた「向き・速さ・ダッシュ中か・しゃがみ中か」を Animator に伝え、見た目（どのアニメを、どの方向に、どの速さで再生するか）を動きに合わせる。

**④ つながり（Animator側）**

| 送る値 | Animatorで使われる場所 |
|---|---|
| MoveX／MoveY | Walk・Run の Blend Tree（2D Simple Directional）の混ぜ具合 |
| Speed | Idle⇔Walk の遷移（0.1 より大きいか小さいか）、Run→Idle |
| IsSprinting | Walk→Run（true）、Run→Walk（false かつ Speed > 0.1） |
| MotionSpeed | Walk・Run ステートの Speed の倍率（Multiplier に MotionSpeed を指定） |
| IsCrouching | しゃがみのアニメ（これから作る） |

**⑤ どういう仕様のときに使うか**
- `SetFloat`：連続した量（速さ、向き、照準の角度）。`SetBool`：続く状態（ダッシュ中、しゃがみ中、構え中）。一瞬の出来事（着地、被ダメージ、攻撃）は `SetTrigger（セット・トリガー）` を使う。
- **`移動速度 ÷ 基準の速さ` → ステートの Speed 倍率**：「歩く速さを変えても、足の動きを速さに合わせたい」とき（忍び足・歩き・ダッシュで同じアニメを速さ違いで使う、疲労で遅くなる等）。
- **方向を `.normalized` で送る**：スティックを少しだけ倒したときでも、Blend Tree の方向の混ぜ具合が崩れないようにしたいとき。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| 全方向で足が地面を滑る（体が先に進む） | Inspectorの `Walk Clip Speeds`（下げると足が速く動く） |
| 足が空回りする（ルームランナー状態） | `Walk Clip Speeds` を上げる |
| ある方向だけ滑る | スクリプトではなく、Blend Tree のその行の **Time Scale**（このクリップ群は方向ごとに歩幅が違う。下の「今後の拡張メモ」参照） |
| ダッシュの足だけ速い／遅い | `Run Clip Speeds` |
| 走るアニメにならない | Animator の Walk→Run 遷移の条件（IsSprinting）、`Has Exit Time` がオンになっていないか |
| 歩き出しや止まるときに滑る | Animator の遷移の Transition Duration（Idle→Walk 0.15秒） |
| 止まっているのに足踏みする | `MotionSpeed` の止まったときの値（今は 1）と、Idle への遷移の条件 |

---

## セット⑫：スタミナと疲労状態（2026-10-16追加）

```csharp
public enum MoveMode { Idle, Sneak, Walk, Sprint, Exhausted }   // Exhausted を末尾に追加

//DecideMoveMode の中（しゃがみの次、Ctrlの前）
if(isExhausted)
{
    sprintToggled = false;
    return MoveMode.Exhausted;
}

//GetSpeed の中
case MoveMode.Exhausted: return sneakSpeed;

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

//Update の中（DecideMoveMode の直後）
UpdateStamina();
```

**① 部品**
- `Mathf.Clamp(値, 最小, 最大)（マスエフ・クランプ）`：値を最小〜最大の範囲に収める。
- `<=`（小なりイコール）：「以下」。`>=`：「以上」。
- `&&`（アンド）：両方が true のときだけ true。
- 変数名：`maxStamina（マックス・スタミナ）`＝最大値、`stamina（スタミナ）`＝今の量、`sprintStaminaCost（スプリント・スタミナ・コスト）`＝ダッシュで1秒に減る量、`idle／sneak／walk／exhaustedRecoveryRate（アイドル／スニーク／ウォーク／イグゾースティッド・リカバリー・レート）`＝状態ごとの1秒の回復量、`exhaustedRecoveryThreshold（イグゾースティッド・リカバリー・スレッショルド）`＝疲労が治る境目、`isExhausted（イズ・イグゾースティッド）`＝疲労中か。
- `Exhausted（イグゾースティッド）`：「疲れ切った」。MoveMode の5つ目の状態。

**② 1行の意味**
- `stamina -= sprintStaminaCost * Time.deltaTime;` は「ダッシュ中は、1秒に20のペースでスタミナを減らす（fpsに関係なく）」という意味。
- `stamina = Mathf.Clamp(stamina, 0f, maxStamina);` は「スタミナが 0 より下や 100 より上にはみ出さないように切る」という意味。
- `if(stamina <= 0f) isExhausted = true;` は「スタミナが0以下になったら疲労状態にする」という意味。
- `else if(isExhausted && stamina >= exhaustedRecoveryThreshold) isExhausted = false;` は「疲労中で、しかもスタミナが30以上まで戻ったら、疲労を治す」という意味。
- `if (isExhausted) return exhaustedRecoveryRate;` は「疲労中なら、止まっていても歩いていても、回復は1秒に3だけ」という意味。

**③ まとまりの意味**：毎フレーム、移動方法に合わせてスタミナを減らすか回復させ、0になったら疲労状態にする。疲労中は `DecideMoveMode` が必ず Exhausted を返すので、ダッシュできず、忍び足と同じ速さになる。疲労は0で入り、30で抜ける（ヒステリシス）。

**④ つながり**
- Inspector の「スタミナ」欄（Stamina・Is Exhausted でテスト中に状態を見られる）
- `isExhausted` → `DecideMoveMode` → `currentMode = Exhausted` → `GetSpeed()` で忍び足の速さ
- Animator は変更なし：Exhausted は `lastAnimSprinting = false` なので Walk ステートのまま、MotionSpeed（0.8 ÷ 基準）でゆっくり再生される
- `Stamina`／`IsExhausted` は読むだけの窓口。将来、スタミナゲージ（UI）・呼吸音・足音（疲労中は歩きと同じ音量）が読む予定

**⑤ どういう仕様のときに使うか**
- **「使うと減る・休むと戻る」資源**：スタミナ、弾薬のオーバーヒート、魔力、酸素ゲージ。`減らす量 × Time.deltaTime`／`回復量 × Time.deltaTime` ＋ `Mathf.Clamp` の3点セット。
- **状態によって回復量を変える**：`switch` で状態ごとの数値を返す（`GetSpeed` と同じ「対応表」の型）。
- **0で入って30で抜ける（ヒステリシス）**：「一度なったら、しばらく続いてほしい状態」（疲労、気絶、武器のオーバーヒート、ゾンビの怯み）。
- **enum に状態を足すときは末尾に**：保存済みの値（番号）がずれないようにする。

**⑥ 起きやすいバグと調整場所**

| こんなバグ | 調整する場所 |
|---|---|
| 0になっても疲労状態にならない | 0かどうかを調べる `if` の比べる記号（実際に起きた。下の記録参照） |
| 歩きの回復が速すぎる（忍び足と同じになる） | `GetRecoveryRate` の `case` の書き方（実際に起きた。下の記録参照） |
| スタミナがマイナスや100超えになる | `Mathf.Clamp` が抜けている |
| 疲労と回復が一瞬で切り替わり続ける（チカチカ） | 抜ける境目（`exhaustedRecoveryThreshold`）が 0 に近すぎる |
| 疲労中でも走れる | `DecideMoveMode` の疲労の `if` がダッシュの判定より後ろにある |
| 走れる時間が短い／長い | `Sprint Stamina Cost`（20 → 15 で約6.7秒走れる） |
| 疲労から治るのが遅い | `Exhausted Recovery Rate` か `Exhausted Recovery Threshold` |
| 疲労が治った瞬間、ボタンを押しっぱなしだとすぐ走り出す | 仕様どおり（ホールド中なら走る）。押し直しを必要にしたい場合は、疲労が治った後にダッシュボタンを一度離したかを覚えるフラグを足す |

---

## バグ修正の記録

### 疲労状態にならない／歩きの回復が速い（2026-10-16）
- **症状①**：スタミナが0になっても Is Exhausted にチェックが入らない
- **原因①**：`if(stamina <- 0f)` と書かれていた。C# では `stamina < -0f`（スタミナが -0 より小さい）と読まれ、すぐ上の `Mathf.Clamp` で 0 未満にならないので、一度も true にならなかった。エラーは出ない
- **修正①**：`if(stamina <= 0f)`
- **症状②**（テストでは気づきにくい）：歩きでも忍び足と同じ速さ（毎秒10）で回復していた
- **原因②**：`case MoveMode Sneak:`（`.` が抜けている）。C# では「MoveMode 型の値なら何でも当てはまり、それを Sneak という名前の変数に入れる」という別の書き方として読まれ、Idle 以外すべてがここに入っていた。エラーは出ない
- **修正②**：`case MoveMode.Sneak:`
- **教訓**：「文法として正しいけれど意味が変わる」1文字のミスはエラーが出ない。テストでは Inspector の値（Stamina）が仕様どおりの速さで変わるかまで見る

### Play中にスクリプトを保存すると NullReferenceException（2026-10-16）
- **症状**：`OnEnable` の `inputActions.Player.Enable();` で `NullReferenceException`
- **原因**：Play中にスクリプトを保存すると Unity がその場で再コンパイルし、`OnEnable` は呼び直されるが `Awake` は呼ばれない。`inputActions`（Inspectorに出ない変数）が空のまま `OnEnable` が動いた
- **対処**：Play を止めてから保存する（普通に Play を始めれば出ない。コードの問題ではない）


### 地面の上で0.08m浮く問題（2026-09-22）
- **症状**：Play して動かすと、Position Y が常に 0.08m 浮く
- **最初に疑った原因（誤り）**：Apply Root Motion がオン → 外しても直らなかった
- **本当の原因**：`Move()` に横の移動しか渡しておらず、下向きの力を与えていなかった。CharacterController は重力・接地維持を自動でやらない
- **修正**：セット⑨の重力・接地維持を追加し、横と縦を1回の `Move()` にまとめた

### 大量のコンパイルエラー（2026-10-12）
- **原因**：`private bool sprintToggled;` が2回宣言されていた（同じ名前の変数は1つのクラスに1つだけ）
- **修正**：片方を削除。あわせて、コメントアウトで残っていた古いコードも削除した（古いコードが残ると、今のコードがどうなっているか分からなくなる）

---

## 汎用パターンまとめ（覚えておく型）

| 型 | 書き方 | 使う仕様 |
|---|---|---|
| 部品の取得 | 宣言 → `Awake` で `GetComponent` | 同じオブジェクトの部品を何度も使う |
| fps に関係なく動かす | `方向 × 速さ × Time.deltaTime` | 動くもの全部 |
| 優先順位で状態を決める | 上から `if … return` | 移動方法、ゾンビAI、行動できるかの判定 |
| トグル | `if (WasPressedThisFrame()) x = !x;` | しゃがみ、ライト、トグルダッシュ |
| チラつき防止（ヒステリシス） | 入る境目と抜ける境目をずらす | 倒し具合、スタミナ、感知範囲 |
| 途切れ防止（バッファ） | タイマーで少しの間だけ直前の値を保つ | キー持ち替え、段差、見失い |
| 足の速さ合わせ | `再生倍率 = 移動速度 ÷ 基準の速さ` → ステートの Speed 倍率 | 速さの違う移動に同じアニメを使う |
| 減る・戻るゲージ | `±量 × Time.deltaTime` → `Mathf.Clamp(値, 0, 最大)` | スタミナ、魔力、酸素、オーバーヒート |

---

## 今後の拡張メモ（未実装・仕様書との対応）

- **足の滑り**：X Bot の歩きクリップは方向ごとに歩幅・リズムがバラバラ（右の横歩きだけ小刻み、斜めだけ大股）。Project Recon では Blend Tree の子クリップごとの Time Scale（後ろ 0.9081／左 0.7187／右 1.3196／斜め 0.5167、走りの後ろ 1.753）で方向をそろえていた。2026-10-15 に試したが、基準の速さの絶対値が決めきれず、元の値に戻した。**8方向がそろったロコモーション素材に差し替えるときに、まとめて調整する**（そのときは MCP で Play 中に実測する）。
- **スタミナ・疲労状態**：実装済み（2026-10-16、セット⑫）。未実装：呼吸音（ダッシュ開始2〜3秒後に発生、終了後5秒かつスタミナ70で解除）→ 音・感知を作るときに `Stamina`／`IsExhausted`／`CurrentMode` を読んで作る。スタミナゲージ（画面右下）→ UI を作るときに `Stamina` を読む。
- **しゃがみ**：`isCrouching` の切り替えまで実装済み。高さ変更（CharacterController の Height／Center）、天井チェック、Animator（`IsCrouching` を使う遷移）は未実装。
- **ダッシュ中の制限**：GameSpec「ダッシュ中はナイフのみ使用可能」。武器の実装時に `CurrentMode == MoveMode.Sprint` を読んで判定する。
- **足音・ゾンビの感知**：`CurrentMode` を読んで音の範囲を決める（`PlayerDetection` 相当を作るとき）。
- **ボタン配置**：パターン切り替え（バインディンググループ）は設定画面を作るときに追加。今は Sprint に L3 と△の両方が割り当たっている。△は Interact にも入っているので、インタラクト実装時に○へ変更する。
