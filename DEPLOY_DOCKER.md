# WebCodeCli Docker 部署文档

## 🚀 快速开始（推荐）

**WebCodeCli 支持一键部署，无需任何配置文件！** 首次访问时，系统会自动引导您完成所有配置。

### 30 秒部署

```bash
# 1. 克隆代码
git clone https://github.com/xuzeyu91/WebCode.git
cd WebCode

# 2. 一键启动
docker-compose up -d

# 3. 访问 http://localhost:5000
#    首次访问会自动进入设置向导
```

就这么简单！🎉

---

## 概述

WebCodeCli 采用 **Web 界面配置** 模式，所有配置都可以在首次访问时通过设置向导完成：

| 配置项 | 需要手动配置？ | 说明 |
|-------|---------------|------|
| 管理员账户 | ❌ 不需要 | 首次访问时在页面设置 |
| Claude Code API | ❌ 不需要 | 首次访问时在页面设置 |
| Codex API | ❌ 不需要 | 首次访问时在页面设置 |
| 数据库 | ❌ 不需要 | 自动使用 SQLite |
| 工作区路径 | ❌ 不需要 | 自动检测 `/app/workspaces` |
| 端口 | ❌ 不需要 | 默认 5000，可通过环境变量修改 |

---

## 一、环境准备

### 1.1 系统要求
- Docker 已安装
- Docker Compose 已安装（推荐）
- 端口 5000 可用

### 1.2 检查环境
```bash
# 检查 Docker
docker --version
docker-compose --version
```

---

## 二、部署方式

### 方式一：Docker Compose（推荐）

```bash
# 克隆代码
git clone https://github.com/xuzeyu91/WebCode.git
cd WebCode

# 一键启动
docker-compose up -d

# 查看状态
docker-compose ps
```

**自定义端口：**
```bash
# 使用环境变量指定端口
APP_PORT=8080 docker-compose up -d
```

### 方式二：Docker Run

```bash
# 构建镜像
docker build -t webcodecli:latest .

# 启动容器
docker run -d \
  --name webcodecli \
  --restart unless-stopped \
  -p 5000:5000 \
  -v webcodecli-data:/app/data \
  -v webcodecli-workspaces:/app/workspaces \
  -v webcodecli-logs:/app/logs \
  webcodecli:latest
```

---

## 三、首次配置向导

启动容器后，访问 `http://localhost:5000`，系统会自动跳转到设置向导：

### 步骤 1：设置管理员账户
- 输入用户名和密码
- 此账户用于登录系统

### 步骤 2：配置 Claude Code（可选）
- `ANTHROPIC_BASE_URL`: API 基础地址
- `ANTHROPIC_AUTH_TOKEN`: API 令牌
- `ANTHROPIC_MODEL`: 模型名称
- 可以跳过，稍后在系统中配置

### 步骤 3：配置 Codex（可选）
- `NEW_API_KEY`: API 密钥
- `CODEX_BASE_URL`: API 基础地址
- `CODEX_MODEL`: 模型名称
- 可以跳过，稍后在系统中配置

完成向导后，系统会自动跳转到登录页面。

---

## 四、数据持久化

Docker Compose 自动创建以下数据卷：

| 数据卷 | 容器路径 | 说明 |
|--------|----------|------|
| `webcodecli-data` | `/app/data` | 数据库和配置 |
| `webcodecli-workspaces` | `/app/workspaces` | 工作区文件 |
| `webcodecli-logs` | `/app/logs` | 应用日志 |

**数据不会丢失**：即使删除容器，只要不删除数据卷，所有配置和数据都会保留。

---

## 五、日常维护

### 5.1 查看日志
```bash
# Docker Compose
docker-compose logs -f

# Docker Run
docker logs -f webcodecli
```

### 5.2 重启服务
```bash
# Docker Compose
docker-compose restart

# Docker Run
docker restart webcodecli
```

### 5.3 更新版本
```bash
# 拉取最新代码
git pull

# 重新构建并启动
docker-compose up -d --build
```

