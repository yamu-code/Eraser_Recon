# Unity用語集（Eraser Recon）

- 授業で出てきた「Unity特有のもの」「C#の少し変わった書き方」を、読み方つきでまとめたもの。一般的な変数名などは載せない。
- 新しく教えた用語は、このファイルに追記していく。
- 最終更新：2026-10-15（`PlayerController.cs` に出てくるもの＋ダッシュの仕上げで出てきたもの）

---

## 1. スクリプトの土台（Unity特有）

| 書き方（読み方） | 意味 | このゲームでの使いどころ |
|---|---|---|
| `MonoBehaviour`（モノビヘイビア） | GameObjectに付けて動かすスクリプトの元になる型。これを継承するとUnityが`Awake`や`Update`を自動で呼んでくれる | `public class PlayerController : MonoBehaviour` |
| `[RequireComponent(typeof(X))]`（リクワイア・コンポーネント） | 「このスクリプトにはXが必ず必要」という印。付けるとXが自動で追加され、外せなくなる | CharacterController と Animator の付け忘れ防止 |
| `Awake()`（アウェイク） | オブジェクトが作られた直後に1回だけUnityが呼ぶ関数 | 部品（`GetComponent`）の取得、入力の準備 |
| `OnEnable()` / `OnDisable()`（オン・イネーブル／オン・ディセーブル） | オブジェクトが有効になった時／無効になった時にUnityが呼ぶ関数 | 入力（Input Actions）の受付開始・停止 |
| `Update()`（アップデート） | 毎フレーム（1秒に数十〜数百回）Unityが呼ぶ関数 | 入力を読む→移動方法を決める→動かす→アニメに伝える |
| `GetComponent<X>()`（ゲット・コンポーネント） | 同じGameObjectに付いているコンポーネントXを取ってくる | `controller = GetComponent<CharacterController>();` |
| `transform`（トランスフォーム） | そのGameObjectの位置・回転・大きさ | `transform.right`（キャラの右方向）、`transform.forward`（キャラの前方向） |

## 2. Inspector と見た目のための印（属性）

| 書き方（読み方） | 意味 | 使いどころ |
|---|---|---|
| `[SerializeField]`（シリアライズ・フィールド） | `private`でもInspectorに表示し、シーンに保存する印 | 速さなどの数値をInspectorで調整できるようにする |
| `[Header("...")]`（ヘッダー） | Inspectorに見出しを出す | 「移動速度」「ダッシュ」などの区切り |
| `[System.Serializable]`（システム・シリアライザブル） | 自分で作った入れ物（struct/class）をInspectorに表示・保存できるようにする印 | （一度使って今は削除済み） |

**覚えておくこと**：Inspectorに一度保存された値は、コードの初期値より優先される。コードの数字を変えても反映されないときは、コンポーネントの **⋮ → Reset** か、Inspectorで直接入力する。

## 3. 時間・計算（Unity特有）

| 書き方（読み方） | 意味 | 使いどころ |
|---|---|---|
| `Time.deltaTime`（タイム・デルタタイム） | 前のフレームから今のフレームまでにかかった秒数（60fpsなら約0.0167） | 「1秒あたりの量 × Time.deltaTime」で、fpsが違っても同じ速さになる |
| `Vector2` / `Vector3`（ベクター・ツー／スリー） | 2つ（x,y）／3つ（x,y,z）の数のセット。向きや位置を表す | スティックの入力（Vector2）、移動の向き（Vector3） |
| `.normalized`（ノーマライズド） | 向きはそのままで長さを1にしたもの | 倒し具合で速さが変わらないように方向だけ取り出す |
| `.magnitude`（マグニチュード） | 長さ | スティックの倒し具合（0〜1） |
| `.sqrMagnitude`（スクエア・マグニチュード） | 長さの2乗。計算が速いので「0かどうか」の判定に使う | 何か入力しているか |
| `Mathf`（マスエフ） | 計算用の道具箱 | 下の各関数 |
| `Mathf.Lerp(a, b, t)`（ラープ） | aとbの間を割合tで選ぶ（t=0でa、t=1でb） | 後ろ向きの度合いで速さを1倍〜0.7倍にする |
| `Mathf.Clamp01(x)`（クランプ・ゼロワン） | xを0〜1の範囲に収める | 後ろ向きの度合いを0〜1にする |
| `Mathf.Clamp(x, 最小, 最大)`（クランプ） | xを最小〜最大の範囲に収める | スタミナを0〜100に収める（HP・弾数・音量にも使う） |

## 4. 移動（Unity特有）

| 書き方（読み方） | 意味 | 使いどころ |
|---|---|---|
| `CharacterController`（キャラクター・コントローラー） | 壁や床にぶつかりながら動けるキャラ用の当たり判定コンポーネント | プレイヤーの移動 |
| `controller.Move(量)`（ムーブ） | 指定した量だけ動かす（壁にはぶつかって止まる） | `controller.Move(velocity * Time.deltaTime);` |
| `controller.isGrounded`（イズ・グラウンデッド） | 地面に接しているか | 重力の処理 |

## 5. 入力（Input System）

