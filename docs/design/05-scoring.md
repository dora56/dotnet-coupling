# スコアリング設計 (Strength / Distance / Volatility / Balance / Grade)

## 12. Integration Strength 設計

Integration Strength は「依存元が依存先についてどれだけ具体的に知っているか」を表す。

| Level | Score | .NET / C# 例 | 意味 |
|---|---:|---|---|
| Contract | `0.25` | `IUserRepository`, interface constraint | 契約への依存。最も弱い |
| Model | `0.50` | DTO, record, entity 型を引数や戻り値で使う | データ構造への依存 |
| Functional | `0.75` | concrete class のメソッド呼び出し、`new`、static call | 具体的な振る舞いへの依存 |
| Intrusive | `1.00` | public mutable field 直接操作、遠方の internal 実装依存、reflection 多用 | 実装詳細への強い依存 |

### 12.1 Contract

以下を Contract とする。

- interface 型への依存
- interface の generic constraint
- interface 経由の constructor injection
- abstract class への依存。ただしこれは設定で Contract / Functional を切り替え可能にする

例:

```csharp
public sealed class CreateUserHandler
{
    private readonly IUserRepository repository;

    public CreateUserHandler(IUserRepository repository)
    {
        this.repository = repository;
    }
}
```

### 12.2 Model

以下を Model とする。

- record / DTO / enum / value object の型参照
- method parameter / return type としての型参照
- property / field の型としての参照

例:

```csharp
public Task<UserDto> GetUserAsync(UserId id)
```

### 12.3 Functional

以下を Functional とする。

- concrete class の `new`
- concrete class の method call
- static method call
- extension method call
- concrete class への constructor injection

例:

```csharp
var client = new StripeClient(apiKey);
await client.CreateCustomerAsync(request);
```

### 12.4 Intrusive

以下を Intrusive とする。

- 他型の public mutable field への直接アクセス
- reflection による private / internal member 操作
- `dynamic` による境界不明な呼び出し
- 遠い namespace / project から `Internal` 相当の実装詳細へ依存している疑い
- service locator 経由で具象型を取得している疑い

例:

```csharp
userRepository.Connection.ConnectionString = "...";
```

C# は private member に通常アクセスできないため、Rust の private access と同じ意味の intrusive は少ない。代わりに、**公開されているが実装詳細に見えるものへの依存**を intrusive として扱う。

---

## 13. Distance 設計

Distance は「依存元と依存先がどれだけ離れているか」を表す。

MVP では **SameType を Distance として扱わない**。同一型内の self-contained な参照は結合リスクの判定対象として薄く、`0.00` を入れると score が不自然に良くなりやすい。したがって、同一型内の依存は観測から除外する。最小距離は `SameNamespace = 0.25` とする。

| Level | Score | .NET / C# 例 |
|---|---:|---|
| SameNamespace | `0.25` | 同一 namespace 内の別型 |
| DifferentNamespace | `0.50` | 同一 project 内の別 namespace |
| DifferentProject | `0.75` | 同一 solution 内の別 project |
| ExternalPackage | `1.00` | NuGet package / 外部 assembly |

MVP の syntax-only では project 境界が曖昧なため、主に namespace と file path で近似する。

v0.2 の semantic mode では `.sln` / `.csproj` を読み込み、project / assembly 境界を正確に判定する。

### 13.1 SameType を除外する理由

`strength = 1.0`、`distance = 0.0` のような組み合わせは、Balance Score 上は「強い結合が近くにある」ため良いスコアになる。しかし C# の同一型内の field / method access は通常の実装詳細であり、architecture coupling の問題として報告してもノイズになりやすい。

そのため MVP では以下のように扱う。

- `source == target`: 除外
- 同一 nested type 内の参照: 除外
- 同一 namespace の別型: `SameNamespace = 0.25`
- 同一ファイルに複数型がある場合: 型が違えば `SameNamespace = 0.25` 以上

### 13.2 syntax-only での Distance 推定ルール

MVP の syntax-only では `.sln` / `.csproj` を正式には読まないため、project 境界は namespace と file path から近似する。

判定順序:

| 順序 | 条件 | Distance |
|---:|---|---|
| 1 | `source` と `target` が同一型 | 除外 |
| 2 | `source.Namespace == target.Namespace` | `SameNamespace = 0.25` |
| 3 | namespace が異なり、共通 prefix が2 segment 以上 | `DifferentNamespace = 0.50` |
| 4 | Component Index に存在する内部型だが、共通 prefix が2 segment 未満 | `DifferentProject = 0.75` の近似 |
| 5 | Component Index に存在しない namespace / 型 | `ExternalPackage = 1.00` |

共通 prefix の例:

```text
MyApp.Api.Controllers -> MyApp.Application.Users
共通 prefix: MyApp
=> 1 segment のみなので DifferentProject 近似

MyCompany.MyApp.Api -> MyCompany.MyApp.Application
共通 prefix: MyCompany.MyApp
=> 2 segment なので DifferentNamespace
```

