# RanaImageTool

RanaImageTool 是一个基于 .NET 10 的命令行工具，用于处理图片文件。它支持以下功能：

- **扫描目录并计数图片文件**
- **将 WebP 文件转换为 PNG 文件**
- **将 JPEG 文件转换为 PNG 文件**
- **为 JPEG/PNG 文件设置 PPI**

## 功能概览

### 1. 扫描图片文件
通过 `scan` 命令扫描指定目录，统计 JPEG、PNG 和 WebP 文件的数量。

### 2. WebP 转换为 PNG
通过 `webp` 命令将 WebP 文件转换为 PNG 文件，并删除原始文件。

### 3. JPEG 转换为 PNG
通过 `trans` 命令将 JPEG 文件转换为 PNG 文件，并删除原始文件。

### 4. 设置 PPI
通过 `setppi` 命令为 JPEG 和 PNG 文件设置 PPI（像素每英寸）。支持两种模式：
- **固定值模式**：将所有文件的 PPI 设置为指定的固定值。在未指定PPI时，默认为 144。
- **线性模式**：根据图像的宽度计算 PPI。关系满足 $PPI = \mathrm{trunc} \left( Width / 10 \right)$ ，其中 $\mathrm{trunc}$ 是截断函数，表示向零取整。在未指定具体模式时，默认为此模式。
- 在设置 PPI 模式下，文件的编码格式一般不会改变，除非：
  - 图片编码格式与扩展名所暗示的不同时，该图片将被转换格式。
  - 非 JPEG 编码的图片将会被转换为 PNG 编码，扩展名亦将被纠正为 .png。
  - JPEG 编码的图片扩展名将被规范为 .jpg。

> [!note]
> 
> **平台差异功能**
> 
> 在 Windows 平台上，本程序的进度条组件将可以跟随系统主题色而改变自身的颜色。

## 安装

RanaImageTool 是一个 .NET 全局工具。您可以通过以下步骤安装：

1. 确保已安装 .NET 10 SDK。
2. 通过 `git clone`或其他方法将项目文件下载到本地。
3. 在项目目录下运行以下命令：

```powershell
# 打包工具
dotnet pack

# 安装工具
dotnet tool install RanaImageTool --global --add-source ./nupkg --version *.*.*
# 建议在安装时指定版本号（从 csproj 文件查询，或使用 /nupkg 文件夹中的任意可用包）。指定具体的版本号有助于确保所有文件均被更新。
```

## 使用方法

### 基本命令

```powershell
RanaImageTool [command] [options]
```

### 可用命令

| 命令      | 描述                                          |
| --------- | --------------------------------------------- |
| `webp`    | 将 WebP 文件转换为 PNG 文件，并删除原始文件。 |
| `setppi`  | 为 JPEG/PNG 文件设置 PPI。                    |
| `convert` | 将 JPEG 文件转换为 PNG 文件，并删除原始文件。 |
| `scan`    | 扫描目录并计数图片文件。                      |

### 参数

- 公共参数
  - `-p|--path <PATH>` : 指定要处理的目录路径。当未传入此参数时，默认为当前工作目录。
- `setppi` 命令特有参数
  - `--val [VALUE]` : 指定固定的 PPI 值。不传入具体值时，默认值为 144。
  - `--linear` : 启用线性模式，根据图像宽度计算 PPI。不传入任何参数时，将启用此模式。

### 示例

#### 扫描图片文件

```powershell
RanaImageTool scan -p "C:\Images"
```

#### 将 WebP 转换为 PNG

```powershell
RanaImageTool webp -p "C:\Images"
```

#### 将 JPEG 转换为 PNG

```powershell
RanaImageTool convert -p "C:\Images"
```

#### 设置 PPI

```powershell
# 固定值模式，设置 PPI 为 300
RanaImageTool setppi -p "C:\Images" --val 300

# 固定值模式，缺省设置为 144
RanaImageTool setppi -p "C:\Images" --val

# 线性模式
RanaImageTool setppi -p "C:\Images" --linear

# 不指定任何模式，缺省使用线性模式
RanaImageTool setppi -p "C:\Images"
```

