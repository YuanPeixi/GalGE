# GalEngine.Sample (WPF + XML galgame 引擎示例)

这是一个最小可运行的 WPF + XML galgame 引擎示例，使用 .NET 8（Windows）构建。

主要特性：
- 每一句话为一个 XML 节点（node），包含 bg、music、speaker、text、character、option、change、counterTrigger 等属性/子元素。
- 除 text 外，节点缺省属性会继承上一个节点（以节省 XML 冗余）。
- 支持选项跳转与计数器变更（affect/changes），支持计数器触发的跳转（counterTrigger）。
- 简易 UI：背景、角色图、对话框、选项按钮、下一句。

运行要求：
- Windows 环境
- .NET 8 SDK（支持 WPF 的桌面运行）

如何运行：
1. 将项目所有文件置于同一文件夹（按本示例的文件结构）。
2. 在项目根目录运行：
   dotnet build
   dotnet run --project GalEngine.Sample.csproj
3. 在输出目录下（或运行时目录）放置资源：
   - 将图片/音频放在 `Assets` 文件夹下，并在 story.xml 中使用相对路径，如 `Assets/bg_home.jpg`、`Assets/bgm.mp3`、`Assets/alice_happy.png`。

示例 story.xml 已包含于项目，会被复制到输出目录。你可以编辑 story.xml 编写自己的剧本。

XML 格式说明（快速）：
- <node id="n1" bg="Assets/bg_home.jpg" music="Assets/bgm.mp3" speaker="旁白">
    <text>这是第一句。</text>
    <character name="Alice" image="Assets/alice_smile.png" side="right" expression="smile" />
    <option text="选择A" goto="n2" affect="like:1" />
    <change counter="like" delta="1" />
    <counterTrigger counter="like" value="3" goto="n_good_end" />
  </node>

扩展建议：
- 支持淡入淡出、过渡动画、文本逐字显示、存档/读档。
- 支持更多复杂的条件触发（范围、布尔条件等）。
- 改为 MVVM 架构以便可测试性和更复杂 UI。