このヒューリスティックは保守的に倒す。つまり、内部型であることは分かるが近さを確信できない場合は、近いと仮定せず `DifferentProject` へ寄せる。v0.2 の semantic mode では `.sln` / `.csproj` / assembly symbol から project 境界を正確に判定する。

### 13.3 外部依存のスコア計算

`ExternalPackage = 1.00` は距離としては遠い。ただし BCL / framework 型や安定した NuGet package をすべて issue にするとノイズが爆発するため、外部依存は health grade の主計算からは原則除外する。別レポートまたは `ScatteredExternalCoupling` で扱う。

---

## 14. Volatility 設計

Volatility は「依存先がどれくらい変更されやすいか」を表す。

### 14.1 Git 履歴による分類

`git log` から対象期間内の `.cs` ファイル変更回数を集計する。

```bash
git log --pretty=format: --name-only --diff-filter=AMRC --since="6 months ago" -- "*.cs"
```

| 変更回数 | Level | Score |
|---:|---|---:|
| `0..2` | Low | `0.00` |
| `3..10` | Medium | `0.50` |
| `11+` | High | `1.00` |

### 14.2 ファイル単位からコンポーネント単位への集約

型が定義されているファイルの変更回数を、その型の volatility として使う。

partial class の場合は、複数ファイルの合計または最大値を採用する。MVP では最大値を採用する。

### 14.3 Temporal co-change / Hidden Coupling

Hidden Coupling を MVP に含める場合、commit 単位の変更ファイルリストが必要になる。単純な `git log --name-only` だけでは commit 境界が曖昧になるため、commit marker を明示して取得する。

```bash
git log --no-merges --pretty=format:"COMMIT:%H" --name-only --diff-filter=AMRC --since="6 months ago" -- "*.cs"
```

集計ロジック:

1. `COMMIT:<sha>` 行で commit を区切る
2. commit 内の `.cs` ファイル一覧を正規化する
3. generated code / excluded path は除外する
4. commit 内のファイル数が `maxTemporalFilesPerCommit` を超える場合、その commit は temporal coupling 集計から除外する
5. 残ったファイル集合から unordered pair `(fileA, fileB)` を生成し、co-change count を加算する
6. `coChangeCount >= minTemporalCoupling` の pair を Hidden Coupling 候補にする
7. 2ファイル間に明示的な code dependency がある場合は Hidden Coupling ではなく、既存の coupling / volatility issue として扱う

計算量対策:

- commit 内ファイル数を `K` とすると pair 生成は `O(K^2)`
- 既定では `maxTemporalFilesPerCommit = 50` とし、大規模 rename / formatting / generated update commit を避ける
- pair key は正規化済み相対 path の昇順 tuple にする
- `minTemporalCoupling` の既定値は `3` とする

MVP ではファイルペア単位で Hidden Coupling を検出する。v0.2 以降で、ファイルペアを type / namespace に集約して表示品質を上げる。

### 14.4 設定による上書き

Git の変更頻度だけでは、ビジネス上の重要な揮発性を判断できない。設定ファイルで上書きできるようにする。

```toml
[volatility]
high = ["src/MyApp.Domain/Core/**"]
low = ["src/MyApp.Infrastructure/Shared/**"]
```

### 14.5 DDD subdomain による補正

```toml
[subdomains]
core = ["src/MyApp.Domain/**"]
supporting = ["src/MyApp.Application/**"]
generic = ["src/MyApp.Infrastructure/**"]
```

基本方針:

- core subdomain は変化してよい
- supporting / generic が頻繁に変化している場合は設計摩擦の疑いがある
- supporting / generic の High volatility は `Accidental Volatility` として issue 化する

---

## 15. Balance Score 設計

### 15.1 単一結合スコア

```text
strength   = clamp(IntegrationStrength value, 0.0, 1.0)
distance   = clamp(Distance value, 0.0, 1.0)
volatility = clamp(Volatility value, 0.0, 1.0)

alignment = clamp(1.0 - abs(strength - (1.0 - distance)), 0.0, 1.0)
volatilityPenalty = clamp(volatility * strength, 0.0, 1.0)
volatilityImpact = clamp(1.0 - volatilityPenalty, 0.0, 1.0)
score = clamp(alignment * volatilityImpact, 0.0, 1.0)
```

実装では `Math.Clamp` を必ず使う。enum 値だけを使う限り範囲外にはなりにくいが、設定ファイルで閾値や重みを導入した時に壊れないようにする。防御的プログラミング、地味だけど財布と同じで落としてからでは遅い。

### 15.2 解釈

| Score | Interpretation |
|---:|---|
| `0.80..1.00` | Balanced |
| `0.60..0.79` | Acceptable |
| `0.40..0.59` | Needs Review |
| `0.20..0.39` | Needs Refactoring |
| `0.00..0.19` | Critical |

### 15.3 なぜこの式にするか

良い結合の例:

- 強い結合だが近い場所にある
- 遠い場所にあるが interface / contract への弱い依存である
- 変更頻度の低い安定した対象に依存している

