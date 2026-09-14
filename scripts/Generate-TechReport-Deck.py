"""米白背景（原版視覺）＋規格／架構寫法（對齊目前 React 實作）。"""
from __future__ import annotations

import os
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.enum.shapes import MSO_SHAPE
from pptx.oxml.ns import qn
from pptx.oxml import parse_xml
from PIL import Image, ImageDraw

ROOT = r"C:\C_projects\CampusAI-Agent"
ASSETS = os.path.join(ROOT, "docs", "assets")
OUT = os.path.join(ROOT, "docs", "CampusAI-TechReport.pptx")
OUT_FALLBACK = os.path.join(ROOT, "docs", "CampusAI-TechReport-locked.pptx")
BG = os.path.join(ASSETS, "slide-bg-campus-plain.png")

SW, SH = 13.333, 7.5
ML, MT = 0.7, 0.35
SAFE_W = 11.9

CREAM = (247, 245, 240)
INK = RGBColor(0x1C, 0x24, 0x28)
MUTED = RGBColor(0x5A, 0x65, 0x6B)
TEAL = RGBColor(0x1F, 0x8A, 0x80)
TEAL_SOFT = RGBColor(0xE6, 0xF4, 0xF2)
PAPER = RGBColor(0xFF, 0xFF, 0xFF)
LINE = RGBColor(0xD4, 0xD8, 0xD6)
ORANGE = RGBColor(0xC4, 0x7A, 0x3A)
GREEN = RGBColor(0x2F, 0x7A, 0x4F)
RED = RGBColor(0xB0, 0x3A, 0x3A)


def make_bg(path: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = Image.new("RGB", (1920, 1080), CREAM)
    d = ImageDraw.Draw(img)
    for y in range(48, 1080, 40):
        for x in range(48, 1920, 40):
            d.ellipse((x, y, x + 2, y + 2), fill=(205, 202, 194))
    img.save(path, "PNG")


def font(run, size=16, bold=False, color=INK):
    name = "Microsoft JhengHei"
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.color.rgb = color
    run.font.name = name
    rPr = run._r.get_or_add_rPr()
    ea = rPr.find(qn("a:ea"))
    if ea is None:
        ea = parse_xml(
            f'<a:ea xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" typeface="{name}"/>'
        )
        rPr.append(ea)
    else:
        ea.set("typeface", name)


def tb(slide, l, t, w, h, lines, size=16, bold=False, color=INK, align=PP_ALIGN.LEFT, after=6):
    box = slide.shapes.add_textbox(Inches(l), Inches(t), Inches(w), Inches(h))
    tf = box.text_frame
    tf.word_wrap = True
    tf.clear()
    first = True
    for line in lines:
        p = tf.paragraphs[0] if first else tf.add_paragraph()
        first = False
        p.alignment = align
        p.space_after = Pt(after)
        r = p.add_run()
        r.text = line
        font(r, size=size, bold=bold, color=color)
    return box


def card(slide, l, t, w, h, fill=PAPER, line=LINE, width=1.0):
    s = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Inches(l), Inches(t), Inches(w), Inches(h))
    s.fill.solid()
    s.fill.fore_color.rgb = fill
    s.line.color.rgb = line
    s.line.width = Pt(width)
    return s


def rule(slide, y):
    sh = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(ML), Inches(y), Inches(SAFE_W), Inches(0.015))
    sh.fill.solid()
    sh.fill.fore_color.rgb = LINE
    sh.line.fill.background()


def blank(prs):
    s = prs.slides.add_slide(prs.slide_layouts[6])
    s.shapes.add_picture(BG, 0, 0, width=prs.slide_width, height=prs.slide_height)
    return s


def head(slide, title, subtitle, page, total=9):
    tb(slide, ML, MT, 10, 0.28, ["Campus AI"], size=12, bold=True, color=TEAL)
    tb(slide, ML, MT + 0.28, 10.5, 0.45, [title], size=26, bold=True, color=INK)
    if subtitle:
        tb(slide, ML, MT + 0.75, 10.8, 0.35, [subtitle], size=13, color=MUTED)
    tb(slide, 11.5, MT + 0.1, 1.2, 0.3, [f"{page}/{total}"], size=12, color=MUTED, align=PP_ALIGN.RIGHT)