## 开发

### 依赖项

- [ExifLibNet](https://github.com/oozcitak/exiflibrary): 用于读取和修改 JPEG 图像的 PPI 数据。
- [Microsoft.IO.RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream): 用于配置高性能的池化内存流。
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp): 当图片需要重编码时，用于图片重编码和 PPI 修改。
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) 与 Spectre.Console.Cli: 用于控制台程序基本框架、参数输入解析和终端输出渲染。
- [System.IO.Hashing](https://learn.microsoft.com/en-us/dotnet/api/system.io.hashing?view=net-10.0-pp): 用于在 PNG 图片元数据编辑方法中计算 CRC32 校验和。

> [!Warning]
> 
> 程序的内存占用与逻辑处理器数量有关，因为并行处理的线程数量取决于逻辑处理器的个数。
> 
> 目前的程序设定，图片格式为 JPEG 或 PNG 时，将通过直接编辑图片二进制文件流实现 PPI 编辑，而不触发重新编码；对其他格式的图片，将尝试在重新编码为 PNG 的同时编辑其元数据。
> 
> 若您的设备有较多处理器，且您需要在设置 PPI 时大量选用重编码路径（即，待处理的图片编码既不是 JPEG 也不是 PNG），则可能导致内存占用大幅增加。
> 
> 这是因为图片重编码需要大量额外的内存存储中间数据。

### 版本历史

| 版本   | 发布日期 | 更改日志                                                                                                                 |
| ------ | -------- | ------------------------------------------------------------------------------------------------------------------------ |
| v4.3.4 | 26-02-28 | 优化终端输出信息。                                                                                                       |
| v4.3.3 | 26-02-28 | 优化 TUI 组件样式。                                                                                                      |
| v4.3.2 | 26-02-23 | 继续优化 PNG 元数据编辑方法。                                                                                            |
| v4.3.1 | 26-02-14 | 优化 PDF 元数据编辑方法。                                                                                                |
| v4.3.0 | 26-02-13 | 手动编写用于编辑 PNG 文件 PPI 的方法，直接编辑 PNG 编码的二进制流，避免重编码图片造成的内存占用                          |
| v4.2.0 | 26-02-11 | 手动添加 RecyclableMemoryStream 配置，优化内存使用；重构后端方法，减少内存浪费；使用服务器GC和后台GC，限制内存无序膨胀。 |
| v4.1.0 | 26-02-09 | 继续重构并行方法，实现更高效的资源管理。                                                                                 |
| v4.0.0 | 26-02-06 | 使用生产者-消费者模型重构并行方法，提升性能和资源利用率。                                                                |
| v3.1.1 | 26-02-05 | 优化程序代码，使用全链路异步模式；修改进度条显示效果。                                                                   |
| v3.0.2 | 26-01-21 | 更新项目依赖；应用新的代码样式。                                                                                         |
| v3.0.0 | 25-12-21 | 重构整个项目，使用分层架构和依赖注入，提升代码可维护性和可扩展性。                                                       |
| v2.2.3 | 25-12-05 | 继续更新输出信息格式，提升用户体验。                                                                                     |
| v2.2.2 | 25-12-04 | 更改几种信息显示方式，修复输出文件路径时可能遇到的问题。                                                                 |
| v2.1.1 | 25-12-04 | 更新运行时间展示方式和几种输出信息的格式。                                                                               |
| v2.0.2 | 25-12-03 | 将命令 `trans` 替换为 `convert` 以提高可读性；更改 setppi 模式的处理逻辑，减少文件打开次数。                             |
| v1.6.2 | 25-12-02 | 更新 scan 模式下结果展示表格的生成逻辑和显示内容。                                                                       |
| v1.6.0 | 25-12-01 | 更新 setppi 模式的处理逻辑，改用 ExifLibNet 以减少重编码次数；增加格式修正能力。                                         |
| v1.4.3 | 25-11-30 | 首个稳定可用版本。                                                                                                       |

## 许可证

[MIT](LICENSE)。
