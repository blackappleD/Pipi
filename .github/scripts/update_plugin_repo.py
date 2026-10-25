"""把构建出的插件清单同步到插件库的 repo.json。

只改写本插件条目里需要同步的字段，其余文本原样保留，避免整文件重新格式化带来的无关 diff。

用法：python update_plugin_repo.py <repo.json> <构建出的 Pipi.json> <下载链接>
"""

import json
import re
import sys

# 从构建清单同步到 repo.json 的字段
SYNC_KEYS = [
    "Author",
    "Name",
    "AssemblyVersion",
    "Description",
    "Punchline",
    "ApplicableVersion",
    "RepoUrl",
    "Tags",
    "DalamudApiLevel",
    "IconUrl",
]

JSON_STRING = r'"(?:[^"\\]|\\.)*"'


def format_value(value, indent):
    if isinstance(value, list):
        if not value:
            return "[]"
        item_indent = indent + "  "
        items = ",\n".join(item_indent + json.dumps(item, ensure_ascii=False) for item in value)
        return "[\n" + items + "\n" + indent + "]"
    return json.dumps(value, ensure_ascii=False)


def replace_value(entry, key, value):
    pattern = re.compile(
        r'(?P<indent>[ \t]*)"' + re.escape(key) + r'": (?P<value>' + JSON_STRING + r'|\[[^\]]*\]|[^,\r\n}]+)'
    )
    match = pattern.search(entry)
    if match is None:
        raise SystemExit(f"repo.json 的条目缺少字段 {key}，请先手动补上")
    new_text = format_value(value, match.group("indent"))
    return entry[: match.start("value")] + new_text + entry[match.end("value") :]


def main():
    repo_path, manifest_path, download_url = sys.argv[1:4]

    with open(repo_path, encoding="utf-8", newline="") as f:
        text = f.read()
    with open(manifest_path, encoding="utf-8-sig") as f:
        manifest = json.load(f)

    internal_name = manifest["InternalName"]
    before = json.loads(text)
    if sum(1 for p in before if p["InternalName"] == internal_name) != 1:
        raise SystemExit(f"repo.json 中 {internal_name} 的条目数量不是 1")

    # 条目是不含嵌套对象的扁平对象，向前找 { 、向后找 } 即为条目边界
    marker = text.index(f'"InternalName": {json.dumps(internal_name)}')
    start = text.rindex("{", 0, marker)
    end = text.index("}", marker)
    entry = text[start:end]

    desired = {key: manifest[key] for key in SYNC_KEYS if key in manifest}
    desired["DownloadLinkInstall"] = download_url
    desired["DownloadLinkUpdate"] = download_url
    for key, value in desired.items():
        entry = replace_value(entry, key, value)

    text = text[:start] + entry + text[end:]

    # 校验：本条目已更新，其余条目完全不变
    after = json.loads(text)
    updated = next(p for p in after if p["InternalName"] == internal_name)
    for key, value in desired.items():
        if updated[key] != value:
            raise SystemExit(f"字段 {key} 写入后校验失败")
    if [p for p in before if p["InternalName"] != internal_name] != [p for p in after if p["InternalName"] != internal_name]:
        raise SystemExit("其他插件条目被意外修改")

    with open(repo_path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print(f"{internal_name} -> {manifest['AssemblyVersion']}")


if __name__ == "__main__":
    main()