### 5.4 停止服务
```bash
# Docker Compose
docker-compose down

# Docker Run
docker stop webcodecli
```

---

## 六、高级配置（可选）

如果您需要在启动时预置配置，可以使用环境变量：

### 6.1 通过 .env 文件

```bash
# 创建 .env 文件
cat > .env << EOF
APP_PORT=5000

# Claude Code（可选，也可在页面配置）
ANTHROPIC_BASE_URL=https://api.anthropic.com/
ANTHROPIC_AUTH_TOKEN=your_token
ANTHROPIC_MODEL=claude-3-5-sonnet-20241022

# Codex（可选，也可在页面配置）
NEW_API_KEY=your_api_key
CODEX_BASE_URL=https://api.openai.com/v1
CODEX_MODEL=gpt-4
EOF

# 启动
docker-compose up -d
```

### 6.2 通过命令行

```bash
docker run -d \
  --name webcodecli \
  -p 5000:5000 \
  -e ANTHROPIC_AUTH_TOKEN=your_token \
  -e NEW_API_KEY=your_api_key \
  -v webcodecli-data:/app/data \
  -v webcodecli-workspaces:/app/workspaces \
  webcodecli:latest
```

### 6.3 使用 Host 网络模式

如果需要容器直接使用主机网络：

```bash
docker run -d \
  --name webcodecli \
  --network=host \
  -v webcodecli-data:/app/data \
  -v webcodecli-workspaces:/app/workspaces \
  webcodecli:latest
```

---

## 七、故障排查

### 7.1 容器无法启动
```bash
# 查看详细日志
docker-compose logs

# 检查容器状态
docker-compose ps -a
```

### 7.2 端口被占用
```bash
# 检查端口
netstat -tlnp | grep 5000

# 使用其他端口
APP_PORT=8080 docker-compose up -d
```

### 7.3 重置系统配置
```bash
# 停止容器
docker-compose down

# 删除数据卷（⚠️ 会清除所有数据）
docker volume rm webcodecli-data

# 重新启动
docker-compose up -d
```

---

## 八、备份与恢复

### 备份
```bash
# 备份数据卷
docker run --rm \
  -v webcodecli-data:/data \
  -v $(pwd)/backup:/backup \
  alpine tar czf /backup/webcodecli-backup-$(date +%Y%m%d).tar.gz /data
```

### 恢复
```bash
# 恢复数据卷
docker run --rm \
  -v webcodecli-data:/data \
  -v $(pwd)/backup:/backup \
  alpine tar xzf /backup/webcodecli-backup-20260114.tar.gz -C /
```

---

## 九、架构说明

### Docker 镜像构建过程

1. **构建阶段** (mcr.microsoft.com/dotnet/sdk:10.0)
   - 安装 Node.js 20.x
   - 还原 NuGet 包
   - 构建 TailwindCSS
   - 编译 .NET 应用

2. **运行时镜像** (mcr.microsoft.com/dotnet/aspnet:10.0)
   - 安装 Node.js 20.x
   - 安装 Claude Code CLI
   - 安装 Codex CLI
   - 复制应用文件

### 端口说明
- `5000`: Web 应用端口
- `8010-9000`: 前端预览服务端口（内部使用）

---

## 常见问题

### Q: 首次访问没有跳转到设置向导？
A: 可能是数据卷中已有旧配置。尝试删除数据卷后重新启动：
```bash
docker-compose down -v
docker-compose up -d
```

### Q: 如何修改已保存的配置？
A: 登录系统后，进入"系统设置"页面修改。

### Q: 支持哪些数据库？
A: 默认使用 SQLite，无需额外配置。也支持 MySQL、PostgreSQL 等，需要修改配置文件。

### Q: 如何查看系统是否正常运行？
A: 访问 `http://localhost:5000/health` 检查健康状态。

---

**文档版本**: 2.0
**更新日期**: 2026-01-14
**维护者**: WebCode Team