| 書き方（読み方） | 意味 | 使いどころ |
|---|---|---|
| `InputSystem_Actions`（インプットシステム・アクションズ） | Input Actionsの設定から自動で作られるクラス。保存すると作り直される | `inputActions.Player.Move` など |
| `.ReadValue<Vector2>()`（リード・バリュー） | 今の入力値を読む | スティック／WASDの方向 |
| `.IsPressed()`（イズ・プレスト） | 押している間ずっとtrue | ダッシュ（ホールド）、忍び足（Ctrl） |
| `.WasPressedThisFrame()`（ワズ・プレスト・ディス・フレーム） | 押した瞬間の1フレームだけtrue | しゃがみの切り替え、ダッシュ（トグル） |
| `.WasReleasedThisFrame()`（ワズ・リリースト・ディス・フレーム） | 離した瞬間の1フレームだけtrue | 弓を離して撃つ、溜め攻撃の発動（予定） |
| バインディング（Binding） | アクションにボタン・キーを割り当てたもの。1つのアクションに複数付けられる | Sprint に Shift・L3・△ |

**覚えておくこと**：同じボタンが2つのアクションに割り当たっていると、両方が同時に動く（今は△が Sprint と Interact の両方に入っている）。

## 6. Animator とつなぐ（Unity特有）

| 書き方（読み方） | 意味 | 使いどころ |
|---|---|---|
| `Animator`（アニメーター） | アニメーションを再生・切り替えるコンポーネント | プレイヤーの体の動き |
| `Animator.StringToHash("名前")`（ストリング・トゥ・ハッシュ） | パラメータ名を番号に変換する。毎回文字で探すより速く、打ち間違いも1か所に集まる | `SpeedParam` などを最初に1回作る |
| `animator.SetFloat(番号, 値)`（セット・フロート） | 小数のパラメータに値を書き込む | Speed、MoveX、MoveY、MotionSpeed |
| `animator.SetBool(番号, 値)`（セット・ブール） | true/falseのパラメータに値を書き込む | IsSprinting、IsCrouching |
| パラメータの種類 | Bool＝続く状態／Trigger＝一瞬の出来事／Float＝連続した量／Int＝番号 | しゃがみ中はBool、着地はTrigger、速さはFloat |
| `animator.SetTrigger(番号)`（セット・トリガー） | 一瞬の出来事を伝える（1回使われると自動で戻る） | 着地、被ダメージ、攻撃（予定） |
| Time Scale（タイム・スケール、Blend Treeの時計マーク） | Blend Tree の中で、クリップ1本だけの再生倍率を変える | 方向ごとに歩幅が違う素材の足の速さをそろえる（素材差し替え時に使う予定） |
| Mirror（ミラー、Blend Treeの人のマーク） | 人型（Humanoid）のクリップを左右反転する | 左右どちらかのクリップの出来が悪いとき、良い方を反転して使う |

## 7. C#の少し変わった書き方

| 書き方（読み方） | 意味 | 使いどころ |
|---|---|---|
| `enum`（イーナム） | 決まった選択肢に名前を付けた型。打ち間違いがエラーで分かる | `MoveMode { Idle, Sneak, Walk, Sprint }`、`SprintStyle { Hold, Toggle }` |
| `MoveMode`（ムーブモード） | 自分で作ったenumの型。「移動方法」を表す | `currentMode`（今の移動方法） |
| `public 型 名前 => 変数;`（アロー） | 読むだけの窓口（プロパティ）。ほかのスクリプトは読めるが書き換えられない | `public MoveMode CurrentMode => currentMode;` |
| `static readonly`（スタティック・リードオンリー） | 全員で1つだけ共有し、最初に決めたら書き換えない値 | Animatorのパラメータ番号 |
| `条件 ? A : B`（三項演算子／さんこうえんざんし） | 条件がtrueならA、falseならBを選ぶ | 忍び足の境目、足合わせの基準の選択 |
| `!変数`（ノット） | true⇔falseを反対にする | `isCrouching = !isCrouching;`（トグル） |
| `*=` `+=` `-=` | 今の値に掛ける／足す／引くして上書き | `moveSpeed *= ...`、`holdTimer -= Time.deltaTime` |
| `switch`（スイッチ）／`case`（ケース）／`default`（デフォルト） | 値によって処理を分ける。caseは「この値のとき」、defaultは「どれにも当てはまらないとき」 | `GetSpeed()`で移動方法ごとの速さを返す |
| `return`（リターン） | 答えを返して、その関数をそこで終わらせる | `DecideMoveMode()`で上から順に調べて、当てはまったら終わる |
| `0.8f` の `f` | その数が小数（float）だという印 | すべての小数の数値 |
| `<=` `>=`（以下・以上）／`&&`（アンド） | 比べる記号／「両方trueのときだけtrue」 | `stamina <= 0f`、`isExhausted && stamina >= 30f` |

**覚えておくこと（2026-10-16に実際に起きたバグ）**：`<=` を `<-` と打つと「〜より小さい、マイナス〜」と読まれてエラーが出ない。`case MoveMode.Sneak:` の `.` を抜かして `case MoveMode Sneak:` と書くと「何でも当てはまる」別の意味になり、これもエラーが出ない。1文字のミスでもエラーにならないことがあるので、テストでは値が仕様どおりに変わるかまで見る。

## 8. よく使う「型」（パターン）

- **トグル**：`if (ボタン.WasPressedThisFrame()) 状態 = !状態;`
- **fpsに関係なく動かす**：`方向 × 速さ × Time.deltaTime`
- **足の速さ合わせ**：`再生倍率 = 実際の移動速度 ÷ 基準の速さ` → Animatorの MotionSpeed → ステートの Speed Multiplier
- **チラつき防止（ヒステリシス）**：入る境目と抜ける境目をずらす（忍び足 0.45／0.55）
- **方向保持バッファ**：入力が一瞬ゼロでも、少しの間は直前の値をアニメに送る