def flow_box(slide, l, t, w, h, title, body, tag=None, accent=False):
    card(slide, l, t, w, h, fill=TEAL_SOFT if accent else PAPER, line=TEAL if accent else LINE, width=1.5 if accent else 1)
    tb(slide, l + 0.1, t + 0.12, w - 0.2, 0.35, [title], size=13, bold=True, color=TEAL)
    tb(slide, l + 0.1, t + 0.5, w - 0.2, h - 0.85, [body], size=11, color=INK)
    if tag:
        tb(slide, l + 0.1, t + h - 0.35, w - 0.2, 0.28, [tag], size=10, bold=True, color=GREEN)


def main():
    make_bg(BG)
    prs = Presentation()
    prs.slide_width = Inches(SW)
    prs.slide_height = Inches(SH)
    N = 9

    # 1 封面
    s = blank(prs)
    tb(s, ML, 1.9, 11, 0.35, ["Campus AI"], size=15, bold=True, color=TEAL)
    tb(s, ML, 2.4, 11.5, 0.9, ["校園個人化助理"], size=34, bold=True, color=INK)
    tb(
        s,
        ML,
        3.4,
        11.5,
        1.0,
        [
            "職責分離：Client 管對話與流程；MCP Server 只做知識檢索。",
            "校務資料在 SQL Server；規章問答走 rag_retrieve；回覆由 Gemini 整理。",
        ],
        size=16,
        color=MUTED,
        after=8,
    )
    x = ML
    for t in ["React", "ASP.NET Core", "SQL Server", "JWT", "Gemini", "MCP"]:
        w = 1.55 if len(t) < 10 else 1.95
        card(s, x, 5.1, w, 0.4, fill=TEAL_SOFT)
        tb(s, x, 5.17, w, 0.28, [t], size=11, bold=True, color=TEAL, align=PP_ALIGN.CENTER)
        x += w + 0.1
    tb(s, ML, 6.3, 8, 0.35, ["報告人：呂紹瑜"], size=15, bold=True, color=INK)

    # 2 題目規格對照
    s = blank(prs)
    head(s, "題目規格對照", "嚴格遵循設計指標：分離 MCP 端點職責，並對齊目前實作", 2, N)
    cols = [
        (
            "MCP Server 規格與實作",
            "規格：只有一個功能——RAG 檢索",
            "實作：rag_retrieve(query, topK)",
            "僅開放單一端點，獨立於對話機制外；針對 campus-docs.json 做語意／關鍵字檢索。",
            GREEN,
        ),
        (
            "MCP Client 規格與實作",
            "規格：負責使用者對話管理",
            "實作：React 多對話 ＋ JWT／SQL",
            "ASP.NET Core 管理 JWT；對話歷程持久化於 SQL Server；校園問答時才呼叫 MCP。",
            ORANGE,
        ),
        (
            "開發環境與語言",
            "本簡報：完整結構化架構解說",
            "熟悉實作語言",
            "C#（ASP.NET Core／SSMS）\nTypeScript（React ＋ Vite）\nMCP RAG Server：C#（mcp-rag-server）",
            TEAL,
        ),
    ]
    cw = (SAFE_W - 0.4) / 3
    for i, (title, req, impl, detail, accent) in enumerate(cols):
        left = ML + i * (cw + 0.2)
        card(s, left, 1.55, cw, 5.2)
        tb(s, left + 0.2, 1.75, cw - 0.4, 0.55, [title], size=15, bold=True, color=INK)
        tb(s, left + 0.2, 2.45, cw - 0.4, 0.8, [req], size=13, color=MUTED)
        tb(s, left + 0.2, 3.35, cw - 0.4, 0.7, [impl], size=14, bold=True, color=accent)
        tb(s, left + 0.2, 4.2, cw - 0.4, 2.2, detail.split("\n"), size=13, color=INK, after=8)

    # 3 系統架構
    s = blank(prs)
    head(s, "系統架構", "對話與流程控制歸於 Client；知識檢索隔離於 MCP Server", 3, N)
    card(s, ML, 1.45, SAFE_W, 0.55, fill=TEAL_SOFT, line=TEAL, width=1.25)
    tb(s, ML + 0.25, 1.55, SAFE_W - 0.5, 0.35, ["核心理念：Client 管聊天與調度；MCP Server 不聊天，只檢索。"], size=14, bold=True, color=TEAL)

    boxes = [
        ("使用者", "瀏覽器操作網站與對話", "Student / Teacher / Admin", False),
        ("React Frontend", "主畫面、側欄、多對話、入口", "TypeScript + Vite", True),
        ("CampusAI API", "JWT、對話持久化、校務 API", "ASP.NET Core", False),
        ("Personal Orchestrator", "意圖判斷、Agent 調度", "Agent 層", True),
        ("MCP RAG Server", "唯一功能：rag_retrieve", "C# · MCP", False),
        ("資料來源", "SQL 校務表 ＋ campus-docs.json", "SQL / JSON", False),
    ]
    bw = (SAFE_W - 0.5) / 6
    for i, (title, body, tag, accent) in enumerate(boxes):
        left = ML + i * (bw + 0.1)
        flow_box(s, left, 2.25, bw, 2.55, title, body, tag, accent)

    card(s, ML, 5.15, SAFE_W, 1.7, fill=PAPER, line=ORANGE, width=1.25)
    tb(s, ML + 0.25, 5.3, SAFE_W - 0.5, 0.35, ["關鍵分流策略"], size=14, bold=True, color=ORANGE)
    tb(
        s,
        ML + 0.25,
        5.75,
        SAFE_W - 0.5,
        0.9,
        [
            "獎學金／選課／活動等校務操作 → 直接走校務 Tools，讀寫 SQL Server。",
            "校園規章、辦法等知識問答 → 才走 MCP rag_retrieve，再由 Gemini 整理回覆。",
        ],
        size=14,
        color=INK,
        after=8,
    )

    # 4 系統分層
    s = blank(prs)
    head(s, "系統分層", "由上而下對齊目前程式結構", 4, N)
    layers = [
        ("應用層", "React：主畫面、校務系統、平台服務、助理功能"),
        ("Agent 層", "Personal Orchestrator ＋ 推薦／規劃／申請／溝通／提醒／分析"),
        ("API 層", "校務 API、平台服務、JWT、對話與 MCP Client"),
        ("資料層", "SQL Server 正式校務表；校園問答另接 MCP 找文件"),
    ]
    y = 1.5
    for title, body in layers:
        card(s, ML, y, SAFE_W, 1.15)
        tb(s, ML + 0.25, y + 0.25, 2.2, 0.4, [title], size=16, bold=True, color=TEAL)
        tb(s, ML + 2.6, y + 0.3, SAFE_W - 3.0, 0.55, [body], size=15, color=INK)
        y += 1.28

    # 5 MCP Server
    s = blank(prs)
    head(s, "MCP Server 設計", "極簡、無狀態的知識索取端點，單一職責", 5, N)
    card(s, ML, 1.5, 5.7, 5.3)
    tb(s, ML + 0.25, 1.7, 5.2, 0.35, ["服務基本配置"], size=16, bold=True, color=TEAL)
    conf = [
        ("專案名稱", "mcp-rag-server"),
        ("通訊協定", "MCP over HTTP"),
        ("單一開放 Tool", "rag_retrieve(query, topK)"),
        ("知識來源", "knowledge/campus-docs.json"),
    ]
    yy = 2.25
    for k, v in conf:
        tb(s, ML + 0.25, yy, 2.0, 0.3, [k], size=12, color=MUTED)
        tb(s, ML + 2.3, yy, 3.1, 0.3, [v], size=13, bold=True, color=INK)
        yy += 0.45
    tb(s, ML + 0.25, 4.3, 5.2, 0.35, ["Server 絕對不做的事"], size=15, bold=True, color=INK)
    for i, line in enumerate(["不維護對話狀態", "不處理 JWT 與帳號登入", "不呼叫 LLM、不生成自然語言回覆"]):
        tb(s, ML + 0.25, 4.8 + i * 0.4, 5.2, 0.35, [f"✕  {line}"], size=13, color=RED)

    card(s, ML + 6.05, 1.5, SAFE_W - 6.05, 5.3)
    tb(s, ML + 6.3, 1.7, 5.2, 0.35, ["Tool API 規格"], size=16, bold=True, color=TEAL)
    tb(s, ML + 6.3, 2.25, 5.2, 0.3, ["輸入"], size=13, bold=True, color=TEAL)
    tb(s, ML + 6.3, 2.6, 5.2, 0.8, ["query：自然語言問題", "topK：回傳最相關片段數（預設 3）"], size=13, color=INK, after=6)
    tb(s, ML + 6.3, 3.6, 5.2, 0.3, ["輸出"], size=13, bold=True, color=TEAL)
    tb(s, ML + 6.3, 3.95, 5.2, 1.2, ["Top-K 規章片段：標題、類別、內容、分數", "僅回傳原始檢索結果，不做聊天包裝"], size=13, color=INK, after=6)
    card(s, ML + 6.3, 5.55, 5.2, 0.9, fill=TEAL_SOFT, line=TEAL, width=1.25)
    tb(s, ML + 6.5, 5.75, 4.8, 0.5, ["一句話：Server 只檢索，不聊天。"], size=14, bold=True, color=TEAL)

    # 6 MCP Client
    s = blank(prs)
    head(s, "MCP Client／對話管理", "統籌身分、對話持久化，並在需要時調度 MCP 與 Gemini", 6, N)
    card(s, ML, 1.5, 5.7, 5.3)
    tb(s, ML + 0.25, 1.7, 5.2, 0.35, ["對話與身分持久化"], size=16, bold=True, color=TEAL)
    tb(s, ML + 0.25, 2.25, 5.2, 0.3, ["JWT 身分驗證"], size=14, bold=True, color=INK)
    tb(s, ML + 0.25, 2.65, 5.2, 0.8, ["學生／教師／行政角色分流；入口與功能依身分切換。"], size=13, color=MUTED)
    tb(s, ML + 0.25, 3.55, 5.2, 0.3, ["SQL Server 對話持久化"], size=14, bold=True, color=INK)
    tb(
        s,
        ML + 0.25,
        3.95,
        5.2,
        2.2,
        [
            "後端寫入對話紀錄，可用 SSMS 查核。",
            "前端側欄：新對話、切換多對話、歷史清單。",
            "校務正式資料（成績、獎助、課程…）同庫管理。",
        ],
        size=13,
        color=MUTED,
        after=10,
    )

    card(s, ML + 6.05, 1.5, SAFE_W - 6.05, 5.3)
    tb(s, ML + 6.3, 1.7, 5.2, 0.35, ["知識整合與生成流程"], size=16, bold=True, color=TEAL)
    steps = [
        ("1. 意圖評估", "涉及校園規章等知識時，才呼叫 MCP rag_retrieve。"),
        ("2. 校務 Tools", "獎學金／選課／活動等直接查 SQL，不經 MCP。"),
        ("3. Gemini 整理", "把查得結果與對話脈絡組成回覆。"),
    ]
    yy = 2.3
    for title, body in steps:
        tb(s, ML + 6.3, yy, 5.2, 0.3, [title], size=13, bold=True, color=GREEN)
        tb(s, ML + 6.3, yy + 0.35, 5.2, 0.55, [body], size=12, color=INK)
        yy += 1.0
    card(s, ML + 6.3, 5.5, 5.2, 1.0, fill=TEAL_SOFT, line=TEAL, width=1.25)
    tb(s, ML + 6.5, 5.7, 4.8, 0.65, ["RAG 與對話解耦：檢索交給 MCP；安全與歷史交給 ASP.NET Core。"], size=12, bold=True, color=TEAL)

    # 7 一輪請求
    s = blank(prs)
    head(s, "一輪請求怎麼走", "從輸入問題到畫面回覆的完整生命週期", 7, N)
    steps7 = [
        ("1 輸入", "使用者在網站提問"),
        ("2 送出", "帶 conversationId 與 JWT"),
        ("3 意圖", "Orchestrator 判斷路徑"),
        ("4 分流", "SQL 校務 或 MCP 檢索"),
        ("5 取回", "成績／規章片段"),
        ("6 生成", "Gemini 整理回覆"),
        ("7 落地", "SQL 存檔並渲染畫面"),
    ]
    bw = (SAFE_W - 0.6) / 7
    for i, (title, body) in enumerate(steps7):
        left = ML + i * (bw + 0.1)
        flow_box(s, left, 1.55, bw, 2.2, title, body, None, accent=(i == 6))

    card(s, ML, 4.15, SAFE_W, 2.65)
    tb(s, ML + 0.25, 4.35, SAFE_W - 0.5, 0.35, ["兩條實際路徑"], size=15, bold=True, color=TEAL)
    tb(
        s,
        ML + 0.25,
        4.9,
        SAFE_W - 0.5,
        1.6,
        [
            "問獎學金／選課／活動 → API → 校務 Tools → SQL Server → Gemini 整理。",
            "問校規／辦法 → API → MCP Client → rag_retrieve → campus-docs.json → Gemini 整理。",
            "執行紀錄可看到工具呼叫軌跡（例如 Call tool: rag_retrieve）。",
        ],
        size=15,
        color=INK,
        after=10,
    )

    # 8 現場示範
    s = blank(prs)
    head(s, "現場示範", "用同一套資料，對照資料庫、畫面與執行紀錄", 8, N)
    demos = [
        ("1. 資料庫", "SSMS 打開 Students，先看 GPA 與正式校務表。", "SQL Server"),
        ("2. 主畫面", "登入後走一圈：校務系統、平台服務、助理功能三塊入口。", "React"),
        ("3. 精準問答", "問獎學金條件對照資料庫；問校規可見 MCP 軌跡。", "RAG / Tools"),
    ]
    cw = (SAFE_W - 0.4) / 3
    for i, (title, body, tag) in enumerate(demos):
        left = ML + i * (cw + 0.2)
        card(s, left, 1.55, cw, 3.6)
        tb(s, left + 0.2, 1.8, cw - 0.4, 0.45, [title], size=16, bold=True, color=TEAL)
        tb(s, left + 0.2, 2.5, cw - 0.4, 1.8, [body], size=14, color=INK, after=8)
        card(s, left + 0.2, 4.45, 2.2, 0.4, fill=TEAL_SOFT)
        tb(s, left + 0.2, 4.52, 2.2, 0.28, [tag], size=11, bold=True, color=TEAL, align=PP_ALIGN.CENTER)

    card(s, ML, 5.5, SAFE_W, 1.3, fill=TEAL_SOFT, line=TEAL, width=1.25)
    tb(s, ML + 0.25, 5.7, SAFE_W - 0.5, 0.3, ["KEY CONCEPT"], size=12, bold=True, color=TEAL)
    tb(
        s,
        ML + 0.25,
        6.15,
        SAFE_W - 0.5,
        0.45,
        ["入口依身分與功能分；校務資料在 SQL；問答找文件走 MCP；回覆由 Gemini 整理。"],
        size=15,
        bold=True,
        color=INK,
    )

    # 9 Q&A
    s = blank(prs)
    card(s, 2.3, 2.0, 8.7, 3.4)
    tb(s, 2.3, 2.45, 8.7, 0.8, ["Q & A"], size=42, bold=True, color=TEAL, align=PP_ALIGN.CENTER)
    tb(s, 2.3, 3.4, 8.7, 0.5, ["Campus AI｜校園個人化助理"], size=15, color=MUTED, align=PP_ALIGN.CENTER)
    tb(s, 2.3, 4.1, 8.7, 0.4, ["報告人：呂紹瑜"], size=14, bold=True, color=INK, align=PP_ALIGN.CENTER)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    try:
        prs.save(OUT)
        print("saved", OUT)
    except PermissionError:
        prs.save(OUT_FALLBACK)
        print("locked, saved", OUT_FALLBACK)


if __name__ == "__main__":
    main()
