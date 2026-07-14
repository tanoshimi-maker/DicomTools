# DICOM Classifier v2.0

DICOM 文件分类与管理工具，支持排序（Sort）、分类（Classify）和统计（Statistics）三种模式。

## 功能预览

| 模式 | 说明 |
|------|------|
| **Sort** | 对文件夹内的 DICOM 文件按采集时间排序，支持原地重命名/修改时间戳或导出到目标文件夹 |
| **Classify** | 递归扫描目录树，按患者、年月等维度将 DICOM 文件归类到结构化文件夹 |
| **Statistics** | 对 DICOM 数据集进行多维度统计分析，支持导出 Excel / JSON / CSV |

---

## 使用说明

### Sort（排序）模式

1. 点击 **Browse** 选择源文件夹
2. 设置排序选项：
   - **Base Time**: 设置基准时间，排序后的文件时间戳从该时间开始递增（YYYY/MM/DD HH:mm 下拉选择）
   - **In-place（复选框）**: 
     - ✅ 勾选：在源文件夹内直接重命名 + 修改时间戳（执行前会弹出确认提示）
     - ❌ 不勾选：复制到 Target Folder（需填写目标路径，否则执行时会弹窗提醒）
3. 点击 **Scan & Preview** 预览操作列表
4. 确认无误后点击 **Execute** 执行

> ⚠ **注意**：In-place 操作会**直接修改源文件夹内文件的名称和时间戳**，不可撤销。建议先备份或在副本上测试。

### Classify（分类）模式

1. 选择源文件夹（递归扫描所有子目录）
2. 设置分类规则：
   - **Year-Month / Direct Patient**: 选择顶层是 `年/月/患者ID` 结构还是 `患者ID` 结构
   - **命名规则**: 患者文件夹的命名格式（PatientID_Name / Name_ID / ID only / Name only / 匿名序列 / 日期序列）
   - **Sort within Patient**: 是否在患者文件夹内按时间排序文件
3. 分类结果**始终输出到 Target Folder**（若未指定则输出到桌面 `DicomClassifier_Classified/`）
4. 点击 **Scan & Preview** 预览，确认后 **Execute**

### Statistics（统计）模式

1. 选择源文件夹（递归扫描所有子目录）
2. 点击 **Start Statistics** 开始扫描与计算
3. 左侧勾选需要查看的统计项（支持全选/取消全选）
4. 统计内容包括：
   - 患者总数 / 检查总数 / 序列总数 / 文件总数
   - 性别分布（男 / 女 / 未知）
   - 平均年龄
   - RTPLAN / RTDOSE / RTSTRUCT 实例数
   - 文件总大小 / 剂量文件大小
   - Modality 分布
   - Manufacturer 分布
   - 平均检查数/患者、平均实例数/患者
   - 时间跨度（最早/最晚检查日期）
   - 缺失关键标签数
   - 匿名化率
5. 支持导出：
   - **Excel**（.xlsx，Clo sedXML 带样式）
   - **JSON**（.json，缩进格式化）
   - **CSV**（.csv，UTF-8 BOM）

---

## 界面操作

- **左侧导航栏**: 三个图标分别切换 Sort / Classify / Statistics 模式
- **标题栏按钮**: 最小化 / 最大化（□ ↔ ❐）/ 关闭（✕），悬停有闪烁边框反馈
- **拖拽支持**: 文件夹可以拖拽到源文件夹区域
- **日志面板**: 底部实时显示操作日志，可清空

---

## 注意事项

1. **In-place 操作不可逆**：勾选 In-place 后执行的操作会直接修改源文件夹，强烈建议先预览操作列表再执行
2. **Base Time 精度**：时间仅精确到分钟，秒数固定为 0
3. **分类模式**始终复制文件到目标文件夹，不会修改源文件
4. **统计模式**为只读操作，不修改任何文件
5. **设置持久化**：窗口状态、各模式配置自动保存到 `%LOCALAPPDATA%/DicomClassifier/settings.json`

---

## 系统要求

- Windows 10 / 11
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）

## 构建

```bash
dotnet restore
dotnet build -c Release(0008,0060)        Modality 
dotnet publish -c Release -o publish
```

### 依赖项

| 包 | 版本 | 用途 |
|----|------|------|
| fo-dicom | 5.1.3 | DICOM 文件解析 |
| ClosedXML | 0.105.0 | Excel 导出 |
| CommunityToolkit.Mvvm | 8.2.2 | MVVM 源生成器 |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | 依赖注入 |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.122 | XAML 行为 |

## 自定义图标

用 `.ico` 文件替换 `DicomClassifier/sources/favicon.ico` 即可更换应用图标（exe 图标 + 任务栏图标）。
