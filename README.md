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
- **线性模式**：根据图像的宽度线性计算 PPI。在未指定具体模式时，默认为此模式。
- 在设置 PPI 模式下，某些文件将发生变化：
  - 图片编码格式与扩展名所暗示的不同时，该图片将被转换格式。
  - 默认转换目标格式为 PNG，扩展名亦将被纠正为 .png。
  - 与此同时，JPEG 格式图片的扩展名将被规范为 .jpg。

## 安装

RanaImageTool 是一个 .NET 全局工具。您可以通过以下步骤安装：

1. 确保已安装 .NET 10 SDK。
2. `git clone`或其他方法将项目文件下载到本地。
3. 在项目目录下运行以下命令：

```powershell
# 打包工具
dotnet pack

# 安装工具
dotnet tool install RanaImageTool --global --add-source ./nupkg --version *.*.*
# 建议在安装时指定版本号（从项目文件查询），这有助于确保所有文件均被更新。
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

- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp): 用于图像处理。
- [ExifLibNet](https://github.com/oozcitak/exiflibrary): 用于读取和修改 JPEG 图像的 PPI 数据。
- [Spectre.Console](https://github.com/spectreconsole/spectre.console): 用于命令行界面和参数解析。
- [Microsoft.IO.RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream): 用于配置高性能的池化内存流。

> [!Warning]
> 由于程序的并发数量和内存占用与逻辑处理器数量有关，当您的设备有较多处理器时，需注意避免内存占用过多。
> 
> 设您将要处理的图像的压缩大小为 $a$，未压缩大小为 $A$，处理器数量为 $P$。则一般地，程序运行时所需的内存为：
> $(P - 1) \times A + (3P + 3) \times a$，
> 其中第一项是执行图形处理时各线程存储未压缩的像素数据所需的最大总内存大小；第二项是流水线上各管道中积压的图片字节流所占用的总大小。
> 
> 对于32核的处理器而言，理想的内存占用是 2.9GiB。通过启用 Server GC 和 Concurrent GC，在开发者的设备上实际的内存占用可以被控制在 3.1GiB 左右。若不启用这些配置，则内存占用可能达到 6GiB 甚至更多，请您注意。

## 版本历史
| 版本   | 发布日期 | 更改日志                                                                                                                 |
| ------ | -------- | ------------------------------------------------------------------------------------------------------------------------ |
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