悪い結合の例:

- 遠い project / namespace の concrete class に強く依存している
- 変更頻度の高い型へ static call や `new` で直接依存している
- Infrastructure から Domain の内部構造を直接いじっている

### 15.4 health grade との関係

単一結合の `score` は issue 検出と詳細表示に使う。プロジェクト全体の Grade は平均点ではなく、§16 の issue density で決める。これは、少数の Critical issue が大量の正常な結合に埋もれることを避けるためである。

---

## 16. Project Score と Grade

### 16.1 基本方針

`dotnet-coupling` の Project Grade は、単純な平均スコアではなく **issue density** を使う。これは `cargo-coupling` v0.3.3 の考え方に合わせる。

平均スコア方式には致命的な弱点がある。たとえば 1000 件の健全な結合があるプロジェクトで 5 件の Critical issue があっても、平均点では見えにくい。設計リスクは「平均すると大丈夫」では済まない。Critical は Critical。火事は家全体の平均温度では測れない。

### 16.2 計算対象

Health Grade の主計算には **internal coupling** を使う。

- `SameNamespace`
- `DifferentNamespace`
- `DifferentProject`

`ExternalPackage` は health grade の density 分母から除外する。外部依存は開発者が直接変更できないためである。ただし、外部 package の直接使用が多くの内部コンポーネントに散らばる場合は `ScatteredExternalCoupling` として issue 化する。

`ScatteredExternalCoupling` は「外部 package そのもの」ではなく、内部コード側の使い方に対する issue である。そのため `issues` 配列には含め、severity に応じて `--check` の失敗判定にも影響させる。ただし、health grade の density 分母である `internalCouplings` には外部 coupling を含めない。

### 16.3 集計値

```text
internalCouplings = max(number_of_internal_couplings, 1)
critical = count(Critical issues)
high     = count(High issues)
medium   = count(Medium issues)

highDensity = high / internalCouplings
mediumDensity = medium / internalCouplings
```

Low severity は strict mode では既定で除外されるため、Grade の主計算にも入れない。

### 16.4 Grade 判定

判定は **必ず以下の順序** で適用する。B は fallback の1回だけにする。S / A に到達する前に B で確定する実装を書かないように、条件付き B 行は置かない。

| 順序 | Grade | Display | 条件 | 意味 |
|---:|---|---|---|---|
| 1 | F | Immediate action required | `critical > 3` | Critical issue が多すぎる。安全な変更を阻害している |
| 2 | D | Attention needed | `critical > 0` または `highDensity > 0.05` | 重大な保守性リスクがある |
| 3 | C | Room for improvement | `high > 0` または `mediumDensity > 0.25` | 構造的な改善計画が必要 |
| 4 | S | Over-optimized warning | `mediumDensity <= 0.05` かつ `internalCouplings >= 20` | 過剰最適化の疑い。最高評価ではなく警告 |
| 5 | A | Well-balanced | `mediumDensity <= 0.10` かつ `internalCouplings >= 10` | 理想的なバランス |
| 6 | B | Healthy | 上記以外 | 管理可能、または小規模 / データ不足のため B に倒す |

S と A は C を通過した後に評価されるため、この時点で `high == 0` は暗黙に成立する。したがって S / A の条件には `high == 0` を重複して書かない。

実装例:

```csharp
if (critical > 3) return Grade.F;
if (critical > 0 || highDensity > 0.05) return Grade.D;
if (high > 0 || mediumDensity > 0.25) return Grade.C;
if (mediumDensity <= 0.05 && internalCouplings >= 20) return Grade.S;
if (mediumDensity <= 0.10 && internalCouplings >= 10) return Grade.A;
return Grade.B;
```

### 16.5 S grade の解釈

S は "Excellent" ではない。`cargo-coupling` と同じく、**現実のコードに issue が少なすぎるため、過剰に抽象化・過剰にリファクタしているかもしれない** という警告として扱う。

表示例:

```text
Grade: S (Over-optimized warning)
No serious issues were found across enough internal couplings.
This may indicate healthy code, but it may also mean you are over-abstracting or optimizing coupling too aggressively.
Ship it before the architecture eats the product.
```

### 16.6 Project Score

`averageScore` は参考値として出すが、Grade 判定には使わない。

```text
averageScore = average(balanceScores of internal couplings)
```

JSON では以下のように分ける。

```json
{
  "grade": {
    "letter": "B",
    "display": "Healthy",
    "basis": "issue-density"
  },
  "scores": {
    "averageBalanceScore": 0.84
  }
}
```

### 16.7 `--check` における Grade 順序

`--check --min-grade <grade>` では以下の順序を使う。

```text
S >= A > B > C > D > F
```

S は A 以上として扱い、`--min-grade A` でも pass する。ただし S の場合は常に warning を stderr に出す。

```text
Warning: Grade S is an over-optimized warning, not a trophy. Review whether abstractions and thresholds are realistic.
```

厳密に S を失敗扱いにしたい場合は、v0.2 以降で `--fail-on-over-optimized` を追加する。
