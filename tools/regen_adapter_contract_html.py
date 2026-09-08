#!/usr/bin/env python3
"""Regenerate AIStudio docs/AdapterContract.html from AdapterContract.md."""

from pathlib import Path

import markdown

AISTUDIO_DOCS = Path(r"D:\ISIDA\Programms\app\AIStudio\docs")
MD_PATH = AISTUDIO_DOCS / "AdapterContract.md"
HTML_PATH = AISTUDIO_DOCS / "AdapterContract.html"

HTML_HEAD = """<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Контракт платформы адаптеров среды — AIStudio</title>
  <style>
    * { box-sizing: border-box; }
    html, body { margin: 0; padding: 0; height: 100%; }
    body { font-family: "Segoe UI", Tahoma, Geneva, sans-serif; font-size: 14px; line-height: 1.5; color: #1a1a1a; background: #fff; }
    .help-content { max-width: 960px; margin: 0 auto; padding: 28px 40px 48px; }
    h1 { font-size: 26px; font-weight: 300; color: #005a9e; margin: 0 0 8px; padding-bottom: 8px; border-bottom: 1px solid #d6d6d6; }
    h2 { font-size: 20px; color: #005a9e; margin: 2rem 0 0.75rem; border-bottom: 1px solid #eee; padding-bottom: 0.2em; }
    h3 { font-size: 16px; margin: 1.5rem 0 0.5rem; }
    p, li { margin: 0.5rem 0; }
    code, pre { font-family: Consolas, "Courier New", monospace; font-size: 13px; }
    pre { background: #f6f8fa; padding: 12px 16px; overflow-x: auto; border: 1px solid #e1e4e8; border-radius: 4px; }
    table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
    th, td { border: 1px solid #d6d6d6; padding: 8px 12px; text-align: left; vertical-align: top; }
    th { background: #f3f3f3; }
    hr { border: none; border-top: 1px solid #d6d6d6; margin: 2rem 0; }
    a { color: #005a9e; }
  </style>
</head>
<body>
<main class="help-content">
"""

HTML_TAIL = """
</main>
</body>
</html>
"""


def main() -> None:
    md_text = MD_PATH.read_text(encoding="utf-8")
    body = markdown.markdown(
        md_text,
        extensions=["tables", "fenced_code", "nl2br", "sane_lists"],
        output_format="html5",
    )
    HTML_PATH.write_text(HTML_HEAD + body + HTML_TAIL, encoding="utf-8", newline="\r\n")
    print(f"Updated {HTML_PATH}")


if __name__ == "__main__":
    main()
