#!/usr/bin/env bash
# 为 Unity UPM 的 git 包建立本地镜像兜底。
#
# 背景（2026-10-20 实测）：本机直连 github.com 是**间歇性**可用的——
#   * `git ls-remote` 5 次里 4 次成功、1 次 `Recv failure: Connection was reset`
#   * `curl https://github.com/` 曾 20 秒超时，几分钟后又 HTTP 200
#   * `curl https://codeload.github.com/...` 稳定 200
# Unity 的 UPM（UnityPackageManager.exe）克隆 git 包时没有重试，一旦被重置就
# 整个 resolve 作废：upm.log 写 `project:resolve-packages --> 200`，但
# `Library/PackageManager/projectResolution.json` 里一个新包都没有，
# `Packages/packages-lock.json` 也不更新（表面看像"没反应"）。
# 第一次 resolve 因此耗时 1283592 ms（21.4 分钟）后整体失败。
#
# 本脚本做两件事：
#   1. 把 4 个 git 依赖仓库用 `git clone --mirror` 镜像到 _gitcache/
#   2. 写入全局 git 配置 url.<镜像路径>.insteadOf = <github 原 URL>
#      之后任何 git 访问这些 URL 都由本地镜像秒回（实测 166ms）
#
# 用法：  bash Tools/install_git_mirrors.sh
# 卸载：  bash Tools/install_git_mirrors.sh --uninstall
#
# 注意：
#   * 镜像路径必须写**绝对路径**，且用正斜杠
#   * insteadOf 是**前缀匹配**，不支持 `*` 通配（实测 `*` 会被当字面量，
#     反而导致不匹配）。因此只对**不带查询参数**的 URL 生效。
#     例如 `https://github.com/annulusgames/LitMotion.git?path=...#v2.0.2`
#     这种形态改不到，仍需真实网络——这也是为什么镜像只是"兜底"而非万能。
#   * UIEffect 走的就是 `?path=Packages/src#main` 这种形态，所以它每次
#     resolve 仍会连网；好在包已落在 Library/PackageCache，正常不会再克隆。

set -u

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE_URI="$PROJECT_ROOT/_gitcache"
CACHE_NATIVE="$(cygpath -m "$CACHE_URI" 2>/dev/null || echo "$CACHE_URI")"

# 组件名:owner/repo  （4 个 UPM git 依赖）
REPOS=(
  "LitMotion:annulusgames/LitMotion"
  "UIEffect:mob-sakai/UIEffect"
  "NavMeshPlus:h8man/NavMeshPlus"
  "YarnSpinner-Unity:YarnSpinnerTool/YarnSpinner-Unity"
)

uninstall() {
  echo "=== 卸载 insteadOf 规则 ==="
  for pair in "${REPOS[@]}"; do
    name="${pair%%:*}"
    git config --global --unset-all "url.$CACHE_NATIVE/$name.git.insteadOf" 2>/dev/null \
      && echo "  已移除 $name" || echo "  (无 $name 规则)"
  done
  echo
  echo "镜像目录保留在 $CACHE_URI（如需删除请手动 rm -rf）"
  exit 0
}

[ "${1:-}" = "--uninstall" ] && uninstall

echo "=== 工程根目录: $PROJECT_ROOT ==="
mkdir -p "$CACHE_URI" || { echo "无法创建 $CACHE_URI"; exit 1; }

echo
echo "=== 1/2 建立镜像 ==="
for pair in "${REPOS[@]}"; do
  name="${pair%%:*}"
  repo="${pair##*:}"
  url="https://github.com/$repo.git"
  dest="$CACHE_URI/$name.git"

  if [ -d "$dest" ]; then
    echo "  已存在，更新: $name"
    git -C "$dest" remote update --prune >/dev/null 2>&1 && echo "    OK" || echo "    更新失败（保留旧镜像）"
    continue
  fi

  ok=0
  for attempt in 1 2 3; do
    echo "  克隆 $name (第 $attempt 次) ..."
    # 网络抖动时重试；--mirror 拿全部 refs（含 tag，UPM 需要按 tag 解析）
    if timeout 600 git clone --mirror --quiet "$url" "$dest" 2>&1; then
      ok=1; break
    fi
    rm -rf "$dest"
    sleep 4
  done
  if [ "$ok" = 1 ]; then
    echo "    OK  $name  ($(du -sh "$dest" 2>/dev/null | cut -f1))"
  else
    echo "    ** 失败 $name —— 稍后重跑本脚本即可"
  fi
done

echo
echo "=== 2/2 写入全局 insteadOf 规则 ==="
for pair in "${REPOS[@]}"; do
  name="${pair%%:*}"
  repo="${pair##*:}"
  git config --global --unset-all "url.$CACHE_NATIVE/$name.git.insteadOf" 2>/dev/null
  git config --global --add "url.$CACHE_NATIVE/$name.git.insteadOf" "https://github.com/$repo.git"
  echo "  https://github.com/$repo.git  ->  $CACHE_NATIVE/$name.git"
done

echo
echo "=== 验证（应 <1s 返回且退出码 0） ==="
for pair in "${REPOS[@]}"; do
  name="${pair%%:*}"
  repo="${pair##*:}"
  start=$(date +%s%N)
  if timeout 60 git ls-remote "https://github.com/$repo.git" >/dev/null 2>&1; then code=0; else code=$?; fi
  end=$(date +%s%N)
  echo "  $repo  退出码=$code  用时=$(( (end - start) / 1000000 ))ms"
done

echo
echo "完成。当前规则："
git config --global --get-regexp "insteadof" || echo "  (无)"