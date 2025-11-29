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

## 安装

RanaImageTool 是一个 .NET 全局工具。您可以通过以下步骤安装：

1. 确保已安装 .NET 10 SDK。
2. 在项目目录下运行以下命令：

```powershell
# 打包工具
dotnet pack

# 安装工具
dotnet tool install --global --add-source ./nupkg RanaImageTool
```

## 使用方法

### 基本命令

```powershell
RanaImageTool [command] [options]
```

### 可用命令

| 命令   | 描述 |
|--------|------|
| `scan` | 扫描目录并计数图片文件。 |
| `webp` | 将 WebP 文件转换为 PNG 文件，并删除原始文件。 |
| `trans`| 将 JPEG 文件转换为 PNG 文件，并删除原始文件。 |
| `setppi` | 为 JPEG/PNG 文件设置 PPI。 |

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
RanaImageTool trans -p "C:\Images"
```

#### 设置 PPI
- 固定值模式：
```powershell
RanaImageTool setppi -p "C:\Images" --val 300
```

- 线性模式：
```powershell
RanaImageTool setppi -p "C:\Images" --linear
```

## 开发

### 项目结构

- **Program.cs**: 包含命令行入口和命令配置。
- **RanaImageTool.csproj**: 项目文件，定义了依赖项和目标框架。

### 依赖项

- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp): 用于图像处理。
- [Spectre.Console](https://github.com/spectreconsole/spectre.console): 用于命令行界面和参数解析。

## 版本历史
- v1.4.3: 首个稳定可用版本。

## 许可证

本项目使用 [MIT 许可证](LICENSE)。
